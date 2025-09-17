using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HushOps.Core.Persistence;

namespace HushOps.Core.AntiDetection;

/// <summary>
/// 默认反检测调优器实现，基于滑动窗口计算异常率并生成节奏决策。
/// </summary>
public sealed class DefaultAntiDetectionOrchestrator : IAntiDetectionOrchestrator
{
    private readonly IJsonLocalStore jsonStore;
    private readonly AntiDetectionOrchestratorOptions options;
    private readonly ILogger<DefaultAntiDetectionOrchestrator>? logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> contextLocks = new();

    public DefaultAntiDetectionOrchestrator(
        IJsonLocalStore jsonStore,
        IOptions<AntiDetectionOrchestratorOptions>? options,
        ILogger<DefaultAntiDetectionOrchestrator>? logger = null)
    {
        this.jsonStore = jsonStore ?? throw new ArgumentNullException(nameof(jsonStore));
        this.options = options?.Value ?? new AntiDetectionOrchestratorOptions();
        this.logger = logger;
        ValidateOptions();
    }

    /// <inheritdoc />
    public async Task<AntiDetectionAdjustment> RecordAsync(AntiDetectionSignal signal, CancellationToken ct = default)
    {
        if (signal is null)
        {
            throw new ArgumentNullException(nameof(signal));
        }

        if (string.IsNullOrWhiteSpace(signal.ContextId))
        {
            throw new ArgumentException("上下文标识不能为空", nameof(signal));
        }

        var gate = contextLocks.GetOrAdd(signal.ContextId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var state = await LoadStateAsync(signal.ContextId, ct).ConfigureAwait(false)
                        ?? new AntiDetectionContextState
                        {
                            ContextId = signal.ContextId,
                            Workflow = signal.Workflow
                        };

            UpsertSignal(state, signal);
            var adjustment = BuildAdjustment(state);
            await PersistStateAsync(state, signal, adjustment, ct).ConfigureAwait(false);
            return adjustment;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AntiDetectionContextState?> GetStateAsync(string contextId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            throw new ArgumentException("上下文标识不能为空", nameof(contextId));
        }

        return await LoadStateAsync(contextId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AntiDetectionAdjustment>> GetRecentAdjustmentsAsync(string contextId, int take = 5, CancellationToken ct = default)
    {
        var state = await GetStateAsync(contextId, ct).ConfigureAwait(false);
        if (state?.History is null || state.History.Count == 0)
        {
            return Array.Empty<AntiDetectionAdjustment>();
        }

        return state.History
            .OrderByDescending(h => h.IssuedAtUtc)
            .Take(Math.Clamp(take, 1, options.HistoryDepth))
            .ToArray();
    }

    private void ValidateOptions()
    {
        if (options.SlidingWindow < 3)
        {
            throw new ArgumentException("SlidingWindow 至少为 3", nameof(options.SlidingWindow));
        }

        if (options.HistoryDepth < 1)
        {
            throw new ArgumentException("HistoryDepth 至少为 1", nameof(options.HistoryDepth));
        }
    }

    private static string Sanitize(string id)
    {
        var builder = new StringBuilder(id.Length);
        foreach (var ch in id)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }
        return builder.ToString();
    }

    private string GetStatePath(string contextId)
    {
        var safe = Sanitize(contextId);
        return $"{options.StateDirectory.TrimEnd('/')}/{safe}.json";
    }

    private string GetAdjustmentDirectory(string contextId)
    {
        var safe = Sanitize(contextId);
        return $"{options.AdjustmentDirectory.TrimEnd('/')}/{safe}";
    }

    private async Task<AntiDetectionContextState?> LoadStateAsync(string contextId, CancellationToken ct)
    {
        var statePath = GetStatePath(contextId);
        return await jsonStore.LoadAsync<AntiDetectionContextState>(statePath, ct).ConfigureAwait(false);
    }

    private void UpsertSignal(AntiDetectionContextState state, AntiDetectionSignal signal)
    {
        state.Workflow = string.IsNullOrWhiteSpace(state.Workflow) ? signal.Workflow : state.Workflow;
        state.Signals.Add(signal);
        state.Signals = state.Signals
            .OrderByDescending(s => s.ObservedAtUtc)
            .Take(options.SlidingWindow)
            .OrderBy(s => s.ObservedAtUtc)
            .ToList();

        var alpha = 0.2;
        state.SmoothedHumanLikeScore = state.SmoothedHumanLikeScore * (1 - alpha) + alpha * signal.HumanLikeScore;
    }

    private AntiDetectionAdjustment BuildAdjustment(AntiDetectionContextState state)
    {
        var totalInteractions = Math.Max(state.Signals.Sum(s => s.TotalInteractions), 1);
        var http429 = state.Signals.Sum(s => s.Http429);
        var http403 = state.Signals.Sum(s => s.Http403);
        var captcha = state.Signals.Sum(s => s.CaptchaChallenges);
        var fallbackCount = state.Signals.Count(s => s.JsInjectionFallbackUsed);
        var latestSignal = state.Signals.LastOrDefault();

        var http429Rate = http429 / (double)totalInteractions;
        var http403Rate = http403 / (double)totalInteractions;
        var captchaRate = captcha / (double)totalInteractions;
        var hasFallback = fallbackCount > 0;
        var currentProfile = state.LastAdjustment?.PacingProfile ?? state.CurrentPacing;
        var targetProfile = currentProfile;
        var reasonParts = new List<string>();

        if (http429Rate >= options.Http429High || http403Rate >= options.Http403High || captchaRate >= options.CaptchaHigh)
        {
            targetProfile = AntiDetectionPacingProfile.Conservative;
            reasonParts.Add($"高风险命中率 429={http429Rate:P1} 403={http403Rate:P1} Captcha={captchaRate:P1}");
        }
        else if (currentProfile == AntiDetectionPacingProfile.Conservative &&
                 http429Rate <= options.Http429Recover &&
                 http403Rate <= options.Http403Recover &&
                 captchaRate <= options.CaptchaHigh / 2)
        {
            targetProfile = AntiDetectionPacingProfile.Normal;
            reasonParts.Add("指标回落，恢复标准节奏");
        }
        else if (currentProfile != AntiDetectionPacingProfile.Paused &&
                 http429 == 0 && http403 == 0 && captcha == 0 &&
                 state.Signals.Count >= options.AggressiveWindowRequirement)
        {
            targetProfile = AntiDetectionPacingProfile.Aggressive;
            reasonParts.Add("连续窗口零异常，提升至激进节奏");
        }

        var shouldPause = http403Rate >= 0.12 || captchaRate >= 0.08;
        if (shouldPause)
        {
            targetProfile = AntiDetectionPacingProfile.Paused;
            reasonParts.Add("严重异常，暂停交互待人工复核");
        }

        var enableNavigatorPatch = http403 > 0 || hasFallback;
        var enableUaScrub = http429Rate > options.Http429High / 2;
        var rotateFingerprint = http403Rate > options.Http403High || fallbackCount >= 2;
        var refreshCookies = captchaRate >= options.CaptchaHigh || rotateFingerprint;

        var issuedAt = DateTimeOffset.UtcNow;
        var confidence = Math.Clamp(1.0 - (http429Rate + http403Rate) - captchaRate * 0.5, 0, 1);
        if (latestSignal is not null)
        {
            reasonParts.Add($"最近窗口 P95={latestSignal.P95LatencyMs:F0}ms P99={latestSignal.P99LatencyMs:F0}ms HL={state.SmoothedHumanLikeScore:F2}");
        }

        var adjustment = new AntiDetectionAdjustment
        {
            ContextId = state.ContextId,
            Workflow = state.Workflow,
            IssuedAtUtc = issuedAt,
            PacingProfile = targetProfile,
            EnableNavigatorPatch = enableNavigatorPatch,
            EnableUaLanguageScrub = enableUaScrub,
            RotateFingerprint = rotateFingerprint,
            RefreshCookies = refreshCookies,
            PauseInteractions = shouldPause,
            Confidence = Math.Round(confidence, 3),
            Reason = string.Join("; ", reasonParts.Distinct(StringComparer.Ordinal))
        };

        return adjustment;
    }

    private async Task PersistStateAsync(AntiDetectionContextState state, AntiDetectionSignal signal, AntiDetectionAdjustment adjustment, CancellationToken ct)
    {
        var now = adjustment.IssuedAtUtc;
        var shouldPersistAdjustment = ShouldPersistNewAdjustment(state, adjustment, now);
        if (shouldPersistAdjustment)
        {
            state.CurrentPacing = adjustment.PacingProfile;
            state.LastAdjustment = adjustment;
            state.LastAdjustmentAtUtc = now;
            state.History.Add(adjustment);
            if (state.History.Count > options.HistoryDepth)
            {
                state.History = state.History
                    .OrderByDescending(h => h.IssuedAtUtc)
                    .Take(options.HistoryDepth)
                    .ToList();
            }

            await WriteAdjustmentArtifactsAsync(signal.ContextId, adjustment, ct).ConfigureAwait(false);
        }

        var statePath = GetStatePath(signal.ContextId);
        await jsonStore.SaveAsync(statePath, state, ct).ConfigureAwait(false);

        logger?.LogInformation(
            "[AntiDetect] Context={Context} Workflow={Workflow} Pacing={Pacing} Reason={Reason}",
            state.ContextId,
            state.Workflow,
            adjustment.PacingProfile,
            adjustment.Reason);
    }

    private bool ShouldPersistNewAdjustment(AntiDetectionContextState state, AntiDetectionAdjustment adjustment, DateTimeOffset now)
    {
        if (state.LastAdjustment is null)
        {
            return true;
        }

        if (adjustment.PacingProfile != state.LastAdjustment.PacingProfile ||
            adjustment.RotateFingerprint != state.LastAdjustment.RotateFingerprint ||
            adjustment.RefreshCookies != state.LastAdjustment.RefreshCookies ||
            adjustment.PauseInteractions != state.LastAdjustment.PauseInteractions ||
            adjustment.EnableNavigatorPatch != state.LastAdjustment.EnableNavigatorPatch ||
            adjustment.EnableUaLanguageScrub != state.LastAdjustment.EnableUaLanguageScrub ||
            !string.Equals(adjustment.Reason, state.LastAdjustment.Reason, StringComparison.Ordinal))
        {
            return true;
        }

        if (!state.LastAdjustmentAtUtc.HasValue)
        {
            return true;
        }

        return (now - state.LastAdjustmentAtUtc.Value) >= options.MinimumAdjustmentInterval;
    }

    private async Task WriteAdjustmentArtifactsAsync(string contextId, AntiDetectionAdjustment adjustment, CancellationToken ct)
    {
        var dir = GetAdjustmentDirectory(contextId);
        var timestamp = adjustment.IssuedAtUtc.ToString("yyyyMMddHHmmssfff");
        var filePath = $"{dir}/{timestamp}.json";
        await jsonStore.SaveAsync(filePath, adjustment, ct).ConfigureAwait(false);

        var manifestPath = $"{dir}/manifest.json";
        var manifest = await jsonStore.LoadAsync<AntiDetectionAdjustmentManifest>(manifestPath, ct).ConfigureAwait(false)
                       ?? new AntiDetectionAdjustmentManifest { ContextId = contextId };
        manifest.Items ??= new List<AntiDetectionAdjustmentSummary>();
        manifest.Latest = adjustment;
        manifest.Items.Add(new AntiDetectionAdjustmentSummary
        {
            IssuedAtUtc = adjustment.IssuedAtUtc,
            PacingProfile = adjustment.PacingProfile,
            Reason = adjustment.Reason,
            Confidence = adjustment.Confidence,
            FileName = $"{timestamp}.json"
        });

        manifest.Items = manifest.Items
            .OrderByDescending(i => i.IssuedAtUtc)
            .Take(options.HistoryDepth)
            .ToList();

        await jsonStore.SaveAsync(manifestPath, manifest, ct).ConfigureAwait(false);
    }

    private sealed class AntiDetectionAdjustmentManifest
    {
        /// <summary>上下文标识。</summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>最新决策快照。</summary>
        public AntiDetectionAdjustment? Latest { get; set; }
            = null;

        /// <summary>历史摘要集合。</summary>
        public List<AntiDetectionAdjustmentSummary> Items { get; set; } = new();
    }

    private sealed class AntiDetectionAdjustmentSummary
    {
        public DateTimeOffset IssuedAtUtc { get; set; }
            = DateTimeOffset.UtcNow;

        public AntiDetectionPacingProfile PacingProfile { get; set; }
            = AntiDetectionPacingProfile.Normal;

        public string Reason { get; set; } = string.Empty;

        public double Confidence { get; set; }
            = 1.0;

        public string FileName { get; set; } = string.Empty;
    }
}
