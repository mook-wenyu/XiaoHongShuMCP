using FluentAssertions;
using HushOps.Core.Config;

namespace HushOps.Core.Tests;

public class ConfigurationTests
{
    [Fact]
    public void LoadFromEnvironment_WhiteListOnly()
    {
        Environment.SetEnvironmentVariable("XHS__BrowserSettings__Headless", "false");
        Environment.SetEnvironmentVariable("XHS__Unknown__Foo", "bar");
        var settings = XhsConfiguration.LoadFromEnvironment();
        settings.BrowserSettings.Headless.Should().BeFalse();
    }
}

