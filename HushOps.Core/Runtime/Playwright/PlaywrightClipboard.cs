using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Runtime.Playwright;

internal sealed class PlaywrightClipboard : IClipboard
{
    private readonly IPage page;
    public PlaywrightClipboard(IPage page) => this.page = page;

    public async Task WriteTextAsync(string text, CancellationToken ct = default)
    {
        try { await page.EvaluateAsync("async (txt) => await navigator.clipboard.writeText(txt)", text); }
        catch (Exception ex) { throw new Exception($"写剪贴板失败：{ex.Message}", ex); }
    }

    public async Task<string> ReadTextAsync(CancellationToken ct = default)
    {
        try { return await page.EvaluateAsync<string>("async () => await navigator.clipboard.readText()"); }
        catch (Exception ex) { throw new Exception($"读剪贴板失败：{ex.Message}", ex); }
    }
}
