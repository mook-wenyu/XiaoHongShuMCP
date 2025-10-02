using System;
using HushOps.FingerprintBrowser.Core;
using HushOps.FingerprintBrowser.Playwright;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Infrastructure.FileSystem;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services;

/// <summary>
/// 中文：集中注册小红书服务器所需的服务与依赖。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXiaoHongShuServer(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        // 注册 FingerprintBrowser SDK 配置
        services.AddPlaywrightInstallation(configuration);
        services.AddSingleton<XiaoHongShuDiagnosticsService>();
        services.AddSingleton<IFileSystem>(_ => new DefaultFileSystem(environment.ContentRootPath));

        services.AddSingleton<IAccountPortraitStore, AccountPortraitStore>();
        services.AddSingleton<IDefaultKeywordProvider, DefaultKeywordProvider>();
        services.AddSingleton<IRandomDelayConfiguration, RandomDelayConfiguration>();
        services.AddSingleton<IHumanDelayProvider, HumanDelayProvider>();
        services.AddSingleton<IKeywordResolver, KeywordResolver>();
        services.AddSingleton<IBehaviorController, DefaultBehaviorController>();
        services.AddSingleton<IInteractionLocatorBuilder, InteractionLocatorBuilder>();
        services.AddSingleton<IHumanizedActionScriptBuilder, DefaultHumanizedActionScriptBuilder>();
        services.AddSingleton<IHumanizedInteractionExecutor, HumanizedInteractionExecutor>();
        services.AddSingleton<ISessionConsistencyInspector, SessionConsistencyInspector>();
        services.AddSingleton<IHumanizedActionService, HumanizedActionService>();

        // 注册 FingerprintBrowser SDK
        services.AddSingleton(sp =>
        {
            var playwright = Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return playwright;
        });
        services.AddSingleton<IFingerprintBrowser>(sp =>
        {
            var playwright = sp.GetRequiredService<Microsoft.Playwright.IPlaywright>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new PlaywrightFingerprintBrowser(playwright, loggerFactory);
        });

        services.AddSingleton<INetworkStrategyManager, NetworkStrategyManager>();
        services.AddSingleton<IPlaywrightSessionManager, PlaywrightSessionManager>();
        services.AddSingleton<Diagnostics.VerificationScenarioRunner>();

        services.AddSingleton<INoteRepository, NoteRepository>();
        services.AddSingleton<INoteEngagementService, NoteEngagementService>();
        services.AddSingleton<INoteCaptureService, NoteCaptureService>();
        services.AddSingleton<IPageNoteCaptureService, PageNoteCaptureService>();

        services.AddSingleton<IBrowserAutomationService, BrowserAutomationService>();

        return services;
    }
}
