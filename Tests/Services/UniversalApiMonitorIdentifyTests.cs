using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 通用API监听器 URL 端点识别测试（覆盖 v1/v2 并兼容未来版本）。
/// </summary>
[TestFixture]
public class UniversalApiMonitorIdentifyTests
{
    /// <summary>
    /// 验证 v1 feed 路径可正确识别为 Feed 端点。
    /// </summary>
    [Test]
    public void Identify_Feed_V1()
    {
        var url = "https://edith.xiaohongshu.com/api/sns/web/v1/feed";
        var t = UniversalApiMonitor.IdentifyApiEndpoint(url);
        Assert.That(t, Is.EqualTo(ApiEndpointType.Feed));
    }

    /// <summary>
    /// 验证 v2 评论分页路径可正确识别为 Comments 端点（带查询串）。
    /// </summary>
    [Test]
    public void Identify_Comments_V2_WithQuery()
    {
        var url = "https://edith.xiaohongshu.com/api/sns/web/v2/comment/page?note_id=68befacc000000001d023f6a&cursor=&top_comment_id=&image_formats=jpg,webp,avif&xsec_token=ABC";
        var t = UniversalApiMonitor.IdentifyApiEndpoint(url);
        Assert.That(t, Is.EqualTo(ApiEndpointType.Comments));
    }

    /// <summary>
    /// 验证未知路径不会被误识别。
    /// </summary>
    [Test]
    public void Identify_Unknown()
    {
        var url = "https://edith.xiaohongshu.com/page/about";
        var t = UniversalApiMonitor.IdentifyApiEndpoint(url);
        Assert.That(t, Is.Null);
    }
}

