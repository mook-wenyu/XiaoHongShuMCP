using System;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Browser;

public sealed class BrowserOpenModelsTests
{
    #region BrowserOpenRequest 基础创建测试

    [Fact]
    public void ForUser_ShouldCreateUserProfileRequest()
    {
        // Act
        var request = BrowserOpenRequest.ForUser();

        // Assert
        Assert.Equal(BrowserProfileKind.User, request.Kind);
        Assert.Equal(BrowserOpenRequest.UserProfileKey, request.ProfileKey);
        Assert.Null(request.ProfilePath);
        Assert.Null(request.ProfileDirectoryName);
        Assert.Equal(BrowserConnectionMode.Auto, request.ConnectionMode);
        Assert.Equal(9222, request.CdpPort);
    }

    [Fact]
    public void ForUser_WithProfilePath_ShouldSetPath()
    {
        // Arrange
        const string customPath = "C:/CustomProfile";

        // Act
        var request = BrowserOpenRequest.ForUser(customPath);

        // Assert
        Assert.Equal(BrowserProfileKind.User, request.Kind);
        Assert.Equal(customPath, request.ProfilePath);
    }

    [Fact]
    public void ForIsolated_ShouldCreateIsolatedProfileRequest()
    {
        // Arrange
        const string folderName = "test-isolated";

        // Act
        var request = BrowserOpenRequest.ForIsolated(folderName);

        // Assert
        Assert.Equal(BrowserProfileKind.Isolated, request.Kind);
        Assert.Equal(folderName, request.ProfileKey);
        Assert.Equal(folderName, request.ProfileDirectoryName);
        Assert.Null(request.ProfilePath);
        Assert.Equal(BrowserConnectionMode.Launch, request.ConnectionMode);
        Assert.Equal(9222, request.CdpPort);
    }

    #endregion

    #region CDP 连接模式测试

    [Fact]
    public void UseUserProfile_WithAutoMode_ShouldAllowAutoConnection()
    {
        // Act
        var request = BrowserOpenRequest.UseUserProfile(
            profilePath: null,
            profileKey: "test-auto",
            chromiumProfileDirectory: null,
            connectionMode: BrowserConnectionMode.Auto,
            cdpPort: 9222);

        // Assert
        Assert.Equal(BrowserConnectionMode.Auto, request.ConnectionMode);
        Assert.Equal(9222, request.CdpPort);
    }

    [Fact]
    public void UseUserProfile_WithConnectCdpMode_ShouldAllowCdpConnection()
    {
        // Act
        var request = BrowserOpenRequest.UseUserProfile(
            profilePath: null,
            profileKey: "test-cdp",
            chromiumProfileDirectory: null,
            connectionMode: BrowserConnectionMode.ConnectCdp,
            cdpPort: 9223);

        // Assert
        Assert.Equal(BrowserConnectionMode.ConnectCdp, request.ConnectionMode);
        Assert.Equal(9223, request.CdpPort);
    }

    [Fact]
    public void UseUserProfile_WithLaunchMode_ShouldAllowLaunchOnly()
    {
        // Act
        var request = BrowserOpenRequest.UseUserProfile(
            profilePath: null,
            profileKey: "test-launch",
            chromiumProfileDirectory: null,
            connectionMode: BrowserConnectionMode.Launch,
            cdpPort: 9222);

        // Assert
        Assert.Equal(BrowserConnectionMode.Launch, request.ConnectionMode);
    }

    #endregion

    #region 验证逻辑测试

    [Fact]
    public void EnsureValid_WithEmptyProfileKey_ShouldThrow()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BrowserOpenRequest.UseUserProfile(
                profilePath: null,
                profileKey: "",
                chromiumProfileDirectory: null));

        Assert.Contains("ProfileKey 不能为空", exception.Message);
    }

    [Fact]
    public void EnsureValid_WithWhitespaceProfileKey_ShouldThrow()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BrowserOpenRequest.UseUserProfile(
                profilePath: null,
                profileKey: "   ",
                chromiumProfileDirectory: null));

        Assert.Contains("ProfileKey 不能为空", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void EnsureValid_WithInvalidCdpPort_ShouldThrow(int invalidPort)
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BrowserOpenRequest.UseUserProfile(
                profilePath: null,
                profileKey: "test",
                chromiumProfileDirectory: null,
                connectionMode: BrowserConnectionMode.Auto,
                cdpPort: invalidPort));

        Assert.Contains("CDP 端口必须在 1-65535 范围内", exception.Message);
        Assert.Contains(invalidPort.ToString(), exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9222)]
    [InlineData(65535)]
    public void EnsureValid_WithValidCdpPort_ShouldNotThrow(int validPort)
    {
        // Act & Assert - 不应抛出异常
        var request = BrowserOpenRequest.UseUserProfile(
            profilePath: null,
            profileKey: "test",
            chromiumProfileDirectory: null,
            connectionMode: BrowserConnectionMode.Auto,
            cdpPort: validPort);

        Assert.Equal(validPort, request.CdpPort);
    }


    #endregion

    #region 参数规范化测试

    [Fact]
    public void EnsureValid_ShouldTrimWhitespace()
    {
        // Arrange
        var request = BrowserOpenRequest.UseUserProfile(
            profilePath: "  /path/with/spaces  ",
            profileKey: "  test-key  ",
            chromiumProfileDirectory: "  Default  ");

        // Assert
        Assert.Equal("test-key", request.ProfileKey);
        Assert.Equal("/path/with/spaces", request.ProfilePath);
        Assert.Equal("Default", request.ProfileDirectoryName);
    }

    [Fact]
    public void EnsureValid_ShouldHandleNullProfilePath()
    {
        // Act
        var request = BrowserOpenRequest.UseUserProfile(
            profilePath: null,
            profileKey: "test",
            chromiumProfileDirectory: null);

        // Assert
        Assert.Null(request.ProfilePath);
        Assert.Null(request.ProfileDirectoryName);
    }

    #endregion
}
