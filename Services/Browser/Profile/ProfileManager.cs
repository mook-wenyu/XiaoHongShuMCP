using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Profile;

public sealed class ProfileManager : IProfileManager
{
    private readonly ILogger<ProfileManager> _logger;
    private readonly ProfileRegistry _registry;
    private readonly Lazy<Task<IPlaywright>> _playwright;

    public ProfileManager(ILogger<ProfileManager> logger)
    {
        _logger = logger;
        var repoRoot = AppContext.BaseDirectory; // 相对运行目录，存储到 ./storage/profiles
        _registry = new ProfileRegistry(repoRoot);
        _playwright = new Lazy<Task<IPlaywright>>(Microsoft.Playwright.Playwright.CreateAsync);
    }

    public async Task<ProfileRecord> EnsureInitializedAsync(string profileKey, string? regionHint, CancellationToken cancellationToken)
    {
        var existing = _registry.TryGet(profileKey);
        if (existing is not null)
        {
            existing.LastUsedAt = DateTimeOffset.UtcNow;
            _registry.AddOrUpdate(existing);
            return existing;
        }

        var region = RegionMappings.Resolve(regionHint);

        // 采集一次来自 msedge 的 UA/UA-CH 快照
        var (ua, uaChJson, uaChPlatform, uaChMobile) = await DeriveUserAgentAsync(region.Locale, region.TimezoneId, cancellationToken).ConfigureAwait(false);

        // 采样分布：视口与 DPR、硬件
        var (vw, vh) = PickViewport();
        var dpr = PickDpr();
        var (hw, platform, vendor, glVendor, glRenderer) = PickHardware();

        var seedCanvas = NextSeed();
        var seedWebgl = NextSeed();

        var record = new ProfileRecord
        {
            ProfileKey = profileKey,
            Region = regionHint ?? "CN-Shanghai",
            BrowserChannel = "msedge",
            UserDataDir = _registry.GetUserDataDir(profileKey),
            Locale = region.Locale,
            TimezoneId = region.TimezoneId,
            AcceptLanguage = region.AcceptLanguage,
            UserAgent = ua,
            UaChBrandsJson = uaChJson,
            UaChPlatform = uaChPlatform,
            UaChMobile = uaChMobile,
            ViewportWidth = vw,
            ViewportHeight = vh,
            DeviceScaleFactor = dpr,
            HardwareConcurrency = hw,
            Platform = platform,
            Vendor = vendor,
            WebglVendor = glVendor,
            WebglRenderer = glRenderer,
            CanvasSeed = seedCanvas,
            WebglSeed = seedWebgl,
            WebRtc = WebRtcMode.DefaultRouteOnly,
            CreatedAt = DateTimeOffset.UtcNow,
            FrozenAt = DateTimeOffset.UtcNow
        };

        _registry.AddOrUpdate(record);
        _logger.LogInformation("[Profile] initialized profile={Key} region={Region} ua.len={Len}", profileKey, record.Region, record.UserAgent?.Length ?? 0);
        return record;
    }

    public Task AssignProxyIfEmptyAsync(ProfileRecord record, string proxyEndpoint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.ProxyEndpoint))
        {
            record.ProxyEndpoint = proxyEndpoint;
            record.ProxyAssignedAt = DateTimeOffset.UtcNow;
            _registry.AddOrUpdate(record);
            _logger.LogInformation("[Profile] sticky proxy assigned profile={Key} endpoint={Endpoint}", record.ProfileKey, proxyEndpoint);
        }
        return Task.CompletedTask;
    }

    private static (int W, int H) PickViewport()
    {
        // 近似分布：1920x1080、1536x864、1366x768、1440x900
        var r = RandomNumberGenerator.GetInt32(100);
        return r switch
        {
            < 60 => (1920, 1080),
            < 80 => (1536, 864),
            < 95 => (1366, 768),
            _ => (1440, 900)
        };
    }

    private static double PickDpr()
    {
        var r = RandomNumberGenerator.GetInt32(100);
        return r switch
        {
            < 50 => 1.0,
            < 80 => 1.25,
            _ => 1.5
        };
    }

    private static (int Hw, string Platform, string Vendor, string GlVendor, string GlRenderer) PickHardware()
        => (RandomNumberGenerator.GetInt32(0, 100) < 45 ? 8 : 4,
            "Win32",
            "Google Inc.",
            "Intel Inc.",
            "ANGLE (Intel(R) UHD Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)");

    private static double NextSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Math.Abs(BitConverter.ToDouble(bytes));
    }

    private async Task<(string Ua, string? UaChJson, string? Platform, bool Mobile)> DeriveUserAgentAsync(string locale, string timezone, CancellationToken ct)
    {
        var pw = await _playwright.Value.ConfigureAwait(false);
        await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = true
        }).ConfigureAwait(false);

        var ctx = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = locale,
            TimezoneId = timezone
        }).ConfigureAwait(false);

        var page = await ctx.NewPageAsync().ConfigureAwait(false);
        var ua = await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false);
        var uaCh = await page.EvaluateAsync<string?>("() => { try { return JSON.stringify(navigator.userAgentData); } catch(e){ return null; } }").ConfigureAwait(false);
        string? platform = null;
        bool mobile = false;
        if (!string.IsNullOrWhiteSpace(uaCh))
        {
            try
            {
                using var doc = JsonDocument.Parse(uaCh!);
                platform = doc.RootElement.TryGetProperty("platform", out var p) ? p.GetString() : null;
                mobile = doc.RootElement.TryGetProperty("mobile", out var m) && m.GetBoolean();
            }
            catch
            {
                // ignore parsing issue
            }
        }

        await ctx.CloseAsync().ConfigureAwait(false);
        return (ua, uaCh, platform, mobile);
    }
}