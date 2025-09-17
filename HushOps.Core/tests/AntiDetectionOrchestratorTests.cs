using System;
using System.IO;

using FluentAssertions;
using HushOps.Core.AntiDetection;
using HushOps.Core.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HushOps.Core.Tests;

/// <summary>
/// 验证默认反检测调优器的窗口汇聚与决策输出。
/// </summary>
public sealed class AntiDetectionOrchestratorTests : IDisposable
{
    private readonly string tempRoot;
    private readonly JsonLocalStore store;

    public AntiDetectionOrchestratorTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "xhs_antidetect_test_" + Guid.NewGuid().ToString("N"));
        store = new JsonLocalStore(new JsonLocalStoreOptions(tempRoot, writeIndented: false));
    }

    [Fact]
    public async Task HighRiskSignal_Should_Downgrade_To_Conservative()
    {
        var orchestrator = CreateOrchestrator(new AntiDetectionOrchestratorOptions
        {
            SlidingWindow = 6,
            MinimumAdjustmentInterval = TimeSpan.Zero
        });

        var adjustment = await orchestrator.RecordAsync(new AntiDetectionSignal
        {
            ContextId = "ctx-a",
            Workflow = "Comment",
            ObservedAtUtc = DateTimeOffset.UtcNow,
            TotalInteractions = 20,
            Http429 = 2,
            Http403 = 1,
            CaptchaChallenges = 0,
            P95LatencyMs = 1300,
            P99LatencyMs = 1800,
            HumanLikeScore = 0.9
        });

        adjustment.PacingProfile.Should().Be(AntiDetectionPacingProfile.Conservative);
        adjustment.EnableNavigatorPatch.Should().BeTrue();
        adjustment.PauseInteractions.Should().BeFalse();

        var state = await orchestrator.GetStateAsync("ctx-a");
        state.Should().NotBeNull();
        state!.Signals.Should().HaveCount(1);
        state.CurrentPacing.Should().Be(AntiDetectionPacingProfile.Conservative);
    }

    [Fact]
    public async Task ContinuousHealthySignals_Should_Promote_To_Aggressive()
    {
        var orchestrator = CreateOrchestrator(new AntiDetectionOrchestratorOptions
        {
            SlidingWindow = 6,
            AggressiveWindowRequirement = 3,
            MinimumAdjustmentInterval = TimeSpan.Zero
        });

        // 先触发一次保守策略
        await orchestrator.RecordAsync(new AntiDetectionSignal
        {
            ContextId = "ctx-b",
            Workflow = "Discovery",
            ObservedAtUtc = DateTimeOffset.UtcNow,
            TotalInteractions = 10,
            Http429 = 1,
            Http403 = 1,
            CaptchaChallenges = 0,
            P95LatencyMs = 1600,
            P99LatencyMs = 2000,
            HumanLikeScore = 0.8
        });

        // 连续三次健康信号
        for (var i = 0; i < 3; i++)
        {
            await orchestrator.RecordAsync(new AntiDetectionSignal
            {
                ContextId = "ctx-b",
                Workflow = "Discovery",
                ObservedAtUtc = DateTimeOffset.UtcNow.AddMinutes(i + 1),
                TotalInteractions = 12,
                Http429 = 0,
                Http403 = 0,
                CaptchaChallenges = 0,
                P95LatencyMs = 950,
                P99LatencyMs = 1100,
                HumanLikeScore = 0.98
            });
        }

        var adjustments = await orchestrator.GetRecentAdjustmentsAsync("ctx-b", take: 5);
        adjustments.Should().NotBeEmpty();
        adjustments.First().PacingProfile.Should().Be(AntiDetectionPacingProfile.Aggressive);
        adjustments.First().Reason.Should().Contain("连续窗口零异常");
    }

    private DefaultAntiDetectionOrchestrator CreateOrchestrator(AntiDetectionOrchestratorOptions options)
    {
        return new DefaultAntiDetectionOrchestrator(
            store,
            Options.Create(options),
            NullLogger<DefaultAntiDetectionOrchestrator>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
            // 忽略清理异常，测试环境允许残留目录。
        }
    }
}
