using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using XiaoHongShuMCP.Services;

// 配置 Serilog 日志（过滤敏感信息）
Log.Logger = new LoggerConfiguration()
    .Filter.ByExcluding(ContainsSensitive)
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .WriteTo.File("logs/xiaohongshu-mcp-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{

    Log.Information("启动小红书 MCP 服务器...");

    var builder = Host.CreateApplicationBuilder(args);

    // 配置日志（统一使用 Serilog，并输出到标准错误流）
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog();


    // 配置 PageLoadWaitService 配置
    builder.Services.Configure<PageLoadWaitConfig>(
        builder.Configuration.GetSection("PageLoadWaitConfig"));

    // 配置服务 - 重构版注册
    builder.Services
        .AddSingleton<IBrowserManager, PlaywrightBrowserManager>()
        .AddSingleton<IAccountManager, AccountManager>()
        .AddSingleton<IDomElementManager, DomElementManager>()
        .AddSingleton<IDelayManager, DelayManager>()
        .AddSingleton<IElementFinder, ElementFinder>()
        .AddSingleton<ITextInputStrategy, RegularInputStrategy>()
        .AddSingleton<ITextInputStrategy, ContentEditableInputStrategy>()
        .AddSingleton<IHumanizedInteractionService, HumanizedInteractionService>()
        .AddSingleton<IDiscoverPageNavigationService, DiscoverPageNavigationService>()
        .AddSingleton<UniversalApiMonitor>()
        .AddSingleton<ISmartCollectionController, SmartCollectionController>()
        .AddSingleton<IRecommendService, RecommendService>()
        .AddSingleton<IXiaoHongShuService, XiaoHongShuService>()
        .AddSingleton<IPageLoadWaitService, PageLoadWaitService>();

    // 注册浏览器自动连接后台服务
    builder.Services.AddHostedService<BrowserConnectionHostedService>();

    // 配置 MCP 服务器
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var host = builder.Build();

    Log.Information("小红书 MCP 服务器已启动，等待客户端连接...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "服务器启动失败");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static bool ContainsSensitive(LogEvent le)
{
    try
    {
        var text = le.MessageTemplate.Text;
        bool MsgHas(string s) => text.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
        var keyHit = le.Properties.Keys.Any(k =>
            k.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) >= 0 ||
            k.IndexOf("Cookie", StringComparison.OrdinalIgnoreCase) >= 0 ||
            k.IndexOf("Set-Cookie", StringComparison.OrdinalIgnoreCase) >= 0 ||
            k.IndexOf("X-Api-Key", StringComparison.OrdinalIgnoreCase) >= 0);
        return MsgHas("authorization") || MsgHas("cookie") || keyHit;
    }
    catch
    {
        return false;
    }
}
