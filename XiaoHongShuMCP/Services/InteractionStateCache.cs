using System.Collections.Concurrent;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 临时交互状态缓存（进程内，线程安全）。
/// - 用途：当打开“笔记详情”页且命中 Feed API 时，记录是否已点赞/已收藏及计数，供后续交互读取。
/// - 生命周期：仅缓存最近一次捕获的状态；不做持久化，随进程生命周期存在。
/// - 线程安全：使用 <see cref="ConcurrentDictionary{TKey,TValue}"/>。
/// - 注意：仅作为“权威API响应”的读取来源；不从DOM推断，不主动更新（除非上层据API动作回执更新）。
/// </summary>
public static class InteractionStateCache
{
    public record Snapshot(
        string NoteId,
        bool IsLiked,
        bool IsCollected,
        int LikeCount,
        int CollectCount,
        int CommentCount,
        DateTime CapturedAtUtc
    );

    private static readonly ConcurrentDictionary<string, Snapshot> Map = new();
    private static volatile string? _lastNoteId;

    /// <summary>
    /// 从 NoteDetail 写入/覆盖快照。
    /// </summary>
    public static void SetFromNoteDetail(NoteDetail note)
    {
        if (string.IsNullOrWhiteSpace(note.Id)) return;
        Map[note.Id] = new Snapshot(
            NoteId: note.Id,
            IsLiked: note.IsLiked,
            IsCollected: note.IsCollected,
            LikeCount: note.LikeCount ?? 0,
            CollectCount: note.FavoriteCount ?? 0,
            CommentCount: note.CommentCount ?? 0,
            CapturedAtUtc: DateTime.UtcNow
        );
        _lastNoteId = note.Id;
    }

    /// <summary>
    /// 尝试读取快照（可选校验时效）。
    /// </summary>
    /// <param name="noteId">笔记ID</param>
    /// <param name="maxAge">允许的最大年龄；传 null 表示不校验时效</param>
    public static bool TryGet(string noteId, out Snapshot? snapshot, TimeSpan? maxAge = null)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(noteId)) return false;
        if (!Map.TryGetValue(noteId, out var snap)) return false;
        if (maxAge.HasValue && DateTime.UtcNow - snap.CapturedAtUtc > maxAge.Value) return false;
        snapshot = snap;
        return true;
    }

    /// <summary>
    /// 尝试获取最近一次由 API 监听写入的笔记快照。
    /// 依赖 UniversalApiMonitor 的处理器调用 SetFromNoteDetail 后维护的最后ID。
    /// </summary>
    public static bool TryGetMostRecent(out Snapshot? snapshot, TimeSpan? maxAge = null)
    {
        snapshot = null;
        var id = _lastNoteId;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return TryGet(id!, out snapshot, maxAge);
    }

    /// <summary>
    /// 按需清理指定笔记的快照。
    /// </summary>
    public static void Invalidate(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return;
        Map.TryRemove(noteId, out _);
    }

    /// <summary>
    /// 清空全部缓存（测试/调试用途）。
    /// </summary>
    public static void Clear() => Map.Clear();

    /// <summary>
    /// 应用点赞动作的结果（回执驱动）。
    /// - 若提供 likeCount 则覆盖；否则按 liked 与现有计数做 +1/-1（不小于0）。
    /// </summary>
    public static void ApplyLikeResult(string noteId, bool liked, int? likeCount = null)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return;

        Map.AddOrUpdate(noteId,
            addValueFactory: _ => new Snapshot(noteId,
                IsLiked: liked,
                IsCollected: false,
                LikeCount: Math.Max(0, likeCount ?? (liked ? 1 : 0)),
                CollectCount: 0,
                CommentCount: 0,
                CapturedAtUtc: DateTime.UtcNow),
            updateValueFactory: (_, prev) => prev with
            {
                IsLiked = liked,
                LikeCount = likeCount ?? Math.Max(0, prev.LikeCount + (liked ? 1 : -1)),
                CapturedAtUtc = DateTime.UtcNow
            });
    }

    /// <summary>
    /// 应用收藏动作的结果（回执驱动）。
    /// - 若提供 collectCount 则覆盖；否则按 collected 与现有计数做 +1/-1（不小于0）。
    /// </summary>
    public static void ApplyCollectResult(string noteId, bool collected, int? collectCount = null)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return;

        Map.AddOrUpdate(noteId,
            addValueFactory: _ => new Snapshot(noteId,
                IsLiked: false,
                IsCollected: collected,
                LikeCount: 0,
                CollectCount: Math.Max(0, collectCount ?? (collected ? 1 : 0)),
                CommentCount: 0,
                CapturedAtUtc: DateTime.UtcNow),
            updateValueFactory: (_, prev) => prev with
            {
                IsCollected = collected,
                CollectCount = collectCount ?? Math.Max(0, prev.CollectCount + (collected ? 1 : -1)),
                CapturedAtUtc = DateTime.UtcNow
            });
    }

    /// <summary>
    /// 应用评论数变更（回执或推断）。
    /// - 若提供 absoluteCount 则覆盖；否则按 delta 调整（不小于0）。
    /// </summary>
    public static void ApplyCommentDelta(string noteId, int delta, int? absoluteCount = null)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return;
        Map.AddOrUpdate(noteId,
            addValueFactory: _ => new Snapshot(noteId,
                IsLiked: false,
                IsCollected: false,
                LikeCount: 0,
                CollectCount: 0,
                CommentCount: Math.Max(0, absoluteCount ?? Math.Max(0, delta)),
                CapturedAtUtc: DateTime.UtcNow),
            updateValueFactory: (_, prev) => prev with
            {
                CommentCount = absoluteCount ?? Math.Max(0, prev.CommentCount + delta),
                CapturedAtUtc = DateTime.UtcNow
            });
    }
}
