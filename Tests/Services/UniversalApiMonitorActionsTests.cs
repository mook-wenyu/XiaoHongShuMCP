using Microsoft.Extensions.Logging;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 通用API监听 - 互动动作端点（点赞/收藏/评论）的处理器与识别测试。
/// 说明：
/// - 验证 IdentifyApiEndpoint 对给定 URL 的识别结果。
/// - 验证处理器对示例响应的解析产物（ProcessedData）。
/// </summary>
[TestFixture]
public class UniversalApiMonitorActionsTests
{
    private readonly ILogger _logger = new LoggerFactory().CreateLogger("Tests");

    [Test]
    public void IdentifyApiEndpoint_ShouldMap_ActionEndpoints()
    {
        Assert.That(UniversalApiMonitor.IdentifyApiEndpoint("https://edith.xiaohongshu.com/api/sns/web/v1/note/like"), Is.EqualTo(ApiEndpointType.LikeNote));
        Assert.That(UniversalApiMonitor.IdentifyApiEndpoint("https://edith.xiaohongshu.com/api/sns/web/v1/note/dislike"), Is.EqualTo(ApiEndpointType.DislikeNote));
        Assert.That(UniversalApiMonitor.IdentifyApiEndpoint("https://edith.xiaohongshu.com/api/sns/web/v1/note/collect"), Is.EqualTo(ApiEndpointType.CollectNote));
        Assert.That(UniversalApiMonitor.IdentifyApiEndpoint("https://edith.xiaohongshu.com/api/sns/web/v1/note/uncollect"), Is.EqualTo(ApiEndpointType.UncollectNote));
        Assert.That(UniversalApiMonitor.IdentifyApiEndpoint("https://edith.xiaohongshu.com/api/sns/web/v1/comment/post"), Is.EqualTo(ApiEndpointType.CommentPost));
        Assert.That(UniversalApiMonitor.IdentifyApiEndpoint("https://edith.xiaohongshu.com/api/sns/web/v1/comment/delete"), Is.EqualTo(ApiEndpointType.CommentDelete));
    }

    [Test]
    public async Task LikeActionResponseProcessor_ShouldParse_NewLike()
    {
        const string url = "https://edith.xiaohongshu.com/api/sns/web/v1/note/like";
        const string body = "{\"code\":0,\"success\":true,\"msg\":\"成功\",\"data\":{\"new_like\":true}}";

        var p = new LikeActionResponseProcessor(_logger);
        var resp = await p.ProcessResponseAsync(url, body);
        Assert.That(resp, Is.Not.Null);
        Assert.That(resp!.EndpointType, Is.EqualTo(ApiEndpointType.LikeNote));
        Assert.That((string)resp.ProcessedData!["Action"], Is.EqualTo("like"));
        Assert.That((bool)resp.ProcessedData!["Success"], Is.True);
        Assert.That((bool)resp.ProcessedData!["NewLike"], Is.True);
    }

    [Test]
    public async Task DislikeActionResponseProcessor_ShouldParse_LikeCount()
    {
        const string url = "https://edith.xiaohongshu.com/api/sns/web/v1/note/dislike";
        const string body = "{\"data\":{\"like_count\":347},\"code\":0,\"success\":true,\"msg\":\"成功\"}";

        var p = new DislikeActionResponseProcessor(_logger);
        var resp = await p.ProcessResponseAsync(url, body);
        Assert.That(resp, Is.Not.Null);
        Assert.That(resp!.EndpointType, Is.EqualTo(ApiEndpointType.DislikeNote));
        Assert.That((string)resp.ProcessedData!["Action"], Is.EqualTo("dislike"));
        Assert.That((bool)resp.ProcessedData!["Success"], Is.True);
        Assert.That((int)resp.ProcessedData!["LikeCount"], Is.EqualTo(347));
    }

    [Test]
    public async Task Collect_Uncollect_Processors_ShouldParse_Success()
    {
        var pc = new CollectActionResponseProcessor(_logger);
        var pr = await pc.ProcessResponseAsync("https://edith.xiaohongshu.com/api/sns/web/v1/note/collect", "{\"code\":0,\"success\":true,\"msg\":\"成功\"}");
        Assert.That(pr, Is.Not.Null);
        Assert.That(pr!.EndpointType, Is.EqualTo(ApiEndpointType.CollectNote));
        Assert.That((string)pr.ProcessedData!["Action"], Is.EqualTo("collect"));
        Assert.That((bool)pr.ProcessedData!["Success"], Is.True);

        var pu = new UncollectActionResponseProcessor(_logger);
        var pr2 = await pu.ProcessResponseAsync("https://edith.xiaohongshu.com/api/sns/web/v1/note/uncollect", "{\"code\":0,\"success\":true,\"msg\":\"成功\"}");
        Assert.That(pr2, Is.Not.Null);
        Assert.That(pr2!.EndpointType, Is.EqualTo(ApiEndpointType.UncollectNote));
        Assert.That((string)pr2.ProcessedData!["Action"], Is.EqualTo("uncollect"));
        Assert.That((bool)pr2.ProcessedData!["Success"], Is.True);
    }

    [Test]
    public async Task CommentPost_Processor_ShouldParse_CommentId_And_Content()
    {
        const string url = "https://edith.xiaohongshu.com/api/sns/web/v1/comment/post";
        const string body = "{\n    \"code\": 0,\n    \"success\": true,\n    \"msg\": \"成功\",\n    \"data\": {\n        \"comment\": {\n            \"show_tags\": [],\n            \"create_time\": 1757395160992,\n            \"ip_location\": \"广东\",\n            \"note_id\": \"68bea815000000001b01f33a\",\n            \"content\": \"吓人\",\n            \"at_users\": [],\n            \"liked\": false,\n            \"user_info\": {\n                \"user_id\": \"66482064000000001e00e245\",\n                \"nickname\": \"云速物流空运专线\",\n                \"image\": \"https://sns-avatar-qc.xhscdn.com/avatar/fdfcdde9-0ff1-3d3b-8dac-46a167a10935?imageView2/2/w/120/format/jpg\"\n            },\n            \"id\": \"68bfb8d8000000002400d653\",\n            \"status\": 2,\n            \"like_count\": \"0\"\n        },\n        \"time\": 1757395161009,\n        \"toast\": \"评论已发布\"\n    }\n}";

        var p = new CommentPostResponseProcessor(_logger);
        var resp = await p.ProcessResponseAsync(url, body);
        Assert.That(resp, Is.Not.Null);
        Assert.That(resp!.EndpointType, Is.EqualTo(ApiEndpointType.CommentPost));
        Assert.That((string)resp.ProcessedData!["Action"], Is.EqualTo("comment_post"));
        Assert.That((bool)resp.ProcessedData!["Success"], Is.True);
        Assert.That((string)resp.ProcessedData!["CommentId"], Is.EqualTo("68bfb8d8000000002400d653"));
        Assert.That((string)resp.ProcessedData!["NoteId"], Is.EqualTo("68bea815000000001b01f33a"));
        Assert.That((string)resp.ProcessedData!["Content"], Does.Contain("吓人"));
    }
}
