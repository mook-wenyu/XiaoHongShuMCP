using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

/// <summary>
/// 中文：根据配置提供拟人化动作之间的随机延迟。
/// </summary>
public sealed class HumanDelayProvider : IHumanDelayProvider
{
    private readonly IRandomDelayConfiguration _configuration;
    private readonly ILogger<HumanDelayProvider> _logger;

    public HumanDelayProvider(IRandomDelayConfiguration configuration, ILogger<HumanDelayProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task DelayBetweenActionsAsync(CancellationToken cancellationToken)
    {
        var delay = _configuration.GetDelay();
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        _logger.LogDebug("[HumanDelay] wait {Delay}ms", delay.TotalMilliseconds);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
}

public interface IRandomDelayConfiguration
{
    TimeSpan GetDelay();
}
