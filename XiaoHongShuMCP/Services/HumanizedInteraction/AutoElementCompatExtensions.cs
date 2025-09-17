using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// IAutoElement 兼容扩展：为遗留代码提供 Focus/Press/Fill 能力。
/// - 实现原则：仅在可解包为 Playwright 句柄时调用底层 API；否则采取温和降级（尽量不抛异常）。
/// - 反检测：不进行任何 JS 注入，仅使用原生输入与键盘。
/// </summary>
public static class AutoElementCompatExtensions
{
    /// <summary>尝试聚焦元素；不可解包时静默忽略。</summary>
    public static async Task FocusAsync(this IAutoElement element)
    {
        var h = await PlaywrightAutoFactory.TryUnwrapAsync(element);
        if (h != null)
        {
            try { await h.FocusAsync(); } catch { }
        }
    }

    /// <summary>尝试向元素所在页面发送按键；不可解包时静默忽略。</summary>
    public static async Task PressAsync(this IAutoElement element, string key)
    {
        var h = await PlaywrightAutoFactory.TryUnwrapAsync(element);
        if (h != null)
        {
            try
            {
                var frame = await h.OwnerFrameAsync();
                var page = frame?.Page;
                if (page != null) await page.Keyboard.PressAsync(key);
            }
            catch { }
        }
    }

    /// <summary>
    /// 尝试填充文本（原子化替换）；不可解包时降级为直接输入文本（不清空）。
    /// </summary>
    public static async Task FillAsync(this IAutoElement element, string text)
    {
        var h = await PlaywrightAutoFactory.TryUnwrapAsync(element);
        if (h != null)
        {
            try { await h.FillAsync(text); return; } catch { }
        }
        // 降级：直接 Type 文本
        try { await element.TypeAsync(text); } catch { }
    }
}

