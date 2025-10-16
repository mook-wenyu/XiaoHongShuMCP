using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

/// <summary>
/// 中文：登录助手（无认证逻辑）。仅负责打开登录入口与会话有效性检查；不采集二维码/验证码，不读写 Cookie 文件。
/// English: Login helper without any auth logic. Opens login entry and checks session state; no QR/captcha handling, no cookie I/O.
/// </summary>
[McpServerToolType]
public sealed class LoginTool
{
    private readonly IBrowserAutomationService _browser;
    private readonly ILogger<LoginTool> _logger;

    public LoginTool(IBrowserAutomationService browser, ILogger<LoginTool> logger)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_open_login"), Description("打开登录入口并等待人工登录 | Open login entry and wait for manual sign-in")]
    public async Task<OperationResult<LoginOpenResult>> OpenLoginAsync(
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        return await ServerToolExecutor.TryAsync(
            _logger,
            nameof(LoginTool),
            nameof(OpenLoginAsync),
            requestId,
            async () =>
            {
                // 仅使用 headed 的持久化/临时上下文，由 PlaywrightSessionManager 保证；此处不处理认证细节
                var ctx = await _browser.EnsurePageContextAsync(string.IsNullOrWhiteSpace(browserKey) ? "user" : browserKey.Trim(), cancellationToken).ConfigureAwait(false);

                // 优先到首页，若站点存在独立登录页可在前端手动点击“登录/注册”
                // 保持最小导航：不注入脚本、不填写表单
                try
                {
                    await ctx.Page.GotoAsync("https://www.xiaohongshu.com/explore", new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.Load,
                        Timeout = 30000
                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LoginTool] navigate to explore failed; keep current page");
                }

                var data = new LoginOpenResult("ready_for_manual_login", ctx.Page.Url);
                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["requestId"] = requestId,
                    ["url"] = ctx.Page.Url
                };
                return OperationResult<LoginOpenResult>.Ok(data, "ready", metadata);
            },
            (ex, rid) => OperationResult<LoginOpenResult>.Fail(ServerToolExecutor.MapExceptionCode(ex), ex.Message, new()
            {
                ["requestId"] = rid ?? string.Empty
            }));
    }

    [McpServerTool(Name = "xhs_check_session"), Description("检查当前页面是否已登录（启发式） | Check whether the session is signed-in (heuristic)")]
    public async Task<OperationResult<SessionCheckResult>> CheckSessionAsync(
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        return await ServerToolExecutor.TryAsync(
            _logger,
            nameof(LoginTool),
            nameof(CheckSessionAsync),
            requestId,
            async () =>
            {
                var ctx = await _browser.EnsurePageContextAsync(string.IsNullOrWhiteSpace(browserKey) ? "user" : browserKey.Trim(), cancellationToken).ConfigureAwait(false);

                // 启发式：页面上是否存在“登录/注册/Log in/Sign in”等入口文本；仅用于提示，不做认证判断
                bool hasLoginCta = false;
                try
                {
                    hasLoginCta = await ctx.Page.EvaluateAsync<bool>(
                        @"() => Array.from(document.querySelectorAll('a,button'))
                               .some(el => /登录|注册|Log\s*in|Sign\s*in/i.test(el.textContent || ''))").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[LoginTool] heuristic evaluation failed");
                }

                var isLoggedIn = !hasLoginCta; // 粗略启发式：没有“登录/注册”入口时，倾向于已登录
                var result = new SessionCheckResult(isLoggedIn, ctx.Page.Url, hasLoginCta ? "cta_present" : "cta_absent");
                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["requestId"] = requestId,
                    ["url"] = ctx.Page.Url,
                    ["heuristic"] = hasLoginCta ? "cta_present" : "cta_absent"
                };
                return OperationResult<SessionCheckResult>.Ok(result, isLoggedIn ? "likely_logged_in" : "likely_logged_out", metadata);
            },
            (ex, rid) => OperationResult<SessionCheckResult>.Fail(ServerToolExecutor.MapExceptionCode(ex), ex.Message, new()
            {
                ["requestId"] = rid ?? string.Empty
            }));
    }
}

public sealed record LoginOpenResult(string State, string? Url);

public sealed record SessionCheckResult(bool IsLoggedIn, string? Url, string Heuristic);
