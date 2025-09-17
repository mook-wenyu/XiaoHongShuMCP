using FluentAssertions;
using HushOps.Core.Browser;
using HushOps.Core.Humanization;

namespace HushOps.Core.Tests;

public class MinimumJerkTrajectoryTests
{
    [Fact]
    public void Generate_Path_Reaches_Target_And_Is_Reasonable()
    {
        var gen = new MinimumJerkTrajectoryGenerator();
        var p = new TrajectoryParams{ BaseDurationMs = 120, HotspotPauseProbability = 0.2, MicroJitterPx = 1.0 };
        var path = gen.Generate(new Point(0,0), new Point(100,50), p);

        path.Steps.Should().NotBeEmpty();
        var first = path.Steps.First();
        var last = path.Steps.Last();
        first.X.Should().Be(0);
        first.Y.Should().Be(0);
        last.X.Should().Be(100);
        last.Y.Should().Be(50);
        path.ExpectedDurationMs.Should().BeGreaterThan(80);
    }
}

