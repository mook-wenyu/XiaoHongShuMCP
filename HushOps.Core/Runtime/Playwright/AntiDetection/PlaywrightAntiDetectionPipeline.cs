using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.AntiDetection;

namespace HushOps.Core.Runtime.Playwright.AntiDetection;

public interface IPlaywrightAntiDetectionPipeline
{
    /// <summary>
    /// 应用反检测策略到上下文（仅在策略放行时执行“受控注入”，默认不注入）。
    /// </summary>
    Task ApplyAsync(IBrowserContext context, CancellationToken ct = default);
    /// <summary>
    /// 仅做“只读采集”快照，不改变页面任何状态。
    /// </summary>
    Task<AntiDetectionSnapshot> CollectSnapshotAsync(IPage page, CancellationToken ct = default);

    /// <summary>
    /// 受控执行一次“UI 注入兜底”动作（例如 dispatchEvent 触发），仅在策略允许时放行，且写入审计。
    /// - label：注入标签（如 "click.dispatchEvent"），用于审计与指标聚合；
    /// - action：具体注入逻辑，由调用方提供（例如对 handle 执行 EvaluateAsync）。
    /// 返回：是否实际执行了注入（true 表示已执行；false 表示策略禁止或发生异常）。
    /// </summary>
    Task<bool> TryUiInjectionAsync(IElementHandle handle, string label, Func<IElementHandle, Task> action, CancellationToken ct = default);
    /// <summary>
    /// 页面级注入兜底（少见），同上。
    /// </summary>
    Task<bool> TryUiInjectionAsync(IPage page, string label, Func<IPage, Task> action, CancellationToken ct = default);
}

public sealed class DefaultPlaywrightAntiDetectionPipeline : IPlaywrightAntiDetectionPipeline
{
    private readonly IAntiDetectionPolicy _policy;
    public DefaultPlaywrightAntiDetectionPipeline() : this(new PlaywrightAntiDetectionPolicyEngine()) { }
    public DefaultPlaywrightAntiDetectionPipeline(IAntiDetectionPolicy policy) { _policy = policy; }

    public async Task ApplyAsync(IBrowserContext context, CancellationToken ct = default)
    {
        var auditDir = Environment.GetEnvironmentVariable("XHS__AntiDetection__AuditDirectory") ?? ".audit";
        if (!_policy.Enabled)
        {
            _policy.WriteAudit(auditDir, "antidetect-disabled", new
            {
                ts = DateTimeOffset.UtcNow.ToString("u"),
                strategies = new { enabled = false },
                browser = new { isConnected = context.Browser?.IsConnected ?? false },
            });
            return;
        }

        // 注入动作：仅在策略显式允许时执行，并记录审计。
        if (_policy.AllowNavigatorWebdriverPatch)
        {
            try
            {
                await context.AddInitScriptAsync(@"() => { try { Object.defineProperty(Navigator.prototype, 'webdriver', { get: () => undefined }); } catch {} }");
                _policy.WriteAudit(auditDir, "inject-navigator-webdriver", new { success = true });
            }
            catch (Exception ex)
            {
                _policy.WriteAudit(auditDir, "inject-navigator-webdriver", new { success = false, error = ex.Message });
            }
        }

        if (_policy.AllowUaLanguageScrub)
        {
            try
            {
                await context.AddInitScriptAsync(@"() => { try {
                    const lang = (navigator.languages && navigator.languages[0]) || navigator.language || 'zh-CN';
                    Object.defineProperty(navigator, 'languages', { get: () => [lang, 'zh'] });
                    const ua = navigator.userAgent.replace('Headless', '');
                    Object.defineProperty(navigator, 'userAgent', { get: () => ua });
                } catch {} }");
                _policy.WriteAudit(auditDir, "inject-ua-language-scrub", new { success = true });
            }
            catch (Exception ex)
            {
                _policy.WriteAudit(auditDir, "inject-ua-language-scrub", new { success = false, error = ex.Message });
            }
        }
    }

    public async Task<AntiDetectionSnapshot> CollectSnapshotAsync(IPage page, CancellationToken ct = default)
    {
        // 只读采集：将路径标签纳入门控白名单，统一通过 PlaywrightAdapterTelemetry 计量
        var json = await HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry.EvalAsync<string>(page, @"() => (async () => {
            try {
                const tz = (Intl && Intl.DateTimeFormat && Intl.DateTimeFormat().resolvedOptions().timeZone) || null;
                const langs = (navigator.languages && navigator.languages.slice(0,3)) || [];
                // WebGL
                let glVendor=null, glRenderer=null;
                try {
                  const canvas = document.createElement('canvas');
                  const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
                  if (gl) {
                    const dbgInfo = gl.getExtension('WEBGL_debug_renderer_info');
                    if (dbgInfo) {
                      glVendor = gl.getParameter(dbgInfo.UNMASKED_VENDOR_WEBGL) || null;
                      glRenderer = gl.getParameter(dbgInfo.UNMASKED_RENDERER_WEBGL) || null;
                    }
                  }
                } catch {}
                // 存储
                let ls=0, ss=0, cookiesEnabled = navigator.cookieEnabled===true;
                try { ls = (typeof localStorage!=='undefined') ? Object.keys(localStorage).length : 0; } catch {}
                try { ss = (typeof sessionStorage!=='undefined') ? Object.keys(sessionStorage).length : 0; } catch {}

                // 字体（低基数名单）
                const fontCandidates = [
                  'Arial','Helvetica','Times New Roman','Courier New','Microsoft YaHei','SimSun','PingFang SC','Noto Sans CJK SC','Songti SC'
                ];
                const fonts = [];
                try {
                  if (document.fonts && document.fonts.check) {
                    for (const f of fontCandidates) { if (document.fonts.check('12px ' + f)) fonts.push(f); }
                  }
                } catch {}

                // 权限状态（有限集合）
                const permNames = ['notifications','clipboard-read','clipboard-write','geolocation','camera','microphone'];
                const perms = {};
                try {
                  if (navigator.permissions && navigator.permissions.query) {
                    for (const name of permNames) {
                      try { const st = await navigator.permissions.query({ name }); perms[name] = st.state || 'unknown'; } catch { perms[name] = 'unknown'; }
                    }
                  }
                } catch {}

                // 媒体设备计数
                let mediaVideoInputs=0, mediaAudioInputs=0, mediaAudioOutputs=0;
                try {
                  if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
                    const devs = await navigator.mediaDevices.enumerateDevices();
                    for (const d of devs) {
                      if (d.kind==='videoinput') mediaVideoInputs++;
                      else if (d.kind==='audioinput') mediaAudioInputs++;
                      else if (d.kind==='audiooutput') mediaAudioOutputs++;
                    }
                  }
                } catch {}

                // 传感器能力（支持标记）
                const sensors = {};
                try {
                  sensors['DeviceMotionEvent'] = typeof DeviceMotionEvent !== 'undefined';
                  sensors['DeviceOrientationEvent'] = typeof DeviceOrientationEvent !== 'undefined';
                  sensors['Gyroscope'] = typeof Gyroscope !== 'undefined';
                } catch {}

                const data = {
                    CapturedAtUtc: new Date().toISOString(),
                    Ua: navigator.userAgent || null,
                    Webdriver: navigator.webdriver === true,
                    Languages: langs,
                    Language: navigator.language || null,
                    TimeZone: tz,
                    Platform: navigator.platform || null,
                    WebglVendor: glVendor,
                    WebglRenderer: glRenderer,
                    DevicePixelRatio: (window.devicePixelRatio||1),
                    HardwareConcurrency: (navigator.hardwareConcurrency||0),
                    CookiesEnabled: cookiesEnabled,
                    LocalStorageKeys: ls,
                    SessionStorageKeys: ss,
                    Fonts: fonts,
                    Permissions: perms,
                    MediaVideoInputs: mediaVideoInputs,
                    MediaAudioInputs: mediaAudioInputs,
                    MediaAudioOutputs: mediaAudioOutputs,
                    Sensors: sensors
                };
                return JSON.stringify(data);
            } catch(e) {
                return JSON.stringify({ error: String(e) });
            }
        })()", "antidetect.snapshot", ct) ?? "{}";
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AntiDetectionSnapshot>(json!) ?? new AntiDetectionSnapshot();
        }
        catch
        {
            return new AntiDetectionSnapshot();
        }
    }

    public async Task<bool> TryUiInjectionAsync(IElementHandle handle, string label, Func<IElementHandle, Task> action, CancellationToken ct = default)
    {
        var auditDir = Environment.GetEnvironmentVariable("XHS__AntiDetection__AuditDirectory") ?? ".audit";
        if (!_policy.Enabled || !_policy.AllowUiInjectionFallback)
        {
            _policy.WriteAudit(auditDir, "ui-injection-blocked", new { label, reason = "policy_denied" });
            return false;
        }
        try
        {
            await action(handle);
            _policy.WriteAudit(auditDir, "ui-injection-executed", new { label, ok = true });
            return true;
        }
        catch (Exception ex)
        {
            _policy.WriteAudit(auditDir, "ui-injection-failed", new { label, ok = false, error = ex.Message });
            return false;
        }
    }

    public async Task<bool> TryUiInjectionAsync(IPage page, string label, Func<IPage, Task> action, CancellationToken ct = default)
    {
        var auditDir = Environment.GetEnvironmentVariable("XHS__AntiDetection__AuditDirectory") ?? ".audit";
        if (!_policy.Enabled || !_policy.AllowUiInjectionFallback)
        {
            _policy.WriteAudit(auditDir, "ui-injection-blocked", new { label, reason = "policy_denied" });
            return false;
        }
        try
        {
            await action(page);
            _policy.WriteAudit(auditDir, "ui-injection-executed", new { label, ok = true });
            return true;
        }
        catch (Exception ex)
        {
            _policy.WriteAudit(auditDir, "ui-injection-failed", new { label, ok = false, error = ex.Message });
            return false;
        }
    }
}
