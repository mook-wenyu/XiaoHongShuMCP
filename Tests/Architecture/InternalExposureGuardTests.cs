using System.Linq;
using NUnit.Framework;

namespace Tests.Architecture;

/// <summary>
/// 暴露面守卫：禁止命名空间 XiaoHongShuMCP.Internal 下出现公开类型。
/// </summary>
public class InternalExposureGuardTests
{
    [Test]
    public void InternalNamespace_Should_Not_Have_Public_Types()
    {
        var asm = typeof(XiaoHongShuMCP.Tools.XiaoHongShuTools).Assembly;
        var publics = asm.GetTypes().Where(t => string.Equals(t.Namespace, "XiaoHongShuMCP.Internal") && t.IsPublic).ToList();
        if (publics.Count > 0)
        {
            Assert.Fail("检测到公开 Internal 类型:\n" + string.Join("\n", publics.Select(t => t.FullName)));
        }
        Assert.Pass();
    }
}
