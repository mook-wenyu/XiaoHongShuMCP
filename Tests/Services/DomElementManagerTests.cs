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

    /// <summary>
    /// 新增的 LikeIcon/CollectIcon 兜底点击别名应存在且包含 svg 类名。
    /// </summary>
    [Test]
    public void IconAliases_ShouldExist_AndContainSvgClass()
    {
        var likeIcon = _domElementManager.GetSelectors("LikeIcon");
        var collectIcon = _domElementManager.GetSelectors("CollectIcon");

        Assert.That(likeIcon, Is.Not.Null);
        Assert.That(collectIcon, Is.Not.Null);
        Assert.That(likeIcon, Is.Not.Empty);
        Assert.That(collectIcon, Is.Not.Empty);

        Assert.That(likeIcon.Any(s => s.Contains("like-icon")), Is.True);
        Assert.That(collectIcon.Any(s => s.Contains("collect-icon")), Is.True);
    }

    /// <summary>
    /// like/favorite 关键别名应采用 :has(use[href|xlink:href]) 语义，以图标状态判定取代脆弱类名。
    /// </summary>
    [Test]
    public void LikeAndFavoriteSelectors_ShouldContain_Has_Use_Semantics()
    {
        var like = _domElementManager.GetSelectors("likeButton");
        var liked = _domElementManager.GetSelectors("likeButtonActive");
        var fav = _domElementManager.GetSelectors("favoriteButton");
        var faved = _domElementManager.GetSelectors("favoriteButtonActive");

        bool HasHasUse(IEnumerable<string> xs) => xs.Any(s => s.Contains(":has(") && s.Contains("use[", StringComparison.Ordinal));

        Assert.That(HasHasUse(like), Is.True, "likeButton 应至少包含一次 :has(use[...]) 语义选择器");
        Assert.That(HasHasUse(liked), Is.True, "likeButtonActive 应至少包含一次 :has(use[...]) 语义选择器");
        Assert.That(HasHasUse(fav), Is.True, "favoriteButton 应至少包含一次 :has(use[...]) 语义选择器");
        Assert.That(HasHasUse(faved), Is.True, "favoriteButtonActive 应至少包含一次 :has(use[...]) 语义选择器");
    }

    /// <summary>
    /// NoteHiddenLink/NoteVisibleLink 应包含语义化 :has 结构，提升稳定性。
    /// </summary>
    [Test]
    public void NoteLinks_ShouldContain_Has_Semantics()
    {
        var hidden = _domElementManager.GetSelectors("NoteHiddenLink");
        var visible = _domElementManager.GetSelectors("NoteVisibleLink");
        Assert.That(hidden.Any(s => s.Contains(":has(")), Is.True);
        Assert.That(visible.Any(s => s.Contains(":has(")), Is.True);
    }

    /// <summary>
    /// NoteDetailTitle 应包含容器语义选择器（:has）以降低 DOM 变化风险。
    /// </summary>
    [Test]
    public void NoteDetailTitle_ShouldContain_ContainerHas_Semantics()
    {
        var sels = _domElementManager.GetSelectors("NoteDetailTitle");
        Assert.That(sels.Any(s => s.Contains("#detail-desc:has(")), Is.True);
    }

    [Test]
    public void SortOptions_ShouldContain_HasText_Semantics()
    {
        var comp = _domElementManager.GetSelectors("SortOptionComprehensive");
        var latest = _domElementManager.GetSelectors("SortOptionLatest");
        var liked = _domElementManager.GetSelectors("SortOptionMostLiked");
        var commented = _domElementManager.GetSelectors("SortOptionMostCommented");
        var favorited = _domElementManager.GetSelectors("SortOptionMostFavorited");

        bool HasText(IEnumerable<string> xs, string text) => xs.Any(s => s.Contains($":has-text('{text}')"));

        Assert.That(HasText(comp, "综合"), Is.True);
        Assert.That(HasText(latest, "最新"), Is.True);
        Assert.That(HasText(liked, "最多点赞"), Is.True);
        Assert.That(HasText(commented, "最多评论"), Is.True);
        Assert.That(HasText(favorited, "最多收藏"), Is.True);
    }

    [Test]
    public void NoteType_ShouldContain_HasText_Semantics()
    {
        var video = _domElementManager.GetSelectors("NoteTypeVideo");
        var image = _domElementManager.GetSelectors("NoteTypeImage");
        Assert.That(video.Any(s => s.Contains(":has-text('视频')")), Is.True);
        Assert.That(image.Any(s => s.Contains(":has-text('图文')")), Is.True);
    }

    [Test]
    public void CommentButton_ShouldContain_Has_Or_HasText_Semantics()
    {
        var sels = _domElementManager.GetSelectors("CommentButton");
        Assert.That(sels.Any(s => s.Contains(":has(") || s.Contains(":has-text(")), Is.True);
    }

    [Test]
    public void SearchSelectors_ShouldContain_SemanticVariants()
    {
        var input = _domElementManager.GetSelectors("SearchInput");
        var button = _domElementManager.GetSelectors("SearchButton");
        Assert.That(input.Any(s => s.Contains("type='search'") || s.Contains("role='search'")), Is.True);
        Assert.That(button.Any(s => s.Contains(":has-text('搜索')") || s.Contains("aria-label='搜索'")), Is.True);
    }

    [Test]
    public void FilterPanel_ShouldContain_DialogHasText_Semantics()
    {
        var panel = _domElementManager.GetSelectors("FilterPanel");
        Assert.That(panel.Any(s => s.Contains("[role='dialog']:has-text('筛选')")), Is.True);
    }

    [Test]
    public void DetailMoreActions_ShouldExist_AndContain_Semantics()
    {
        var more = _domElementManager.GetSelectors("DetailMoreActions");
        Assert.That(more, Is.Not.Null);
        Assert.That(more, Is.Not.Empty);
        Assert.That(more.Any(s => s.Contains(":has(") || s.Contains(":has-text(")), Is.True);
    }

    [Test]
    public void JsonBackedSelectors_ShouldExpose_NoteItemAlias()
    {
        var all = _domElementManager.GetAllSelectors();
        Assert.That(all.ContainsKey("NoteItem"), Is.True);
        Assert.That(all["NoteItem"], Is.Not.Empty);
    }

    [Test]
    public void ExploreState_ShouldPrioritize_StateSpecificSelectors()
    {
        var exploreSelectors = _domElementManager.GetSelectors("NoteItem", PageState.Explore);
        Assert.That(exploreSelectors, Is.Not.Empty);
        Assert.That(exploreSelectors[0], Is.EqualTo("#exploreFeeds .note-item"));
    }

    [TearDown]
    public void TearDown()
    {
        _domElementManager = null!;
    }
}
