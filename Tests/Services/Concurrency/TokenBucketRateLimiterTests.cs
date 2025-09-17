using System.Diagnostics;
using XiaoHongShuMCP.Services;

namespace Tests.Services.Concurrency;

public class RateLimitingRateLimiterTests
{
    private sealed class FakeAccountManager : IAccountManager
    {
        public UserInfo? CurrentUser { get; private set; } = new UserInfo { UserId = "u1", Nickname = "tester" };
        public DateTime? LastUpdated { get; private set; } = DateTime.UtcNow;
        public Task<OperationResult<bool>> ConnectToBrowserAsync() => Task.FromResult(OperationResult<bool>.Ok(true));
        public Task<bool> IsLoggedInAsync() => Task.FromResult(true);
        public Task<bool> WaitUntilLoggedInAsync(TimeSpan maxWait, TimeSpan pollInterval, CancellationToken ct = default) => Task.FromResult(true);
        public Task<OperationResult<UserInfo>> GetUserProfileDataAsync(string userId) => Task.FromResult(OperationResult<UserInfo>.Ok(CurrentUser!));

        // 新增接口成员实现（保持测试桩简单可控）
        public bool HasValidUserInfo => CurrentUser != null;
        public void UpdateUserInfo(UserInfo? userInfo)
        {
            CurrentUser = userInfo;
            LastUpdated = DateTime.UtcNow;
        }
        public bool UpdateFromApiResponse(string responseJson) => true; // 测试桩：不解析，恒成立
        public string GetUserInfoSummary() => CurrentUser == null ? "<null>" : $"{CurrentUser.Nickname}({CurrentUser.UserId})";
    }

    [Test]
    public async Task Acquire_Should_Wait_When_Tokens_Exhausted()
    {
        var am = new FakeAccountManager();
        var settings = new XhsSettings
        {
            Concurrency = new XhsSettings.ConcurrencySection
            {
                Rate = new XhsSettings.ConcurrencySection.RateSection
                {
                    LikeCapacity = 2,
                    LikeRefillPerSecond = 100 // 快速回补，保证测试不过长
                }
            }
        };
        var rl = new RateLimitingRateLimiter(am, Microsoft.Extensions.Options.Options.Create(settings));

        // 前两次应立即通过
        await rl.AcquireAsync(EndpointCategory.Like, am.CurrentUser!.UserId);
        await rl.AcquireAsync(EndpointCategory.Like, am.CurrentUser!.UserId);

        var sw = Stopwatch.StartNew();
        await rl.AcquireAsync(EndpointCategory.Like, am.CurrentUser!.UserId);
        sw.Stop();

        // 第三次需要等到回补（以 LikeRefillPerSecond=100，等待应在几十毫秒内）
        Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(1));
    }
}
