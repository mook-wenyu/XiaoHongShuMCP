using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

/// <summary>
/// Breaker→Limiter 联动测试：Breaker 打开时，写端点限流应直接拒绝并抛出受控异常。
/// </summary>
public class BreakerLimiterIntegrationTests
{
    private sealed class OpenBreaker : ICircuitBreaker
    {
        public bool IsOpen(string key) => true;
        public TimeSpan? RemainingOpen(string key) => TimeSpan.FromSeconds(30);
        public void RecordSuccess(string key) { }
        public void RecordFailure(string key, string reasonCode) { }
    }

    private sealed class MockAccountManager : IAccountManager
    {
        public MockAccountManager(string id) { CurrentUser = new UserInfo { UserId = id }; }
        public Task<OperationResult<bool>> ConnectToBrowserAsync() => Task.FromResult(OperationResult<bool>.Ok(true));
        public Task<bool> IsLoggedInAsync() => Task.FromResult(true);
        public Task<bool> WaitUntilLoggedInAsync(TimeSpan maxWait, TimeSpan pollInterval, CancellationToken ct = default) => Task.FromResult(true);
        public Task<OperationResult<UserInfo>> GetUserProfileDataAsync(string userId) => Task.FromResult(OperationResult<UserInfo>.Ok(new UserInfo { UserId = userId }));
        public UserInfo? CurrentUser { get; private set; }
        public DateTime? LastUpdated { get; private set; }
        public bool HasValidUserInfo => true;
        public void UpdateUserInfo(UserInfo? userInfo) { CurrentUser = userInfo; }
        public bool UpdateFromApiResponse(string responseJson) => true;
        public string GetUserInfoSummary() => CurrentUser?.UserId ?? string.Empty;
    }

    [Test]
    public void Acquire_OnWriteEndpoint_WhenBreakerOpen_ShouldThrow()
    {
        var am = new MockAccountManager("u1");
        var options = Options.Create(new XhsSettings
        {
            Concurrency = new XhsSettings.ConcurrencySection
            {
                Breaker = new XhsSettings.ConcurrencySection.BreakerSection
                {
                    FailureThreshold = 3,
                    WindowSeconds = 60,
                    OpenSeconds = 600
                },
                Rate = new XhsSettings.ConcurrencySection.RateSection
                {
                    LikeCapacity = 10,
                    LikeRefillPerSecond = 100
                }
            }
        });
        var limiter = new RateLimitingRateLimiter(am, options, null, null, new OpenBreaker());
        Assert.ThrowsAsync<InvalidOperationException>(async () => await limiter.AcquireAsync(EndpointCategory.Like, "u1", CancellationToken.None));
    }
}
