using System;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

public static class McpLoggingServiceCollectionExtensions
{
    public static IServiceCollection AddMcpLoggingBridge(
        this IServiceCollection services,
        Action<McpLoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<McpLoggingOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IMcpLoggingState, McpLoggingState>();
        services.TryAddSingleton<IMcpLogSanitizer, DefaultMcpLogSanitizer>();

        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<McpLoggingOptions>>().Value;
            var channelOptions = new BoundedChannelOptions(Math.Max(1, options.QueueCapacity))
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

            return Channel.CreateBounded<McpLogEntry>(channelOptions);
        });

        services.TryAddSingleton(sp => sp.GetRequiredService<Channel<McpLogEntry>>().Writer);
        services.TryAddSingleton(sp => sp.GetRequiredService<Channel<McpLogEntry>>().Reader);

        services.TryAddSingleton<IMcpLoggingNotificationSender, McpServerLoggingNotificationSender>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, McpLoggingProvider>());
        services.AddHostedService<McpLoggingDispatcher>();

        return services;
    }
}
