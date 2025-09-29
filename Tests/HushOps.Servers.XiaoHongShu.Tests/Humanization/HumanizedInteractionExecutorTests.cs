using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class HumanizedInteractionExecutorTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private HumanizedInteractionExecutor _executor = null!;

    public async Task InitializeAsync()
    {
        await PlaywrightTestSupport.EnsureBrowsersInstalledAsync();

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var options = new HumanBehaviorOptions
        {
            DefaultProfile = "test",
            Profiles = new Dictionary<string, HumanBehaviorProfileOptions>
            {
                ["test"] = new HumanBehaviorProfileOptions
                {
                    PreActionDelay = new DelayRangeOptions(50, 80),
                    PostActionDelay = new DelayRangeOptions(40, 80),
                    TypingInterval = new DelayRangeOptions(30, 50),
                    ClickJitter = new PixelRangeOptions(0, 1),
                    MouseMoveSteps = new IntRangeOptions(6, 8),
                    WheelDelta = new DoubleRangeOptions(180, 220),
                    IdlePause = new DelayRangeOptions(10, 20),
                    RandomIdleProbability = 0,
                    RandomMoveProbability = 0,
                    ReverseScrollProbability = 0,
                    RandomIdleDuration = new DelayRangeOptions(10, 20)
                }
            }
        };

        var locatorBuilder = new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance);
        _executor = new HumanizedInteractionExecutor(locatorBuilder, Options.Create(options), NullLogger<HumanizedInteractionExecutor>.Instance, new Random(42));
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
    public async Task ExecuteAsync_Click_ShouldTriggerClickHandler()
    {
        await using var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("""
        <button id='target' data-clicks='0'>点我</button>
        <script>
        document.querySelector('#target').addEventListener('click', () => {
            const el = document.querySelector('#target');
            el.dataset.clicks = (Number(el.dataset.clicks) + 1).toString();
        });
        </script>
        """);

        var action = HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Role: AriaRole.Button, Text: "点我"),
            behaviorProfile: "test");

        await _executor.ExecuteAsync(page, action);

        var clicks = await page.EvaluateAsync<string>("() => document.querySelector('#target').dataset.clicks");
        Assert.Equal("1", clicks);
    }

    [Fact]
    public async Task ExecuteAsync_InputText_ShouldFillInput()
    {
        await using var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync("""
        <label>搜索
            <input id='search-box' placeholder='输入关键词'/>
        </label>
        """);

        var action = HumanizedAction.Create(
            HumanizedActionType.InputText,
            new ActionLocator(Placeholder: "输入关键词"),
            parameters: new HumanizedActionParameters(text: "小红书反检测"),
            behaviorProfile: "test");

        await _executor.ExecuteAsync(page, action);

        var value = await page.EvaluateAsync<string>("() => document.querySelector('#search-box').value");
        Assert.Equal("小红书反检测", value);
    }

    [Fact]
    public async Task ExecuteAsync_Script_ShouldScrollToElement()
    {
        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

        var page = await context.NewPageAsync();
        await page.SetContentAsync("""
        <style>
        body { margin:0; }
        .spacer { height: 2000px; background: linear-gradient(#fff, #eee); }
        </style>
        <div class='spacer'></div>
        <button id='bottom'>筛选</button>
        """);

        var actions = new HumanizedActionScript(new[]
        {
            HumanizedAction.Create(
                HumanizedActionType.ScrollTo,
                new ActionLocator(Role: AriaRole.Button, Text: "筛选"),
                behaviorProfile: "test"),
            HumanizedAction.Create(
                HumanizedActionType.Hover,
                new ActionLocator(Role: AriaRole.Button, Text: "筛选"),
                behaviorProfile: "test")
        });

        await _executor.ExecuteAsync(page, actions);

        var scrollY = await page.EvaluateAsync<double>("() => window.scrollY");
        Assert.True(scrollY > 0);
    }
}
