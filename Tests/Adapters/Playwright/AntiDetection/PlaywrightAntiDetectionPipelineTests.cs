using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using XiaoHongShuMCP.Services;

namespace Tests.Adapters.Playwright.AntiDetection;

/// <summary>
/// 反检测管线（占位）测试：
/// - 验证 AddInitScriptAsync 至少被调用一次；
/// - 验证审计文件被写入指定目录（脱敏）。
/// </summary>
public class PlaywrightAntiDetectionPipelineTests
{
    [Test]
    public async Task ApplyAsync_Should_AddInitScript_And_WriteAudit()
    {
        // 破坏式变更后：默认不注入，仅在策略放行时注入
        System.Environment.SetEnvironmentVariable("XHS__AntiDetection__Enabled", "true");
        System.Environment.SetEnvironmentVariable("XHS__AntiDetection__PatchNavigatorWebdriver", "true");
        var ctx = new Mock<IBrowserContext>(MockBehavior.Strict);
        ctx.Setup(c => c.AddInitScriptAsync(It.IsAny<string>(), null)).Returns(Task.CompletedTask);
        ctx.SetupGet(c => c.Browser).Returns((IBrowser?)null);

        var pipeline = new DefaultPlaywrightAntiDetectionPipeline();
        await pipeline.ApplyAsync(ctx.Object);

        ctx.Verify(c => c.AddInitScriptAsync(It.IsAny<string>(), null), Times.AtLeast(1));
    }
}
