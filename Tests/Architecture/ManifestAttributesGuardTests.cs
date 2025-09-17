using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Tests.Architecture;

/// <summary>
/// Manifest 特性守卫：
/// 1) 仅允许带有 [McpServerTool] 的方法出现在带有 [McpServerToolType] 的类型中；
/// 2) 带有 [McpServerToolType] 的类型中，所有 public static 方法都必须带 [McpServerTool]；
///    （如果存在特殊情况，请将方法设为非 public，或提取到 Internal）。
/// </summary>
public class ManifestAttributesGuardTests
{
    [Test]
    public void ToolMethods_Must_Reside_In_ToolTypes_And_All_Public_Static_Methods_Must_Be_Annotated()
    {
        var asm = typeof(XiaoHongShuMCP.Tools.XiaoHongShuTools).Assembly;
        Func<Attribute, bool> IsToolTypeAttr = a => string.Equals(a.GetType().Name, "McpServerToolTypeAttribute", StringComparison.Ordinal);
        Func<Attribute, bool> IsToolMethodAttr = a => string.Equals(a.GetType().Name, "McpServerToolAttribute", StringComparison.Ordinal);

        var types = asm.GetTypes().Where(t => t.Namespace == "XiaoHongShuMCP.Tools").ToArray();

        // 1) 带 [McpServerTool] 的方法必须位于带 [McpServerToolType] 的类型中
        var toolMethods = types.SelectMany(t => t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance))
                               .Where(m => m.GetCustomAttributes().Any(a => IsToolMethodAttr(a)))
                               .ToArray();
        var violations1 = toolMethods.Where(m => m.DeclaringType!.GetCustomAttributes().All(a => !IsToolTypeAttr(a)))
                                     .Select(m => m.DeclaringType!.FullName + "." + m.Name).ToArray();
        if (violations1.Length > 0)
            Assert.Fail("发现带 [McpServerTool] 但不在 ToolType 类型中的方法:\n" + string.Join("\n", violations1));

        // 2) 对于带 [McpServerToolType] 的类型，所有 public static 方法必须带 [McpServerTool]
        var toolTypes = types.Where(t => t.GetCustomAttributes().Any(a => IsToolTypeAttr(a))).ToArray();
        var violations2 = toolTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public|BindingFlags.Static))
            .Where(m => !m.IsSpecialName) // 排除属性/运算符等
            .Where(m => m.GetCustomAttributes().All(a => !IsToolMethodAttr(a)))
            .Select(m => m.DeclaringType!.FullName + "." + m.Name)
            .ToArray();
        if (violations2.Length > 0)
            Assert.Fail("发现 ToolType 类型中未标注 [McpServerTool] 的 public static 方法:\n" + string.Join("\n", violations2));

        Assert.Pass();
    }
}
