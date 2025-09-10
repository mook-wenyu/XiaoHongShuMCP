using System.Diagnostics;
using System.Text.Json;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// Feed API 数据转换器
/// 将Feed API响应数据转换为现有的NoteDetail格式
/// </summary>
public static class FeedApiConverter
{
    /// <summary>
    /// 将FeedApiNoteCard转换为NoteDetail
    /// </summary>
    public static NoteDetail ConvertToNoteDetail(FeedApiNoteCard noteCard, string sourceNoteId)
    {
        // 选择作者昵称（nickname 或 nick_name）
        var authorName = noteCard.User != null && !string.IsNullOrWhiteSpace(noteCard.User.Nickname)
            ? noteCard.User.Nickname
            : (noteCard.User?.NicknameAlt ?? string.Empty);

        var noteDetail = new NoteDetail
        {
            Id = noteCard.NoteId,
            Title = noteCard.Title,
            Content = noteCard.Description,
            Author = !string.IsNullOrWhiteSpace(authorName) ? authorName : "未知作者",
            Url = $"https://www.xiaohongshu.com/explore/{sourceNoteId}",
            ExtractedAt = DateTime.UtcNow
        };

        // 原始类型与作者 token
        noteDetail.RawNoteType = noteCard.Type;
        if (!string.IsNullOrEmpty(noteCard.User?.XsecToken))
        {
            noteDetail.AuthorXsecToken = noteCard.User!.XsecToken;
        }

        // 用户详情
        if (noteCard.User != null)
        {
            noteDetail.AuthorId = noteCard.User.UserId;
            noteDetail.AuthorAvatar = noteCard.User.Avatar;
            noteDetail.UserInfo = new RecommendedUserInfo
            {
                UserId = noteCard.User.UserId,
                Nickname = authorName,
                Avatar = noteCard.User.Avatar,
                IsVerified = false,
                Description = string.Empty
            };
        }

        // 设置发布时间（时间戳转换）
        if (noteCard.Time > 0)
        {
            noteDetail.PublishTime = DateTimeOffset.FromUnixTimeMilliseconds(noteCard.Time).DateTime;
        }

        // 最后更新时间
        if (noteCard.LastUpdateTime > 0)
        {
            noteDetail.LastUpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(noteCard.LastUpdateTime).DateTime;
        }

        // 设置交互信息
        if (noteCard.InteractInfo != null)
        {
            // 计数支持中文单位解析
            noteDetail.LikeCount = SafeParseCount(noteCard.InteractInfo.LikedCountRaw);
            noteDetail.CommentCount = SafeParseCount(noteCard.InteractInfo.CommentCountRaw);
            noteDetail.FavoriteCount = SafeParseCount(noteCard.InteractInfo.CollectedCountRaw);
            noteDetail.ShareCount = SafeParseCount(noteCard.InteractInfo.ShareCountRaw);

            noteDetail.IsLiked = noteCard.InteractInfo.Liked;
            noteDetail.IsCollected = noteCard.InteractInfo.Collected;
        }

        // 设置图片信息
        if (noteCard.ImageList != null && noteCard.ImageList.Count != 0)
        {
            noteDetail.Images = noteCard.ImageList
                .Select(img => img.UrlDefault)
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();
            
            noteDetail.CoverImage = noteDetail.Images.FirstOrDefault() ?? string.Empty;

            // 填充封面详细信息（首图）
            var first = noteCard.ImageList.First();
            var scenes = new List<ImageSceneInfo>();
            foreach (var inf in first.InfoList ?? new List<FeedApiImageInfo>())
            {
                if (!string.IsNullOrEmpty(inf.Url))
                    scenes.Add(new ImageSceneInfo { SceneType = inf.ImageScene, Url = inf.Url });
            }
            noteDetail.CoverInfo = new RecommendedCoverInfo
            {
                DefaultUrl = first.UrlDefault ?? string.Empty,
                PreviewUrl = first.UrlPre ?? string.Empty,
                Width = first.Width,
                Height = first.Height,
                FileId = first.FileId ?? string.Empty,
                Scenes = scenes
            };
        }

        // 设置视频信息
        if (noteCard.Video != null)
        {
            noteDetail.VideoDuration = noteCard.Video.Capa?.Duration ?? noteCard.Video.Media?.Video?.Duration;
            
            // 尝试获取视频URL
            var videoUrl = GetBestVideoUrl(noteCard.Video);
            if (!string.IsNullOrEmpty(videoUrl))
            {
                noteDetail.VideoUrl = videoUrl;
            }

            // 可选：填充视频详细结构
            if ((noteCard.Video.Capa?.Duration ?? 0) > 0 || !string.IsNullOrEmpty(noteDetail.VideoUrl))
            {
                noteDetail.VideoInfo = new RecommendedVideoInfo
                {
                    Duration = noteDetail.VideoDuration ?? 0,
                    Cover = string.Empty,
                    Url = noteDetail.VideoUrl,
                    Width = 0,
                    Height = 0
                };
            }
        }

        // 设置标签信息
        if (noteCard.TagList != null && noteCard.TagList.Count != 0)
        {
            noteDetail.Tags = noteCard.TagList.Select(tag => tag.Name).ToList();
        }

        // IP属地
        if (!string.IsNullOrEmpty(noteCard.IpLocation))
        {
            noteDetail.IpLocation = noteCard.IpLocation;
        }

        // @用户列表
        if (noteCard.AtUserList != null && noteCard.AtUserList.Count != 0)
        {
            foreach (var au in noteCard.AtUserList)
            {
                noteDetail.AtUsers.Add(new AtUserInfo
                {
                    UserId = au.UserId,
                    Nickname = au.Nickname,
                    XsecToken = string.IsNullOrEmpty(au.XsecToken) ? null : au.XsecToken
                });
            }
        }

        // 分享开关
        if (noteCard.ShareInfo != null)
        {
            noteDetail.ShareDisabled = noteCard.ShareInfo.UnShare;
        }

        // 自动确定笔记类型
        noteDetail.DetermineType();

        // 设置数据质量
        noteDetail.Quality = DetermineDataQuality(noteDetail, noteCard);

        return noteDetail;
    }

    /// <summary>
    /// 安全解析整数字符串
    /// </summary>
    

    /// <summary>
    /// 获取最佳视频URL
    /// </summary>
    private static string GetBestVideoUrl(FeedApiVideo video)
    {
        // 优先级：H265 > H264
        var allStreams = new List<FeedApiVideoStreamDetail>();
        
        if (video.Media?.Stream?.H265?.Count > 0)
        {
            allStreams.AddRange(video.Media!.Stream!.H265!);
        }
        
        if (video.Media?.Stream?.H264?.Count > 0)
        {
            allStreams.AddRange(video.Media!.Stream!.H264!);
        }

        // 选择最佳质量的流
        var bestStream = allStreams
            .Where(s => !string.IsNullOrEmpty(s.MasterUrl))
            .OrderByDescending(s => s.Height) // 优先选择高分辨率
            .ThenByDescending(s => s.VideoBitrate) // 然后选择高码率
            .FirstOrDefault();

        return bestStream?.MasterUrl ?? string.Empty;
    }

    /// <summary>
    /// 解析包含中文单位的计数字符串
    /// </summary>
    private static int SafeParseCount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        raw = raw.Trim();
        try
        {
            if (raw.EndsWith("万", StringComparison.Ordinal))
            {
                if (double.TryParse(raw[..^1], out var n)) return (int)Math.Round(n * 10000);
                return 0;
            }
            if (raw.EndsWith("亿", StringComparison.Ordinal))
            {
                if (double.TryParse(raw[..^1], out var n)) return (int)Math.Round(n * 100000000);
                return 0;
            }
            return int.TryParse(raw, out var i) ? i : 0;
        }
        catch { return 0; }
    }

    /// <summary>
    /// 确定数据质量
    /// </summary>
    private static DataQuality DetermineDataQuality(NoteDetail noteDetail, FeedApiNoteCard noteCard)
    {
        var completeFields = 0;
        var totalFields = 8; // 定义总字段数

        // 检查基础字段
        if (!string.IsNullOrEmpty(noteDetail.Title)) completeFields++;
        if (!string.IsNullOrEmpty(noteDetail.Content)) completeFields++;
        if (!string.IsNullOrEmpty(noteDetail.Author)) completeFields++;
        if (noteDetail.PublishTime.HasValue) completeFields++;
        if (noteDetail.LikeCount.HasValue) completeFields++;
        if (noteDetail.CommentCount.HasValue) completeFields++;
        
        // 检查媒体字段
        if (noteDetail.Images.Count != 0 || !string.IsNullOrEmpty(noteDetail.VideoUrl)) completeFields++;
        if (noteDetail.Tags.Count != 0) completeFields++;

        // 根据完整度确定质量
        var completionRatio = (double)completeFields / totalFields;
        
        return completionRatio switch
        {
            >= 0.8 => DataQuality.Complete,
            >= 0.5 => DataQuality.Partial,
            _ => DataQuality.Minimal
        };
    }

    /// <summary>
    /// 将FeedApiResponse转换为NoteDetail列表
    /// </summary>
    public static List<NoteDetail> ConvertToNoteDetails(FeedApiResponse response)
    {
        var noteDetails = new List<NoteDetail>();
        
        if (response?.Data?.Items == null)
        {
            return noteDetails;
        }
        
        foreach (var item in response.Data.Items)
        {
            if (item.NoteCard != null)
            {
                try
                {
                    var noteDetail = ConvertToNoteDetail(item.NoteCard, item.Id ?? string.Empty);
                    noteDetail.ModelType = item.ModelType;
                    noteDetails.Add(noteDetail);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"转换笔记详情失败: {ex.Message}");
                }
            }
        }
        
        return noteDetails;
    }

    /// <summary>
    /// 批量转换Feed API响应为NoteDetail列表
    /// </summary>
    public static List<NoteDetail> ConvertBatchToNoteDetails(List<MonitoredFeedResponse> monitoredResponses)
    {
        var noteDetails = new List<NoteDetail>();

        foreach (var response in monitoredResponses)
        {
            // 先做空值与计数判定，避免空引用与可空性告警
            var items = response.ResponseData?.Data?.Items;
            if (response.ResponseData?.Success != true || items == null || items.Count == 0)
            {
                continue;
            }

            foreach (var item in items)
            {
                if (item.NoteCard != null)
                {
                    try
                    {
                        var noteDetail = ConvertToNoteDetail(item.NoteCard, response.SourceNoteId);
                        noteDetail.ModelType = item.ModelType;
                        noteDetails.Add(noteDetail);
                    }
                    catch (Exception ex)
                    {
                        // 记录转换失败但不中断处理
                        Debug.WriteLine($"转换笔记详情失败: {ex.Message}");
                    }
                }
            }
        }

        return noteDetails;
    }

    /// <summary>
    /// 从请求体中提取source_note_id
    /// </summary>
    public static string ExtractSourceNoteId(string requestBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            if (doc.RootElement.TryGetProperty("source_note_id", out var sourceNoteIdElement))
            {
                return sourceNoteIdElement.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // 解析失败时静默处理
        }

        return string.Empty;
    }

    /// <summary>
    /// 验证Feed API响应的有效性
    /// </summary>
    public static bool IsValidFeedResponse(FeedApiResponse? response)
    {
        return response is { Success: true, Code: 0 }
               && ((response.Data?.Items?.Count ?? 0) > 0);
    }
}
