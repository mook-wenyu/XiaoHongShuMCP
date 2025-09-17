using System;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

namespace Tests.Internal;

public class HtmlAuditSamplerServiceTests
{
    [Test]
    public async Task TrySampleAsync_Should_Return_Sanitized_When_Enabled_And_Whitelisted()
    {
        // 启用审计与白名单
        Environment.SetEnvironmentVariable("XHS__InteractionPolicy__EnableHtmlSampleAudit", "true");
        Environment.SetEnvironmentVariable("XHS__InteractionPolicy__EnableJsReadEval", "true");
        Environment.SetEnvironmentVariable("XHS__InteractionPolicy__EvalAllowedPaths", "element.html.sample");

        var handle = new Mock<IElementHandle>(MockBehavior.Strict);
        // PlaywrightAdapterTelemetry.EvalAsync 将调用 handle.EvaluateAsync<string>(script)
        handle.Setup(h => h.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(@"<div data=""x"">cookie</div>");
        IAutoElement el = PlaywrightAutoFactory.Wrap(handle.Object);

        var html = await XiaoHongShuMCP.Internal.HtmlAuditSamplerService.TrySampleAsync(el, 1);
        Assert.That(html, Is.Not.Null);
        Assert.That(html, Does.Contain("ck")); // 脱敏替换
    }
}
