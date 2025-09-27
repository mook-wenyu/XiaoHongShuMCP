using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

public interface IBrowserAutomationService
{
    Task<BrowserOpenResult> EnsureProfileAsync(string profileKey, string? profilePath, CancellationToken cancellationToken);
    Task<BrowserOpenResult> OpenAsync(BrowserOpenRequest request, CancellationToken cancellationToken);
    bool TryGetOpenProfile(string profileKey, out BrowserOpenResult? result);
    IReadOnlyDictionary<string, BrowserOpenResult> OpenProfiles { get; }
    Task NavigateRandomAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken);
    Task NavigateKeywordAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken);
}
