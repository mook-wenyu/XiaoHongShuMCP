using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Humanization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services.EngagementFlow
{
    /// <summary>
    /// 提供笔记查找与详情匹配能力，统一复用列表滚动与内容解析逻辑。
    /// </summary>
    internal sealed class NoteDiscoveryService : INoteDiscoveryService
    {
        private readonly ILogger<NoteDiscoveryService> _logger;
        private readonly IBrowserManager _browserManager;
        private readonly IHumanizedInteractionService _humanizedInteraction;
        private readonly IDomElementManager _domElementManager;
        private readonly XhsSettings.DetailMatchSection _detailMatch;

        public NoteDiscoveryService(
            ILogger<NoteDiscoveryService> logger,
            IBrowserManager browserManager,
            IHumanizedInteractionService humanizedInteraction,
            IDomElementManager domElementManager,
            IOptions<XhsSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
            _humanizedInteraction = humanizedInteraction ?? throw new ArgumentNullException(nameof(humanizedInteraction));
            _domElementManager = domElementManager ?? throw new ArgumentNullException(nameof(domElementManager));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _detailMatch = settings.Value.DetailMatchConfig ?? new XhsSettings.DetailMatchSection();
        }

        /// <inheritdoc />
        public async Task<IElementHandle?> FindMatchingNoteElementAsync(string keyword, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return null;

            var page = await _browserManager.GetPageAsync().ConfigureAwait(false);
            var noteItemSelectors = _domElementManager.GetSelectors("NoteItem");

            foreach (var selector in noteItemSelectors)
            {
                ct.ThrowIfCancellationRequested();

                IReadOnlyList<IElementHandle> noteElements;
                try
                {
                    noteElements = await page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("使用选择器 {Selector} 查找笔记元素失败：{Message}", selector, ex.Message);
                    continue;
                }

                if (noteElements.Count == 0) continue;

                foreach (var noteElement in noteElements)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!await IsElementVisibleAsync(noteElement).ConfigureAwait(false)) continue;

                    var noteText = await ExtractNoteTextForMatchingAsync(noteElement).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(noteText)) continue;

                    if (MatchesKeyword(noteText, keyword))
                    {
                        _logger.LogDebug("找到匹配笔记，片段={Snippet}", noteText[..Math.Min(50, noteText.Length)]);
                        return noteElement;
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<OperationResult<List<IElementHandle>>> FindVisibleMatchingNotesAsync(string keyword, int maxCount, CancellationToken ct = default)
        {
            if (maxCount <= 0)
            {
                return OperationResult<List<IElementHandle>>.Ok(new List<IElementHandle>());
            }

            try
            {
                var page = await _browserManager.GetPageAsync().ConfigureAwait(false);
                var foundNotes = new List<IElementHandle>();
                var processedIds = new HashSet<string>();
                var scrollAttempts = 0;

                while (foundNotes.Count < maxCount)
                {
                    ct.ThrowIfCancellationRequested();

                    var currentMatches = await SearchCurrentVisibleAreaAsync(keyword, maxCount - foundNotes.Count).ConfigureAwait(false);
                    if (!currentMatches.Success)
                    {
                        return currentMatches;
                    }

                    foreach (var match in currentMatches.Data!)
                    {
                        if (foundNotes.Count >= maxCount) break;

                        try
                        {
                            var noteId = await ExtractNoteIdFromElementAsync(match).ConfigureAwait(false);
                            if (string.IsNullOrEmpty(noteId) || !processedIds.Add(noteId)) continue;

                            foundNotes.Add(match);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("提取笔记标识失败：{Message}", ex.Message);
                            var fallbackId = Guid.NewGuid().ToString();
                            if (processedIds.Add(fallbackId))
                            {
                                foundNotes.Add(match);
                            }
                        }
                    }

                    if (foundNotes.Count >= maxCount)
                    {
                        break;
                    }

                    await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ScrollPreparation, cancellationToken: ct).ConfigureAwait(false);
                    await _humanizedInteraction.HumanScrollAsync(page, cancellationToken: ct).ConfigureAwait(false);
                    await _humanizedInteraction.HumanWaitAsync(HumanWaitType.VirtualListUpdate, cancellationToken: ct).ConfigureAwait(false);
                    scrollAttempts++;
                }

                _logger.LogInformation("虚拟化滚动搜索完成，匹配数={Count}，滚动次数={Attempts}", foundNotes.Count, scrollAttempts);
                return OperationResult<List<IElementHandle>>.Ok(foundNotes);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("虚拟化列表搜索被取消");
                return OperationResult<List<IElementHandle>>.Fail(
                    "虚拟化列表搜索被取消",
                    ErrorType.OperationCancelled,
                    "VIRTUALIZED_SEARCH_CANCELLED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "虚拟化滚动搜索失败");
                return OperationResult<List<IElementHandle>>.Fail(
                    $"虚拟化列表搜索失败: {ex.Message}",
                    ErrorType.BrowserError,
                    "VIRTUALIZED_SEARCH_FAILED");
            }
        }

        /// <inheritdoc />
        public async Task<bool> DoesDetailMatchKeywordAsync(IPage page, string keyword, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return false;

            try
            {
                ct.ThrowIfCancellationRequested();

                var options = new KeywordMatchOptions
                {
                    UseFuzzy = _detailMatch.UseFuzzy,
                    MaxDistanceCap = _detailMatch.MaxDistanceCap,
                    TokenCoverageThreshold = _detailMatch.TokenCoverageThreshold,
                    IgnoreSpaces = _detailMatch.IgnoreSpaces
                };

                var (title, author, content, hashtags, imageAlts) = await ExtractDetailFieldsAsync(page).ConfigureAwait(false);

                double score = 0;
                double total = _detailMatch.TitleWeight + _detailMatch.AuthorWeight + _detailMatch.ContentWeight +
                               _detailMatch.HashtagWeight + _detailMatch.ImageAltWeight;

                if (!string.IsNullOrWhiteSpace(title) && KeywordMatcher.Matches(title, keyword, options)) score += _detailMatch.TitleWeight;
                if (!string.IsNullOrWhiteSpace(author) && KeywordMatcher.Matches(author, keyword, options)) score += _detailMatch.AuthorWeight;
                if (!string.IsNullOrWhiteSpace(content) && KeywordMatcher.Matches(content, keyword, options)) score += _detailMatch.ContentWeight;
                if (!string.IsNullOrWhiteSpace(hashtags) && KeywordMatcher.Matches(hashtags, keyword, options)) score += _detailMatch.HashtagWeight;
                if (!string.IsNullOrWhiteSpace(imageAlts) && KeywordMatcher.Matches(imageAlts, keyword, options)) score += _detailMatch.ImageAltWeight;

                if (_detailMatch.UsePinyin)
                {
                    var initialsKeyword = ToPinyinInitials(keyword);
                    if (!string.IsNullOrWhiteSpace(initialsKeyword))
                    {
                        bool PinyinMatch(string? text) => !string.IsNullOrWhiteSpace(text) &&
                            KeywordMatcher.Matches(ToPinyinInitials(text), initialsKeyword, options);

                        if (PinyinMatch(title)) score += _detailMatch.TitleWeight * 0.6;
                        if (PinyinMatch(author)) score += _detailMatch.AuthorWeight * 0.6;
                        if (PinyinMatch(content)) score += _detailMatch.ContentWeight * 0.5;
                        if (PinyinMatch(hashtags)) score += _detailMatch.HashtagWeight * 0.5;
                        if (PinyinMatch(imageAlts)) score += _detailMatch.ImageAltWeight * 0.4;
                    }
                }

                var ratio = total <= 0 ? 0 : score / total;
                return ratio >= _detailMatch.WeightedThreshold;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "详情页关键词匹配失败");
                return false;
            }
        }

        private async Task<OperationResult<List<IElementHandle>>> SearchCurrentVisibleAreaAsync(string keyword, int maxCount)
        {
            try
            {
                var page = await _browserManager.GetPageAsync().ConfigureAwait(false);
                var noteItemSelectors = _domElementManager.GetSelectors("NoteItem");
                var allNoteElements = new List<IElementHandle>();

                foreach (var selector in noteItemSelectors)
                {
                    try
                    {
                        var elements = await page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
                        if (elements.Any())
                        {
                            allNoteElements.AddRange(elements);
                            _logger.LogDebug("当前区域使用选择器 {Selector} 找到 {Count} 个笔记元素", selector, elements.Count);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("当前区域选择器 {Selector} 查找失败: {Message}", selector, ex.Message);
                    }
                }

                if (allNoteElements.Count == 0)
                {
                    return OperationResult<List<IElementHandle>>.Ok(new List<IElementHandle>());
                }

                if (string.IsNullOrWhiteSpace(keyword))
                {
                    var visibleNotes = await FilterVisibleElementsAsync(allNoteElements).ConfigureAwait(false);
                    return OperationResult<List<IElementHandle>>.Ok(visibleNotes.Take(maxCount).ToList());
                }

                var matchingNotes = new List<IElementHandle>();
                foreach (var noteElement in allNoteElements)
                {
                    if (matchingNotes.Count >= maxCount) break;

                    try
                    {
                        if (!await IsElementVisibleAsync(noteElement).ConfigureAwait(false)) continue;

                        var noteText = await ExtractNoteTextForMatchingAsync(noteElement).ConfigureAwait(false);
                        if (string.IsNullOrEmpty(noteText)) continue;

                        if (MatchesKeyword(noteText, keyword))
                        {
                            matchingNotes.Add(noteElement);
                            _logger.LogDebug("当前区域找到匹配笔记: {Text}", noteText[..Math.Min(50, noteText.Length)]);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("处理笔记元素失败: {Message}", ex.Message);
                    }
                }

                return OperationResult<List<IElementHandle>>.Ok(matchingNotes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索当前可视区域失败");
                return OperationResult<List<IElementHandle>>.Fail(
                    $"搜索当前可视区域失败: {ex.Message}",
                    ErrorType.BrowserError,
                    "SEARCH_CURRENT_AREA_FAILED");
            }
        }

        private async Task<List<IElementHandle>> FilterVisibleElementsAsync(List<IElementHandle> elements)
        {
            var visibleElements = new List<IElementHandle>();

            foreach (var element in elements)
            {
                try
                {
                    if (await IsElementVisibleAsync(element).ConfigureAwait(false))
                    {
                        visibleElements.Add(element);
                    }
                }
                catch
                {
                    // 忽略单个元素的可见性异常
                }
            }

            return visibleElements;
        }

        private static async Task<bool> IsElementVisibleAsync(IElementHandle element)
        {
            try
            {
                var boundingBox = await element.BoundingBoxAsync().ConfigureAwait(false);
                return boundingBox is { Height: > 0, Width: > 0 };
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> ExtractNoteIdFromElementAsync(IElementHandle noteElement)
        {
            try
            {
                var linkElement = await noteElement.QuerySelectorAsync("a[href*='/explore/']").ConfigureAwait(false)
                                  ?? await noteElement.QuerySelectorAsync("a[href*='/discovery/item/']").ConfigureAwait(false)
                                  ?? await noteElement.QuerySelectorAsync("a").ConfigureAwait(false);

                if (linkElement != null)
                {
                    var href = await linkElement.GetAttributeAsync("href").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(href))
                    {
                        var matches = Regex.Match(href, "(?:explore|discovery/item)/([a-f0-9]{24})")
                                      ?? Regex.Match(href, "/([a-f0-9]{24})(?:\\?|$)");
                        if (matches.Success)
                        {
                            return matches.Groups[1].Value;
                        }

                        return href;
                    }
                }

                var dataId = await noteElement.GetAttributeAsync("data-id").ConfigureAwait(false)
                             ?? await noteElement.GetAttributeAsync("data-note-id").ConfigureAwait(false)
                             ?? await noteElement.GetAttributeAsync("data-item-id").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(dataId))
                {
                    return dataId;
                }

                var imgElement = await noteElement.QuerySelectorAsync("img").ConfigureAwait(false);
                if (imgElement != null)
                {
                    var imgSrc = await imgElement.GetAttributeAsync("src").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(imgSrc) && imgSrc.Contains('/'))
                    {
                        var parts = imgSrc.Split('/');
                        for (var i = parts.Length - 1; i >= 0; i--)
                        {
                            if (parts[i].Length > 10)
                            {
                                return parts[i].Split('?')[0];
                            }
                        }
                    }
                }

                var textContent = await noteElement.InnerTextAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(textContent))
                {
                    using var sha256 = SHA256.Create();
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(textContent.Trim()));
                    return Convert.ToHexString(hashBytes)[..16];
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("提取笔记ID失败: {Message}", ex.Message);
                return string.Empty;
            }
        }

        private async Task<string> ExtractNoteTextForMatchingAsync(IElementHandle noteElement)
        {
            try
            {
                var textParts = new List<string>();
                var titleSelectors = new[] { ".title", ".note-title", ".content", "[title]" };
                foreach (var selector in titleSelectors)
                {
                    try
                    {
                        var titleElement = await noteElement.QuerySelectorAsync(selector).ConfigureAwait(false);
                        if (titleElement != null)
                        {
                            var titleText = await titleElement.InnerTextAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(titleText))
                            {
                                textParts.Add(titleText.Trim());
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个选择器异常
                    }
                }

                var authorSelectors = new[] { ".author", ".username", ".user-name" };
                foreach (var selector in authorSelectors)
                {
                    try
                    {
                        var authorElement = await noteElement.QuerySelectorAsync(selector).ConfigureAwait(false);
                        if (authorElement != null)
                        {
                            var authorText = await authorElement.InnerTextAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(authorText))
                            {
                                textParts.Add(authorText.Trim());
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个选择器异常
                    }
                }

                return string.Join(' ', textParts);
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool MatchesKeyword(string text, string keyword)
        {
            var options = new KeywordMatchOptions
            {
                UseFuzzy = _detailMatch.UseFuzzy,
                MaxDistanceCap = _detailMatch.MaxDistanceCap,
                TokenCoverageThreshold = _detailMatch.TokenCoverageThreshold,
                IgnoreSpaces = _detailMatch.IgnoreSpaces
            };
            return KeywordMatcher.Matches(text, keyword, options);
        }

        private static string ToPinyinInitials(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            Encoding? gb2312 = null;
            try
            {
                gb2312 = Encoding.GetEncoding("GB2312");
            }
            catch
            {
                // 平台不支持则跳过
            }

            foreach (var ch in text)
            {
                if (ch <= 127)
                {
                    if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                    continue;
                }

                if (gb2312 == null) continue;

                try
                {
                    var bytes = gb2312.GetBytes(new[] { ch });
                    if (bytes.Length < 2) continue;
                    var code = bytes[0] << 8 | bytes[1];
                    sb.Append(MapGb2312CodeToInitial(code));
                }
                catch
                {
                    // 忽略单次转换失败
                }
            }

            return sb.ToString();
        }

        private static char MapGb2312CodeToInitial(int code)
        {
            return code switch
            {
                >= 0xB0A1 and <= 0xB0C4 => 'a',
                >= 0xB0C5 and <= 0xB2C0 => 'b',
                >= 0xB2C1 and <= 0xB4ED => 'c',
                >= 0xB4EE and <= 0xB6E9 => 'd',
                >= 0xB6EA and <= 0xB7A1 => 'e',
                >= 0xB7A2 and <= 0xB8C0 => 'f',
                >= 0xB8C1 and <= 0xB9FD => 'g',
                >= 0xB9FE and <= 0xBBF6 => 'h',
                >= 0xBBF7 and <= 0xBFA5 => 'j',
                >= 0xBFA6 and <= 0xC0AB => 'k',
                >= 0xC0AC and <= 0xC2E7 => 'l',
                >= 0xC2E8 and <= 0xC4C2 => 'm',
                >= 0xC4C3 and <= 0xC5B5 => 'n',
                >= 0xC5B6 and <= 0xC5BD => 'o',
                >= 0xC5BE and <= 0xC6D9 => 'p',
                >= 0xC6DA and <= 0xC8BA => 'q',
                >= 0xC8BB and <= 0xC8F5 => 'r',
                >= 0xC8F6 and <= 0xCBF0 => 's',
                >= 0xCBFA and <= 0xCDD9 => 't',
                >= 0xCDDA and <= 0xCEF3 => 'w',
                >= 0xCEF4 and <= 0xD188 => 'x',
                >= 0xD1B9 and <= 0xD4D0 => 'y',
                >= 0xD4D1 and <= 0xD7F9 => 'z',
                _ => 'z'
            };
        }

        private async Task<(string Title, string Author, string Content, string Hashtags, string ImageAlts)> ExtractDetailFieldsAsync(IPage page)
        {
            var titleHandle = await page.QuerySelectorAsync(".title, .note-title, h1").ConfigureAwait(false);
            var authorHandle = await page.QuerySelectorAsync(".user-name, .author-nickname, [data-testid='user-name']").ConfigureAwait(false);
            var contentHandle = await page.QuerySelectorAsync(".note-content, .content, .desc").ConfigureAwait(false);
            var hashtagHandles = await page.QuerySelectorAllAsync(".hashtag-container .tag, .note-tag").ConfigureAwait(false);
            var imageHandles = await page.QuerySelectorAllAsync("img").ConfigureAwait(false);

            var title = titleHandle != null ? await titleHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;
            var author = authorHandle != null ? await authorHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;
            var content = contentHandle != null ? await contentHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;
            var hashtags = string.Join(' ', await Task.WhenAll(hashtagHandles.Select(async h => await h.InnerTextAsync().ConfigureAwait(false))));
            var imageAlts = string.Join(';', await Task.WhenAll(imageHandles.Select(async h => await h.GetAttributeAsync("alt").ConfigureAwait(false) ?? string.Empty)));

            return (title, author, content, hashtags, imageAlts);
        }
    }
}
