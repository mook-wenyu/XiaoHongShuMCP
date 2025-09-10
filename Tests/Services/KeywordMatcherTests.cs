using XiaoHongShuMCP.Services;

namespace Tests.Services;

/// <summary>
/// 关键词匹配工具测试用例（覆盖子串匹配、全角半角、短语覆盖度与模糊匹配等场景）。
/// </summary>
public class KeywordMatcherTests
{
    [Test]
    public void Matches_Substring_Basic()
    {
        var text = "这是一篇关于iPhone 15 Pro的测评";
        var ok = KeywordMatcher.Matches(text, "iPhone 15 Pro");
        Assert.That(ok, Is.True);
    }

    [Test]
    public void Matches_FullWidth_To_HalfWidth()
    {
        var text = "新品：ｉＰｈｏｎｅ１５ Ｐｒｏ 发布"; // 全角
        var ok = KeywordMatcher.Matches(text, "iphone 15 pro");
        Assert.That(ok, Is.True);
    }

    [Test]
    public void Matches_Phrase_Coverage()
    {
        var text = "秋季发布会：苹果 新品 iPhone 15 Pro Max 亮相";
        var ok = KeywordMatcher.Matches(text, "iPhone 15 Pro");
        Assert.That(ok, Is.True);
    }

    [Test]
    public void Matches_Fuzzy_For_Long_Keyword()
    {
        var text = "评测：iphone15 pr0 摄影能力"; // 把 'o' 打成零 '0'
        var ok = KeywordMatcher.Matches(text, "iphone15 pro");
        Assert.That(ok, Is.True);
    }

    [Test]
    public void NotMatch_ShortKeyword_NoFuzzy()
    {
        var text = "x-pro 试用";
        // 关键字过短（<=3），不启用模糊，且不存在完全子串
        var ok = KeywordMatcher.Matches(text, "xpe");
        Assert.That(ok, Is.False);
    }

    [Test]
    public void Matches_IgnoreSpaces()
    {
        var text = "体验：iphone15pro 夜景模式";
        var ok = KeywordMatcher.Matches(text, "iPhone 15 Pro");
        Assert.That(ok, Is.True);
    }

    [Test]
    public void Matches_Chinese_Substring_Tourism()
    {
        // 中文子串匹配：标题“我要去旅游了”应命中关键词“旅游”
        var text = "我要去旅游了";
        var ok = KeywordMatcher.Matches(text, "旅游");
        Assert.That(ok, Is.True);
    }
}
