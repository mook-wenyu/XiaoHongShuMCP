using XiaoHongShuMCP.Services;

namespace Tests.Services.Concurrency;

/// <summary>
/// ConcurrencyGovernor 并发租约单元测试
/// </summary>
public class ConcurrencyGovernorTests
{
    private sealed class FakeAccountManager : IAccountManager
    {
        public OperationResult<bool> LastConnectResult = OperationResult<bool>.Ok(true);
        public UserInfo? CurrentUser { get; private set; } = new UserInfo { UserId = "u1", Nickname = "tester" };
        public DateTime? LastUpdated { get; private set; } = DateTime.UtcNow;
        public Task<OperationResult<bool>> ConnectToBrowserAsync() => Task.FromResult(LastConnectResult);
        public Task<bool> IsLoggedInAsync() => Task.FromResult(true);
        public Task<bool> WaitUntilLoggedInAsync(TimeSpan maxWait, TimeSpan pollInterval, CancellationToken ct = default) => Task.FromResult(true);
        public Task<OperationResult<UserInfo>> GetUserProfileDataAsync(string userId) => Task.FromResult(OperationResult<UserInfo>.Ok(CurrentUser!));
        // 新增接口成员实现
        public bool HasValidUserInfo => CurrentUser != null;
        public void UpdateUserInfo(UserInfo? userInfo) { CurrentUser = userInfo; LastUpdated = DateTime.UtcNow; }
        public bool UpdateFromApiResponse(string responseJson) => true;
        public string GetUserInfoSummary() => CurrentUser == null ? "<null>" : $"{CurrentUser.Nickname}({CurrentUser.UserId})";
    }

    [Test]
    public async Task WriteLease_Should_Serialize_PerAccount()
    {
        var am = new FakeAccountManager();
        var settings = new XhsSettings
        {
            Concurrency = new XhsSettings.ConcurrencySection
            {
                PerAccountWriteConcurrency = 1
            }
        };

        var gov = new ConcurrencyGovernor(am, Microsoft.Extensions.Options.Options.Create(settings));

        var tcs = new TaskCompletionSource();
        await using var lease1 = await gov.AcquireAsync(OperationKind.Write, "res1");

        var acquired2 = false;
        var task2 = Task.Run(async () =>
        {
            await using var lease2 = await gov.AcquireAsync(OperationKind.Write, "res2");
            acquired2 = true;
            tcs.SetResult();
        });

        // 等待100ms，第二个应尚未获取
        await Task.Delay(100);
        Assert.That(acquired2, Is.False, "第二个写租约不应在第一个释放前获取");

        await lease1.DisposeAsync();
        // 等待任务完成
        await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.That(acquired2, Is.True, "第一个释放后应能获取第二个写租约");
    }
}
