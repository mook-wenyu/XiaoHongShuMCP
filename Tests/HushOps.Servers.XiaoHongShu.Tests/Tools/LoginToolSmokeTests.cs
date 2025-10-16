using System.Threading.Tasks;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Tools;

public sealed class LoginToolSmokeTests
{
    [Fact(Skip = "环境依赖：需要本机 Edge、FingerprintBrowser.dll 与可访问网络；仅作无副作用示例脚本")]
    public async Task OpenLogin_And_CheckSession_Heuristic_Smoke()
    {
        // 说明：此测试仅作为无副作用烟测示例，不执行点赞/评论/发布等写操作。
        // 建议在具备完整依赖的本机环境手动去掉 Skip 运行：
        // 1) 启动服务（stdio）
        // 2) 调用 xhs_open_login → 人工扫码/验证码登录
        // 3) 调用 xhs_check_session → 期望返回 likely_logged_in
        await Task.CompletedTask;
    }
}
