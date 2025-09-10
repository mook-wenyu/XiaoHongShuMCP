using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

[TestFixture]
public class UniversalApiMonitorDeduplicationTests
{
    private UniversalApiMonitor _monitor;
    private ILogger<UniversalApiMonitor> _logger;

    [SetUp]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<UniversalApiMonitor>();
        
        var mcpOptions = Options.Create(new McpSettings { WaitTimeoutMs = 600000 });
        _monitor = new UniversalApiMonitor(_logger, mcpOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _monitor?.Dispose();
    }

    [Test]
    public void ApplyDeduplication_ShouldRemoveDuplicates_WhenSameIdExists()
    {
        // Arrange
        var noteDetail1 = new NoteDetail { Id = "test-note-1", Title = "Test Note 1" };
        var noteDetail2 = new NoteDetail { Id = "test-note-2", Title = "Test Note 2" };
        var noteDetail3 = new NoteDetail { Id = "test-note-1", Title = "Duplicate Note 1" }; // 重复ID

        var response1 = new MonitoredApiResponse
        {
            ProcessedNoteDetails = [noteDetail1, noteDetail2],
            EndpointType = ApiEndpointType.Homefeed
        };

        var response2 = new MonitoredApiResponse
        {
            ProcessedNoteDetails = [noteDetail3], // 重复笔记
            EndpointType = ApiEndpointType.SearchNotes
        };

        // Act - 模拟两次API响应处理
        var applyDeduplicationMethod = typeof(UniversalApiMonitor)
            .GetMethod("ApplyDeduplication", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        applyDeduplicationMethod!.Invoke(_monitor, [response1, ApiEndpointType.Homefeed]);
        applyDeduplicationMethod!.Invoke(_monitor, [response2, ApiEndpointType.SearchNotes]);

        // Assert
        var stats = _monitor.GetDeduplicationStats();
        Assert.That(stats.TotalProcessed, Is.EqualTo(3), "总处理数量应该是3");
        Assert.That(stats.DuplicatesFound, Is.EqualTo(1), "应该发现1个重复");
        Assert.That(stats.UniqueNotesCount, Is.EqualTo(2), "唯一笔记数量应该是2");

        var allUniqueNotes = _monitor.GetAllUniqueNoteDetails();
        Assert.That(allUniqueNotes.Count, Is.EqualTo(2), "应该有2个唯一笔记");
        Assert.That(allUniqueNotes.Select(n => n.Id), Contains.Item("test-note-1"));
        Assert.That(allUniqueNotes.Select(n => n.Id), Contains.Item("test-note-2"));

        // 验证保留的是第一个版本（title应该是"Test Note 1"而不是"Duplicate Note 1"）
        var firstNote = allUniqueNotes.First(n => n.Id == "test-note-1");
        Assert.That(firstNote.Title, Is.EqualTo("Test Note 1"), "应该保留第一个版本的标题");
    }

    [Test]
    public void GetMonitoredNoteDetails_ShouldReturnDeduplicatedNotes_ForSpecificEndpoint()
    {
        // Arrange
        var noteDetail1 = new NoteDetail { Id = "homefeed-1", Title = "Homefeed Note 1" };
        var noteDetail2 = new NoteDetail { Id = "search-1", Title = "Search Note 1" };

        var homefeedResponse = new MonitoredApiResponse
        {
            ProcessedNoteDetails = [noteDetail1],
            EndpointType = ApiEndpointType.Homefeed
        };

        var searchResponse = new MonitoredApiResponse
        {
            ProcessedNoteDetails = [noteDetail2],
            EndpointType = ApiEndpointType.SearchNotes
        };

        // Act
        var applyDeduplicationMethod = typeof(UniversalApiMonitor)
            .GetMethod("ApplyDeduplication", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        applyDeduplicationMethod!.Invoke(_monitor, [homefeedResponse, ApiEndpointType.Homefeed]);
        applyDeduplicationMethod!.Invoke(_monitor, [searchResponse, ApiEndpointType.SearchNotes]);

        // Assert
        var homefeedNotes = _monitor.GetMonitoredNoteDetails(ApiEndpointType.Homefeed);
        var searchNotes = _monitor.GetMonitoredNoteDetails(ApiEndpointType.SearchNotes);

        Assert.That(homefeedNotes.Count, Is.EqualTo(1), "推荐端点应该有1个笔记");
        Assert.That(searchNotes.Count, Is.EqualTo(1), "搜索端点应该有1个笔记");
        Assert.That(homefeedNotes.First().Id, Is.EqualTo("homefeed-1"));
        Assert.That(searchNotes.First().Id, Is.EqualTo("search-1"));
    }

    [Test]
    public void ClearMonitoredData_ShouldClearDeduplicationData_WhenClearingAllData()
    {
        // Arrange
        var noteDetail = new NoteDetail { Id = "test-note", Title = "Test Note" };
        var response = new MonitoredApiResponse
        {
            ProcessedNoteDetails = [noteDetail],
            EndpointType = ApiEndpointType.Homefeed
        };

        var applyDeduplicationMethod = typeof(UniversalApiMonitor)
            .GetMethod("ApplyDeduplication", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        applyDeduplicationMethod!.Invoke(_monitor, [response, ApiEndpointType.Homefeed]);

        // Verify data exists
        var statsBefore = _monitor.GetDeduplicationStats();
        Assert.That(statsBefore.UniqueNotesCount, Is.GreaterThan(0));

        // Act
        _monitor.ClearMonitoredData();

        // Assert
        var statsAfter = _monitor.GetDeduplicationStats();
        Assert.That(statsAfter.TotalProcessed, Is.EqualTo(0), "总处理数量应该重置为0");
        Assert.That(statsAfter.DuplicatesFound, Is.EqualTo(0), "重复数量应该重置为0");
        Assert.That(statsAfter.UniqueNotesCount, Is.EqualTo(0), "唯一笔记数量应该重置为0");

        var allUniqueNotes = _monitor.GetAllUniqueNoteDetails();
        Assert.That(allUniqueNotes.Count, Is.EqualTo(0), "所有唯一笔记应该被清理");
    }

    [Test]
    public void DeduplicationStats_ShouldCalculateCorrectDeduplicationRate()
    {
        // Arrange
        var noteDetail1 = new NoteDetail { Id = "note-1", Title = "Note 1" };
        var noteDetail2 = new NoteDetail { Id = "note-2", Title = "Note 2" };
        var noteDetail3 = new NoteDetail { Id = "note-1", Title = "Duplicate Note 1" }; // 重复
        var noteDetail4 = new NoteDetail { Id = "note-3", Title = "Note 3" };
        var noteDetail5 = new NoteDetail { Id = "note-2", Title = "Duplicate Note 2" }; // 重复

        var response = new MonitoredApiResponse
        {
            ProcessedNoteDetails = [noteDetail1, noteDetail2, noteDetail3, noteDetail4, noteDetail5],
            EndpointType = ApiEndpointType.Homefeed
        };

        // Act
        var applyDeduplicationMethod = typeof(UniversalApiMonitor)
            .GetMethod("ApplyDeduplication", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        applyDeduplicationMethod!.Invoke(_monitor, [response, ApiEndpointType.Homefeed]);

        // Assert
        var stats = _monitor.GetDeduplicationStats();
        Assert.That(stats.TotalProcessed, Is.EqualTo(5), "总处理数量应该是5");
        Assert.That(stats.DuplicatesFound, Is.EqualTo(2), "应该发现2个重复");
        Assert.That(stats.UniqueNotesCount, Is.EqualTo(3), "唯一笔记数量应该是3");
        Assert.That(stats.DeduplicationRate, Is.EqualTo(0.4).Within(0.01), "去重率应该是40%");
    }
}