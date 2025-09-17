using System;
using System.Collections.Generic;

namespace HushOps.Core.Humanization;

/// <summary>
/// 轨迹点：表示一次鼠标移动的目标坐标与在该点建议的停顿（毫秒）。
/// </summary>
public readonly record struct TrajectoryPoint(double X, double Y, int PauseMs = 0);

/// <summary>
/// 轨迹生成器接口：根据起止点与人物画像参数，生成拟人化鼠标移动路径。
/// 约束：
/// - 仅生成坐标与建议停顿，实际延时由上层控制（配合 DelayManager）。
/// - 禁止脚本注入；纯数学生成，无副作用。
/// </summary>
public interface ITrajectoryGenerator
{
    /// <summary>
    /// 生成拟人化轨迹。
    /// </summary>
    /// <param name="start">起点（可空，表示未知，内部将使用安全默认）。</param>
    /// <param name="end">终点。</param>
    /// <param name="speedMultiplier">速度倍率（由上层 PacingAdvisor 决定，1.0 为基准）。</param>
    IReadOnlyList<TrajectoryPoint> Generate((double x, double y)? start, (double x, double y) end, double speedMultiplier = 1.0);
}

/// <summary>
/// 最小加加速度（近似最小 jerk）轨迹生成器：
/// - 使用五次多项式时间标度 p(t)=p0 + (p1-p0)*(10t^3 - 15t^4 + 6t^5)；
/// - 总时长按距离/目标速度估算并加入波动；
/// - 在目标附近引入热点停顿（hotspot pause），再做微小抖动；
/// - 允许缺失 start（首次移动）：退化为从视口左上安全边距随机点起步。
/// </summary>
public sealed class MinimumJerkTrajectoryGenerator : ITrajectoryGenerator
{
    private readonly Random _rng = Random.Shared;

    public IReadOnlyList<TrajectoryPoint> Generate((double x, double y)? start, (double x, double y) end, double speedMultiplier = 1.0)
    {
        // 若缺失起点，取一个“屏幕常见出发点”（左上角 80~160px 边距）
        var s = start ?? (80 + _rng.Next(80), 80 + _rng.Next(80));
        var dx = end.x - s.x; var dy = end.y - s.y; var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return new[] { new TrajectoryPoint(end.x, end.y, 0) };

        // 期望速度：基础 900 px/s，受 Persona 倍率与随机扰动影响（保持在 500~1600 范围）
        var baseSpeed = 900.0 * Clamp(speedMultiplier, 0.8, 1.8) * (0.85 + _rng.NextDouble() * 0.3);
        baseSpeed = Math.Clamp(baseSpeed, 500, 1600);
        var duration = dist / baseSpeed; // 秒
        duration = Math.Clamp(duration, 0.18, 1.6); // 控时：极短或极长都不自然

        // 离散步数：每步 ~8~16ms（60~120 FPS），按时长自适应
        var stepMs = 10 + _rng.Next(6);
        var steps = Math.Max(6, (int)Math.Ceiling(duration * 1000 / stepMs));

        var list = new List<TrajectoryPoint>(steps + 3);
        for (int i = 1; i <= steps; i++)
        {
            var t = i / (double)steps;                        // 0..1
            var tau = (10 * t * t * t) - (15 * t * t * t * t) + (6 * t * t * t * t * t); // 五次最小jerk曲线
            // 轻微横向抖动：与路径正交的微扰（像素级）
            var orthoX = -dy / dist; var orthoY = dx / dist;
            var jitter = (_rng.NextDouble() - 0.5) * 0.8;      // ±0.4 px 级
            var x = s.x + dx * tau + orthoX * jitter;
            var y = s.y + dy * tau + orthoY * jitter;
            // 在靠近终点 85% 处增加微停顿提高自然度（hotspot）
            var pause = (t > 0.85) ? _rng.Next(12, 28) : 0;
            list.Add(new TrajectoryPoint(x, y, pause));
        }

        // 终点微抖与停顿（准备点击）
        var settle = new[]
        {
            new TrajectoryPoint(end.x + (_rng.NextDouble()-0.5)*0.5, end.y + (_rng.NextDouble()-0.5)*0.5, _rng.Next(25, 45)),
            new TrajectoryPoint(end.x, end.y, _rng.Next(20, 35))
        };
        list.AddRange(settle);
        return list;
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
}
