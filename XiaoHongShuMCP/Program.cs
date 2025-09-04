using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using XiaoHongShuMCP.Services;

// 配置 Serilog 日志
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/xiaohongshu-mcp-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{

    Log.Information("启动小红书 MCP 服务器...");

    var builder = Host.CreateApplicationBuilder(args);

    // 配置日志输出到标准错误流 (MCP 协议要求)
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    builder.Services.AddSerilog();


    // 配置服务 - 重构版注册
    builder.Services
        .AddSingleton<IBrowserManager, PlaywrightBrowserManager>()
        .AddSingleton<IAccountManager, AccountManager>()
        .AddSingleton<ISelectorManager, SelectorManager>()
        .AddSingleton<IDelayManager, DelayManager>()
        .AddSingleton<IElementFinder, ElementFinder>()
        .AddSingleton<ITextInputStrategy, RegularInputStrategy>()
        .AddSingleton<ITextInputStrategy, ContentEditableInputStrategy>()
        .AddSingleton<IHumanizedInteractionService, HumanizedInteractionService>()
        .AddSingleton<ISearchDataService, SearchDataService>()
        .AddSingleton<IXiaoHongShuService, XiaoHongShuService>();

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
