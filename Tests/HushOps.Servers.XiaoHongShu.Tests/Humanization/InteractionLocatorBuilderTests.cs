using System;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class InteractionLocatorBuilderTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async Task InitializeAsync()
    {
        await PlaywrightTestSupport.EnsureBrowsersInstalledAsync();
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    [Fact]
    public async Task ResolveAsync_ShouldLocateByRoleAndText()
    {
        var builder = new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance);

        await using var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<button>综合</button>");

        var locator = await builder.ResolveAsync(page, new ActionLocator(Role: AriaRole.Button, Text: "综合"));

        Assert.Equal("综合", (await locator.InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task ResolveAsync_ShouldSupportFuzzyTextMatching()
    {
        var builder = new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance);

        const string html = """
        <div role='group'>
            <span class='tag'>最多收藏</span>
            <span class='tag'>最多评论</span>
        </div>
        """;

        await using var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(html);

        var locator = await builder.ResolveAsync(page, new ActionLocator(Text: "评论"));

        Assert.Equal("最多评论", (await locator.InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task ResolveAsync_ShouldScrollAndRetryWhenElementAppearsLater()
    {
        var builder = new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance, new FixedRandom(0.2));

        const string html = """
        <html>
        <body style='height:4000px;'>
            <div style='height:1800px;'></div>
            <script>
                let appended = false;
                window.addEventListener('scroll', () => {
                    if (appended) { return; }
                    appended = true;
                    const button = document.createElement('button');
                    button.textContent = '筛选';
                    button.setAttribute('data-testid', 'lazy-loaded');
                    document.body.appendChild(button);
                });
            </script>
        </body>
        </html>
        """;

        await using var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(html);

        var locator = await builder.ResolveAsync(page, new ActionLocator(Text: "筛选"));

        Assert.Equal("lazy-loaded", await locator.GetAttributeAsync("data-testid"));
    }

    [Fact]
    public async Task ResolveAsync_ShouldRespectRandomSelectionWhenMultipleMatches()
    {
        const string html = """
        <div>
            <button data-id='first'>收藏</button>
            <button data-id='second'>收藏</button>
        </div>
        """;

        await using var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(html);

        var firstBuilder = new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance, new FixedRandom(0.01));
        var first = await firstBuilder.ResolveAsync(page, new ActionLocator(Text: "收藏"));
        Assert.Equal("first", await first.GetAttributeAsync("data-id"));

        var secondBuilder = new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance, new FixedRandom(0.95));
        var second = await secondBuilder.ResolveAsync(page, new ActionLocator(Text: "收藏"));
        Assert.Equal("second", await second.GetAttributeAsync("data-id"));
    }

    private sealed class FixedRandom : Random
    {
        private readonly double _value;

        public FixedRandom(double value)
        {
            _value = Math.Clamp(value, 0d, 0.999999d);
        }

        protected override double Sample() => _value;

        public override double NextDouble() => _value;
    }
}
