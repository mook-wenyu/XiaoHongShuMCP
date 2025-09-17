using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using HushOps.Core.Observability;

namespace Tests.Services;

/// <summary>
/// 限流器与节律联动测试：
/// - 验证当 PacingAdvisor 的倍率>1 时，Acquire 会以更高的 permits 计入指标；
/// - 通过自定义 IMetrics 采集标签验证（不依赖真实时间或外部限流器状态）。
/// </summary>
public class RateLimitingRateLimiterPacingTests
{
    private sealed class ConstPacing : HushOps.Core.Humanization.IPacingAdvisor
    {
        private readonly double _mult;
        public ConstPacing(double mult) { _mult = mult; }
        public void NotifyHttpStatus(int statusCode) { }
        public void ObserveRtt(System.TimeSpan rtt) { }
        public double CurrentMultiplier => _mult;
    }

    private sealed class CaptureMetrics : IMetrics
    {
        public readonly List<LabelSet> Adds = new();
        private sealed class Ctr : ICounter
        {
            private readonly List<LabelSet> _adds;
            public Ctr(List<LabelSet> adds) { _adds = adds; }
            public void Add(long value, in LabelSet labels) { _adds.Add(labels); }
        }
        private sealed class Hst : IHistogram { public void Record(double value, in LabelSet labels) { } }
        public ICounter CreateCounter(string name, string? description = null) => new Ctr(Adds);
        public IHistogram CreateHistogram(string name, string? description = null) => new Hst();
    }

    [Test]
    public async Task Acquire_Should_Reflect_PacingMultiplier_In_Permits_Label()
    {
        var metrics = new CaptureMetrics();
        var pacing = new ConstPacing(2.7); // 期望 permits=ceil(2.7)=3
        var options = Options.Create(new XhsSettings
        {
            Concurrency = new XhsSettings.ConcurrencySection
            {
                Rate = new XhsSettings.ConcurrencySection.RateSection
                {
                    LikeCapacity = 10,
                    LikeRefillPerSecond = 100,
                    SearchCapacity = 100,
                    SearchRefillPerSecond = 100
                }
            }
        });
        var am = new MockAccountManager("u1");
        var limiter = new RateLimitingRateLimiter(am, options, metrics, pacing);
        await limiter.AcquireAsync(EndpointCategory.Like, "u1", CancellationToken.None);

        Assert.That(metrics.Adds.Count, Is.GreaterThan(0));
        var labels = metrics.Adds[^1].Labels;
        Assert.That(labels.ContainsKey("permits"), Is.True);
        Assert.That(labels["permits"], Is.EqualTo(3));
        Assert.That(labels["multiplier"], Is.EqualTo(2.7));
    }

    private sealed class MockAccountManager : IAccountManager
    {
        public MockAccountManager(string id) { CurrentUser = new UserInfo { UserId = id }; }
        public Task<OperationResult<bool>> ConnectToBrowserAsync() => Task.FromResult(OperationResult<bool>.Ok(true));
        public Task<bool> IsLoggedInAsync() => Task.FromResult(true);
        public Task<bool> WaitUntilLoggedInAsync(System.TimeSpan maxWait, System.TimeSpan pollInterval, System.Threading.CancellationToken ct = default) => Task.FromResult(true);
        public Task<OperationResult<UserInfo>> GetUserProfileDataAsync(string userId) => Task.FromResult(OperationResult<UserInfo>.Ok(new UserInfo { UserId = userId }));
        public UserInfo? CurrentUser { get; private set; }
        public DateTime? LastUpdated { get; private set; }
        public bool HasValidUserInfo => true;
        public void UpdateUserInfo(UserInfo? userInfo) { CurrentUser = userInfo; }
        public bool UpdateFromApiResponse(string responseJson) => true;
        public string GetUserInfoSummary() => CurrentUser?.UserId ?? "";
    }
}
