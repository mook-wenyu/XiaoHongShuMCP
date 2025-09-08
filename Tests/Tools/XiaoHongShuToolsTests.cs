using XiaoHongShuMCP.Tools;
using XiaoHongShuMCP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace XiaoHongShuMCP.Tests.Tools;

/// <summary>
/// MCP 工具集测试
/// </summary>
[TestFixture]
public class XiaoHongShuToolsTests
{
    private Mock<IAccountManager> _mockAccountManager = null!;
    private Mock<IXiaoHongShuService> _mockXiaoHongShuService = null!;
    private IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _mockAccountManager = new Mock<IAccountManager>();
        _mockXiaoHongShuService = new Mock<IXiaoHongShuService>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockAccountManager.Object);
        services.AddSingleton(_mockXiaoHongShuService.Object);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Test]
    public async Task ConnectToBrowser_WhenSuccessful_ReturnsConnectionResult()
    {
        // Arrange
        _mockAccountManager.Setup(x => x.ConnectToBrowserAsync())
            .ReturnsAsync(OperationResult<bool>.Ok(true));

        // Act
        var result = await XiaoHongShuTools.ConnectToBrowser(_serviceProvider);

        // Assert
        Assert.That(result.IsConnected, Is.True);
        Assert.That(result.IsLoggedIn, Is.True);
        Assert.That(result.Message, Is.EqualTo("浏览器连接成功"));
    }

    [Test]
    public async Task ConnectToBrowser_WhenFailed_ReturnsErrorResult()
    {
        // Arrange
        _mockAccountManager.Setup(x => x.ConnectToBrowserAsync())
            .ReturnsAsync(OperationResult<bool>.Fail("连接失败", ErrorType.BrowserError, "BROWSER_ERROR"));

        // Act
        var result = await XiaoHongShuTools.ConnectToBrowser(_serviceProvider);

        // Assert
        Assert.That(result.IsConnected, Is.False);
        Assert.That(result.IsLoggedIn, Is.False);
        Assert.That(result.Message, Is.EqualTo("连接失败"));
        Assert.That(result.ErrorCode, Is.EqualTo("BROWSER_ERROR"));
    }

    [Test]
    public async Task ConnectToBrowser_WhenException_ReturnsExceptionResult()
    {
        // Arrange
        _mockAccountManager.Setup(x => x.ConnectToBrowserAsync())
            .ThrowsAsync(new Exception("测试异常"));

        // Act
        var result = await XiaoHongShuTools.ConnectToBrowser(_serviceProvider);

        // Assert
        Assert.That(result.IsConnected, Is.False);
        Assert.That(result.IsLoggedIn, Is.False);
        Assert.That(result.Message, Contains.Substring("连接异常"));
        Assert.That(result.ErrorCode, Is.EqualTo("CONNECTION_EXCEPTION"));
    }


    [Test]
    public async Task BatchGetNoteDetailsOptimized_WithValidParameters_ReturnsEnhancedResult()
    {
        // Arrange
        var keywords = new List<string> { "美食", "火锅" };
        var expectedNoteDetails = new List<NoteDetail>
        {
            new NoteDetail 
            { 
                Id = "1", 
                Title = "美食推荐", 
                Author = "测试作者",
                LikeCount = 100,
                Type = NoteType.Image,
                Quality = DataQuality.Complete
            },
            new NoteDetail 
            { 
                Id = "2", 
                Title = "火锅攻略", 
                Author = "测试作者2",
                LikeCount = 50,
                Type = NoteType.Video,
                Quality = DataQuality.Partial
            }
        };

        var expectedStatistics = new BatchProcessingStatistics(
            CompleteDataCount: 1,
            PartialDataCount: 1,
            MinimalDataCount: 0,
            AverageProcessingTime: 3000,
            AverageLikes: 75,
            AverageComments: 0,
            TypeDistribution: new Dictionary<NoteType, int> 
            { 
                [NoteType.Image] = 1, 
                [NoteType.Video] = 1 
            },
            ProcessingModeStats: new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 1,
                [ProcessingMode.Standard] = 1,
                [ProcessingMode.Careful] = 0
            },
            CalculatedAt: DateTime.UtcNow
        );

        var expectedResult = new BatchNoteResult(
            SuccessfulNotes: expectedNoteDetails,
            FailedNotes: new List<(string, string)>(),
            ProcessedCount: 2,
            ProcessingTime: TimeSpan.FromSeconds(6),
            OverallQuality: DataQuality.Partial,
            Statistics: expectedStatistics,
            ExportInfo: null
        );

        _mockXiaoHongShuService.Setup(x => x.BatchGetNoteDetailsAsync(
                keywords, 10, false, true, null))
            .ReturnsAsync(OperationResult<BatchNoteResult>.Ok(expectedResult));

        // Act
        var result = await XiaoHongShuTools.BatchGetNoteDetailsOptimized(
            keywords, 10, false, true, null, _serviceProvider);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.SuccessfulNotes, Has.Count.EqualTo(2));
        Assert.That(result.ProcessedCount, Is.EqualTo(2));
        Assert.That(result.OverallQuality, Is.EqualTo(DataQuality.Partial));
        Assert.That(result.Statistics, Is.Not.Null);
        Assert.That(result.Statistics.CompleteDataCount, Is.EqualTo(1));
        Assert.That(result.Statistics.PartialDataCount, Is.EqualTo(1));
        Assert.That(result.Statistics.AverageLikes, Is.EqualTo(75));
        Assert.That(result.Statistics.TypeDistribution, Contains.Key(NoteType.Image));
        Assert.That(result.Statistics.TypeDistribution, Contains.Key(NoteType.Video));
    }

    [Test]
    public async Task BatchGetNoteDetailsOptimized_WithServiceFailure_ReturnsEmptyResult()
    {
        // Arrange
        var keywords = new List<string> { "测试关键词" };
        _mockXiaoHongShuService.Setup(x => x.BatchGetNoteDetailsAsync(
                It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult<BatchNoteResult>.Fail("批量获取失败"));

        // Act
        var result = await XiaoHongShuTools.BatchGetNoteDetailsOptimized(
            keywords, 10, false, true, null, _serviceProvider);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NoteDetails, Is.Empty);
        Assert.That(result.FailedNotes, Has.Count.EqualTo(1));
        Assert.That(result.FailedNotes.First().Item1, Is.EqualTo("测试关键词"));
        Assert.That(result.TotalProcessed, Is.EqualTo(0));
        Assert.That(result.OverallQuality, Is.EqualTo(DataQuality.Minimal));
    }

    [Test]
    public async Task BatchGetNoteDetailsOptimized_WithException_ReturnsErrorResult()
    {
        // Arrange
        var keywords = new List<string> { "测试关键词" };
        _mockXiaoHongShuService.Setup(x => x.BatchGetNoteDetailsAsync(
                It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("测试异常"));

        // Act
        var result = await XiaoHongShuTools.BatchGetNoteDetailsOptimized(
            keywords, 10, false, true, null, _serviceProvider);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NoteDetails, Is.Empty);
        Assert.That(result.FailedNotes, Has.Count.EqualTo(1));
        Assert.That(result.FailedNotes.First().Item2, Contains.Substring("测试异常"));
        Assert.That(result.TotalProcessed, Is.EqualTo(0));
        Assert.That(result.OverallQuality, Is.EqualTo(DataQuality.Minimal));
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task BatchGetNoteDetailsOptimized_WithDifferentAutoExportOptions_CallsServiceWithCorrectParameters(bool autoExport)
    {
        // Arrange
        var keywords = new List<string> { "测试" };
        var emptyResult = new BatchNoteResult(
            new List<NoteDetail>(),
            new List<(string, string)>(),
            0,
            TimeSpan.Zero,
            DataQuality.Minimal,
            new BatchProcessingStatistics(0, 0, 0, 0, 0, 0, new Dictionary<NoteType, int>(), 
                new Dictionary<ProcessingMode, int>(), DateTime.UtcNow),
            null
        );

        _mockXiaoHongShuService.Setup(x => x.BatchGetNoteDetailsAsync(
                It.IsAny<List<string>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult<BatchNoteResult>.Ok(emptyResult));

        // Act
        await XiaoHongShuTools.BatchGetNoteDetailsOptimized(
            keywords, 10, false, autoExport, null, _serviceProvider);

        // Assert
        _mockXiaoHongShuService.Verify(x => x.BatchGetNoteDetailsAsync(
            It.Is<List<string>>(k => k.SequenceEqual(keywords)),
            It.Is<int>(c => c == 10),
            It.Is<bool>(ic => ic == false),
            It.Is<bool>(ae => ae == autoExport),
            It.Is<string?>(fn => fn == null)
        ), Times.Once);
    }

    [TearDown]
    public void TearDown()
    {
        _mockAccountManager = null!;
        _mockXiaoHongShuService = null!;
        (_serviceProvider as IDisposable)?.Dispose();
        _serviceProvider = null!;
    }
}
