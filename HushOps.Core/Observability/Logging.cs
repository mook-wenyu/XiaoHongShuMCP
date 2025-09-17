using Serilog;
using Serilog.Events;

namespace HushOps.Core.Observability;

public static class Logging
{
    public static ILogger CreateBootstrapLogger()
        => new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
}

