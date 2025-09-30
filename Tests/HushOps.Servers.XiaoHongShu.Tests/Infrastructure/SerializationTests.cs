using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Infrastructure;

/// <summary>
/// 中文：序列化测试，验证所有数据结构可正确 JSON 序列化。
/// English: Serialization tests, verifying all data structures can be correctly JSON serialized.
/// </summary>
public sealed class SerializationTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact(DisplayName = "OperationResult_Ok_应该可序列化为JSON")]
    public void OperationResult_Ok_应该可序列化为JSON()
    {
        // Arrange
        var result = OperationResult<string>.Ok("test data", "ok", new Dictionary<string, string> { ["requestId"] = "123" });

        // Act
        var json = JsonSerializer.Serialize(result, _options);
        var deserialized = JsonSerializer.Deserialize<OperationResult<string>>(json, _options);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.NotNull(deserialized);
        Assert.True(deserialized!.Success);
        Assert.Equal("ok", deserialized.Status);
        Assert.Equal("test data", deserialized.Data);
        Assert.Null(deserialized.ErrorMessage);
        Assert.Single(deserialized.Metadata);
        Assert.Equal("123", deserialized.Metadata["requestId"]);
    }

    [Fact(DisplayName = "OperationResult_Fail_应该可序列化为JSON")]
    public void OperationResult_Fail_应该可序列化为JSON()
    {
        // Arrange
        var result = OperationResult<string>.Fail("ERR_TEST", "error message", new Dictionary<string, string> { ["requestId"] = "456" });

        // Act
        var json = JsonSerializer.Serialize(result, _options);
        var deserialized = JsonSerializer.Deserialize<OperationResult<string>>(json, _options);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.NotNull(deserialized);
        Assert.False(deserialized!.Success);
        Assert.Equal("ERR_TEST", deserialized.Status);
        Assert.Null(deserialized.Data);
        Assert.Equal("error message", deserialized.ErrorMessage);
        Assert.Single(deserialized.Metadata);
        Assert.Equal("456", deserialized.Metadata["requestId"]);
    }

    [Fact(DisplayName = "NetworkSessionContext_应该可序列化为JSON")]
    public void NetworkSessionContext_应该可序列化为JSON()
    {
        // Arrange
        var context = new NetworkSessionContext(
            ProxyId: "proxy-1",
            ExitIp: "192.168.1.100",
            AverageLatencyMs: 50.5,
            FailureRate: 0.02,
            BandwidthSimulated: true,
            ProxyAddress: "http://proxy.example.com:8080",
            DelayMinMs: 100,
            DelayMaxMs: 300,
            MaxRetryAttempts: 3,
            RetryBaseDelayMs: 500,
            MitigationCount: 0);

        // Act
        var json = JsonSerializer.Serialize(context, _options);
        var deserialized = JsonSerializer.Deserialize<NetworkSessionContext>(json, _options);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.NotNull(deserialized);
        Assert.Equal("proxy-1", deserialized!.ProxyId);
        Assert.Equal("192.168.1.100", deserialized.ExitIp);
        Assert.Equal(50.5, deserialized.AverageLatencyMs);
        Assert.Equal(0.02, deserialized.FailureRate);
        Assert.True(deserialized.BandwidthSimulated);
        Assert.Equal("http://proxy.example.com:8080", deserialized.ProxyAddress);
        Assert.Equal(100, deserialized.DelayMinMs);
        Assert.Equal(300, deserialized.DelayMaxMs);
        Assert.Equal(3, deserialized.MaxRetryAttempts);
        Assert.Equal(500, deserialized.RetryBaseDelayMs);
        Assert.Equal(0, deserialized.MitigationCount);
    }

    [Fact(DisplayName = "NetworkSessionContext_ExitIp为null_应该可序列化为JSON")]
    public void NetworkSessionContext_ExitIp为null_应该可序列化为JSON()
    {
        // Arrange
        var context = new NetworkSessionContext(
            ProxyId: "proxy-2",
            ExitIp: null,
            AverageLatencyMs: 30.0,
            FailureRate: 0.01,
            BandwidthSimulated: false,
            ProxyAddress: null,
            DelayMinMs: 50,
            DelayMaxMs: 150,
            MaxRetryAttempts: 2,
            RetryBaseDelayMs: 300,
            MitigationCount: 5);

        // Act
        var json = JsonSerializer.Serialize(context, _options);
        var deserialized = JsonSerializer.Deserialize<NetworkSessionContext>(json, _options);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.NotNull(deserialized);
        Assert.Equal("proxy-2", deserialized!.ProxyId);
        Assert.Null(deserialized.ExitIp);
        Assert.Equal(30.0, deserialized.AverageLatencyMs);
        Assert.Equal(0.01, deserialized.FailureRate);
        Assert.False(deserialized.BandwidthSimulated);
        Assert.Null(deserialized.ProxyAddress);
        Assert.Equal(50, deserialized.DelayMinMs);
        Assert.Equal(150, deserialized.DelayMaxMs);
        Assert.Equal(2, deserialized.MaxRetryAttempts);
        Assert.Equal(300, deserialized.RetryBaseDelayMs);
        Assert.Equal(5, deserialized.MitigationCount);
    }

    [Fact(DisplayName = "OperationResult_嵌套复杂类型_应该可序列化为JSON")]
    public void OperationResult_嵌套复杂类型_应该可序列化为JSON()
    {
        // Arrange
        var data = new NetworkSessionContext(
            ProxyId: "nested-proxy",
            ExitIp: "10.0.0.1",
            AverageLatencyMs: 75.3,
            FailureRate: 0.015,
            BandwidthSimulated: true,
            ProxyAddress: "socks5://proxy.example.com:1080",
            DelayMinMs: 200,
            DelayMaxMs: 400,
            MaxRetryAttempts: 5,
            RetryBaseDelayMs: 1000,
            MitigationCount: 2);
        var result = OperationResult<NetworkSessionContext>.Ok(data, "ok", new Dictionary<string, string> { ["requestId"] = "nested-789" });

        // Act
        var json = JsonSerializer.Serialize(result, _options);
        var deserialized = JsonSerializer.Deserialize<OperationResult<NetworkSessionContext>>(json, _options);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.NotNull(deserialized);
        Assert.True(deserialized!.Success);
        Assert.Equal("ok", deserialized.Status);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("nested-proxy", deserialized.Data!.ProxyId);
        Assert.Equal("10.0.0.1", deserialized.Data.ExitIp);
        Assert.Equal(75.3, deserialized.Data.AverageLatencyMs);
        Assert.Equal(0.015, deserialized.Data.FailureRate);
        Assert.True(deserialized.Data.BandwidthSimulated);
        Assert.Equal("socks5://proxy.example.com:1080", deserialized.Data.ProxyAddress);
        Assert.Equal(200, deserialized.Data.DelayMinMs);
        Assert.Equal(400, deserialized.Data.DelayMaxMs);
        Assert.Equal(5, deserialized.Data.MaxRetryAttempts);
        Assert.Equal(1000, deserialized.Data.RetryBaseDelayMs);
        Assert.Equal(2, deserialized.Data.MitigationCount);
        Assert.Single(deserialized.Metadata);
        Assert.Equal("nested-789", deserialized.Metadata["requestId"]);
    }
}
