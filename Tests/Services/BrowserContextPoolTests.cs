using Microsoft.Playwright;
using Moq;
using HushOps.Core.Runtime.Playwright;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

/// <summary>
/// BrowserContextPool 基础单元测试：验证 Acquire 返回的 Page/Context 来自 IBrowserManager，租约释放不抛异常。
/// </summary>
public class BrowserContextPoolTests
{
    [Test]
    public async Task Acquire_Returns_Lease_With_Page_And_Context()
    {
        var mgr = new Mock<IBrowserManager>();
        var ctx = new Mock<IBrowserContext>();
        var page1 = new Mock<IPage>();
        page1.SetupGet(p => p.IsClosed).Returns(false);

        mgr.Setup(m => m.GetBrowserContextAsync()).ReturnsAsync(ctx.Object);
        // 池内首次无可用页，将调用 NewPageAsync 创建页面
        ctx.Setup(c => c.NewPageAsync()).ReturnsAsync(page1.Object);

        var settings = new XhsSettings { Concurrency = new XhsSettings.ConcurrencySection { Pool = new XhsSettings.ConcurrencySection.PoolSection { MaxPages = 2 } } };
        var pool = new BrowserContextPool(mgr.Object, Microsoft.Extensions.Options.Options.Create(settings));
        await using var lease = await pool.AcquireAsync("u1", "test");
        Assert.That(lease.Context, Is.SameAs(ctx.Object));
        Assert.That(PlaywrightAutoFactory.TryUnwrap(lease.Page), Is.SameAs(page1.Object));
    }

    [Test]
    public async Task Pool_Creates_New_Pages_Until_Capacity_And_Reuses_On_Return()
    {
        var mgr = new Mock<IBrowserManager>();
        var ctx = new Mock<IBrowserContext>();
        var p1 = new Mock<IPage>(); p1.SetupGet(p => p.IsClosed).Returns(false);
        var p2 = new Mock<IPage>(); p2.SetupGet(p => p.IsClosed).Returns(false);
        var seq = 0;
        ctx.Setup(c => c.NewPageAsync()).ReturnsAsync(() =>
            {
                seq++;
                return seq == 1 ? p1.Object : p2.Object;
            });
        mgr.Setup(m => m.GetBrowserContextAsync()).ReturnsAsync(ctx.Object);

        var settings = new XhsSettings { Concurrency = new XhsSettings.ConcurrencySection { Pool = new XhsSettings.ConcurrencySection.PoolSection { MaxPages = 2 } } };
        var pool = new BrowserContextPool(mgr.Object, Microsoft.Extensions.Options.Options.Create(settings));

        await using var l1 = await pool.AcquireAsync("u1", "a");
        await using var l2 = await pool.AcquireAsync("u1", "b");
        Assert.That(PlaywrightAutoFactory.TryUnwrap(l1.Page), Is.SameAs(p1.Object));
        Assert.That(PlaywrightAutoFactory.TryUnwrap(l2.Page), Is.SameAs(p2.Object));

        await l1.DisposeAsync();
        await l2.DisposeAsync();

        // 归还后应复用，不再创建新页
        var l3 = await pool.AcquireAsync("u1", "c");
        Assert.That(l3.Page, Is.Not.Null);
        await l3.DisposeAsync();
    }
}
