using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：定义脚本生成器，根据高层动作请求输出拟人化动作脚本。
/// English: Describes a builder that converts high-level action requests into humanized action scripts.
/// </summary>
public interface IHumanizedActionScriptBuilder
{
    /// <summary>
    /// 中文：基于请求、动作类型与关键字生成脚本。
    /// English: Builds a script for the specified request, action kind and resolved keyword.
    /// </summary>
    HumanizedActionScript Build(HumanizedActionRequest request, HumanizedActionKind kind, string keyword);
}
