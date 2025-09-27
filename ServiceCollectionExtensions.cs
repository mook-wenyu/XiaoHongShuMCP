using System;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Infrastructure.FileSystem;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        services.AddSingleton<XiaoHongShuDiagnosticsService>();
        services.AddSingleton<IFileSystem>(_ => new DefaultFileSystem(environment.ContentRootPath));

        services.AddSingleton<IAccountPortraitStore, AccountPortraitStore>();
        services.AddSingleton<IDefaultKeywordProvider, DefaultKeywordProvider>();
        services.AddSingleton<IRandomDelayConfiguration, RandomDelayConfiguration>();
        services.AddSingleton<IHumanDelayProvider, HumanDelayProvider>();
        services.AddSingleton<IKeywordResolver, KeywordResolver>();
        services.AddSingleton<IBehaviorController, DefaultBehaviorController>();
        services.AddSingleton<IHumanizedActionService, HumanizedActionService>();

        services.AddSingleton<IProfileFingerprintManager, ProfileFingerprintManager>();
        services.AddSingleton<INetworkStrategyManager, NetworkStrategyManager>();
        services.AddSingleton<IPlaywrightSessionManager, PlaywrightSessionManager>();
        services.AddSingleton<Diagnostics.VerificationScenarioRunner>();

        services.AddSingleton<INoteRepository, NoteRepository>();
        services.AddSingleton<INoteEngagementService, NoteEngagementService>();
        services.AddSingleton<INoteCaptureService, NoteCaptureService>();

        services.AddSingleton<IBrowserAutomationService, BrowserAutomationService>();

        return services;
    }
}
