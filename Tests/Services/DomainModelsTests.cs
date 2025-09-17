using System;
using System.Collections.Generic;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

/// <summary>
/// 核心领域模型的基础行为校验，确保去除导出后仍维持统计与采集属性。
/// </summary>
[TestFixture]
public class DomainModelsTests
{
    [Test]
    public void BatchProcessingStatistics_ShouldExposeTotalSamples()
    {
        var stats = new BatchProcessingStatistics(
            CompleteDataCount: 5,
            PartialDataCount: 3,
            MinimalDataCount: 1,
            AverageProcessingTime: 120,
            AverageLikes: 10,
            AverageComments: 2,
            TypeDistribution: new Dictionary<NoteType, int> { [NoteType.Video] = 6 },
            ProcessingModeStats: new Dictionary<ProcessingMode, int> { [ProcessingMode.Fast] = 6 },
            CalculatedAt: DateTime.UtcNow);

        Assert.That(stats.TotalSamples, Is.EqualTo(9));
    }

    [Test]
    public void BatchNoteResult_HasFailures_ShouldReflectTupleList()
    {
        var stats = new BatchProcessingStatistics(
            CompleteDataCount: 1,
            PartialDataCount: 1,
            MinimalDataCount: 0,
            AverageProcessingTime: 80,
            AverageLikes: 5,
            AverageComments: 1,
            TypeDistribution: new Dictionary<NoteType, int>(),
            ProcessingModeStats: new Dictionary<ProcessingMode, int>(),
            CalculatedAt: DateTime.UtcNow);

        var result = new BatchNoteResult(
            SuccessfulNotes: new List<NoteDetail> { new NoteDetail() },
            FailedNotes: new List<(string, string)> { ("kw", "超时") },
            ProcessedCount: 1,
            ProcessingTime: TimeSpan.FromSeconds(3),
            OverallQuality: DataQuality.Partial,
            Statistics: stats);

        Assert.That(result.HasFailures, Is.True);
    }

    [Test]
    public void CollectionPerformanceMetrics_ShouldComputeDerivedIndicators()
    {
        var metrics = new CollectionPerformanceMetrics(
            SuccessfulRequests: 5,
            FailedRequests: 1,
            ScrollCount: 2,
            Duration: TimeSpan.FromSeconds(12));

        Assert.Multiple(() =>
        {
            Assert.That(metrics.RequestCount, Is.EqualTo(6));
            Assert.That(metrics.SuccessRate, Is.EqualTo(5d / 6d).Within(1e-6));
        });
    }

    [Test]
    public void SmartCollectionResult_CreateSuccess_ShouldAdoptMetricsDuration()
    {
        var notes = new List<NoteInfo> { new NoteDetail() };
        var metrics = new CollectionPerformanceMetrics(
            SuccessfulRequests: 2,
            FailedRequests: 0,
            ScrollCount: 0,
            Duration: TimeSpan.FromSeconds(5));

        var result = SmartCollectionResult.CreateSuccess(notes, 10, metrics);

        Assert.Multiple(() =>
        {
            Assert.That(result.CollectedCount, Is.EqualTo(1));
            Assert.That(result.Duration, Is.EqualTo(metrics.Duration));
            Assert.That(result.RequestCount, Is.EqualTo(metrics.RequestCount));
        });
    }

    [Test]
    public void SearchStatistics_TotalCount_ShouldSumDifferentQualityBuckets()
    {
        var stats = new SearchStatistics(
            CompleteDataCount: 3,
            PartialDataCount: 2,
            MinimalDataCount: 1,
            AverageLikes: 1.2,
            AverageComments: 0.6,
            CalculatedAt: DateTime.UtcNow,
            VideoNotesCount: 2,
            ImageNotesCount: 4,
            AverageCollects: 0.3,
            AuthorDistribution: new Dictionary<string, int> { { "作者", 2 } },
            TypeDistribution: new Dictionary<NoteType, int> { { NoteType.Image, 3 } },
            DataQualityDistribution: new Dictionary<DataQuality, int> { { DataQuality.Complete, 3 } });

        Assert.That(stats.TotalCount, Is.EqualTo(6));
    }

    [Test]
    public void SearchResult_ShouldCaptureGeneratedTimestamp()
    {
        var stats = new SearchStatistics(
            CompleteDataCount: 1,
            PartialDataCount: 0,
            MinimalDataCount: 0,
            AverageLikes: 0,
            AverageComments: 0,
            CalculatedAt: DateTime.UtcNow,
            VideoNotesCount: 0,
            ImageNotesCount: 1,
            AverageCollects: 0,
            AuthorDistribution: new Dictionary<string, int>(),
            TypeDistribution: new Dictionary<NoteType, int>(),
            DataQualityDistribution: new Dictionary<DataQuality, int>());

        var generatedAt = DateTime.UtcNow;
        var result = new SearchResult(
            Notes: new List<NoteInfo>(),
            TotalCount: 0,
            SearchKeyword: "kw",
            Duration: TimeSpan.FromMilliseconds(500),
            Statistics: stats,
            ApiRequests: 1,
            InterceptedResponses: 1,
            SearchParameters: new SearchParametersInfo(
                Keyword: "kw",
                SortBy: "comprehensive",
                NoteType: "all",
                PublishTime: "all",
                MaxResults: 20,
                RequestedAt: generatedAt),
            GeneratedAt: generatedAt);

        Assert.That(result.GeneratedAt, Is.EqualTo(generatedAt));
    }
}
