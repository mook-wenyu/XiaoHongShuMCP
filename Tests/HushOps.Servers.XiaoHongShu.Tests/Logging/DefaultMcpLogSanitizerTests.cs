namespace HushOps.Servers.XiaoHongShu.Tests.Logging;

public sealed class DefaultMcpLogSanitizerTests
{
    [Theory]
    [InlineData("api_key=abcdef", "api_key=[REDACTED]")]
    [InlineData("Token : super-secret", "Token : [REDACTED]")]
    [InlineData("Bearer sk-123456", "Bearer [REDACTED]")]
    public void Sanitize_RedactsSensitiveTokens(string input, string expected)
    {
        var sanitizer = new DefaultMcpLogSanitizer();

        var sanitized = sanitizer.Sanitize(input);

        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void Sanitize_ReturnsOriginalWhenSafe()
    {
        var sanitizer = new DefaultMcpLogSanitizer();
        const string message = "执行完毕";

        Assert.Equal(message, sanitizer.Sanitize(message));
    }
}
