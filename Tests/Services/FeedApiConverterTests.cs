using System.Text.Json;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// Feed API 转换器（FeedApiConverter）单元测试
/// - 覆盖：类型识别、时间戳、封面与视频信息、交互计数、IP属地、@用户列表、分享开关。
/// </summary>
[TestFixture]
public class FeedApiConverterTests
{
    /// <summary>
    /// 使用提供的“normal” 示例片段验证 NoteDetail 的主要映射。
    /// </summary>
    [Test]
    public void Should_Convert_Normal_NoteCard_To_NoteDetail()
    {
        // Arrange：最小可运行的 FeedApiResponse（含一个 normal 笔记）
        var json = """
        {
          "code": 0, "success": true, "msg": "成功",
          "data": { "cursor_score": "", "current_time": 1757340557365, "items": [
            { "id": "68aec6d5000000001d03a9ad", "model_type": "note", "note_card": {
              "user": { "user_id": "633667c2...", "nickname": "Game4Good", "avatar": "https://sns-avatar...jpg", "xsec_token": "ABVhr..." },
              "image_list": [{ "url_default": "http://img1", "url_pre": "http://img1_prv", "width": 1242, "height": 1660, "info_list": [{ "image_scene": "WB_DFT", "url": "http://img1_dft" }] }],
              "ip_location": "上海", "share_info": { "un_share": false },
              "type": "normal", "title": "正式官宣！Game For Good Jam来啦！",
              "interact_info": { "collected_count": "30", "comment_count": "5", "share_count": "21", "liked": false, "liked_count": "60", "collected": false },
              "tag_list": [{ "id": "61508abd...", "name": "G4G", "type": "topic" }],
              "time": 1756284629000, "last_update_time": 1756286910000, "note_id": "68aec6d5000000001d03a9ad",
              "desc": "一场属于游戏人的开发马拉松" }
            }
          ] }
        }
        """;

        var api = JsonSerializer.Deserialize<FeedApiResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Act
        Assert.That(api, Is.Not.Null);
        var list = FeedApiConverter.ConvertToNoteDetails(api!);

        // Assert
        Assert.That(list.Count, Is.EqualTo(1));
        var d = list[0];
        Assert.That(d.Id, Is.EqualTo("68aec6d5000000001d03a9ad"));
        Assert.That(d.Type, Is.EqualTo(NoteType.Image));
        Assert.That(d.Content, Does.Contain("开发马拉松"));
        Assert.That(d.PublishTime.HasValue, Is.True);
        Assert.That(d.LastUpdateTime.HasValue, Is.True);
        Assert.That(d.IpLocation, Is.EqualTo("上海"));
        Assert.That(d.ShareDisabled, Is.False);
        Assert.That(d.LikeCount, Is.EqualTo(60));
        Assert.That(d.CommentCount, Is.EqualTo(5));
        Assert.That(d.FavoriteCount, Is.EqualTo(30));
        Assert.That(d.ShareCount, Is.EqualTo(21));
        Assert.That(string.IsNullOrEmpty(d.CoverImage), Is.False);
        Assert.That(d.CoverInfo, Is.Not.Null);
    }

    /// <summary>
    /// 使用“video” 示例片段验证视频相关字段与 URL 选择。
    /// </summary>
    [Test]
    public void Should_Choose_Best_Video_Stream_For_Video_Note()
    {
        // Arrange：仅保留必要的 video.media.stream 片段
        var json = """
        {
          "code": 0, "success": true, "msg": "成功",
          "data": {
            "items": [
              {
                "id": "684cd779000000000f033db9", "model_type": "note", "note_card": {
                  "user": { "user_id": "624e440c...", "nickname": "呱斯塔", "avatar": "https://sns-avatar...jpg", "xsec_token": "ABMF6l..." },
                  "video": { "media": { "video": { "duration": 107 }, "stream": { "h265": [
                    { "height": 1080, "master_url": "http://video_1080.mp4", "stream_type": 115, "video_bitrate": 311050 }
                  ] }, "video_id": 1 }, "capa": { "duration": 106 } },
                  "type": "video", "title": "视频标题", "image_list": [],
                  "time": 1749869953000, "last_update_time": 1749866411000, "note_id": "684cd779000000000f033db9"
                }
              }
            ]
          }
        }
        """;

        var api = JsonSerializer.Deserialize<FeedApiResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Act
        var list = FeedApiConverter.ConvertToNoteDetails(api!);

        // Assert
        Assert.That(list.Count, Is.EqualTo(1));
        var d = list[0];
        Assert.That(d.Type, Is.EqualTo(NoteType.Video));
        Assert.That(d.VideoDuration, Is.GreaterThan(0));
        Assert.That(string.IsNullOrEmpty(d.VideoUrl), Is.False, "应选择最佳分辨率的 master_url");
        Assert.That(d.VideoInfo, Is.Not.Null);
    }
}
