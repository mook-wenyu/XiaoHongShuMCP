using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Runtime.Playwright;

internal sealed class PlaywrightFilePicker : IFilePicker
{
    private readonly IPage page;
    public PlaywrightFilePicker(IPage page) => this.page = page;

    public async Task SetFilesAsync(string selector, IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        var loc = page.Locator(selector);
        await loc.SetInputFilesAsync(filePaths.Select(p => new FilePayload { Name = Path.GetFileName(p), MimeType = "application/octet-stream", Buffer = File.ReadAllBytes(p) }));
    }

    public async Task SetFilesAsync(IAutoElement element, IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        var handle = await PlaywrightAutoFactory.TryUnwrapAsync(element);
        if (handle is not null)
        {
            await handle.SetInputFilesAsync(filePaths.Select(p => new FilePayload { Name = Path.GetFileName(p), MimeType = "application/octet-stream", Buffer = File.ReadAllBytes(p) }));
            return;
        }

        var center = await element.GetCenterAsync(ct);
        if (center.HasValue) { await page.Mouse.ClickAsync((float)center.Value.x, (float)center.Value.y); }
    }
}
