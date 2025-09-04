using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 选择器管理服务测试
/// </summary>
[TestFixture]
public class SelectorManagerTests
{
    private SelectorManager _selectorManager;

    [SetUp]
    public void SetUp()
    {
        _selectorManager = new SelectorManager();
    }

    [Test]
    public void GetSelectors_WithValidAlias_ReturnsExpectedSelectors()
    {
        // Arrange
        var alias = "searchBox";

        // Act
        var selectors = _selectorManager.GetSelectors(alias);

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
        var selectors = _selectorManager.GetSelectors(alias);

        // Assert
        Assert.That(selectors, Is.Not.Null);
        Assert.That(selectors, Has.Count.EqualTo(1));
        Assert.That(selectors[0], Is.EqualTo(alias));
    }

    [Test]
    public void GetAllSelectors_ReturnsNonEmptyDictionary()
    {
        // Act
        var allSelectors = _selectorManager.GetAllSelectors();

        // Assert
        Assert.That(allSelectors, Is.Not.Null);
        Assert.That(allSelectors, Is.Not.Empty);
        Assert.That(allSelectors.Values.All(list => list.Any()), Is.True);
    }

    [Test]
    [TestCase("searchBox")]
    [TestCase("noteList")]
    [TestCase("likeButton")]
    public void GetSelectors_WithCommonAliases_ReturnsValidSelectors(string alias)
    {
        // Act
        var selectors = _selectorManager.GetSelectors(alias);

        // Assert
        Assert.That(selectors, Is.Not.Null);
        if (selectors.Any())
        {
            Assert.That(selectors.All(s => !string.IsNullOrWhiteSpace(s)), Is.True);
        }
    }

    [TearDown]
    public void TearDown()
    {
        _selectorManager = null!;
    }
}