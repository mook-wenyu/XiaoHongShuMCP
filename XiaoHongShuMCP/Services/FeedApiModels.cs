using System.Text.Json.Serialization;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// Feed API 响应数据模型 - 匹配真实的 /api/sns/web/v1/feed API 响应结构
/// 基于用户提供的真实API数据设计
/// </summary>

/// <summary>
/// Feed API 响应根对象
/// </summary>
public class FeedApiResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public FeedApiData? Data { get; set; }
}

/// <summary>
/// Feed API 数据部分
/// </summary>
public class FeedApiData
{
    [JsonPropertyName("cursor_score")]
    public string CursorScore { get; set; } = string.Empty;
    
    [JsonPropertyName("items")]
    public List<FeedApiItem> Items { get; set; } = [];
    
    [JsonPropertyName("current_time")]
    public long CurrentTime { get; set; }
}

/// <summary>
/// Feed API 项目
/// </summary>
public class FeedApiItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("model_type")]
    public string ModelType { get; set; } = string.Empty;
    
    [JsonPropertyName("note_card")]
    public FeedApiNoteCard? NoteCard { get; set; }
}

/// <summary>
/// Feed API 笔记卡片 - 核心数据结构
/// </summary>
public class FeedApiNoteCard
{
    [JsonPropertyName("note_id")]
    public string NoteId { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "normal", "video"
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("desc")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("time")]
    public long Time { get; set; }
    
    [JsonPropertyName("last_update_time")]
    public long LastUpdateTime { get; set; }
    
    [JsonPropertyName("ip_location")]
    public string IpLocation { get; set; } = string.Empty;
    
    [JsonPropertyName("user")]
    public FeedApiUser? User { get; set; }
    
    [JsonPropertyName("interact_info")]
    public FeedApiInteractInfo? InteractInfo { get; set; }
    
    [JsonPropertyName("image_list")]
    public List<FeedApiImage> ImageList { get; set; } = [];
    
    [JsonPropertyName("video")]
    public FeedApiVideo? Video { get; set; }
    
    [JsonPropertyName("tag_list")]
    public List<FeedApiTag> TagList { get; set; } = [];
    
    [JsonPropertyName("at_user_list")]
    public List<FeedApiAtUser> AtUserList { get; set; } = [];
    
    [JsonPropertyName("share_info")]
    public FeedApiShareInfo? ShareInfo { get; set; }
}

/// <summary>
/// Feed API 用户信息
/// </summary>
/// <summary>
/// Feed API用户信息（继承自BaseUserInfo，包含API特定字段）
/// </summary>
public class FeedApiUser : BaseUserInfo
{
    [JsonPropertyName("user_id")]
    public new string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("nickname")]
    public new string Nickname { get; set; } = string.Empty;
    
    [JsonPropertyName("avatar")]
    public new string Avatar { get; set; } = string.Empty;
    
    [JsonPropertyName("xsec_token")]
    public string XsecToken { get; set; } = string.Empty;
}

/// <summary>
/// Feed API 交互信息
/// </summary>
/// <summary>
/// Feed API交互信息（继承自BaseInteractInfo，包含API特定字段和序列化属性）
/// </summary>
public class FeedApiInteractInfo : BaseInteractInfo
{
    [JsonPropertyName("liked")]
    public new bool Liked { get; set; }
    
    [JsonPropertyName("liked_count")]
    public string LikedCountRaw { get; set; } = string.Empty; // 注意：API返回字符串格式
    
    [JsonPropertyName("collected")]
    public new bool Collected { get; set; }
    
    [JsonPropertyName("collected_count")]
    public string CollectedCountRaw { get; set; } = string.Empty;
    
    [JsonPropertyName("comment_count")]
    public string CommentCountRaw { get; set; } = string.Empty;
    
    [JsonPropertyName("share_count")]
    public string ShareCountRaw { get; set; } = string.Empty;
    
    [JsonPropertyName("followed")]
    public bool Followed { get; set; }
    
    [JsonPropertyName("relation")]
    public string Relation { get; set; } = string.Empty;

    // 重写基类属性以提供从字符串到整数的转换
    public new int LikedCount => int.TryParse(LikedCountRaw, out var count) ? count : 0;
    public new int CollectedCount => int.TryParse(CollectedCountRaw, out var count) ? count : 0;
    public new int CommentCount => int.TryParse(CommentCountRaw, out var count) ? count : 0;
    public int ShareCount => int.TryParse(ShareCountRaw, out var count) ? count : 0;
}

/// <summary>
/// Feed API 图片信息
/// </summary>
public class FeedApiImage
{
    [JsonPropertyName("url_default")]
    public string UrlDefault { get; set; } = string.Empty;
    
    [JsonPropertyName("url_pre")]
    public string UrlPre { get; set; } = string.Empty;
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }
    
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    
    [JsonPropertyName("trace_id")]
    public string TraceId { get; set; } = string.Empty;
    
    [JsonPropertyName("live_photo")]
    public bool LivePhoto { get; set; }
    
    [JsonPropertyName("info_list")]
    public List<FeedApiImageInfo> InfoList { get; set; } = [];
    
    [JsonPropertyName("stream")]
    public object Stream { get; set; } = new();
}

/// <summary>
/// Feed API 图片场景信息
/// </summary>
public class FeedApiImageInfo
{
    [JsonPropertyName("image_scene")]
    public string ImageScene { get; set; } = string.Empty; // "WB_DFT", "WB_PRV"
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Feed API 视频信息
/// </summary>
public class FeedApiVideo
{
    [JsonPropertyName("media")]
    public FeedApiVideoMedia? Media { get; set; }
    
    [JsonPropertyName("image")]
    public FeedApiVideoImage? Image { get; set; }
    
    [JsonPropertyName("capa")]
    public FeedApiVideoCapa? Capa { get; set; }
    
    [JsonPropertyName("consumer")]
    public FeedApiVideoConsumer? Consumer { get; set; }
}

/// <summary>
/// Feed API 视频媒体信息
/// </summary>
public class FeedApiVideoMedia
{
    [JsonPropertyName("video_id")]
    public long VideoId { get; set; }
    
    [JsonPropertyName("video")]
    public FeedApiVideoDetail? Video { get; set; }
    
    [JsonPropertyName("stream")]
    public FeedApiVideoStream? Stream { get; set; }
}

/// <summary>
/// Feed API 视频详情
/// </summary>
public class FeedApiVideoDetail
{
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    
    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;
    
    [JsonPropertyName("hdr_type")]
    public int HdrType { get; set; }
    
    [JsonPropertyName("drm_type")]
    public int DrmType { get; set; }
    
    [JsonPropertyName("stream_types")]
    public List<int> StreamTypes { get; set; } = [];
    
    [JsonPropertyName("biz_name")]
    public int BizName { get; set; }
    
    [JsonPropertyName("biz_id")]
    public string BizId { get; set; } = string.Empty;
}

/// <summary>
/// Feed API 视频流信息
/// </summary>
public class FeedApiVideoStream
{
    [JsonPropertyName("h264")]
    public List<FeedApiVideoStreamDetail> H264 { get; set; } = [];
    
    [JsonPropertyName("h265")]
    public List<FeedApiVideoStreamDetail> H265 { get; set; } = [];
    
    [JsonPropertyName("h266")]
    public List<FeedApiVideoStreamDetail> H266 { get; set; } = [];
    
    [JsonPropertyName("av1")]
    public List<FeedApiVideoStreamDetail> Av1 { get; set; } = [];
}

/// <summary>
/// Feed API 视频流详情
/// </summary>
public class FeedApiVideoStreamDetail
{
    [JsonPropertyName("stream_type")]
    public int StreamType { get; set; }
    
    [JsonPropertyName("master_url")]
    public string MasterUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("backup_urls")]
    public List<string> BackupUrls { get; set; } = [];
    
    [JsonPropertyName("quality_type")]
    public string QualityType { get; set; } = string.Empty;
    
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }
    
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    
    [JsonPropertyName("video_bitrate")]
    public int VideoBitrate { get; set; }
    
    [JsonPropertyName("audio_bitrate")]
    public int AudioBitrate { get; set; }
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("fps")]
    public int Fps { get; set; }
}

/// <summary>
/// Feed API 视频图片信息
/// </summary>
public class FeedApiVideoImage
{
    [JsonPropertyName("first_frame_fileid")]
    public string FirstFrameFileId { get; set; } = string.Empty;
    
    [JsonPropertyName("thumbnail_fileid")]
    public string ThumbnailFileId { get; set; } = string.Empty;
}

/// <summary>
/// Feed API 视频能力信息
/// </summary>
public class FeedApiVideoCapa
{
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}

/// <summary>
/// Feed API 视频消费者信息
/// </summary>
public class FeedApiVideoConsumer
{
    [JsonPropertyName("origin_video_key")]
    public string OriginVideoKey { get; set; } = string.Empty;
}

/// <summary>
/// Feed API 标签信息
/// </summary>
public class FeedApiTag
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Feed API @用户信息
/// </summary>
public class FeedApiAtUser
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;
}

/// <summary>
/// Feed API 分享信息
/// </summary>
public class FeedApiShareInfo
{
    [JsonPropertyName("un_share")]
    public bool UnShare { get; set; }
}


/// <summary>
/// 监听到的 Feed API 响应数据
/// </summary>
/// <summary>
/// Feed API 监听响应数据
/// 存储从被动监听中收集到的 API 响应信息
/// </summary>
public class MonitoredFeedResponse
{
    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTime ResponseTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 请求URL
    /// </summary>
    public string RequestUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 请求参数（JSON字符串）
    /// 注意：在被动监听模式下此字段通常为空
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;
    
    /// <summary>
    /// 响应数据
    /// </summary>
    public FeedApiResponse? ResponseData { get; set; }
    
    /// <summary>
    /// 原始响应JSON
    /// </summary>
    public string RawResponse { get; set; } = string.Empty;
    
    /// <summary>
    /// 笔记ID（从请求参数中提取）
    /// 注意：在被动监听模式下此字段通常为空
    /// </summary>
    public string SourceNoteId { get; set; } = string.Empty;
}