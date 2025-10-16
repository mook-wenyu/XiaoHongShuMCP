using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Network;

/// <summary>
/// 中文：网络会话上下文，ExitIp使用string类型确保可JSON序列化。
/// English: Network session context, ExitIp uses string type to ensure JSON serializability.
/// </summary>
public sealed record NetworkSessionContext(
    string ProxyId,
    string? ExitIp,
    double AverageLatencyMs,
    double FailureRate,
    bool BandwidthSimulated,
    string? ProxyAddress,
    string? ProxyUsername,
    string? ProxyPassword,
    int DelayMinMs,
    int DelayMaxMs,
    int MaxRetryAttempts,
    int RetryBaseDelayMs,
    int MitigationCount);

    public interface INetworkStrategyManager
    {
        Task<NetworkSessionContext> PrepareSessionAsync(string profileKey, CancellationToken cancellationToken);
        void RecordMitigation(string profileKey, int statusCode);
        int GetMitigationCount(string profileKey);
    }

/// <summary>
/// 中文：默认网络策略管理器，构造代理/节流元数据供后续集成使用。
/// English: Default network strategy manager producing metadata for throttling/ proxy usage.
/// </summary>
public sealed class NetworkStrategyManager : INetworkStrategyManager
{
    private readonly NetworkStrategyOptions _options;
    private readonly ILogger<NetworkStrategyManager> _logger;
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<string, int> _mitigationCounter = new(StringComparer.OrdinalIgnoreCase);

    public NetworkStrategyManager(IOptions<NetworkStrategyOptions> options, ILogger<NetworkStrategyManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<NetworkSessionContext> PrepareSessionAsync(string profileKey, CancellationToken cancellationToken)
    {
        var template = ResolveTemplate(profileKey);
        var latencyRange = template.RequestDelay.Normalize();
        var avgLatency = _random.Next(latencyRange.Min, latencyRange.Max);
        var failureRate = Math.Round(_random.NextDouble() * 0.02, 4); // 0-2%

        var proxyId = template.ProxyPool;
        string? exitIp = null;
        try
        {
            var ip = IPAddress.Parse("10." + _random.Next(10, 200) + "." + _random.Next(0, 255) + "." + _random.Next(0, 255));
            exitIp = ip.ToString();
        }
        catch
        {
            // ignore parse issue, keep null
        }

        var mitigationCount = GetMitigationCount(profileKey);

        var context = new NetworkSessionContext(
            proxyId,
            exitIp,
            avgLatency,
            failureRate,
            template.SimulateBandwidth,
            template.ProxyAddress,
            template.ProxyUsername,
            template.ProxyPassword,
            latencyRange.Min,
            latencyRange.Max,
            template.MaxRetryAttempts,
            template.RetryBaseDelayMs,
            mitigationCount);

        _logger.LogDebug(
            "[Network] profile={Profile} proxy={Proxy} latency={Latency}ms failureRate={FailureRate} address={Address}",
            profileKey,
            proxyId,
            avgLatency,
            failureRate,
            template.ProxyAddress ?? string.Empty);

        return Task.FromResult(context);
    }

    private NetworkTemplateOptions ResolveTemplate(string profileKey)
    {
        if (_options.Templates.TryGetValue(profileKey, out var template))
        {
            return template;
        }

        if (_options.Templates.TryGetValue(_options.DefaultTemplate, out var fallback))
        {
            return fallback;
        }

        return NetworkTemplateOptions.CreateDefault();
    }

    public void RecordMitigation(string profileKey, int statusCode)
    {
        var count = _mitigationCounter.AddOrUpdate(profileKey, 1, (_, current) => current + 1);
        _logger.LogWarning(
            "[Network] mitigation triggered profile={Profile} status={Status} total={Total}",
            profileKey,
            statusCode,
            count);
    }

    public int GetMitigationCount(string profileKey)
        => _mitigationCounter.TryGetValue(profileKey, out var count) ? count : 0;
}
