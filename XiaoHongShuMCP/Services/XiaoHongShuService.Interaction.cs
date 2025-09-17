using System;
using System.Threading.Tasks;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// XiaoHongShuService 的交互重载（partial）：
/// - 提供字符串动作版签名以契合 MCP 工具层：likeAction/favoriteAction ∈ { "do", "none" }；
/// - 内部归一化为布尔开关后委托既有实现。
/// </summary>
public partial class XiaoHongShuService
{
    /// <summary>
    /// 交互组合：点赞/收藏（字符串动作版）
    /// - likeAction: "do" 表示执行点赞；"none" 表示不操作。
    /// - favoriteAction: 同上，用于收藏操作。
    /// 说明：为兼容 CLI/MCP 入参而提供的语义包装；后续若扩展 "undo"、"toggle" 可在此扩展。
    /// </summary>
    public async Task<OperationResult<InteractionBundleResult>> InteractNoteAsync(string keyword, string likeAction, string favoriteAction)
    {
        static bool ToFlag(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            var a = action.Trim().ToLowerInvariant();
            return a is "do" or "yes" or "y" or "true" or "on" or "1";
        }

        var like = ToFlag(likeAction);
        var fav = ToFlag(favoriteAction);
        return await _noteEngagementWorkflow.InteractAsync(keyword, like, fav).ConfigureAwait(false);
    }
}

