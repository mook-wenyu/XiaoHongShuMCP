using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using HushOps.Servers.XiaoHongShu.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Tools;

/// <summary>
/// 测试 PageNoteCaptureTool 参数验证和错误处理逻辑。
/// 注意：CaptureAsync 内部调用 PageNoteCaptureService，该服务需要真实浏览器环境，
/// 因此此测试仅覆盖工具层的参数处理逻辑，不执行实际采集。
/// </summary>
public sealed class PageNoteCaptureToolTests
{
    [Fact]
    public async Task CaptureAsync_Request为null_应抛出ArgumentNullException()
    {
        // Arrange
        var service = new FakePageNoteCaptureService(shouldSucceed: true);
        var tool = new PageNoteCaptureTool(service, NullLogger<PageNoteCaptureTool>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await tool.CaptureAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CaptureAsync_使用默认参数_应正常化BrowserKey和TargetCount()
    {
        // Arrange
        var service = new FakePageNoteCaptureService(shouldSucceed: true);
        var tool = new PageNoteCaptureTool(service, NullLogger<PageNoteCaptureTool>.Instance);
        var request = new PageNoteCaptureToolRequest(TargetCount: 0, BrowserKey: "");

        // Act
        var result = await tool.CaptureAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("user", service.CapturedContext?.BrowserKey);
        Assert.Equal(20, service.CapturedContext?.TargetCount); // 0 -> 20 default
    }

    [Fact]
    public async Task CaptureAsync_TargetCount超出范围_应Clamp到1到200()
    {
        // Arrange
        var service = new FakePageNoteCaptureService(shouldSucceed: true);
        var tool = new PageNoteCaptureTool(service, NullLogger<PageNoteCaptureTool>.Instance);
        var request = new PageNoteCaptureToolRequest(TargetCount: 500, BrowserKey: "test");

        // Act
        var result = await tool.CaptureAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, service.CapturedContext?.TargetCount); // 500 clamped to 200
    }

    [Fact]
    public async Task CaptureAsync_服务抛出InvalidOperationException_应返回ERR_INVALID_PAGE_TYPE()
    {
        // Arrange
        var service = new FakePageNoteCaptureService(shouldSucceed: false);
        var tool = new PageNoteCaptureTool(service, NullLogger<PageNoteCaptureTool>.Instance);
        var request = new PageNoteCaptureToolRequest(TargetCount: 20, BrowserKey: "user");

        // Act
        var result = await tool.CaptureAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("ERR_INVALID_PAGE_TYPE", result.Status);
        Assert.Contains("当前页面不是列表页", result.ErrorMessage);
    }

    /// <summary>
    /// Fake 实现，用于测试工具层逻辑，不依赖真实浏览器。
    /// </summary>
    private sealed class FakePageNoteCaptureService : IPageNoteCaptureService
    {
        private readonly bool _shouldSucceed;

        public PageNoteCaptureContext? CapturedContext { get; private set; }

        public FakePageNoteCaptureService(bool shouldSucceed)
        {
            _shouldSucceed = shouldSucceed;
        }

        public Task<PageNoteCaptureResult> CaptureAsync(PageNoteCaptureContext context, CancellationToken cancellationToken = default)
        {
            CapturedContext = context;

            if (!_shouldSucceed)
            {
                throw new InvalidOperationException("当前页面不是列表页");
            }

            return Task.FromResult(new PageNoteCaptureResult(
                "./logs/page-note-capture/fake.csv",
                20,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["browserKey"] = context.BrowserKey,
                    ["targetCount"] = context.TargetCount.ToString()
                }));
        }
    }
}
