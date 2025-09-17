using System.Text.Json;
using NUnit.Framework;
using HushOps.Core.AntiDetection;

namespace Tests.Tools;

/// <summary>
/// AntiDetectionBaselineValidator 扩展键校验（Fonts/Permissions/MediaDevices/Sensors/MaxViolations）。
/// </summary>
public class AntiDetectionBaselineValidatorExtendedTests
{
    [Test]
    public void Fonts_And_Permissions_And_Media_And_Sensors_Should_Pass_When_Within_Whitelist()
    {
        var s = new AntiDetectionSnapshot
        {
            Ua = "Mozilla/5.0",
            Webdriver = false,
            Platform = "Win32",
            Language = "zh-CN",
            Languages = new[] { "zh-CN", "zh" },
            TimeZone = "Asia/Shanghai",
            WebglVendor = "Google Inc.",
            WebglRenderer = "ANGLE (Google, OpenGL)",
            DevicePixelRatio = 2,
            HardwareConcurrency = 8,
            CookiesEnabled = true,
            LocalStorageKeys = 1,
            SessionStorageKeys = 0,
            Fonts = new[] { "Microsoft YaHei", "Arial" },
            Permissions = new Dictionary<string, string> { ["notifications"] = "granted", ["clipboard-read"] = "prompt" },
            MediaVideoInputs = 1,
            MediaAudioInputs = 1,
            MediaAudioOutputs = 0,
            Sensors = new Dictionary<string, bool> { ["DeviceMotionEvent"] = true, ["Gyroscope"] = false }
        };

        var w = new AntiDetectionWhitelist
        {
            AllowedWebdrivers = new[] { false },
            FontsMustContainAny = new[] { "Microsoft YaHei", "PingFang SC" },
            PermissionStates = new Dictionary<string, string[]> { ["notifications"] = new[] { "granted", "prompt" } },
            MinMediaVideoInputs = 1,
            MinMediaAudioInputs = 1,
            MinMediaAudioOutputs = 0,
            RequiredSensorsAny = new[] { "DeviceMotionEvent", "Gyroscope" },
            MaxViolations = 0
        };

        var res = AntiDetectionBaselineValidator.Validate(s, w);
        Assert.That(res.TotalViolations, Is.EqualTo(0));
        Assert.That(res.DegradeRecommended, Is.False);
    }

    [Test]
    public void MaxViolations_Should_Allow_Degrade_When_Within_Threshold()
    {
        var s = new AntiDetectionSnapshot
        {
            Ua = "Mozilla/5.0",
            Webdriver = false,
            Platform = "Win32",
            Language = "zh-CN",
            Languages = new[] { "zh-CN" },
            TimeZone = "Asia/Shanghai",
            Fonts = new[] { "Arial" },
            Permissions = new Dictionary<string, string> { ["notifications"] = "denied" },
            MediaVideoInputs = 0,
            MediaAudioInputs = 1,
            MediaAudioOutputs = 0,
            Sensors = new Dictionary<string, bool> { ["Gyroscope"] = false }
        };

        var w = new AntiDetectionWhitelist
        {
            AllowedWebdrivers = new[] { false },
            FontsMustContainAny = new[] { "Microsoft YaHei" }, // 不满足 → 1 违反
            PermissionStates = new Dictionary<string, string[]> { ["notifications"] = new[] { "granted" } }, // 不满足 → 1 违反
            MinMediaVideoInputs = 1, // 不满足 → 1 违反
            RequiredSensorsAny = new[] { "DeviceMotionEvent" }, // 不满足 → 1 违反
            MaxViolations = 4
        };

        var res = AntiDetectionBaselineValidator.Validate(s, w);
        Assert.That(res.TotalViolations, Is.EqualTo(4));
        Assert.That(res.DegradeRecommended, Is.True, "在阈值内应建议降级");
    }
}

