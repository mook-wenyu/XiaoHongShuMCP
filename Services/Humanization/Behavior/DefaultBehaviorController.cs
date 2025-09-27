using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;

/// <summary>
/// 中文：行为控制器接口，负责在操作前后注入拟人化行为。
/// English: Interface for controllers that simulate human behaviour around each action.
/// </summary>
public interface IBehaviorController
{
    Task<BehaviorTrace> ExecuteBeforeActionAsync(BehaviorActionContext context);
    Task<BehaviorTrace> ExecuteAfterActionAsync(BehaviorActionContext context, BehaviorResult result);
}

/// <summary>
/// 中文：默认实现，基于配置执行延迟与犹豫。
/// English: Default controller that applies configurable delays and hesitation.
/// </summary>
public sealed class DefaultBehaviorController : IBehaviorController
{
    private readonly HumanBehaviorOptions _options;
    private readonly ILogger<DefaultBehaviorController> _logger;

    public DefaultBehaviorController(IOptions<HumanBehaviorOptions> options, ILogger<DefaultBehaviorController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BehaviorTrace> ExecuteBeforeActionAsync(BehaviorActionContext context)
    {
        var profile = ResolveProfile(context.ProfileKey);
        var preDelay = NextDelay(profile.PreActionDelay);
        var hesitationDelay = ShouldHesitate(profile, context.ActionType)
            ? NextDelay(profile.ScrollDelay)
            : TimeSpan.Zero;

        var combinedDelay = preDelay + hesitationDelay;
        if (combinedDelay > TimeSpan.Zero)
        {
            _logger.LogDebug(
                "[Behavior] pre-action delay={Delay}ms hesitation={Hesitation}ms profile={Profile} action={Action}",
                preDelay.TotalMilliseconds,
                hesitationDelay.TotalMilliseconds,
                context.ProfileKey,
                context.ActionType);

            await Task.Delay(combinedDelay, context.CancellationToken).ConfigureAwait(false);
        }

        return new BehaviorTrace(
            context.ActionType,
            combinedDelay.TotalMilliseconds,
            Array.Empty<double>(),
            TypoCount: 0,
            ScrollSegments: hesitationDelay > TimeSpan.Zero ? Math.Min(profile.MaxScrollSegments, 1) : 0,
            Extras: new Dictionary<string, string>
            {
                ["preDelayMs"] = preDelay.TotalMilliseconds.ToString("F0"),
                ["hesitationDelayMs"] = hesitationDelay.TotalMilliseconds.ToString("F0")
            });
    }

    public async Task<BehaviorTrace> ExecuteAfterActionAsync(BehaviorActionContext context, BehaviorResult result)
    {
        var profile = ResolveProfile(context.ProfileKey);
        var postDelay = NextDelay(profile.PostActionDelay);
        if (postDelay > TimeSpan.Zero)
        {
            _logger.LogDebug(
                "[Behavior] post-action delay={Delay}ms profile={Profile} action={Action} success={Success}",
                postDelay.TotalMilliseconds,
                context.ProfileKey,
                context.ActionType,
                result.Success);

            await Task.Delay(postDelay, context.CancellationToken).ConfigureAwait(false);
        }

        return new BehaviorTrace(
            context.ActionType,
            postDelay.TotalMilliseconds,
            Array.Empty<double>(),
            TypoCount: 0,
            ScrollSegments: 0,
            Extras: new Dictionary<string, string>
            {
                ["postDelayMs"] = postDelay.TotalMilliseconds.ToString("F0"),
                ["resultStatus"] = result.Status
            });
    }

    private HumanBehaviorProfileOptions ResolveProfile(string? profileKey)
    {
        if (!string.IsNullOrWhiteSpace(profileKey) &&
            _options.Profiles.TryGetValue(profileKey, out var configured))
        {
            return configured;
        }

        if (_options.Profiles.TryGetValue(_options.DefaultProfile, out var fallback))
        {
            return fallback;
        }

        // 保底返回默认模板，避免空配置。
        return HumanBehaviorProfileOptions.CreateDefault();
    }

    private static TimeSpan NextDelay(DelayRangeOptions range)
    {
        var (min, max) = range.Normalize();
        if (max <= 0)
        {
            return TimeSpan.Zero;
        }

        var value = Random.Shared.Next(min, max);
        return TimeSpan.FromMilliseconds(value);
    }

    private static bool ShouldHesitate(HumanBehaviorProfileOptions profile, BehaviorActionType actionType)
    {
        if (profile.HesitationProbability <= 0)
        {
            return false;
        }

        return actionType is BehaviorActionType.NavigateKeyword or BehaviorActionType.NavigateRandom or BehaviorActionType.Capture
            ? Random.Shared.NextDouble() < profile.HesitationProbability
            : false;
    }
}
