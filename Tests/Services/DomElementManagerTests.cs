using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// DOM元素管理服务测试
/// </summary>
[TestFixture]
public class DomElementManagerTests
{
    private DomElementManager _domElementManager = null!;

    [SetUp]
    public void SetUp()
    {
        _domElementManager = new DomElementManager();
    }

    [Test]
    public void GetSelectors_WithValidAlias_ReturnsExpectedSelectors()
    {
        // Arrange
        var alias = "searchBox";

        // Act
        var selectors = _domElementManager.GetSelectors(alias);

        // Assert
        Assert.That(selectors, Is.Not.Null);
        Assert.That(selectors, Is.Not.Empty);
        Assert.That(selectors.All(s => !string.IsNullOrWhiteSpace(s)), Is.True);
    }

    [Test]
    public void GetSelectors_WithInvalidAlias_ReturnsAliasAsList()
    {
        // Arrange
        var alias = "invalidSelector";

        // Act
        var selectors = _domElementManager.GetSelectors(alias);

        // Assert
        Assert.That(selectors, Is.Not.Null);
        Assert.That(selectors, Has.Count.EqualTo(1));
        Assert.That(selectors[0], Is.EqualTo(alias));
    }

    [Test]
    public void GetAllSelectors_ReturnsNonEmptyDictionary()
    {
        // Act
        var allSelectors = _domElementManager.GetAllSelectors();

        // Assert
        Assert.That(allSelectors, Is.Not.Null);
        Assert.That(allSelectors, Is.Not.Empty);
        Assert.That(allSelectors.Values.All(list => list.Any()), Is.True);
    }

    [Test]
    [TestCase("searchBox")]
    [TestCase("noteList")]
    [TestCase("likeButton")]
    [TestCase("NoteItem")] // 新增：保障服务层使用到的别名在管理器中存在
    public void GetSelectors_WithCommonAliases_ReturnsValidSelectors(string alias)
    {
        // Act
        var selectors = _domElementManager.GetSelectors(alias);

        // Assert
        Assert.That(selectors, Is.Not.Null);
        if (selectors.Any())
        {
            Assert.That(selectors.All(s => !string.IsNullOrWhiteSpace(s)), Is.True);
        }
    }

    /// <summary>
    /// 断言：新增的评论激活按钮别名应返回非空选择器集合。
    /// </summary>
    [Test]
    public void GetSelectors_DetailPageCommentButton_ShouldReturnSelectors()
    {
        var selectors = _domElementManager.GetSelectors("DetailPageCommentButton");
        Assert.That(selectors, Is.Not.Null);
        Assert.That(selectors, Is.Not.Empty);
        // 至少应包含 engage-bar 的 chat-wrapper 作为首要选择器
        Assert.That(selectors.Any(s => s.Contains("chat-wrapper")), Is.True);
    }

    /// <summary>
    /// 破坏性变更：likeButton/likeButtonActive 选择器不再依赖 like-active 类名，避免误判。
    /// </summary>
    [Test]
    public void LikeSelectors_ShouldNotContain_LikeActive_Class()
    {
        var like = _domElementManager.GetSelectors("likeButton");
        var liked = _domElementManager.GetSelectors("likeButtonActive");

        Assert.That(like.All(s => !s.Contains("like-active")), Is.True);
        Assert.That(liked.All(s => !s.Contains("like-active")), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        _domElementManager = null!;
    }
}
