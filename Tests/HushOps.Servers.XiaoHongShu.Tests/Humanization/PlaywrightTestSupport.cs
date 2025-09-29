using System;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

internal static class PlaywrightTestSupport
{
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static bool _browsersInstalled;

    public static async Task EnsureBrowsersInstalledAsync()
    {
        if (_browsersInstalled)
        {
            return;
        }

        await InstallLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_browsersInstalled)
            {
                return;
            }

            var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(new[] { "install", "chromium" })).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Playwright 浏览器安装失败，退出码：{exitCode}");
            }

            _browsersInstalled = true;
        }
        finally
        {
            InstallLock.Release();
        }
    }
}
