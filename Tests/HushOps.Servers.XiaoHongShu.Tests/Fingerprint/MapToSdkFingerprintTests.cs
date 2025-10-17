using System;
using System.Linq;
using System.Reflection;
using HushOps.FingerprintBrowser.Core;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Profile;
using Microsoft.Playwright;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Fingerprint;

public class MapToSdkFingerprintTests
{
    [Fact]
    public void MapToSdkFingerprint_ShouldProject_CoreFields_Correctly()
    {
        // Arrange
        var profile = new ProfileRecord
        {
            ProfileKey = "test-key",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Platform = "Win32",
            ViewportWidth = 1366,
            ViewportHeight = 768,
            Locale = "zh-CN",
            TimezoneId = "Asia/Shanghai",
            HardwareConcurrency = 8,
            Vendor = "Google Inc.",
            WebglVendor = "Intel Inc.",
            WebglRenderer = "ANGLE",
            CanvasSeed = 0.33,
            WebglSeed = 0.77
        };

        var open = new BrowserOpenResult(
            Kind: BrowserProfileKind.User,
            ProfileKey: "test-key",
            ProfilePath: "C:/Users/test/AppData/Local/Microsoft/Edge/User Data",
            IsNewProfile: false,
            UsedFallbackPath: false,
            ProfileDirectoryName: null,
            AlreadyOpen: false,
            AutoOpened: false,
            SessionMetadata: null,
            ConnectionMode: Services.Browser.BrowserConnectionMode.Auto,
            CdpPort: 9222
        );

        // 反射定位私有静态 MapToSdkFingerprint
        var type = typeof(HushOps.Servers.XiaoHongShu.Services.Browser.Playwright.PlaywrightSessionManager);
        var mi = type.GetMethod("MapToSdkFingerprint", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);

        // Act
        var fp = (FingerprintProfile)mi!.Invoke(null, new object[] { profile, open })!;

        // Assert 关键字段
        Assert.Equal(profile.UserAgent, fp.UserAgent);
        Assert.Equal(profile.Platform, fp.Platform);
        Assert.Equal(profile.ViewportWidth, fp.ViewportWidth);
        Assert.Equal(profile.ViewportHeight, fp.ViewportHeight);
        Assert.Equal(profile.Locale, fp.Locale);
        Assert.Equal(profile.TimezoneId, fp.TimezoneId);
        Assert.Equal(profile.HardwareConcurrency, fp.HardwareConcurrency);
        Assert.Equal(profile.Vendor, fp.Vendor);
        Assert.Equal(profile.WebglVendor, fp.WebglVendor);
        Assert.Equal(profile.WebglRenderer, fp.WebglRenderer);
        Assert.Equal(profile.CanvasSeed, fp.CanvasSeed);
        Assert.Equal(profile.WebglSeed, fp.WebglSeed);
        Assert.Equal("test-key", fp.ProfileKey);
        Assert.Equal(ProfileType.User, fp.ProfileType);
        Assert.Equal(open.ProfilePath, fp.UserDataDir);
    }
}
