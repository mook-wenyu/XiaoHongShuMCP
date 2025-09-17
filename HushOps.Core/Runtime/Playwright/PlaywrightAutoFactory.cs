using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Runtime.Playwright;

/// <summary>Playwright 与 IAuto* 之间的桥接工厂（中文注释）。</summary>
public static class PlaywrightAutoFactory
{
    /// <summary>将 IPage 包装为 IAutoPage。</summary>
    public static IAutoPage Wrap(IPage page) => new PlaywrightPage(page);

    /// <summary>将 IElementHandle 包装为 IAutoElement（用于服务层桥接过渡）。</summary>
    public static IAutoElement Wrap(IElementHandle handle) => new PlaywrightElementFromHandle(handle);

    /// <summary>尝试从 IAutoPage 拆解出 IPage（若非 Playwright 实现则返回 null）。</summary>
    public static IPage? TryUnwrap(IAutoPage page) => page is PlaywrightPage p ? p.GetUnderlyingPage() : null;

    /// <summary>尝试从 IAutoElement 获取底层 IElementHandle（若非 Playwright 实现则返回 null）。</summary>
    public static async Task<IElementHandle?> TryUnwrapAsync(IAutoElement element)
    {
        if (element is PlaywrightElement pe)
        {
            try { return await pe.ToHandleAsync(); } catch { return null; }
        }
        if (element is PlaywrightElementFromHandle peh)
        {
            return await Task.FromResult<IElementHandle?>(peh.GetHandle());
        }
        return null;
    }
}

internal sealed class PlaywrightElementFromHandle : IAutoElement
{
    private readonly IElementHandle handle;
    public PlaywrightElementFromHandle(IElementHandle handle) => this.handle = handle;
    public IElementHandle GetHandle() => handle;

    public async Task ClickAsync(System.Threading.CancellationToken ct = default) => await handle.ClickAsync();
    public async Task TypeAsync(string text, System.Threading.CancellationToken ct = default)
    {
        await handle.FocusAsync();
        var frame = await handle.OwnerFrameAsync();
        var page = frame?.Page;
        if (page is not null)
        {
            await page.Keyboard.TypeAsync(text, new KeyboardTypeOptions { Delay = Random.Shared.Next(60, 140) });
        }
        else
        {
            foreach (var ch in text) { await handle.PressAsync(ch.ToString()); }
        }
    }
    public async Task<bool> IsVisibleAsync(System.Threading.CancellationToken ct = default) => await handle.IsVisibleAsync();
    public async Task HoverAsync(System.Threading.CancellationToken ct = default) => await handle.HoverAsync();
    public async Task ScrollIntoViewIfNeededAsync(System.Threading.CancellationToken ct = default) { try { await handle.ScrollIntoViewIfNeededAsync(); } catch { } }
    public async Task<T?> EvaluateAsync<T>(string script, System.Threading.CancellationToken ct = default) => await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<T>(handle, script, "element.evaluate", ct);
    public async Task<HushOps.Core.Automation.Abstractions.BoundingBox?> GetBoundingBoxAsync(System.Threading.CancellationToken ct = default)
    {
        var box = await handle.BoundingBoxAsync();
        if (box == null) return null;
        return new HushOps.Core.Automation.Abstractions.BoundingBox { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height };
    }
    public async Task<(double x, double y)?> GetCenterAsync(System.Threading.CancellationToken ct = default)
    {
        var box = await handle.BoundingBoxAsync();
        if (box == null || box.Width <= 0 || box.Height <= 0) return null;
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    public async Task<string?> GetAttributeAsync(string name, System.Threading.CancellationToken ct = default)
        => await handle.GetAttributeAsync(name);

    public async Task<string> InnerTextAsync(System.Threading.CancellationToken ct = default)
        => (await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(handle, "el => (el.innerText||'').trim()", "element.innerText", ct)) ?? string.Empty;

    public async Task<string> GetTagNameAsync(System.Threading.CancellationToken ct = default)
        => (await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(handle, "el => el.tagName.toLowerCase()", "element.tagName", ct)) ?? string.Empty;


    public async Task<HushOps.Core.Automation.Abstractions.IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, System.Threading.CancellationToken ct = default)
    {
        try
        {
            var child = await handle.QuerySelectorAsync(selector);
            if (child == null) return null;
            return new PlaywrightElementFromHandle(child);
        }
        catch { return null; }
    }

    public async Task<HushOps.Core.Automation.Abstractions.ElementVisibilityProbe> ProbeVisibilityAsync(System.Threading.CancellationToken ct = default)
    {
        var json = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(handle, @"(el) => {
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
              return JSON.stringify({ inViewport, visible, peEnabled, occluded });
            } catch(_) { return JSON.stringify({ inViewport:false, visible:false, peEnabled:false, occluded:false }); }
        }", "element.probeVisibility", ct) ?? "{}";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new HushOps.Core.Automation.Abstractions.ElementVisibilityProbe
            {
                InViewport = root.TryGetProperty("inViewport", out var p) && p.GetBoolean(),
                VisibleByStyle = root.TryGetProperty("visible", out p) && p.GetBoolean(),
                PointerEventsEnabled = root.TryGetProperty("peEnabled", out p) && p.GetBoolean(),
                CenterOccluded = root.TryGetProperty("occluded", out p) && p.GetBoolean(),
            };
        }
        catch { return new HushOps.Core.Automation.Abstractions.ElementVisibilityProbe(); }
    }

    /// <summary>
    /// 计算样式探针（集中门控+计量）。
    /// </summary>
    public async Task<HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe> GetComputedStyleProbeAsync(System.Threading.CancellationToken ct = default)
    {
        var json = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(handle, @"(el) => {
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

    /// <summary>
    /// 文本探针实现（集中门控+计量）。
    /// </summary>
    public async Task<HushOps.Core.Automation.Abstractions.ElementTextProbe> TextProbeAsync(System.Threading.CancellationToken ct = default)
    {
        var json = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(handle, @"(el) => {
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

    public async Task<HushOps.Core.Automation.Abstractions.ElementClickabilityProbe> GetClickabilityProbeAsync(System.Threading.CancellationToken ct = default)
    {
        var box = await handle.BoundingBoxAsync();
        var hasBox = box is { Width: > 0, Height: > 0 };
        double w = hasBox ? box!.Width : 0, h = hasBox ? box!.Height : 0;

        var vis = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<VisDto>(handle, @"(el) => {
            try {
              const r = el.getBoundingClientRect();
              const inViewport = r.bottom>0 && r.right>0 && r.left<window.innerWidth && r.top<window.innerHeight;
              let visible = true, peEnabled = true;
              try { const s = getComputedStyle(el); visible = !!(s && s.visibility !== 'hidden' && s.display !== 'none' && parseFloat(s.opacity||'1')>0); peEnabled = !s || (s.pointerEvents !== 'none'); } catch(_){}
              let occluded = false; try { const cx = Math.floor(r.left + r.width/2), cy = Math.floor(r.top + r.height/2); const topEl = document.elementFromPoint(cx, cy); occluded = !(topEl && (topEl===el || el.contains(topEl))); } catch(_){}
              return { inViewport, visible, peEnabled, occluded };
            } catch(_) { return { inViewport:false, visible:false, peEnabled:false, occluded:false }; }
        }", "element.clickability", ct) ?? new VisDto();

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

// 本文件内部使用的可见性 DTO（低基数字段，避免高基数标签），与 PlaywrightBrowserDriver 内部的 VisibilityDto 等价。
internal sealed class VisDto
{
    public bool inViewport { get; set; }
    public bool visible { get; set; }
    public bool peEnabled { get; set; }
    public bool occluded { get; set; }
}
