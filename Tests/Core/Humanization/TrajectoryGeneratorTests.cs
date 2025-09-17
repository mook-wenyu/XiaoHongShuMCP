using NUnit.Framework;
using HushOps.Core.Humanization;

namespace Tests.Core.Humanization;

/// <summary>
/// 最小加速度（近似最小 jerk）轨迹生成器的基本性质测试。
/// </summary>
public class TrajectoryGeneratorTests
{
    [Test]
    public void Generate_Returns_Path_From_Start_To_End_With_Settling()
    {
        var gen = new MinimumJerkTrajectoryGenerator();
        var start = (100.0, 100.0); var end = (400.0, 260.0);
        var path = gen.Generate(start, end, 1.0);

        Assert.That(path.Count, Is.GreaterThan(5));
        // 终点两步应接近目标
        var last = path[^1];
        Assert.That(System.Math.Abs(last.X - end.Item1), Is.LessThan(2.0));
        Assert.That(System.Math.Abs(last.Y - end.Item2), Is.LessThan(2.0));
        // 中间进度应大致单调（允许微抖动）：选取若干采样
        var p25 = path[path.Count/4];
        var p50 = path[path.Count/2];
        Assert.That(p25.X, Is.GreaterThan(start.Item1));
        Assert.That(p50.X, Is.GreaterThan(p25.X));
    }
}
