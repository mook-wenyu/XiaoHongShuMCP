using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.FingerprintBrowser.Installation;
using HushOps.Servers.XiaoHongShu.Services;
using HushOps.Servers.XiaoHongShu.Services.Logging;
using HushOps.Servers.XiaoHongShu.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Threading.Tasks;

var builder = Host.CreateApplicationBuilder(args);

ConfigureConfiguration(builder.Configuration);

builder.Services.AddLogging(static options =>
{
    options.AddSimpleConsole(configure =>
    {
        configure.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffK ";
        configure.SingleLine = true;
    });
    options.Services.Configure<ConsoleLoggerOptions>(static console =>
    {
        console.LogToStandardErrorThreshold = LogLevel.Trace;
    });
});

builder.Services
    .AddOptions<XiaoHongShuOptions>()
    .Bind(builder.Configuration.GetSection(XiaoHongShuOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<HumanBehaviorOptions>()
    .Bind(builder.Configuration.GetSection(HumanBehaviorOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<NetworkStrategyOptions>()
    .Bind(builder.Configuration.GetSection(NetworkStrategyOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<VerificationOptions>()
    .Bind(builder.Configuration.GetSection(VerificationOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddXiaoHongShuServer(builder.Configuration, builder.Environment);
builder.Services.AddMcpLoggingBridge();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "HushOps.Servers.XiaoHongShu",
            Title = "HushOps XiaoHongShu Server",
            Version = string.Empty
        };

        options.Capabilities ??= new ServerCapabilities();
        options.Capabilities.Logging ??= new LoggingCapability();
    })
    .WithSetLoggingLevelHandler(McpLoggingHandlers.HandleSetLoggingLevelAsync)
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

builder.Services.AddHostedService<XiaoHongShuServerHostedService>();

var host = builder.Build();

if (await CliExecutor.TryHandleAsync(host.Services, args).ConfigureAwait(false))
{
    return;
}

await host.RunAsync().ConfigureAwait(false);

static void ConfigureConfiguration(IConfigurationBuilder configuration)
{
    configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile("config/xiao-hong-shu.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables(prefix: "HUSHOPS_XHS_SERVER_");
}

static class CliExecutor
{
    public static async Task<bool> TryHandleAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        if (args.Any(static a => string.Equals(a, "--tools-list", StringComparison.OrdinalIgnoreCase)))
        {
            HandleToolsList();
            return true;
        }

        if (args.Any(static a => string.Equals(a, "--verification-run", StringComparison.OrdinalIgnoreCase)))
        {
            await HandleVerificationRunAsync(services).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static void HandleToolsList()
    {
        var assembly = typeof(Program).Assembly;
        var toolEntries = new List<ToolEntry>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attr is null)
                {
                    continue;
                }

                var toolName = string.IsNullOrWhiteSpace(attr.Name) ? method.Name : attr.Name;
                var declaringTypeName = type.FullName ?? type.Name;

                toolEntries.Add(new ToolEntry(toolName, declaringTypeName));
            }
        }

        toolEntries.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        var payload = new
        {
            tools = toolEntries
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        Console.WriteLine(json);
    }

    private static async Task HandleVerificationRunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<VerificationScenarioRunner>();
        await runner.RunAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
    }

    private sealed record ToolEntry(string Name, string Type);
}
