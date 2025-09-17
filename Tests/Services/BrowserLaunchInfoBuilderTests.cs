using System;
using System.IO;
using HushOps.Core.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiaoHongShuMCP.Services;
using ServiceXhsSettings = XiaoHongShuMCP.Services.XhsSettings;

namespace Tests.Services;

public class BrowserLaunchInfoBuilderTests
{
    /// <summary>
    /// 构造最小配置并验证启动参数构建（不实际启动外部浏览器）。
    /// </summary>
    [Test]
    public void BuildPersistentLaunchPlan_WithExplicitExecutableAndUserDataDir_Works()
    {
        // Arrange: 使用内存配置，指定端口/可执行路径/数据目录/无头
        var dict = new Dictionary<string, string?>
        {
            ["XHS:BrowserSettings:Headless"] = "true",
            ["XHS:BrowserSettings:ExecutablePath"] = OperatingSystem.IsWindows() ? "C:/Chrome/chrome.exe" : "/opt/chrome",
            ["XHS:BrowserSettings:UserDataDir"] = "profiles/test-profile",
            ["XHS:BrowserSettings:Channel"] = "chrome"
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var store = new JsonLocalStore(new JsonLocalStoreOptions(Path.Combine(Path.GetTempPath(), "xhs_browser_test_" + Guid.NewGuid().ToString("N"))));
        var mgr = new PlaywrightBrowserManager(new NullLogger<PlaywrightBrowserManager>(), configuration, new DomElementManager(), store, Options.Create(new ServiceXhsSettings()));

        // Act: 直接传入我们期望的显式路径与参数
        var headless = true;
        var exe = OperatingSystem.IsWindows() ? "C:/Chrome/chrome.exe" : "/opt/chrome";
        var channel = "chrome";
        var userDataDir = Path.Combine(Directory.GetCurrentDirectory(), "profiles", "test-profile");
        var plan = (PlaywrightLaunchPlan)mgr.GetType()
            .GetMethod("BuildPersistentLaunchPlan", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(mgr, new object?[] { userDataDir, headless, exe, channel })!;

        // Assert
        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.ExecutablePath, Is.EqualTo(exe));
        Assert.That(plan.Channel, Is.EqualTo(channel));
        Assert.That(plan.Headless, Is.True);
        Assert.That(plan.UserDataDir, Does.Contain(Path.Combine("profiles","test-profile")));
        Assert.That(string.Join(' ', plan.Args), Does.Contain("--no-first-run"));
        Assert.That(string.Join(' ', plan.Args), Does.Contain("--no-default-browser-check"));
    }

    [Test]
    public void BuildPersistentLaunchPlan_WithPathDetection_WhenExecutableMissing_TriesDetect()
    {
        // Arrange: 伪造一个 PATH 目录并放入可执行占位文件
        var tempDir = Directory.CreateTempSubdirectory();
        var fake = Path.Combine(tempDir.FullName, OperatingSystem.IsWindows() ? "google-chrome.exe" : "google-chrome");
        File.WriteAllText(fake, string.Empty);
        if (!OperatingSystem.IsWindows())
        {
            try { System.Diagnostics.Process.Start("chmod", $"+x {fake}")?.WaitForExit(); } catch { }
        }

        var originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        Environment.SetEnvironmentVariable("PATH", tempDir.FullName + sep + originalPath);

        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XHS:BrowserSettings:Headless"] = "true",
                ["XHS:BrowserSettings:UserDataDir"] = "profiles/test-detect"
            }).Build();
            var store = new JsonLocalStore(new JsonLocalStoreOptions(Path.Combine(Path.GetTempPath(), "xhs_browser_test_" + Guid.NewGuid().ToString("N"))));
            var mgr = new PlaywrightBrowserManager(new NullLogger<PlaywrightBrowserManager>(), configuration, new DomElementManager(), store, Options.Create(new ServiceXhsSettings()));

            var plan = (PlaywrightLaunchPlan)mgr.GetType()
                .GetMethod("BuildPersistentLaunchPlan", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(mgr, new object?[] {
                    Path.Combine(Directory.GetCurrentDirectory(), "profiles", "test-detect"),
                    true,
                    null,
                    null
                })!;

            Assert.That(plan.ExecutablePath, Is.Not.Null);
            Assert.That(Path.GetFullPath(plan.ExecutablePath!), Is.EqualTo(Path.GetFullPath(fake)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { tempDir.Delete(true); } catch { }
        }
    }
}
