using System;
using System.Collections.Generic;
using HushOps.Core.AntiDetection;
using HushOps.Core.Config;
using HushOps.Core.Persistence;
using HushOps.Core.Core.Selectors;
using HushOps.Core.Humanization;
using HushOps.Core.Observability;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using HushOps.Core.Selectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Services.CommentFlow;
using XiaoHongShuMCP.Services.EngagementFlow;
using XiaoHongShuMCP.Tools;
using ServiceXhsSettings = XiaoHongShuMCP.Services.XhsSettings;
using CoreXhsSettings = HushOps.Core.Config.XhsSettings;

namespace XiaoHongShuMCP.Services.Extensions;

/// <summary>
/// 服务注册扩展：集中复用 Core 中的拟人化、反检测与运营组件，避免到处重复绑定。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册小红书自动化运行所需的全部核心服务与配置。
    /// </summary>
    /// <param name="services">DI 容器</param>
    /// <param name="configuration">XHS 服务级配置根节点</param>
    /// <param name="coreSettings">Core 层环境聚合配置</param>
    /// <returns>原始服务集合，便于链式调用</returns>
    public static IServiceCollection AddXiaoHongShuAutomation(this IServiceCollection services, IConfiguration configuration, CoreXhsSettings coreSettings)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (coreSettings == null) throw new ArgumentNullException(nameof(coreSettings));

        // 绑定 XHS 服务配置，供业务层与策略层读取
        services.Configure<ServiceXhsSettings>(configuration.GetSection("XHS"));
        services.TryAddSingleton<IOptions<CoreXhsSettings>>(_ => Options.Create(coreSettings));

        services.TryAddSingleton<IJsonLocalStore>(_ =>
        {
            var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
            var storageRoot = Path.Combine(projectRoot, "storage");
            return new JsonLocalStore(new JsonLocalStoreOptions(storageRoot));
        });

        services.Configure<AntiDetectionOrchestratorOptions>(configuration.GetSection("XHS:AntiDetection:Orchestrator"));
        services.Configure<SelectorRegistryOptions>(configuration.GetSection("XHS:Selectors:Registry"));
        services.TryAddSingleton<IAntiDetectionOrchestrator, DefaultAntiDetectionOrchestrator>();
        services.TryAddSingleton<ISelectorRegistry, DefaultSelectorRegistry>();

        // 拟人化节奏：保持最小安全基线，仅根据 Persona 参数控制延迟倍数
        services.TryAddSingleton<IPacingAdvisor>(sp =>
        {
            var serviceOptions = sp.GetRequiredService<IOptions<ServiceXhsSettings>>().Value;
            var persona = serviceOptions?.Persona ?? new ServiceXhsSettings.PersonaSection();
            var personaOptions = new PersonaOptions
            {
                Http403BaseMultiplier = persona.Http403BaseMultiplier,
                Http429BaseMultiplier = persona.Http429BaseMultiplier,
                MaxDelayMultiplier = persona.MaxDelayMultiplier,
                DegradeHalfLifeSeconds = persona.DegradeHalfLifeSeconds
            };
            return new PacingAdvisor(Options.Create(personaOptions));
        });

        services.TryAddSingleton<IDelayManager>(sp =>
        {
            var pacing = sp.GetService<IPacingAdvisor>();
            var metrics = sp.GetService<IMetrics>();
            return new DelayManager(pacing, metrics);
        });
        services.TryAddSingleton<ISelectorTelemetry, SelectorTelemetryService>();
        services.TryAddSingleton<IElementFinder, ElementFinder>();
        services.TryAddSingleton<IClickabilityDetector, ClickabilityDetector>();
        services.TryAddSingleton<IDomPreflightInspector, DomPreflightInspector>();
        services.TryAddSingleton<IHumanizedClickPolicy>(sp =>
        {
            var delayManager = sp.GetRequiredService<IDelayManager>();
            var detector = sp.GetRequiredService<IClickabilityDetector>();
            var preflight = sp.GetRequiredService<IDomPreflightInspector>();
            var options = sp.GetRequiredService<IOptions<ServiceXhsSettings>>();
            var metrics = sp.GetService<IMetrics>();
            var logger = sp.GetService<ILogger<HumanizedClickPolicy>>();
            var pacing = sp.GetService<IPacingAdvisor>();
            var anti = sp.GetRequiredService<IPlaywrightAntiDetectionPipeline>();
            return new HumanizedClickPolicy(delayManager, detector, preflight, options, metrics, logger, pacing, anti);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITextInputStrategy, RegularInputStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITextInputStrategy, ContentEditableInputStrategy>());
        services.TryAddSingleton<IHumanizedInteractionService>(sp =>
        {
            var browserManager = sp.GetRequiredService<IBrowserManager>();
            var delayManager = sp.GetRequiredService<IDelayManager>();
            var finder = sp.GetRequiredService<IElementFinder>();
            var strategies = sp.GetRequiredService<IEnumerable<ITextInputStrategy>>();
            var dom = sp.GetRequiredService<IDomElementManager>();
            var options = sp.GetRequiredService<IOptions<ServiceXhsSettings>>();
            var clickPolicy = sp.GetRequiredService<IHumanizedClickPolicy>();
            var logger = sp.GetService<ILogger<HumanizedInteractionService>>();
            return new HumanizedInteractionService(browserManager, delayManager, finder, strategies, dom, options, clickPolicy, logger);
        });

        // 反检测与浏览器托管
        services.TryAddSingleton<IDomElementManager, DomElementManager>();
        services.TryAddSingleton<IBrowserManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PlaywrightBrowserManager>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var domManager = sp.GetRequiredService<IDomElementManager>();
            var store = sp.GetRequiredService<IJsonLocalStore>();
            var options = sp.GetRequiredService<IOptions<ServiceXhsSettings>>();
            return new PlaywrightBrowserManager(logger, configuration, domManager, store, options);
        });
        services.TryAddSingleton<IPageLoadWaitService, PageLoadWaitService>();
        services.TryAddSingleton<IAccountManager, AccountManager>();
        services.TryAddSingleton<IPlaywrightAntiDetectionPipeline>(_ => new DefaultPlaywrightAntiDetectionPipeline());
        services.TryAddSingleton<IRateLimiter, RateLimitingRateLimiter>();
        services.TryAddSingleton<ICircuitBreaker, PollyCircuitBreakerAdapter>();
        services.TryAddSingleton<IBrowserContextPool, BrowserContextPool>();
        services.TryAddSingleton<IPageStateGuard, PageStateGuard>();
        services.TryAddSingleton<IUniversalApiMonitor, UniversalApiMonitor>();
        services.TryAddSingleton<IMcpElicitationClient, DefaultMcpElicitationClient>();

        services.TryAddSingleton<IPageGuardian, PageGuardian>();
        services.TryAddSingleton<IInteractionExecutor, InteractionExecutor>();
        services.TryAddSingleton<IFeedbackCoordinator>(sp =>
        {
            var apiMonitor = sp.GetRequiredService<IUniversalApiMonitor>();
            var settings = sp.GetRequiredService<IOptions<ServiceXhsSettings>>();
            var orchestrator = sp.GetRequiredService<IAntiDetectionOrchestrator>();
            var logger = sp.GetRequiredService<ILogger<FeedbackCoordinator>>();
            return new FeedbackCoordinator(apiMonitor, settings, orchestrator, logger);
        });
        services.TryAddSingleton<INoteDiscoveryService, NoteDiscoveryService>();
        services.TryAddSingleton<INoteEngagementWorkflow>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<NoteEngagementWorkflow>>();
            var browserManager = sp.GetRequiredService<IBrowserManager>();
            var accountManager = sp.GetRequiredService<IAccountManager>();
            var pageStateGuard = sp.GetRequiredService<IPageStateGuard>();
            var pageGuardian = sp.GetRequiredService<IPageGuardian>();
            var noteDiscovery = sp.GetRequiredService<INoteDiscoveryService>();
            var apiMonitor = sp.GetRequiredService<IUniversalApiMonitor>();
            var feedback = sp.GetRequiredService<IFeedbackCoordinator>();
            var humanized = sp.GetRequiredService<IHumanizedInteractionService>();
            var settings = sp.GetRequiredService<IOptions<ServiceXhsSettings>>();
            return new NoteEngagementWorkflow(logger, browserManager, accountManager, pageStateGuard, pageGuardian, noteDiscovery, apiMonitor, feedback, humanized, settings);
        });

        services.TryAddSingleton<ICommentWorkflow>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<CommentWorkflow>>();
            var browserManager = sp.GetRequiredService<IBrowserManager>();
            var accountManager = sp.GetRequiredService<IAccountManager>();
            var pageStateGuard = sp.GetRequiredService<IPageStateGuard>();
            var pageGuardian = sp.GetRequiredService<IPageGuardian>();
            var interactionExecutor = sp.GetRequiredService<IInteractionExecutor>();
            var feedbackCoordinator = sp.GetRequiredService<IFeedbackCoordinator>();
            var humanizedInteraction = sp.GetRequiredService<IHumanizedInteractionService>();
            var domManager = sp.GetRequiredService<IDomElementManager>();
            var serviceOptions = sp.GetRequiredService<IOptions<ServiceXhsSettings>>().Value;
            var timeouts = serviceOptions.SearchTimeoutsConfig ?? new XhsSettings.SearchTimeoutsSection();
            var detailMatch = serviceOptions.DetailMatchConfig ?? new XhsSettings.DetailMatchSection();
            return new CommentWorkflow(logger, browserManager, accountManager, pageStateGuard, pageGuardian,
                interactionExecutor, feedbackCoordinator, humanizedInteraction, domManager, timeouts, detailMatch);
        });
        // 业务服务
        services.TryAddSingleton<IXiaoHongShuService, XiaoHongShuService>();
        services.AddHostedService<SelectorPlanHostedService>();
        services.AddHostedService<BrowserConnectionHostedService>();

        return services;
    }
}
