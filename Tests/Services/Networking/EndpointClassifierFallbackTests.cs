using NUnit.Framework;
using XiaoHongShuMCP.Services;
using HushOps.Core.Networking;
using HushOps.Core.Automation.Abstractions;

namespace XiaoHongShuMCP.Tests.Services.Networking;

/// <summary>
/// 端点分类器回退策略（基于正文体征）测试。
/// </summary>
public class EndpointClassifierFallbackTests
{
    private readonly EndpointClassifier classifier = new();

    [Test]
    public void Classify_Fallback_Comments_ByBody()
    {
        var json = "{" +
                   "\"success\":true,\"data\":{\"comments\":[{\"id\":\"1\"}]}}";
        var ep = classifier.Classify(NetworkEventKind.HttpResponse, "https://edith.xiaohongshu.com/api/unknown", 200, json, null);
        Assert.That(ep, Is.EqualTo(ApiEndpointType.Comments.ToString()));
    }

    [Test]
    public void Classify_Fallback_Search_ByBody()
    {
        var json = "{" +
                   "\"success\":true,\"data\":{\"page_token\":\"abc\",\"items\":[{}]}}";
        var ep = classifier.Classify(NetworkEventKind.HttpResponse, "https://edith.xiaohongshu.com/api/unknown", 200, json, null);
        Assert.That(ep, Is.EqualTo(ApiEndpointType.SearchNotes.ToString()));
    }

    [Test]
    public void Classify_Fallback_Feed_ByBody_CurrentTime()
    {
        var json = "{" +
                   "\"success\":true,\"data\":{\"current_time\":123,\"items\":[{\"note_card\":{}}]}}";
        var ep = classifier.Classify(NetworkEventKind.HttpResponse, "https://edith.xiaohongshu.com/api/unknown", 200, json, null);
        Assert.That(ep, Is.EqualTo(ApiEndpointType.Feed.ToString()));
    }

    [Test]
    public void Classify_Fallback_Homefeed_ByBody_Default()
    {
        var json = "{" +
                   "\"success\":true,\"data\":{\"cursor_score\":\"c\",\"items\":[{\"note_card\":{}}]}}";
        var ep = classifier.Classify(NetworkEventKind.HttpResponse, "https://edith.xiaohongshu.com/api/unknown", 200, json, null);
        Assert.That(ep, Is.EqualTo(ApiEndpointType.Homefeed.ToString()));
    }
}

