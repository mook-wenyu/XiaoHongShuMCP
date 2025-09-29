using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tests.Logging;

public sealed class McpServerLoggingCapabilityTests
{
    [Fact]
    public void AddMcpServer_RegistersLoggingCapability()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace));
        services.AddMcpLoggingBridge();

        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "Test",
                    Title = "Test",
                    Version = "1.0"
                };
            })
            .WithSetLoggingLevelHandler(McpLoggingHandlers.HandleSetLoggingLevelAsync);

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        Assert.NotNull(options.Capabilities);
        Assert.NotNull(options.Capabilities.Logging);
    }
}
