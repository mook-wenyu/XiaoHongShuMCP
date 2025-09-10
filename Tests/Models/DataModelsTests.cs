using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Models;

/// <summary>
/// 数据模型测试
/// </summary>
[TestFixture]
public class DataModelsTests
{
    [Test]
    public void NoteInfo_DetermineType_WithVideoContent_ReturnsVideoType()
    {
        // Arrange
        var noteDetail = new NoteDetail
        {
            Id = "test",
            Title = "测试视频",
            VideoUrl = "https://video.example.com/test.mp4"
        };

        // Act
        noteDetail.DetermineType();

        // Assert
        Assert.That(noteDetail.Type, Is.EqualTo(NoteType.Video));
        Assert.That(noteDetail.GetTypeConfidence(), Is.EqualTo(TypeIdentificationConfidence.High));
    }

    [Test]
    public void NoteInfo_DetermineType_WithImages_ReturnsImageType()
    {
        // Arrange
        var noteDetail = new NoteDetail
        {
            Id = "test",
            Title = "测试图文",
            Images = new List<string> { "image1.jpg", "image2.jpg" }
        };

        // Act
        noteDetail.DetermineType();

        // Assert
        Assert.That(noteDetail.Type, Is.EqualTo(NoteType.Image));
        Assert.That(noteDetail.GetTypeConfidence(), Is.EqualTo(TypeIdentificationConfidence.High));
    }

    [Test]
    public void NoteInfo_DetermineType_WithLongContent_ReturnsArticleType()
    {
        // Arrange
        var noteDetail = new NoteDetail
        {
            Id = "test",
            Title = "长文测试",
            Content = new string('测', 600) // 600个字符的长文
        };

        // Act
        noteDetail.DetermineType();

        // Assert - 长文内容现在统一归类为图文类型
        Assert.That(noteDetail.Type, Is.EqualTo(NoteType.Image));
        Assert.That(noteDetail.GetTypeConfidence(), Is.EqualTo(TypeIdentificationConfidence.Medium));
    }

    [Test]
    public void UserInfo_ExtractRedIdFromText_WithValidFormat_ReturnsId()
    {
        // Arrange
        var redIdText = "小红书号：27456090856";

        // Act
        var result = UserInfo.ExtractRedIdFromText(redIdText);

        // Assert
        Assert.That(result, Is.EqualTo("27456090856"));
    }

    [Test]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("无效格式")]
    public void UserInfo_ExtractRedIdFromText_WithInvalidFormat_ReturnsEmpty(string? input)
    {
        // Act
        var result = UserInfo.ExtractRedIdFromText(input!);

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void UserInfo_HasCompleteProfileData_WithAllFields_ReturnsTrue()
    {
        // Arrange
        var userInfo = new UserInfo
        {
            RedId = "27456090856",
            Nickname = "testuser",
            AvatarUrl = "https://avatar.example.com/test.jpg",
            FollowingCount = 100,
            FollowersCount = 1000,
            LikesCollectsCount = 5000
        };

        // Act
        var result = userInfo.HasCompleteProfileData();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void UserInfo_HasCompleteProfileData_WithMissingFields_ReturnsFalse()
    {
        // Arrange
        var userInfo = new UserInfo
        {
            RedId = "27456090856",
            Nickname = "testuser"
            // 缺少其他字段
        };

        // Act
        var result = userInfo.HasCompleteProfileData();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void SearchRequest_IsValid_WithValidData_ReturnsTrue()
    {
        // Arrange
        var request = new SearchRequest
        {
            Keyword = "测试关键词",
            MaxResults = 20
        };

        // Act
        var result = request.IsValid();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    [TestCase("", 10)]
    [TestCase(null, 10)]
    [TestCase("关键词", 0)]
    [TestCase("关键词", -1)]
    public void SearchRequest_IsValid_WithInvalidData_ReturnsFalse(string? keyword, int maxResults)
    {
        // Arrange
        var request = new SearchRequest
        {
            Keyword = keyword!,
            MaxResults = maxResults
        };

        // Act
        var result = request.IsValid();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void OperationResult_Ok_CreatesSuccessfulResult()
    {
        // Arrange
        var testData = "测试数据";

        // Act
        var result = OperationResult<string>.Ok(testData);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data, Is.EqualTo(testData));
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void OperationResult_Fail_CreatesFailedResult()
    {
        // Arrange
        var errorMessage = "测试错误";
        var errorType = ErrorType.ValidationError;

        // Act
        var result = OperationResult<string>.Fail(errorMessage, errorType);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Data, Is.Null);
        Assert.That(result.ErrorMessage, Is.EqualTo(errorMessage));
        Assert.That(result.ErrorType, Is.EqualTo(errorType));
    }
}
