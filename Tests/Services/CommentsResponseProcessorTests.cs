using System.Text.Json;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 评论响应处理器（CommentsResponseProcessor）单元测试
/// - 覆盖：基本解析、图片解析、子评论解析、计数与时间戳解析、NoteId 关联。
/// - 说明：使用项目内提供的处理器，模拟传入 URL 与 JSON 响应体。
/// </summary>
[TestFixture]
public class CommentsResponseProcessorTests
{
    private ILogger _logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning)).CreateLogger("Tests");

    /// <summary>
    /// 验证：能解析评论主字段、用户、计数与发布时间，并关联 NoteId。
    /// </summary>
    [Test]
    public void Should_Parse_TopLevel_Comments_With_Metadata()
    {
        // Arrange
        var url = "https://edith.xiaohongshu.com/api/sns/web/v2/comment/page?note_id=68a0be19000000001c032874&cursor=&top_comment_id=&image_formats=jpg,webp,avif&xsec_token=AB45oy2...";
        var json = """
        {
            "code": 0, "success": true, "msg": "成功",
            "data": { "cursor": "68a1326d", "has_more": false, "time": 1757339611767, "xsec_token": "A...", "user_id": "66482064", "comments": [
               { "user_info": { "user_id": "5b39a21e6b58b70ff2d97f34", "nickname": "锅元虾", "image": "https://sns-avatar...jpg", "xsec_token": "ABfKn..." },
                 "id": "68a0c75500000000260358e4", "status": 0, "content": "厉害[哇R]", "like_count": "3", "ip_location": "上海", "sub_comments": [],
                 "note_id": "68a0be19000000001c032874", "create_time": 1755367253000, "sub_comment_count": "0", "sub_comment_cursor": "",
                 "sub_comment_has_more": false, "at_users": [], "liked": false }
            ] }
        }
        """;

        var proc = new CommentsResponseProcessor(_logger);

        // Act
        var result = proc.ProcessResponseAsync(url, json).GetAwaiter().GetResult();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.EndpointType, Is.EqualTo(ApiEndpointType.Comments));
        Assert.That(result.ProcessedData, Is.Not.Null);
        Assert.That(result.ProcessedData!["NoteId"].ToString(), Is.EqualTo("68a0be19000000001c032874"));

        var comments = result.ProcessedData!["Comments"] as System.Collections.Generic.List<CommentInfo>;
        Assert.That(comments, Is.Not.Null);
        Assert.That(comments!.Count, Is.GreaterThan(0));

        var first = comments[0];
        Assert.That(first.Author, Is.EqualTo("锅元虾"));
        Assert.That(first.AuthorId, Is.EqualTo("5b39a21e6b58b70ff2d97f34"));
        Assert.That(first.LikeCount, Is.EqualTo(3));
        Assert.That(first.PublishTime.HasValue, Is.True, "应解析毫秒时间戳为发布时间");
        Assert.That(first.IpLocation, Is.EqualTo("上海"));
        Assert.That(first.NoteId, Is.EqualTo("68a0be19000000001c032874"));
    }

    /// <summary>
    /// 验证：能解析图片列表（WB_DFT 优先）以及子评论。
    /// </summary>
    [Test]
    public void Should_Parse_Pictures_And_SubComments()
    {
        // Arrange：包含 pictures 与 sub_comments 的片段（简化版）
        var url = "https://edith.xiaohongshu.com/api/sns/web/v2/comment/page?note_id=68bab69e000000001c005cc1";
        var json = """
        {
            "code": 0, "success": true, "msg": "成功",
            "data": { "cursor": "c", "has_more": true, "comments": [
              { "id": "68bac3b10000000030012405", "content": "", "like_count": "2", "liked": false,
                "user_info": { "user_id": "5c768121000000001101543b", "nickname": "TapTap聚光灯游戏创作挑战", "image": "https://sns-avatar...jpg", "xsec_token": "ABpvx7..." },
                "pictures": [{ "info_list": [
                    { "image_scene": "WB_PRV", "url": "http://..._prv_1" },
                    { "image_scene": "WB_DFT", "url": "http://..._dft_1" }
                ], "height": 100, "width": 100 }],
                "sub_comments": [{ "id": "68bebac000000000300192f2", "content": "子评", "user_info": { "user_id": "62d17df2...", "nickname": "我真的好想喝奶茶啊" }, "like_count": "0" }]
              }
            ] }
        }
        """;

        var proc = new CommentsResponseProcessor(_logger);

        // Act
        var result = proc.ProcessResponseAsync(url, json).GetAwaiter().GetResult();

        // Assert
        Assert.That(result, Is.Not.Null);
        var comments = result!.ProcessedData!["Comments"] as System.Collections.Generic.List<CommentInfo>;
        Assert.That(comments, Is.Not.Null);
        Assert.That(comments![0].PictureUrls.Count, Is.EqualTo(1), "应选择 WB_DFT 或首个可用 URL");
        Assert.That(comments![0].Replies.Count, Is.EqualTo(1), "应解析子评论");
    }
}
