using System;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.Extensions.Options;
using HushOps.Servers.XiaoHongShu.Configuration;

namespace HushOps.Servers.XiaoHongShu.Diagnostics;

/// <summary>
/// 中文：用于验证指纹与网络策略的示例流程。
/// English: Demonstrates fingerprint/network integration in a controlled scenario.
/// </summary>
public sealed class VerificationScenarioRunner
{
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly IPlaywrightSessionManager _sessionManager;
    private readonly IProfileFingerprintManager _fingerprintManager;
    private readonly INetworkStrategyManager _networkStrategyManager;
    private readonly ILogger<VerificationScenarioRunner> _logger;
    private readonly VerificationOptions _options;

    public VerificationScenarioRunner(
        IBrowserAutomationService browserAutomation,
        IPlaywrightSessionManager sessionManager,
        IProfileFingerprintManager fingerprintManager,
        INetworkStrategyManager networkStrategyManager,
        IOptions<VerificationOptions> options,
        ILogger<VerificationScenarioRunner> logger)
    {
        _browserAutomation = browserAutomation;
        _sessionManager = sessionManager;
        _fingerprintManager = fingerprintManager;
        _networkStrategyManager = networkStrategyManager;
        _options = options?.Value ?? new VerificationOptions();
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Verification] Starting sample scenario...");

        var profile = await _browserAutomation.EnsureProfileAsync(BrowserOpenRequest.UserProfileKey, null, cancellationToken).ConfigureAwait(false);

        var fingerprint = await _fingerprintManager.GenerateAsync(profile.ProfileKey, cancellationToken).ConfigureAwait(false);
        var network = await _networkStrategyManager.PrepareSessionAsync(profile.ProfileKey, cancellationToken).ConfigureAwait(false);
        var session = await _sessionManager.EnsureSessionAsync(profile, network, fingerprint, cancellationToken).ConfigureAwait(false);

        var page = await session.Context.NewPageAsync().ConfigureAwait(false);

        await page.GotoAsync("https://example.com", new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
        _logger.LogInformation("[Verification] Loaded example.com");

        var statusUrl = string.IsNullOrWhiteSpace(_options.StatusUrl)
            ? "https://httpbin.org/status/429"
            : _options.StatusUrl.Trim();

        Func<IRoute, Task>? mockHandler = null;
        if (_options.MockStatusCode.HasValue)
        {
            mockHandler = async route =>
            {
                if (!string.Equals(route.Request.Url, statusUrl, StringComparison.OrdinalIgnoreCase))
                {
                    await route.ContinueAsync().ConfigureAwait(false);
                    return;
                }

                await route.FulfillAsync(new()
                {
                    Status = _options.MockStatusCode.Value,
                    ContentType = "text/plain",
                    Body = $"Mocked status {_options.MockStatusCode.Value}"
                }).ConfigureAwait(false);
                await page.UnrouteAsync(statusUrl, mockHandler!).ConfigureAwait(false);
            };

            await page.RouteAsync(statusUrl, mockHandler).ConfigureAwait(false);
        }

        var statusRequestSucceeded = false;

        try
        {
            await page.GotoAsync(statusUrl, new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
            _logger.LogInformation("[Verification] Triggered status endpoint {StatusUrl} to test mitigation.", statusUrl);
            statusRequestSucceeded = true;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogWarning(ex, "[Verification] Failed to reach status endpoint {StatusUrl}.", statusUrl);

            var fallbackStatus = _options.MockStatusCode ?? 429;
            mockHandler ??= async route =>
            {
                if (!string.Equals(route.Request.Url, statusUrl, StringComparison.OrdinalIgnoreCase))
                {
                    await route.ContinueAsync().ConfigureAwait(false);
                    return;
                }

                await route.FulfillAsync(new()
                {
                    Status = fallbackStatus,
                    ContentType = "text/plain",
                    Body = $"Mocked status {fallbackStatus}"
                }).ConfigureAwait(false);
                await page.UnrouteAsync(statusUrl, mockHandler!).ConfigureAwait(false);
            };

            await page.RouteAsync(statusUrl, mockHandler).ConfigureAwait(false);

            await page.GotoAsync(statusUrl, new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
            _logger.LogInformation("[Verification] Mocked status endpoint {StatusUrl} with HTTP {Status}.", statusUrl, fallbackStatus);
            statusRequestSucceeded = true;
        }

        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        var mitigationCount = _networkStrategyManager.GetMitigationCount(profile.ProfileKey);
        if (statusRequestSucceeded)
        {
            _logger.LogInformation("[Verification] Mitigation count for profile {Profile} = {Count}", profile.ProfileKey, mitigationCount);
        }
        else
        {
            _logger.LogInformation("[Verification] Mitigation count unavailable because status endpoint was unreachable for profile {Profile}.", profile.ProfileKey);
        }

        _logger.LogInformation("[Verification] Scenario finished.");
    }
}
