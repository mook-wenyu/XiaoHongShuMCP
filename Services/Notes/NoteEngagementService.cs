using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Notes;

/// <summary>
/// 中文：基于本地笔记索引模拟互动操作，并输出审计日志。
/// </summary>
public sealed class NoteEngagementService : INoteEngagementService
{
    private readonly INoteRepository _repository;
    private readonly ILogger<NoteEngagementService> _logger;

    public NoteEngagementService(INoteRepository repository, ILogger<NoteEngagementService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LikeAsync(string keyword, CancellationToken cancellationToken)
        => await ExecuteAsync("like", keyword, note => _logger.LogInformation("[NoteEngagement] liked noteId={NoteId}", note.Id), cancellationToken).ConfigureAwait(false);

    public async Task FavoriteAsync(string keyword, CancellationToken cancellationToken)
        => await ExecuteAsync("favorite", keyword, note => _logger.LogInformation("[NoteEngagement] favorited noteId={NoteId}", note.Id), cancellationToken).ConfigureAwait(false);

    public async Task CommentAsync(string keyword, string comment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("comment 不能为空", nameof(comment));
        }

        await ExecuteAsync(
            "comment",
            keyword,
            note => _logger.LogInformation("[NoteEngagement] comment noteId={NoteId} comment={Comment}", note.Id, comment.Trim()),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(string action, string keyword, Action<NoteRecord> onSuccess, CancellationToken cancellationToken)
    {
        var notes = await _repository.QueryAsync(keyword, 1, cancellationToken).ConfigureAwait(false);
        var note = notes.FirstOrDefault();
        if (note is null)
        {
            throw new InvalidOperationException($"未找到与关键字 {keyword} 匹配的笔记，无法执行 {action} 操作。");
        }

        onSuccess(note);
    }
}
