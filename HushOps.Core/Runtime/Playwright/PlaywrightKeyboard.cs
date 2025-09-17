using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;
using AutoKeyboard = HushOps.Core.Automation.Abstractions.IKeyboard;

namespace HushOps.Core.Runtime.Playwright;

internal sealed class PlaywrightKeyboard : AutoKeyboard
{
    private readonly IPage page;
    public PlaywrightKeyboard(IPage page) => this.page = page;

    public async Task TypeAsync(string text, int? delayMs = null, CancellationToken ct = default)
    {
        var opt = new KeyboardTypeOptions();
        if (delayMs.HasValue) opt.Delay = delayMs.Value;
        await page.Keyboard.TypeAsync(text, opt);
    }

    public async Task PressAsync(string key, int? delayMs = null, CancellationToken ct = default)
    {
        var opt = new KeyboardPressOptions();
        if (delayMs.HasValue) opt.Delay = delayMs.Value;
        await page.Keyboard.PressAsync(key, opt);
    }
}
