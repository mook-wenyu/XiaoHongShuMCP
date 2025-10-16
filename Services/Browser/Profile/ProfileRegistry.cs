using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Profile;

/// <summary>
/// Profile 注册表（storage/profiles/registry.json）。
/// </summary>
public sealed class ProfileRegistry
{
    private readonly string _rootDir;
    private readonly string _registryPath;
    private readonly object _gate = new();
    private Dictionary<string, ProfileRecord> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProfileRegistry(string repoRoot)
    {
        _rootDir = Path.Combine(repoRoot, "storage", "profiles");
        _registryPath = Path.Combine(_rootDir, "registry.json");
    }

    public string GetUserDataDir(string profileKey)
        => Path.Combine(_rootDir, profileKey);

    public ProfileRecord? TryGet(string profileKey)
    {
        EnsureLoaded();
        return _cache.TryGetValue(profileKey, out var rec) ? rec : null;
    }

    public void AddOrUpdate(ProfileRecord record)
    {
        EnsureLoaded();
        lock (_gate)
        {
            if (_cache.TryGetValue(record.ProfileKey, out var existing))
            {
                // 黏性代理不可变：一旦分配，不允许在原 Profile 上更改到不同端点
                if (!string.IsNullOrWhiteSpace(existing.ProxyEndpoint) &&
                    !string.IsNullOrWhiteSpace(record.ProxyEndpoint) &&
                    !string.Equals(existing.ProxyEndpoint, record.ProxyEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Profile '{record.ProfileKey}' 已绑定黏性代理，禁止修改到新的端点。请新建 Profile。");
                }

                // 区域不可变：一旦一次定型，禁止跨地域修改
                if (!string.Equals(existing.Region ?? string.Empty, record.Region ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Profile '{record.ProfileKey}' 区域已固定为 '{existing.Region}'，禁止修改为 '{record.Region}'。请新建 Profile。");
                }
            }

            _cache[record.ProfileKey] = record;
            Persist();
        }
    }

    private void EnsureLoaded()
    {
        if (_cache.Count > 0) return;
        lock (_gate)
        {
            Directory.CreateDirectory(_rootDir);
            if (!File.Exists(_registryPath))
            {
                _cache = new Dictionary<string, ProfileRecord>(StringComparer.OrdinalIgnoreCase);
                Persist();
                return;
            }

            var json = File.ReadAllText(_registryPath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            _cache = JsonSerializer.Deserialize<Dictionary<string, ProfileRecord>>(json, options)
                     ?? new Dictionary<string, ProfileRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(_cache, options);
        File.WriteAllText(_registryPath, json);
    }
}