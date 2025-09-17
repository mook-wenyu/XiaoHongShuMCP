using Serilog;
using HushOps.Core.Config;
using HushOps.Core.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Services.Extensions;
using XiaoHongShuMCP.Tools;
using XiaoHongShuMCP.Tooling;

class Program
{
    static int Main(string[] args)
    {
        Log.Logger = Logging.CreateBootstrapLogger();
        try
        {
            var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "serve";
            return cmd switch
            {
                "serve" => ServeMcp(args).GetAwaiter().GetResult(),
                _ => PrintHelp()
            };
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[MCP] Unexpected fatal error");
            return 2;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// 启动仅包含核心功能的 MCP 服务器，复用标准输入输出传输。
    /// </summary>
    private static async Task<int> ServeMcp(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var configurationRoot = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        builder.Services.AddSingleton<IToolBroker, ToolBroker>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithListToolsHandler((context, token) => ResolveToolBroker(context).ListToolsAsync(context, token))
            .WithCallToolHandler((context, token) => ResolveToolBroker(context).InvokeAsync(context, token));

        var coreSettings = XhsConfiguration.LoadFromEnvironment();
        builder.Services.AddLogging();
        builder.Services.AddXiaoHongShuAutomation(configurationRoot, coreSettings);
        builder.Services.AddSingleton<XiaoHongShuTools>();

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("用法: xiaohongshu-mcp serve");
        return 1;
    }

    private static IToolBroker ResolveToolBroker<TParams>(RequestContext<TParams> context)
    {
        if (context.Services is IServiceProvider provider)
        {
            return provider.GetRequiredService<IToolBroker>();
        }

        throw new InvalidOperationException("未能从当前请求解析 IToolBroker，请检查 MCP 主机服务注册。");
    }
}



