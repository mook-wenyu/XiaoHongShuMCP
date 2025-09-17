using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Browser;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using AutoKeyboard = HushOps.Core.Automation.Abstractions.IKeyboard;
using AutoClipboard = HushOps.Core.Automation.Abstractions.IClipboard;
using AutoFilePicker = HushOps.Core.Automation.Abstractions.IFilePicker;

namespace HushOps.Core.Runtime.Playwright;

/// <summary>
/// Playwright 驱动适配器：将 Playwright API 映射为抽象层接口（中文注释）。
/// 设计要点：
/// - 所有对 Microsoft.Playwright 的引用仅存在于适配器命名空间，平台/领域层不得直接依赖。
/// - 使用持久化配置（UserDataDir）与 Headful/Headless 切换，以便更接近真实用户环境。
/// - 人类化细节在输入适配器中实现（微等待/轻抖动等）。
/// </summary>
public sealed class PlaywrightBrowserDriver : IBrowserDriver, IAutomationRuntime, IBrowserRuntime, IAsyncDisposable
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private readonly IPlaywrightAntiDetectionPipeline? antiDetection;

    public IBrowserDriver DefaultDriver => this;

    public PlaywrightBrowserDriver(IPlaywrightAntiDetectionPipeline? antiDetection = null)
    {
        this.antiDetection = antiDetection;
    }

    /// <summary>创建浏览器会话（Browser+Context）。</summary>
    public async Task<IAutoSession> CreateSessionAsync(BrowserLaunchOptions options, CancellationToken ct = default)
    {
        playwright ??= await Microsoft.Playwright.Playwright.CreateAsync();

        // 启动浏览器（优先使用 Chromium，兼容现有工程）。
        if (browser == null)
        {
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = options.Headless,
                ExecutablePath = options.ExecutablePath,
                Args = options.RemoteDebuggingUrl is null
                    ? Array.Empty<string>()
                    : new[] { $"--remote-debugging-port={new Uri(options.RemoteDebuggingUrl).Port}" },
            };

            if (!string.IsNullOrWhiteSpace(options.ProxyServer))
            {
                launchOptions.Proxy = new Proxy { Server = options.ProxyServer };
            }

            browser = await playwright.Chromium.LaunchAsync(launchOptions);
        }

        var contextOptions = new BrowserNewContextOptions
        {
            Locale = options.Locale,
            TimezoneId = options.TimezoneId,
            ViewportSize = options.Viewport.HasValue
                ? new ViewportSize { Width = options.Viewport.Value.width, Height = options.Viewport.Value.height }
                : null,
        };

        if (!string.IsNullOrWhiteSpace(options.UserDataDir))
        {
            contextOptions.UserAgent = null; // 保留真实 UA（来自持久化配置），如需策略中心统一设置再行注入。
        }

        var context = await browser.NewContextAsync(contextOptions);
        if (antiDetection != null)
        {
            try { await antiDetection.ApplyAsync(context, ct); } catch { /* 忽略占位失败 */ }
        }
        return new PlaywrightSession(context);
    }

    public async ValueTask DisposeAsync()
    {
        if (browser != null) await browser.DisposeAsync();
        playwright?.Dispose();
    }
}

internal sealed class PlaywrightSession : IAutoSession
{
    private readonly IBrowserContext context;
    private readonly List<IAutoPage> pages = new();
    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    public PlaywrightSession(IBrowserContext context)
    {
        this.context = context;
    }

    public async Task<IAutoPage> NewPageAsync(CancellationToken ct = default)
    {
        var page = await context.NewPageAsync();
        var auto = new PlaywrightPage(page);
        pages.Add(auto);
        return auto;
    }

    public Task<IReadOnlyList<IAutoPage>> GetPagesAsync(CancellationToken ct = default)
    {
        return Task.FromResult((IReadOnlyList<IAutoPage>)pages.ToList());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var p in pages.OfType<IAsyncDisposable>())
        {
            await p.DisposeAsync();
        }
        await context.DisposeAsync();
    }
}

internal sealed class PlaywrightPage : IAutoPage, IAsyncDisposable
{
    private readonly IPage page;
    public string PageId { get; } = Guid.NewGuid().ToString("N");
    public INavigator Navigator { get; }
    public IInput Input { get; }
    public AutoKeyboard Keyboard { get; }
    public AutoClipboard Clipboard { get; }
    public AutoFilePicker FilePicker { get; }

    public PlaywrightPage(IPage page)
    {
        this.page = page;
        Navigator = new PlaywrightNavigator(page);
        Input = new PlaywrightInput(page);
        Keyboard = new PlaywrightKeyboard(page);
        Clipboard = new PlaywrightClipboard(page);
        FilePicker = new PlaywrightFilePicker(page);
    }

    public async Task<string> ContentAsync(CancellationToken ct = default)
        => await page.ContentAsync();

    public Task<string> GetUrlAsync(CancellationToken ct = default)
        => Task.FromResult(page.Url ?? string.Empty);

    public async Task<T> EvaluateAsync<T>(string script, CancellationToken ct = default)
        => (await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<T>(page, script, "page.eval.read", ct))!;

    public async Task<IAutoElement?> QueryAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default)
    {
        try
        {
            var loc = page.Locator(selector).First;
            await loc.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return new PlaywrightElement(loc);
        }
        catch (PlaywrightException ex)
        {
            throw new SelectorNotFoundError(selector, $"未找到元素：{selector}", 300, ex);
        }
    }

    public async Task<IReadOnlyList<IAutoElement>> QueryAllAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default)
    {
        var loc = page.Locator(selector);
        await loc.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeoutMs });
        var count = await loc.CountAsync();
        var list = new List<IAutoElement>(count);
        for (int i = 0; i < count; i++) list.Add(new PlaywrightElement(loc.Nth(i)));
        return list;
    }

    public async Task MouseClickAsync(double x, double y, CancellationToken ct = default)
    {
        await page.Mouse.ClickAsync((float)x, (float)y);
    }

    public async Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken ct = default)
    {
        await page.Mouse.WheelAsync((float)deltaX, (float)deltaY);
    }

    public async Task MouseMoveAsync(double x, double y, CancellationToken ct = default)
    {
        // 供人类化轨迹按步调用的单步 Mouse.Move 封装
        await page.Mouse.MoveAsync((float)x, (float)y);
    }

    public async ValueTask DisposeAsync() => await page.CloseAsync();

    internal IPage GetUnderlyingPage() => page;
}

internal sealed partial class PlaywrightElement : IAutoElement
{
    private readonly ILocator locator;
    public PlaywrightElement(ILocator locator) => this.locator = locator;

    public async Task ClickAsync(CancellationToken ct = default)
    {
        // 人类化微等待：在真实点击前加入短暂停顿，降低机械化特征。
        await Task.Delay(Random.Shared.Next(40, 120), ct);
        await locator.ClickAsync(new LocatorClickOptions { Delay = Random.Shared.Next(10, 35) });
    }

    public async Task TypeAsync(string text, CancellationToken ct = default)
    {
        // 使用 PressSequentiallyAsync 替代已过时的 TypeAsync，并引入微延迟模拟人类输入节律。
        await locator.FocusAsync();
        await locator.PressSequentiallyAsync(text, new LocatorPressSequentiallyOptions
        {
            Delay = Random.Shared.Next(60, 140)
        });
    }

    public async Task<bool> IsVisibleAsync(CancellationToken ct = default) => await locator.IsVisibleAsync();

    internal Task<IElementHandle> ToHandleAsync() => locator.ElementHandleAsync();

    public async Task HoverAsync(CancellationToken ct = default)
    {
        await locator.HoverAsync();
    }

    public async Task ScrollIntoViewIfNeededAsync(CancellationToken ct = default)
    {
        try { await locator.ScrollIntoViewIfNeededAsync(); } catch { }
    }

    public async Task<T?> EvaluateAsync<T>(string script, CancellationToken ct = default)
        => await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<T>(locator, script, "element.evaluate", ct);

    public async Task<HushOps.Core.Automation.Abstractions.BoundingBox?> GetBoundingBoxAsync(CancellationToken ct = default)
    {
        var box = await locator.BoundingBoxAsync();
        if (box == null) return null;
        return new HushOps.Core.Automation.Abstractions.BoundingBox
        {
            X = box.X,
            Y = box.Y,
            Width = box.Width,
            Height = box.Height
        };
    }

    public async Task<(double x, double y)?> GetCenterAsync(CancellationToken ct = default)
    {
        var box = await locator.BoundingBoxAsync();
        if (box == null || box.Width <= 0 || box.Height <= 0) return null;
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    public async Task<ElementVisibilityProbe> ProbeVisibilityAsync(CancellationToken ct = default)
    {
        // 在适配器层以最小脚本评估完成检查，避免业务层直接 Evaluate
        var data = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<VisibilityDto>(locator, @"(el) => {
            try {
              const r = el.getBoundingClientRect();
              const inViewport = r.bottom>0 && r.right>0 && r.left<window.innerWidth && r.top<window.innerHeight;
              let visible = true, peEnabled = true;
              try {
                const s = getComputedStyle(el);
                visible = !!(s && s.visibility !== 'hidden' && s.display !== 'none' && parseFloat(s.opacity||'1')>0);
                peEnabled = !s || (s.pointerEvents !== 'none');
              } catch(_){}
              let occluded = false;
              try {
                const cx = Math.floor(r.left + r.width/2);
                const cy = Math.floor(r.top + r.height/2);
                const topEl = document.elementFromPoint(cx, cy);
                occluded = !(topEl && (topEl===el || el.contains(topEl)));
              } catch(_){}
              return { inViewport, visible, peEnabled, occluded };
            } catch(_) { return { inViewport:false, visible:false, peEnabled:false, occluded:false }; }
        }", "element.probeVisibility", ct) ?? new VisibilityDto { inViewport = false, visible = false, peEnabled = false, occluded = false };
        return new ElementVisibilityProbe
        {
            InViewport = data.inViewport,
            VisibleByStyle = data.visible,
            PointerEventsEnabled = data.peEnabled,
            CenterOccluded = data.occluded
        };
    }

    private sealed class VisibilityDto
    {
        public bool inViewport { get; set; }
        public bool visible { get; set; }
        public bool peEnabled { get; set; }
        public bool occluded { get; set; }
    }

    public async Task<string?> GetAttributeAsync(string name, CancellationToken ct = default)
        => await locator.GetAttributeAsync(name);

    public async Task<string> InnerTextAsync(CancellationToken ct = default)
        => (await locator.InnerTextAsync())?.Trim() ?? string.Empty;

    public async Task<string> GetTagNameAsync(CancellationToken ct = default)
        => (await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(locator, "el => el.tagName.toLowerCase()", "element.tagName", ct)) ?? string.Empty;


    public async Task<IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, CancellationToken ct = default)
    {
        try
        {
            var child = locator.Locator(selector).First;
            await child.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeoutMs });
            return new PlaywrightElement(child);
        }
        catch
        {
            return null;
        }
    }
}

// 扩展：强类型“计算样式/文本”探针（集中封装脚本，门控+计量）。
internal sealed partial class PlaywrightElement : IAutoElement
{
    public async Task<HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe> GetComputedStyleProbeAsync(CancellationToken ct = default)
    {
        var json = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(locator, @"(el) => {
            try {
              const s = getComputedStyle(el);
              const obj = {
                display: s.display || '',
                visibility: s.visibility || '',
                pointerEvents: s.pointerEvents || '',
                opacity: s.opacity || '1',
                position: s.position || '',
                overflowX: s.overflowX || '',
                overflowY: s.overflowY || ''
              };
              return JSON.stringify(obj);
            } catch(_) { return JSON.stringify({display:'',visibility:'',pointerEvents:'',opacity:'1',position:'',overflowX:'',overflowY:''}); }
        }", "element.computedStyle", ct) ?? "{}";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe
            {
                Display = root.TryGetProperty("display", out var p) ? p.GetString() : null,
                Visibility = root.TryGetProperty("visibility", out p) ? p.GetString() : null,
                PointerEvents = root.TryGetProperty("pointerEvents", out p) ? p.GetString() : null,
                Opacity = root.TryGetProperty("opacity", out p) && double.TryParse(p.GetString(), out var d) ? d : 1.0,
                Position = root.TryGetProperty("position", out p) ? p.GetString() : null,
                OverflowX = root.TryGetProperty("overflowX", out p) ? p.GetString() : null,
                OverflowY = root.TryGetProperty("overflowY", out p) ? p.GetString() : null
            };
        }
        catch { return new HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe { Opacity = 1.0 }; }
    }

    public async Task<HushOps.Core.Automation.Abstractions.ElementTextProbe> TextProbeAsync(CancellationToken ct = default)
    {
        var json = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(locator, @"(el) => {
            try {
              const it = (el.innerText||'').trim();
              const tc = (el.textContent||'').replace(/\s+/g,' ').trim();
              return JSON.stringify({ innerText: it, textContent: tc, il: it.length, tl: tc.length });
            } catch(_) { return JSON.stringify({ innerText:'', textContent:'', il:0, tl:0 }); }
        }", "element.textProbe", ct) ?? "{}";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new HushOps.Core.Automation.Abstractions.ElementTextProbe
            {
                InnerText = root.TryGetProperty("innerText", out var p) ? (p.GetString() ?? string.Empty) : string.Empty,
                TextContent = root.TryGetProperty("textContent", out p) ? (p.GetString() ?? string.Empty) : string.Empty,
                InnerTextLength = root.TryGetProperty("il", out p) ? p.GetInt32() : 0,
                TextContentLength = root.TryGetProperty("tl", out p) ? p.GetInt32() : 0
            };
        }
        catch { return new HushOps.Core.Automation.Abstractions.ElementTextProbe(); }
    }

    public async Task<HushOps.Core.Automation.Abstractions.ElementClickabilityProbe> GetClickabilityProbeAsync(CancellationToken ct = default)
    {
        var box = await locator.BoundingBoxAsync();
        var hasBox = box is { Width: > 0, Height: > 0 };
        double w = hasBox ? box!.Width : 0, h = hasBox ? box!.Height : 0;

        var vis = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<VisibilityDto>(locator, @"(el) => {
            try {
              const r = el.getBoundingClientRect();
              const inViewport = r.bottom>0 && r.right>0 && r.left<window.innerWidth && r.top<window.innerHeight;
              let visible = true, peEnabled = true;
              try {
                const s = getComputedStyle(el);
                visible = !!(s && s.visibility !== 'hidden' && s.display !== 'none' && parseFloat(s.opacity||'1')>0);
                peEnabled = !s || (s.pointerEvents !== 'none');
              } catch(_){ }
              let occluded = false;
              try {
                const cx = Math.floor(r.left + r.width/2);
                const cy = Math.floor(r.top + r.height/2);
                const topEl = document.elementFromPoint(cx, cy);
                occluded = !(topEl && (topEl===el || el.contains(topEl)));
              } catch(_){ }
              return { inViewport, visible, peEnabled, occluded };
            } catch(_) { return { inViewport:false, visible:false, peEnabled:false, occluded:false }; }
        }", "element.clickability", ct) ?? new VisibilityDto();

        var clickable = hasBox && vis.inViewport && vis.visible && vis.peEnabled && !vis.occluded;
        return new HushOps.Core.Automation.Abstractions.ElementClickabilityProbe
        {
            HasBox = hasBox,
            Width = w,
            Height = h,
            InViewport = vis.inViewport,
            VisibleByStyle = vis.visible,
            PointerEventsEnabled = vis.peEnabled,
            CenterOccluded = vis.occluded,
            Clickable = clickable
        };
    }
}

internal sealed class PlaywrightNavigator : INavigator
{
    private readonly IPage page;
    public PlaywrightNavigator(IPage page) => this.page = page;

    public async Task GoToAsync(string url, HushOps.Core.Automation.Abstractions.PageGotoOptions? options = null, CancellationToken ct = default)
    {
        var wait = options?.WaitUntil switch
        {
            HushOps.Core.Automation.Abstractions.WaitUntilState.Load => Microsoft.Playwright.WaitUntilState.Load,
            HushOps.Core.Automation.Abstractions.WaitUntilState.NetworkIdle => Microsoft.Playwright.WaitUntilState.NetworkIdle,
            _ => Microsoft.Playwright.WaitUntilState.DOMContentLoaded
        };

        try
        {
            await page.GotoAsync(url, new Microsoft.Playwright.PageGotoOptions
            {
                WaitUntil = wait,
                Timeout = options?.TimeoutMs
            });
        }
        catch (PlaywrightException ex)
        {
            throw new NavigationError($"导航失败：{url}", 800, ex);
        }
    }
}

internal sealed class PlaywrightInput : IInput
{
    private readonly IPage page;
    public PlaywrightInput(IPage page) => this.page = page;

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        var loc = page.Locator(selector).First;
        // 轻微 hover 与可见性等待，降低误触与遮挡概率。
        await loc.HoverAsync();
        await loc.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 3000 });
        await Task.Delay(Random.Shared.Next(30, 90), ct);
        await loc.ClickAsync(new LocatorClickOptions { Delay = Random.Shared.Next(15, 40) });
    }

    public async Task TypeAsync(string selector, string text, CancellationToken ct = default)
    {
        var loc = page.Locator(selector).First;
        await loc.FocusAsync();
        foreach (var ch in text)
        {
            await page.Keyboard.TypeAsync(ch.ToString(), new KeyboardTypeOptions { Delay = Random.Shared.Next(60, 140) });
        }
    }
}
