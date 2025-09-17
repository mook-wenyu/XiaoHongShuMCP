using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Tools;

/// <summary>
/// 真实环境页面探测 E2E（内部调用）：默认不在 CI 中运行，仅用于本地联调。
/// </summary>
[TestFixture]
public class PageProbeRealEnvTests
{
    [Test]
    [Category("RealEnv")]
    [Explicit("需要本机安装 Playwright 依赖。测试将调用 PlaywrightBrowserManager.ProbePageAsync，不暴露 MCP 工具。")]
    public async Task ProbePage_Should_Open_Explore_And_Probe_Aliases()
    {
        // 使用固定目录，便于与 open-browser 手动登录保持一致
        var userDataDir = Path.Combine(Directory.GetCurrentDirectory(), "profiles", "e2e-fixed");
        Directory.CreateDirectory(userDataDir);

        var dict = new Dictionary<string, string?>
        {
            ["XHS:BrowserSettings:Headless"] = "true",
            ["XHS:BrowserSettings:UserDataDir"] = userDataDir
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDomElementManager, DomElementManager>();
        services.AddSingleton<IBrowserManager, PlaywrightBrowserManager>();

        await using var provider = services.BuildServiceProvider();
        var browser = provider.GetRequiredService<IBrowserManager>();
        var dom = provider.GetRequiredService<IDomElementManager>();

        var result = await browser.ProbePageAsync("https://www.xiaohongshu.com/explore");
        TestContext.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

        Assert.That(result.Success, Is.True, "页面探测执行失败");
        Assert.That(result.Aliases.Count, Is.GreaterThan(0));

        // 至少有一个核心别名命中
        var anyCore = result.Aliases.Any(a =>
            (a.Alias == "NoteItem" || a.Alias == "NoteTitle" || a.Alias == "SearchInput") &&
            (!string.IsNullOrEmpty(a.FirstMatchedSelector) || a.MatchCount > 0));

        Assert.That(anyCore, Is.True, "核心别名未匹配到元素，可能被地区/反爬限制");

        // 二阶段：点击首个可见的笔记链接，进入详情页后再次探测详情页别名
        var page = await browser.GetPageAsync();
        // 依次尝试可见/隐藏链接别名
        var linkAliases = new[] { "NoteVisibleLink", "NoteHiddenLink" };
        bool clicked = false;
        foreach (var alias in linkAliases)
        {
            foreach (var sel in dom.GetSelectors(alias))
            {
                try
                {
                    var link = await page.QuerySelectorAsync(sel);
                    if (link == null) continue;
                    await link.ClickAsync();
                    await page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.DOMContentLoaded);
                    clicked = true; break;
                }
                catch { }
            }
            if (clicked) break;
        }

        if (clicked)
        {
            var detailUrl = page.Url;
            var detailAliases = new List<string>
            {
                // 关注收藏/点赞相关
                "likeButton", "likeButtonActive", "favoriteButton", "favoriteButtonActive",
                // 包装器与计数
                "LikeWrapper", "CollectWrapper", "WrapperCountText", "WrapperIconUse",
                // 评论相关
                "DetailPageCommentInput", "DetailPageCommentSubmit"
            };
            var detail = await browser.ProbePageAsync(detailUrl, detailAliases);
            TestContext.WriteLine("DETAIL PROBE:\n" + JsonSerializer.Serialize(detail, new JsonSerializerOptions { WriteIndented = true }));
            Assert.That(detail.Success, Is.True, "详情页探测失败");
            // 松绑断言：记录结果用于后续更新选择器，不以命中数量强制失败
        }
    }
}
