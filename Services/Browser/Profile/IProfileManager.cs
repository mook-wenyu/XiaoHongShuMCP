using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Profile;

public interface IProfileManager
{
    Task<ProfileRecord> EnsureInitializedAsync(string profileKey, string? regionHint, CancellationToken cancellationToken);
    Task AssignProxyIfEmptyAsync(ProfileRecord record, string proxyEndpoint, CancellationToken cancellationToken);
}