using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// HtmlSanitizer 纯函数单元测试：确保新增工具逻辑具备可验证性与覆盖率。
/// </summary>
[TestFixture]
public class HtmlSanitizerTests
{
    [Test]
    public void SanitizeForLogging_Should_Redact_Sensitive_Keywords_Ignoring_Case()
    {
        var raw = "Authorization=abc; Cookie=foo; Set-Cookie=bar;";
        var sanitized = HtmlSanitizer.SanitizeForLogging(raw);
        Assert.That(sanitized, Does.Not.Contain("Authorization"));
        Assert.That(sanitized, Does.Not.Contain("Cookie="));
        Assert.That(sanitized, Does.Not.Contain("Set-Cookie"));
        Assert.That(sanitized, Does.Contain("auth="));
        Assert.That(sanitized, Does.Contain("ck="));
        Assert.That(sanitized, Does.Contain("sc="));
    }

    [Test]
    public void SafeTruncate_Should_Respect_Kb_Limit_And_Be_Non_Negative()
    {
        var text = new string('x', 10_000); // 10k chars
        var truncated = HtmlSanitizer.SafeTruncate(text, 1); // 1KB
        Assert.That(truncated.Length, Is.LessThanOrEqualTo(1024));
        // 小于限制不截断
        var small = "abc";
        Assert.That(HtmlSanitizer.SafeTruncate(small, 64), Is.EqualTo(small));
    }
}
