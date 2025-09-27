using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;

/// <summary>
/// 中文：描述指纹上下文的模型。
/// English: Represents a generated fingerprint context for the browser session.
/// </summary>
public sealed record FingerprintContext(
    string Hash,
    string UserAgent,
    string Timezone,
    string Language,
    int ViewportWidth,
    int ViewportHeight,
    double DeviceScaleFactor,
    bool IsMobile,
    bool HasTouch,
    bool CanvasNoise,
    bool WebglMask,
    IReadOnlyDictionary<string, string> ExtraHeaders);

public interface IProfileFingerprintManager
{
    Task<FingerprintContext> GenerateAsync(string profileKey, CancellationToken cancellationToken);
}

/// <summary>
/// 中文：基于配置模板计算指纹哈希的默认实现。
/// English: Default implementation that derives fingerprint metadata from configured templates.
/// </summary>
public sealed class ProfileFingerprintManager : IProfileFingerprintManager
{
    private readonly FingerprintOptions _options;
    private readonly ILogger<ProfileFingerprintManager> _logger;

    public ProfileFingerprintManager(IOptions<FingerprintOptions> options, ILogger<ProfileFingerprintManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<FingerprintContext> GenerateAsync(string profileKey, CancellationToken cancellationToken)
    {
        var template = ResolveTemplate(profileKey);
        var hash = ComputeHash(profileKey, template);

        _logger.LogDebug(
            "[Fingerprint] profile={Profile} template={Template} hash={Hash}",
            profileKey,
            template.UserAgent,
            hash);

        return Task.FromResult(new FingerprintContext(
            hash,
            template.UserAgent,
            template.Timezone,
            template.Language,
            template.ViewportWidth,
            template.ViewportHeight,
            template.DeviceScaleFactor,
            template.IsMobile,
            template.HasTouch,
            template.CanvasNoise,
            template.WebglMask,
            new Dictionary<string, string>(template.ExtraHeaders, StringComparer.OrdinalIgnoreCase)));
    }

    private FingerprintTemplateOptions ResolveTemplate(string profileKey)
    {
        if (_options.Templates.TryGetValue(profileKey, out var template))
        {
            return template;
        }

        if (_options.Templates.TryGetValue(_options.DefaultTemplate, out var fallback))
        {
            return fallback;
        }

        return FingerprintTemplateOptions.CreateDefault();
    }

    private static string ComputeHash(string profileKey, FingerprintTemplateOptions template)
    {
        var payload = string.Join("|", profileKey, template.UserAgent, template.Timezone, template.Language, template.CanvasNoise, template.WebglMask);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
