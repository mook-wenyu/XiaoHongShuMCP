using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using XiaoHongShuMCP.Infrastructure;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tools;

/// <summary>
/// 小红书工具集：
/// - 复用既有服务能力，保持拟人化与反检测增强；
/// - 禁止使用 JS 注入，通过服务层统一实现；
/// - 集成 MCP Elicitation，为关键写操作提供拟人化确认链路。
/// </summary>
[McpServerToolType]
public class XiaoHongShuTools
{
    private readonly IAccountManager _accountManager;
    private readonly IXiaoHongShuService _xiaoHongShuService;
    private readonly IBrowserManager _browserManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IMcpElicitationClient _elicitationClient;

    /// <summary>
    /// 通过依赖注入装配核心服务，避免 Service Locator，提升可测试性。
    /// </summary>
    public XiaoHongShuTools(
        IAccountManager accountManager,
        IXiaoHongShuService xiaoHongShuService,
        IBrowserManager browserManager,
        IHumanizedInteractionService humanizedInteraction,
        IMcpElicitationClient elicitationClient)
    {
        _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
        _xiaoHongShuService = xiaoHongShuService ?? throw new ArgumentNullException(nameof(xiaoHongShuService));
        _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
        _humanizedInteraction = humanizedInteraction ?? throw new ArgumentNullException(nameof(humanizedInteraction));
        _elicitationClient = elicitationClient ?? throw new ArgumentNullException(nameof(elicitationClient));
    }

    /// <summary>
    /// 连接浏览器并可选等待登录完成。
    /// </summary>
    [McpServerTool]
    public async Task<BrowserConnectionResult> ConnectToBrowser(
        [Description("是否等待至登录成功（默认 false）")] bool waitUntilLoggedIn = false,
        [Description("最长等待秒数（默认120）")] int maxWaitSeconds = 120,
        [Description("轮询间隔毫秒（默认2000）")] int pollMs = 2000,
        [Description("可选请求ID（便于日志串联）")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        return await McpToolExecutor.TryAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _accountManager.ConnectToBrowserAsync();
            var connected = result.Success || string.Equals(result.ErrorCode, "NOT_LOGGED_IN", StringComparison.Ordinal);
            var loggedIn = result.Success && result.Data;
            if (!loggedIn && connected && waitUntilLoggedIn)
            {
                var maxWait = TimeSpan.FromSeconds(Math.Max(1, maxWaitSeconds));
                var poll = TimeSpan.FromMilliseconds(Math.Max(200, pollMs));
                loggedIn = await _accountManager.WaitUntilLoggedInAsync(maxWait, poll);
            }

            if (!connected)
            {
                return new BrowserConnectionResult(false, false, result.ErrorMessage ?? "连接失败", result.ErrorCode, false, requestId);
            }

            return new BrowserConnectionResult(
                true,
                loggedIn,
                loggedIn ? "浏览器连接并已登录" : (waitUntilLoggedIn ? "已连接，但等待登录超时" : (result.ErrorMessage ?? "已连接，未登录")),
                loggedIn ? null : (waitUntilLoggedIn ? "WAIT_LOGIN_TIMEOUT" : result.ErrorCode),
                loggedIn ? null : (waitUntilLoggedIn ? true : null),
                requestId);
        },
        (ex, rid) => new BrowserConnectionResult(false, false, $"连接异常: {ex.Message}", "CONNECTION_EXCEPTION", true, rid),
        requestId);
    }

    /// <summary>
    /// 基于关键词查找单个笔记详情。
    /// </summary>
    [McpServerTool]
    public async Task<NoteDetailResult> GetNoteDetail(
        [Description("搜索关键词")] string keyword,
        [Description("是否包含评论")] bool includeComments = false,
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        return await McpToolExecutor.TryAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var op = await _xiaoHongShuService.GetNoteDetailAsync(keyword, includeComments);
            if (op.Success && op.Data is not null)
            {
                return new NoteDetailResult(op.Data, true, "ok", null);
            }

            return new NoteDetailResult(null, false, op.ErrorMessage ?? "获取失败", "GET_NOTE_DETAIL_FAILED");
        },
        (ex, rid) => new NoteDetailResult(null, false, $"异常: {ex.Message}", McpToolExecutor.MapExceptionCode(ex)),
        requestId);
    }

    /// <summary>
    /// 基于关键词定位并点赞笔记（拟人化）。
    /// </summary>
    [McpServerTool]
    public async Task<InteractionResult> LikeNote(
        [Description("关键词")] string keyword,
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var idemKey = McpToolUtils.ComputeInputHash(("keyword", keyword));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(LikeNote),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var op = await _xiaoHongShuService.LikeNoteAsync(keyword);
                if (op.Success && op.Data is not null) return op.Data;
                return new InteractionResult(false, "like", "unknown", "unknown", op.ErrorMessage ?? "点赞失败", "LIKE_FAILED");
            },
            (ex, rid) => new InteractionResult(false, "like", "unknown", "unknown", $"异常: {ex.Message}", McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 基于关键词定位并收藏笔记（拟人化）。
    /// </summary>
    [McpServerTool]
    public async Task<InteractionResult> FavoriteNote(
        [Description("关键词")] string keyword,
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var idemKey = McpToolUtils.ComputeInputHash(("keyword", keyword));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(FavoriteNote),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var op = await _xiaoHongShuService.FavoriteNoteAsync(keyword);
                if (op.Success && op.Data is not null) return op.Data;
                return new InteractionResult(false, "favorite", "unknown", "unknown", op.ErrorMessage ?? "收藏失败", "FAVORITE_FAILED");
            },
            (ex, rid) => new InteractionResult(false, "favorite", "unknown", "unknown", $"异常: {ex.Message}", McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 基于关键词执行组合交互（点赞/收藏）。
    /// </summary>
    [McpServerTool]
    public async Task<InteractionBundleResult> InteractNote(
        [Description("关键词")] string keyword,
        [Description("点赞指令：do/cancel/none")] string likeAction = "none",
        [Description("收藏指令：do/cancel/none")] string favoriteAction = "none",
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var idemKey = McpToolUtils.ComputeInputHash(("keyword", keyword), ("likeAction", likeAction), ("favoriteAction", favoriteAction));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(InteractNote),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var op = await _xiaoHongShuService.InteractNoteAsync(keyword, likeAction, favoriteAction);
                if (op.Success && op.Data is not null) return op.Data;
                return new InteractionBundleResult(false, null, null, op.ErrorMessage ?? "交互失败", "INTERACT_FAILED");
            },
            (ex, rid) => new InteractionBundleResult(false, null, null, $"异常: {ex.Message}", McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 基于关键词发布评论（拟人化输入），执行前通过 Elicitation 二次确认。
    /// </summary>
    [McpServerTool]
    public async Task<CommentResult> PostComment(
        [Description("关键词")] string keyword,
        [Description("评论内容")] string content,
        [Description("请求ID")] string? requestId = null,
        IMcpServer? server = null,
        CancellationToken cancellationToken = default)
    {
        var elicited = await ExecuteCommentElicitationAsync(server, keyword, content, cancellationToken);
        if (!elicited.Proceed)
        {
            return new CommentResult(false, "用户在确认环节取消了评论", string.Empty, "COMMENT_CANCELLED");
        }

        var finalContent = elicited.Content ?? content;
        var idemKey = McpToolUtils.ComputeInputHash(("keyword", keyword), ("content", finalContent));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(PostComment),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var op = await _xiaoHongShuService.PostCommentAsync(keyword, finalContent);
                if (op.Success && op.Data is not null) return op.Data;
                return new CommentResult(false, op.ErrorMessage ?? "评论失败", string.Empty, "COMMENT_FAILED");
            },
            (ex, rid) => new CommentResult(false, $"异常: {ex.Message}", string.Empty, McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 滚动当前页面（拟人化滚轮分段），不使用 JS 注入。
    /// </summary>
    [McpServerTool]
    public async Task<object> ScrollCurrentPage(
        [Description("目标滚动距离，0 表示随机 300-800")] int targetDistance = 0,
        [Description("是否等待内容加载")] bool waitForLoad = true,
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(ScrollCurrentPage),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await _browserManager.GetAutoPageAsync();
                await _humanizedInteraction.HumanScrollAsync(page, targetDistance, waitForLoad, ct);
                return new { status = "ok", targetDistance, waitForLoad } as object;
            },
            (ex, rid) => new { status = "error", errorCode = McpToolExecutor.MapExceptionCode(ex), message = ex.Message, requestId = rid } as object,
            requestId: requestId);
    }

    /// <summary>
    /// 暂存笔记并离开编辑页面（拟人化输入），执行前通过 Elicitation 二次确认。
    /// </summary>
    [McpServerTool]
    public async Task<DraftSaveResult> TemporarySaveAndLeave(
        [Description("标题")] string title,
        [Description("正文")] string content,
        [Description("类型 Image/Video")] NoteType noteType,
        [Description("图片路径列表（数组优先）")] string[]? images = null,
        [Description("图片路径（;分隔，兼容旧参数）")] string? imagePaths = null,
        [Description("视频路径")] string? videoPath = null,
        [Description("标签（;分隔）")] string? tags = null,
        [Description("请求ID")] string? requestId = null,
        IMcpServer? server = null,
        CancellationToken cancellationToken = default)
    {
        var elicited = await ExecuteDraftElicitationAsync(server, title, content, cancellationToken);
        if (!elicited.Proceed)
        {
            return new DraftSaveResult(false, "用户在确认环节取消了暂存", null, "DRAFT_CANCELLED");
        }

        var finalContent = elicited.Content ?? content;
        var idemKey = McpToolUtils.ComputeInputHash(("title", title), ("content", finalContent), ("noteType", noteType.ToString()));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(TemporarySaveAndLeave),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<string>? imgs = null;
                if (images is { Length: > 0 })
                {
                    imgs = images.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
                }
                else if (!string.IsNullOrWhiteSpace(imagePaths))
                {
                    imgs = imagePaths.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                }

                var tagList = string.IsNullOrWhiteSpace(tags)
                    ? null
                    : tags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                var op = await _xiaoHongShuService.TemporarySaveAndLeaveAsync(title, finalContent, noteType, imgs, videoPath, tagList);
                if (op.Success && op.Data is not null) return op.Data;
                return new DraftSaveResult(false, op.ErrorMessage ?? "暂存失败", null, "DRAFT_SAVE_FAILED");
            },
            (ex, rid) => new DraftSaveResult(false, $"异常: {ex.Message}", null, McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 取消点赞（新 MCP 工具）。
    /// </summary>
    [McpServerTool]
    public async Task<InteractionResult> UnlikeNote(
        [Description("关键词")] string keyword,
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var idemKey = McpToolUtils.ComputeInputHash(("keyword", keyword));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(UnlikeNote),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var op = await _xiaoHongShuService.UnlikeNoteAsync(keyword);
                if (op.Success && op.Data is not null) return op.Data;
                return new InteractionResult(false, "取消点赞", "未知", "未知", op.ErrorMessage ?? "取消点赞失败", "UNLIKE_FAILED");
            },
            (ex, rid) => new InteractionResult(false, "取消点赞", "未知", "未知", $"异常: {ex.Message}", McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 取消收藏（新 MCP 工具）。
    /// </summary>
    [McpServerTool]
    public async Task<InteractionResult> UncollectNote(
        [Description("关键词")] string keyword,
        [Description("请求ID")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var idemKey = McpToolUtils.ComputeInputHash(("keyword", keyword));
        return await McpToolExecutor.TryWithPolicyAsync(
            nameof(XiaoHongShuTools),
            nameof(UncollectNote),
            async ct =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var op = await _xiaoHongShuService.UncollectNoteAsync(keyword);
                if (op.Success && op.Data is not null) return op.Data;
                return new InteractionResult(false, "取消收藏", "未知", "未知", op.ErrorMessage ?? "取消收藏失败", "UNFAVORITE_FAILED");
            },
            (ex, rid) => new InteractionResult(false, "取消收藏", "未知", "未知", $"异常: {ex.Message}", McpToolExecutor.MapExceptionCode(ex)),
            idempotencyKey: idemKey,
            requestId: requestId);
    }

    /// <summary>
    /// 评论操作的 Elicitation 建模：确认与可选内容修订。
    /// </summary>
    private async Task<(bool Proceed, string? Content)> ExecuteCommentElicitationAsync(
        IMcpServer? server,
        string keyword,
        string originalContent,
        CancellationToken cancellationToken)
    {
        if (server is null)
        {
            return (true, null);
        }

        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["confirm"] = new ElicitRequestParams.BooleanSchema
                {
                    Title = "是否继续发布",
                    Description = "选择 true 表示确认发布，false 表示取消。"
                },
                ["updatedContent"] = new ElicitRequestParams.StringSchema
                {
                    Title = "可选的修改后评论",
                    Description = "如需调整评论内容，可填写新的文本；留空表示维持原内容。",
                    MaxLength = 2000
                }
            },
            Required = new List<string> { "confirm" }
        };

        var prompt = new ElicitRequestParams
        {
            Message = $"即将在关键词“{keyword}”命中的笔记下发布如下评论：\n{originalContent}\n请确认是否继续。",
            RequestedSchema = schema
        };

        var result = await _elicitationClient.TryElicitAsync(server, prompt, cancellationToken);
        if (result is null || !IsSubmitted(result.Action))
        {
            return (false, null);
        }

        if (result.Content is null ||
            !result.Content.TryGetValue("confirm", out var confirmElement) ||
            confirmElement.ValueKind != JsonValueKind.True)
        {
            return (false, null);
        }

        if (result.Content.TryGetValue("updatedContent", out var updatedElement) && updatedElement.ValueKind == JsonValueKind.String)
        {
            var updated = updatedElement.GetString();
            if (!string.IsNullOrWhiteSpace(updated))
            {
                return (true, updated);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 草稿暂存前的 Elicitation：确保用户确认并提供可选正文修订。
    /// </summary>
    private async Task<(bool Proceed, string? Content)> ExecuteDraftElicitationAsync(
        IMcpServer? server,
        string title,
        string originalContent,
        CancellationToken cancellationToken)
    {
        if (server is null)
        {
            return (true, null);
        }

        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["confirm"] = new ElicitRequestParams.BooleanSchema
                {
                    Title = "是否继续暂存",
                    Description = "选择 true 表示确认保存草稿，false 表示取消。"
                },
                ["updatedContent"] = new ElicitRequestParams.StringSchema
                {
                    Title = "可选的修改后正文",
                    Description = "如需对正文做最后调整，可填写新的内容；留空保持原样。",
                    MaxLength = 5000
                }
            },
            Required = new List<string> { "confirm" }
        };

        var prompt = new ElicitRequestParams
        {
            Message = $"准备暂存标题为“{title}”的草稿，正文预览：\n{TruncateForPreview(originalContent)}\n请确认是否继续。",
            RequestedSchema = schema
        };

        var result = await _elicitationClient.TryElicitAsync(server, prompt, cancellationToken);
        if (result is null || !IsSubmitted(result.Action))
        {
            return (false, null);
        }

        if (result.Content is null ||
            !result.Content.TryGetValue("confirm", out var confirmElement) ||
            confirmElement.ValueKind != JsonValueKind.True)
        {
            return (false, null);
        }

        if (result.Content.TryGetValue("updatedContent", out var updatedElement) && updatedElement.ValueKind == JsonValueKind.String)
        {
            var updated = updatedElement.GetString();
            if (!string.IsNullOrWhiteSpace(updated))
            {
                return (true, updated);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 判断 Elicitation 的 Action 是否表示提交成功。
    /// </summary>
    private static bool IsSubmitted(string? action)
    {
        return string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "submitted", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 将正文裁剪为适合展示的预览文本。
    /// </summary>
    private static string TruncateForPreview(string content)
    {
        const int limit = 200;
        if (string.IsNullOrWhiteSpace(content))
        {
            return "(正文为空)";
        }

        return content.Length <= limit ? content : content[..limit] + "…";
    }
}

internal static class McpToolUtils
{
    /// <summary>
    /// 计算稳定输入哈希（用于 ByInputHash 幂等键）。
    /// </summary>
    public static string ComputeInputHash(params (string name, object? value)[] entries)
    {
        var ordered = entries.OrderBy(e => e.name, StringComparer.Ordinal).ToArray();
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var ms = new System.IO.MemoryStream();
        using (var tw = new System.IO.StreamWriter(ms, System.Text.Encoding.UTF8, 1024, leaveOpen: true))
        {
            for (int i = 0; i < ordered.Length; i++)
            {
                var (n, v) = ordered[i];
                tw.Write(n);
                tw.Write('=');
                tw.Write(v?.ToString() ?? "<null>");
                if (i < ordered.Length - 1) tw.Write('|');
            }
        }
        ms.Position = 0;
        var hash = sha.ComputeHash(ms);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 简单 CSV 转义（双引号包裹并转义内部双引号）。
    /// </summary>
    public static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var v = s.Replace("\"", "\"\"");
        return $"\"{v}\"";
    }
}

/// <summary>
/// MCP Elicitation 调用抽象，方便在测试中替换实现。
/// </summary>
public interface IMcpElicitationClient
{
    /// <summary>
    /// 调用服务器侧 Elicitation，返回结果；允许根据需要返回 null 代表无法完成。
    /// </summary>
    Task<ElicitResult?> TryElicitAsync(IMcpServer server, ElicitRequestParams request, CancellationToken cancellationToken);
}

/// <summary>
/// 默认的 Elicitation 调用实现，直接委托给 MCP SDK 扩展方法。
/// </summary>
public sealed class DefaultMcpElicitationClient : IMcpElicitationClient
{
    public async Task<ElicitResult?> TryElicitAsync(IMcpServer server, ElicitRequestParams request, CancellationToken cancellationToken)
    {
        return await server.ElicitAsync(request, cancellationToken);
    }
}
