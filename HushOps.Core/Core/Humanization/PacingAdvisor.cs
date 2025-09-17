using Microsoft.Extensions.Options;

namespace HushOps.Core.Humanization;

/// <summary>
/// 节律自调优建议器（Core）：根据 429/403/RTT 信号调整延时倍率，并指数衰减回 1.0。
/// </summary>
public interface IPacingAdvisor
{
    void NotifyHttpStatus(int statusCode);
    void ObserveRtt(TimeSpan rtt);
    double CurrentMultiplier { get; }
}

/// <summary>
/// Core 人格化配置选项（仅承载与节律相关的参数）。
/// </summary>
public sealed class PersonaOptions
{
    public double Http429BaseMultiplier { get; set; } = 2.5;
    public double Http403BaseMultiplier { get; set; } = 2.0;
    public double MaxDelayMultiplier { get; set; } = 3.0;
    public int DegradeHalfLifeSeconds { get; set; } = 60;
}

public sealed class PacingAdvisor : IPacingAdvisor
{
    private readonly object _lock = new();
    private readonly double _maxMultiplier;
    private readonly double _base403;
    private readonly double _base429;
    private readonly TimeSpan _halfLife;
    private long _lastSignalTicks; // UTC ticks
    private double _lastBase; // 上次信号目标倍率

    public PacingAdvisor(IOptions<PersonaOptions> options)
    {
        var opt = options.Value ?? new PersonaOptions();
        _maxMultiplier = Math.Clamp(opt.MaxDelayMultiplier <= 0 ? 2.5 : opt.MaxDelayMultiplier, 1.0, 5.0);
        _base403 = Math.Min(_maxMultiplier, opt.Http403BaseMultiplier <= 0 ? 2.0 : opt.Http403BaseMultiplier);
        _base429 = Math.Min(_maxMultiplier, opt.Http429BaseMultiplier <= 0 ? 2.5 : opt.Http429BaseMultiplier);
        _halfLife = TimeSpan.FromSeconds(Math.Max(10, opt.DegradeHalfLifeSeconds <= 0 ? 60 : opt.DegradeHalfLifeSeconds));
        _lastSignalTicks = DateTimeOffset.UtcNow.Ticks;
        _lastBase = 1.0;
    }

    public void NotifyHttpStatus(int statusCode)
    {
        if (statusCode != 429 && statusCode != 403) return;
        lock (_lock)
        {
            _lastSignalTicks = DateTimeOffset.UtcNow.Ticks;
            _lastBase = Math.Min(_maxMultiplier, statusCode == 429 ? _base429 : _base403);
        }
    }

    public void ObserveRtt(TimeSpan rtt)
    {
        if (rtt <= TimeSpan.Zero) return;
        lock (_lock)
        {
            double adj = 0.0;
            if (rtt.TotalMilliseconds > 2000) adj = 0.2;
            else if (rtt.TotalMilliseconds < 300) adj = -0.1;
            _lastBase = Math.Clamp(_lastBase + adj, 1.0, _maxMultiplier);
        }
    }

    public double CurrentMultiplier
    {
        get
        {
            lock (_lock)
            {
                var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(_lastSignalTicks, TimeSpan.Zero);
                if (_lastBase <= 1.0) return 1.0;
                var factor = Math.Pow(0.5, elapsed.TotalSeconds / _halfLife.TotalSeconds);
                var mult = 1.0 + (_lastBase - 1.0) * factor;
                return Math.Clamp(mult, 1.0, _maxMultiplier);
            }
        }
    }
}

