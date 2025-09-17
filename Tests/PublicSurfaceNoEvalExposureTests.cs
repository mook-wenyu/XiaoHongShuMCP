using System.Reflection;
using NUnit.Framework;

namespace Tests;

public class PublicSurfaceNoEvalExposureTests
{
    [Test]
    public void XiaoHongShuMCP_PublicSurface_ShouldNotExpose_Evaluate_Or_OuterHtml()
    {
        var asm = Assembly.Load("XiaoHongShuMCP");
        var bad = new List<string>();
        foreach (var t in asm.GetExportedTypes())
        {
            // 仅检查本项目命名空间公开类型
            if (t.Namespace is null || !t.Namespace.StartsWith("XiaoHongShuMCP", StringComparison.Ordinal)) continue;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.Name.Contains("Evaluate", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("OuterHtml", StringComparison.OrdinalIgnoreCase))
                {
                    bad.Add($"{t.FullName}.{m.Name}");
                    continue;
                }
                foreach (var p in m.GetParameters())
                {
                    var pn = p.Name ?? string.Empty;
                    if (pn.Contains("script", StringComparison.OrdinalIgnoreCase) ||
                        pn.Contains("javascript", StringComparison.OrdinalIgnoreCase))
                    {
                        bad.Add($"{t.FullName}.{m.Name}({p.ParameterType.Name} {p.Name})");
                        break;
                    }
                }
            }
        }
        if (bad.Count > 0)
        {
            Assert.Fail("发现公开 API 含 Evaluate/OuterHtml 或脚本参数：\n" + string.Join("\n", bad));
        }
    }
}

