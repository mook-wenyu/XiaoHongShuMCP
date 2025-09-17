using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Observability;

namespace HushOps.Core.Runtime.Playwright
{
    /// <summary>
    /// 适配器内部的 Evaluate 门控与计量桥接。
    /// - EnableJsReadEval：是否允许只读 Evaluate（默认从环境变量 XHS__InteractionPolicy__EnableJsReadEval 读取；缺省 true）。
    /// - Metrics：可选计量接口（由 Observability HostedService 通过反射初始化）。
    /// - 计量指标：ui_injection_total{type="eval",path}
    /// </summary>
    internal static class PlaywrightAdapterTelemetry
    {
        private static bool enableJsReadEval = ReadEnvEnableFlag();
        private static HashSet<string>? allowedPaths;
        private static IMetrics? metrics;

        private static bool ReadEnvEnableFlag()
        {
            var v = Environment.GetEnvironmentVariable("XHS__InteractionPolicy__EnableJsReadEval");
            if (string.IsNullOrWhiteSpace(v)) return true;
            return !string.Equals(v, "false", StringComparison.OrdinalIgnoreCase);
        }

        public static void Init(IMetrics? m, bool enable, string[]? allowed)
        {
            metrics = m;
            enableJsReadEval = enable;
            var defaults = LoadAllowedFromEnvOrDefaults();
            if (allowed is { Length: > 0 })
            {
                allowedPaths = new HashSet<string>(allowed, StringComparer.Ordinal);
                foreach (var d in defaults) allowedPaths.Add(d); // 统一并集，避免丢失关键标签
            }
            else
            {
                allowedPaths = defaults;
            }
        }

        private static HashSet<string> LoadAllowedFromEnvOrDefaults()
        {
            // 支持通过环境变量集中配置允许的路径标签；为空则回落到安全默认集合
            var env = Environment.GetEnvironmentVariable("XHS__InteractionPolicy__EvalAllowedPaths");
            if (!string.IsNullOrWhiteSpace(env))
                return new HashSet<string>(env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);

            return new HashSet<string>(new[]
            {
                // 元素级只读探针与轻量读取（默认不含 html.sample）
                "element.tagName","element.computedStyle","element.textProbe",
                "element.clickability","element.probeVisibility",
                // 页面级只读评估（收紧：不再使用通用标签，改为细化标签）
                "page.eval.read",
                // 反检测快照专用标签（仅用于 AntiDetectionPipeline 只读采集）
                "antidetect.snapshot"
            }, StringComparer.Ordinal);
        }

        public static async Task<T?> EvalAsync<T>(ILocator locator, string script, string pathLabel, CancellationToken ct)
        {
            GuardEvalAllowed(pathLabel);
            CountEval(pathLabel);
            return await locator.EvaluateAsync<T>(script);
        }

        public static async Task<T?> EvalAsync<T>(IElementHandle handle, string script, string pathLabel, CancellationToken ct)
        {
            GuardEvalAllowed(pathLabel);
            CountEval(pathLabel);
            return await handle.EvaluateAsync<T>(script);
        }

        public static async Task<T?> EvalAsync<T>(IPage page, string script, string pathLabel, CancellationToken ct)
        {
            GuardEvalAllowed(pathLabel);
            CountEval(pathLabel);
            return await page.EvaluateAsync<T>(script);
        }

        private static void GuardEvalAllowed(string path)
        {
            if (!enableJsReadEval)
            {
                throw new NotSupportedException("已禁用只读 Evaluate（XHS:InteractionPolicy:EnableJsReadEval=false）");
            }
            // 若配置白名单，则仅允许出现在白名单中的 path
            // 若尚未初始化，按环境变量或默认集合填充
            allowedPaths ??= LoadAllowedFromEnvOrDefaults();
            if (allowedPaths is not null && !allowedPaths.Contains(path))
            {
                throw new NotSupportedException($"Evaluate 路径未在白名单中：{path}");
            }
        }

        private static void CountEval(string path)
        {
            try
            {
                var c = metrics?.CreateCounter("ui_injection_total", "UI 注入/评估使用计数（应为0，>0告警）");
                c?.Add(1, LabelSet.From(("type", "eval"), ("path", path)));
            }
            catch { /* 计量不可用时忽略 */ }
        }
    }
}
