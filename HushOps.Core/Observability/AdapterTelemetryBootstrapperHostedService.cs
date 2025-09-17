using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using HushOps.Core.Observability;

namespace HushOps.Observability
{
    /// <summary>
    /// 中文注释：启动时通过反射初始化适配器层的 Evaluate 门控与计量（避免 Observability 对 Adapters 的编译期依赖）。
    /// </summary>
    internal sealed class AdapterTelemetryBootstrapperHostedService : IHostedService
    {
        private readonly IConfiguration cfg;
        private readonly IMetrics? metrics;

    public AdapterTelemetryBootstrapperHostedService(IConfiguration cfg, IMetrics? metrics)
    { this.cfg = cfg; this.metrics = metrics; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var enable = !string.Equals(cfg["XHS:InteractionPolicy:EnableJsReadEval"], "false", StringComparison.OrdinalIgnoreCase);
                var allowCsv = cfg["XHS:InteractionPolicy:EvalWhitelist:Paths"];
                string[]? allowed = null;
                if (!string.IsNullOrWhiteSpace(allowCsv))
                {
                    allowed = allowCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
                var t = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(tp => string.Equals(tp.FullName, "HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry", StringComparison.Ordinal));
                var mi = t?.GetMethod("Init", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (allowed is not null)
                    mi?.Invoke(null, new object?[] { metrics, enable, allowed });
                else
                    mi?.Invoke(null, new object?[] { metrics, enable, null });
            }
            catch { }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static System.Type[] SafeGetTypes(System.Reflection.Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch { return Array.Empty<System.Type>(); }
        }
    }
}
