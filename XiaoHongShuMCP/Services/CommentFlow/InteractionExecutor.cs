using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using Microsoft.Extensions.Logging;

namespace XiaoHongShuMCP.Services.CommentFlow;

/// <summary>
/// 中文：评论交互执行器，实现输入、表情增强与提交流程。
/// </summary>
internal sealed class InteractionExecutor : IInteractionExecutor
{
    private readonly IHumanizedInteractionService _interactionService;
    private readonly IDomElementManager _domElementManager;
    private readonly ILogger<InteractionExecutor> _logger;

    public InteractionExecutor(
        IHumanizedInteractionService interactionService,
        IDomElementManager domElementManager,
        ILogger<InteractionExecutor> logger)
    {
        _interactionService = interactionService;
        _domElementManager = domElementManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InputCommentAsync(IAutoPage page, CommentDraft draft, CancellationToken ct)
    {
        var inputSelectors = _domElementManager.GetSelectors("CommentInputReady");
        IAutoElement? input = null;

        foreach (var selector in inputSelectors)
        {
            ct.ThrowIfCancellationRequested();
            input = await page.QueryAsync(selector, 2000, ct).ConfigureAwait(false);
            if (input != null) break;
        }

        if (input is null)
        {
            throw new InvalidOperationException("未找到评论输入框");
        }

        await _interactionService.HumanClickAsync(input).ConfigureAwait(false);
        await _interactionService.HumanWaitAsync(HumanWaitType.ThinkingPause, ct).ConfigureAwait(false);
        await input.FillAsync(string.Empty).ConfigureAwait(false);

        if (draft.UseEmoji && draft.Emojis is { Count: > 0 })
        {
            await AddEmojisAsync(page, draft.Emojis, ct).ConfigureAwait(false);
            await _interactionService.HumanWaitAsync(HumanWaitType.ThinkingPause, ct).ConfigureAwait(false);
        }

        await _interactionService.InputTextAsync(page, input, draft.Content).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InteractionSubmitResult> SubmitCommentAsync(IAutoPage page, CancellationToken ct)
    {
        var submitSelectors = _domElementManager.GetSelectors("CommentSubmitEnabled");
        var start = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow - start < timeout)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var selector in submitSelectors)
            {
                var button = await page.QueryAsync(selector, 1000, ct).ConfigureAwait(false);
                if (button is null) continue;

                    await _interactionService.HumanClickAsync(button).ConfigureAwait(false);
                    await _interactionService.HumanWaitAsync(HumanWaitType.ModalWaiting, ct).ConfigureAwait(false);
                return new InteractionSubmitResult(true, "已点击发送");
            }

            await _interactionService.HumanWaitAsync(HumanWaitType.RetryBackoff, ct).ConfigureAwait(false);
        }

        _logger.LogWarning("发送按钮在 {Timeout} 内未启用", timeout);
        return new InteractionSubmitResult(false, $"发送按钮未在 {timeout.TotalMilliseconds}ms 内启用");
    }

    private async Task AddEmojisAsync(IAutoPage page, IReadOnlyList<string> emojis, CancellationToken ct)
    {
        var emojiTriggerSelectors = _domElementManager.GetSelectors("CommentEmojiButton");
        IAutoElement? trigger = null;

        foreach (var selector in emojiTriggerSelectors)
        {
            trigger = await page.QueryAsync(selector, 1000, ct).ConfigureAwait(false);
            if (trigger != null) break;
        }

        if (trigger is null)
        {
            _logger.LogDebug("未找到表情按钮，跳过表情注入");
            return;
        }

        await _interactionService.HumanClickAsync(trigger).ConfigureAwait(false);
        await _interactionService.HumanWaitAsync(HumanWaitType.ThinkingPause, ct).ConfigureAwait(false);

        foreach (var emoji in emojis)
        {
            ct.ThrowIfCancellationRequested();
            var selectors = _domElementManager.BuildSelectors("EmojiByText", new Dictionary<string, string>
            {
                ["value"] = emoji,
                ["valueLower"] = emoji.ToLowerInvariant()
            });

            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.QueryAsync(selector, 1000, ct).ConfigureAwait(false);
                    if (element is null) continue;
                    await _interactionService.HumanClickAsync(element).ConfigureAwait(false);
                    await _interactionService.HumanWaitAsync(HumanWaitType.ThinkingPause, ct).ConfigureAwait(false);
                    break;
                }
                catch
                {
                    // 忽略单个选择器失败，继续尝试下一个
                }
            }
        }
    }
}
