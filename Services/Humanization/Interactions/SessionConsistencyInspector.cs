using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

public interface ISessionConsistencyInspector
{
    Task<SessionConsistencyReport> InspectAsync(BrowserPageContext pageContext, HumanBehaviorProfileOptions profileOptions, CancellationToken cancellationToken);
}

public sealed record SessionConsistencyReport(
    bool UserAgentMatch,
    bool LanguageMatch,
    bool TimezoneMatch,
    bool ViewportMatch,
    bool IsMobileMatch,
    bool ProxyConfigured,
    bool ProxyRequirementSatisfied,
    bool GpuInfoAvailable,
    bool GpuRequirementSatisfied,
    bool GpuSuspicious,
    bool AutomationIndicatorsDetected,
    string? PageUserAgent,
    string? PageLanguage,
    string? PageTimezone,
    int PageViewportWidth,
    int PageViewportHeight,
    int HardwareConcurrency,
    double? DeviceMemoryGb,
    string? Platform,
    string? Vendor,
    string? GpuVendor,
    string? GpuRenderer,
    string? ConnectionEffectiveType,
    double? ConnectionDownlinkMbps,
    double? ConnectionRttMs,
    bool ConnectionSaveDataEnabled,
    IReadOnlyList<string> Warnings);

public sealed class SessionConsistencyInspector : ISessionConsistencyInspector
{
    private readonly ILogger<SessionConsistencyInspector> _logger;
    private static readonly string[] SuspiciousGpuKeywords =
    {
        "swiftshader",
        "software",
        "llvmpipe",
        "microsoft basic render"
    };

    public SessionConsistencyInspector(ILogger<SessionConsistencyInspector> logger)
    {
        _logger = logger;
    }

    public async Task<SessionConsistencyReport> InspectAsync(BrowserPageContext pageContext, HumanBehaviorProfileOptions profileOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageContext);
        ArgumentNullException.ThrowIfNull(profileOptions);

        const string script = """
() => {
    const nav = navigator;
    const formatter = (() => {
        try {
            return Intl.DateTimeFormat();
        } catch (err) {
            return null;
        }
    })();
    const dtf = formatter && typeof formatter.resolvedOptions === 'function' ? formatter.resolvedOptions() : null;
    const connection = nav.connection || nav.webkitConnection || nav.mozConnection || null;
    const languages = Array.isArray(nav.languages) ? nav.languages : [];
    let gpuVendor = '';
    let gpuRenderer = '';
    try {
        const canvas = document.createElement('canvas');
        const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
        if (gl) {
            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
            if (debugInfo) {
                gpuVendor = gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) || '';
                gpuRenderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) || '';
            }
        }
    } catch (err) {
        // ignore GPU sampling errors
    }

    return {
        ua: nav.userAgent ?? '',
        lang: nav.language ?? '',
        tz: (dtf?.timeZone) ?? '',
        width: window.innerWidth || 0,
        height: window.innerHeight || 0,
        languages,
        platform: nav.platform ?? '',
        vendor: nav.vendor ?? '',
        hardwareConcurrency: typeof nav.hardwareConcurrency === 'number' ? nav.hardwareConcurrency : 0,
        deviceMemory: typeof nav.deviceMemory === 'number' ? nav.deviceMemory : 0,
        webdriver: !!nav.webdriver,
        connection: connection ? {
            effectiveType: connection.effectiveType ?? '',
            downlink: typeof connection.downlink === 'number' ? connection.downlink : 0,
            rtt: typeof connection.rtt === 'number' ? connection.rtt : 0,
            saveData: !!connection.saveData
        } : { effectiveType: '', downlink: 0, rtt: 0, saveData: false },
        gpuVendor,
        gpuRenderer
    };
}
""";

        var sample = await pageContext.Page.EvaluateAsync<NavigatorSample>(script).ConfigureAwait(false);

        var warnings = new List<string>();

        var uaMatch = string.Equals(pageContext.Fingerprint.UserAgent, sample.UserAgent, StringComparison.Ordinal);
        if (!uaMatch)
        {
            warnings.Add($"UserAgent mismatch: fingerprint={pageContext.Fingerprint.UserAgent}, page={sample.UserAgent}");
        }

        var languageMatch = string.Equals(pageContext.Fingerprint.Language, sample.Language, StringComparison.OrdinalIgnoreCase)
                            || sample.Languages.Contains(pageContext.Fingerprint.Language, StringComparer.OrdinalIgnoreCase);
        if (!languageMatch)
        {
            warnings.Add($"Language mismatch: fingerprint={pageContext.Fingerprint.Language}, page={sample.Language}");
        }

        var timezoneMatch = string.Equals(pageContext.Fingerprint.Timezone, sample.Timezone, StringComparison.OrdinalIgnoreCase);
        if (!timezoneMatch)
        {
            warnings.Add($"Timezone mismatch: fingerprint={pageContext.Fingerprint.Timezone}, page={sample.Timezone}");
        }

        var viewportTolerance = Math.Max(0, profileOptions.ViewportTolerancePx);
        var viewportMatch = Math.Abs(pageContext.Fingerprint.ViewportWidth - sample.Width) <= viewportTolerance
                            && Math.Abs(pageContext.Fingerprint.ViewportHeight - sample.Height) <= viewportTolerance;
        if (!viewportMatch)
        {
            warnings.Add($"Viewport mismatch: fingerprint={pageContext.Fingerprint.ViewportWidth}x{pageContext.Fingerprint.ViewportHeight}, page={sample.Width}x{sample.Height}");
        }

        var isMobileMatch = pageContext.Fingerprint.IsMobile == (sample.Width <= 768 || pageContext.Fingerprint.HasTouch);
        if (!isMobileMatch)
        {
            warnings.Add($"IsMobile mismatch: fingerprint={pageContext.Fingerprint.IsMobile}, pageWidth={sample.Width}");
        }

        var proxyConfigured = !string.IsNullOrWhiteSpace(pageContext.Network.ProxyAddress);
        var proxyRequirementSatisfied = !profileOptions.RequireProxy || proxyConfigured;
        if (!proxyRequirementSatisfied)
        {
            warnings.Add("Proxy requirement not satisfied: behavior profile requires proxy but none configured.");
        }

        if (proxyConfigured && profileOptions.AllowedProxyPrefixes is { Length: > 0 } allowedPrefixes)
        {
            var hasAllowedPrefix = allowedPrefixes
                .Any(prefix => pageContext.Network.ProxyAddress!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!hasAllowedPrefix)
            {
                warnings.Add($"Proxy address does not match allowed prefixes: {pageContext.Network.ProxyAddress}");
            }
        }

        if (profileOptions.RandomMoveProbability <= 0 && pageContext.Fingerprint.HasTouch)
        {
            warnings.Add("Behavior profile random move probability is 0 while fingerprint indicates touch support.");
        }

        if (!string.IsNullOrWhiteSpace(pageContext.Network.ProxyAddress))
        {
            var address = pageContext.Network.ProxyAddress!;
            var supported = address.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            || address.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                            || address.StartsWith("socks", StringComparison.OrdinalIgnoreCase);
            if (!supported)
            {
                warnings.Add($"Proxy address may be invalid or use unsupported scheme: {address}");
            }
        }

        var gpuInfoAvailable = !string.IsNullOrWhiteSpace(sample.GpuVendor) || !string.IsNullOrWhiteSpace(sample.GpuRenderer);
        var gpuRequirementSatisfied = !profileOptions.RequireGpuInfo || gpuInfoAvailable;
        if (!gpuRequirementSatisfied)
        {
            warnings.Add("GPU fingerprint requirement not satisfied: renderer information unavailable.");
        }

        var gpuSuspicious = gpuInfoAvailable && SuspiciousGpuKeywords.Any(keyword =>
            sample.GpuRenderer.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (gpuSuspicious)
        {
            warnings.Add($"GPU renderer appears suspicious: {sample.GpuRenderer}");
        }

        var automationIndicatorsDetected = sample.Webdriver;
        if (automationIndicatorsDetected && !profileOptions.AllowAutomationIndicators)
        {
            warnings.Add("Automation indicator detected: navigator.webdriver reported true.");
        }

        var deviceMemory = sample.DeviceMemory > 0 ? sample.DeviceMemory : (double?)null;
        var downlink = sample.Connection.Downlink > 0 ? sample.Connection.Downlink : (double?)null;
        var rtt = sample.Connection.Rtt > 0 ? sample.Connection.Rtt : (double?)null;

        if (warnings.Count > 0)
        {
            _logger.LogWarning("[ConsistencyInspector] profile={Profile} warnings={Count}", pageContext.Profile.ProfileKey, warnings.Count);
        }

        _logger.LogInformation(
            "[ConsistencyInspector] profile={Profile} ua={UAMatch} lang={LangMatch} tz={TimezoneMatch} viewport={ViewportMatch} proxyConfigured={ProxyConfigured} proxyRequirementSatisfied={ProxyRequirementSatisfied} gpuAvailable={GpuInfoAvailable} automationDetected={AutomationDetected} warnings={Warnings}",
            pageContext.Profile.ProfileKey,
            uaMatch,
            languageMatch,
            timezoneMatch,
            viewportMatch,
            proxyConfigured,
            proxyRequirementSatisfied,
            gpuInfoAvailable,
            automationIndicatorsDetected,
            warnings.Count);

        return new SessionConsistencyReport(
            uaMatch,
            languageMatch,
            timezoneMatch,
            viewportMatch,
            isMobileMatch,
            proxyConfigured,
            proxyRequirementSatisfied,
            gpuInfoAvailable,
            gpuRequirementSatisfied,
            gpuSuspicious,
            automationIndicatorsDetected,
            sample.UserAgent,
            sample.Language,
            sample.Timezone,
            sample.Width,
            sample.Height,
            sample.HardwareConcurrency,
            deviceMemory,
            sample.Platform,
            sample.Vendor,
            sample.GpuVendor,
            sample.GpuRenderer,
            string.IsNullOrWhiteSpace(sample.Connection.EffectiveType) ? null : sample.Connection.EffectiveType,
            downlink,
            rtt,
            sample.Connection.SaveData,
            warnings.AsReadOnly());
    }

    private sealed record NavigatorSample(
        [property: JsonPropertyName("ua")] string UserAgent,
        [property: JsonPropertyName("lang")] string Language,
        [property: JsonPropertyName("tz")] string Timezone,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height,
        [property: JsonPropertyName("languages")] string[] Languages,
        [property: JsonPropertyName("platform")] string Platform,
        [property: JsonPropertyName("vendor")] string Vendor,
        [property: JsonPropertyName("hardwareConcurrency")] int HardwareConcurrency,
        [property: JsonPropertyName("deviceMemory")] double DeviceMemory,
        [property: JsonPropertyName("webdriver")] bool Webdriver,
        [property: JsonPropertyName("connection")] NavigatorConnectionSample Connection,
        [property: JsonPropertyName("gpuVendor")] string GpuVendor,
        [property: JsonPropertyName("gpuRenderer")] string GpuRenderer)
    {
        public NavigatorSample()
            : this(
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                Array.Empty<string>(),
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                new NavigatorConnectionSample(),
                string.Empty,
                string.Empty)
        {
        }
    }

    private sealed record NavigatorConnectionSample(
        [property: JsonPropertyName("effectiveType")] string EffectiveType,
        [property: JsonPropertyName("downlink")] double Downlink,
        [property: JsonPropertyName("rtt")] double Rtt,
        [property: JsonPropertyName("saveData")] bool SaveData)
    {
        public NavigatorConnectionSample() : this(string.Empty, 0d, 0d, false)
        {
        }
    }
}
