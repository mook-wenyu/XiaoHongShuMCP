using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using HushOps.Core.Vision;
using Microsoft.Extensions.Logging;

namespace HushOps.Core.Persistence;

/// <summary>
/// 中文：面向本地 JSON 的视觉资源注册表，实现模板/模型的加载、校验与缓存。
/// </summary>
public sealed class VisionModelRegistry : IVisionModelRegistry, IDisposable
{
    private readonly string rootDir;
    private readonly string indexPath;
    private readonly JsonSerializerOptions options;
    private readonly ILogger<VisionModelRegistry>? logger;
    private readonly ReaderWriterLockSlim gate = new(LockRecursionPolicy.NoRecursion);

    private Dictionary<string, LocatorProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, VisionTemplateAsset> templates = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, VisionModelAsset> models = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 中文：初始化注册表，并立即加载一次索引文件。
    /// </summary>
    /// <param name="rootDirectory">视觉资源根目录（通常为 profiles/vision）。</param>
    /// <param name="logger">可选日志记录器。</param>
    public VisionModelRegistry(string rootDirectory, ILogger<VisionModelRegistry>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("视觉资源根目录不能为空。", nameof(rootDirectory));
        }

        rootDir = Path.GetFullPath(rootDirectory);
        indexPath = Path.Combine(rootDir, "index.json");
        this.logger = logger;

        options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        Directory.CreateDirectory(rootDir);
        Reload();
    }

    /// <inheritdoc />
    public LocatorProfile GetProfile(string locatorId)
    {
        if (!TryGetProfile(locatorId, out var profile) || profile is null)
        {
            throw new KeyNotFoundException($"未找到视觉定位配置：{locatorId}");
        }

        return profile;
    }

    /// <inheritdoc />
    public bool TryGetProfile(string locatorId, out LocatorProfile? profile)
    {
        gate.EnterReadLock();
        try
        {
            return profiles.TryGetValue(locatorId, out profile);
        }
        finally
        {
            gate.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public VisionTemplateAsset GetTemplate(string templateId)
    {
        if (!TryGetTemplate(templateId, out var template) || template is null)
        {
            throw new KeyNotFoundException($"未找到视觉模板：{templateId}");
        }

        return template;
    }

    /// <inheritdoc />
    public bool TryGetTemplate(string templateId, out VisionTemplateAsset? template)
    {
        gate.EnterReadLock();
        try
        {
            return templates.TryGetValue(templateId, out template);
        }
        finally
        {
            gate.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public VisionModelAsset GetModel(string modelId)
    {
        if (!TryGetModel(modelId, out var model) || model is null)
        {
            throw new KeyNotFoundException($"未找到视觉模型：{modelId}");
        }

        return model;
    }

    /// <inheritdoc />
    public bool TryGetModel(string modelId, out VisionModelAsset? model)
    {
        gate.EnterReadLock();
        try
        {
            return models.TryGetValue(modelId, out model);
        }
        finally
        {
            gate.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<LocatorProfile> ListProfiles()
    {
        gate.EnterReadLock();
        try
        {
            return profiles.Values.ToArray();
        }
        finally
        {
            gate.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public Stream OpenTemplateStream(string templateId)
    {
        var template = GetTemplate(templateId);
        return new FileStream(template.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <inheritdoc />
    public void Reload()
    {
        gate.EnterWriteLock();
        try
        {
            if (!File.Exists(indexPath))
            {
                throw new FileNotFoundException($"视觉索引未找到：{indexPath}");
            }

            using var fs = File.OpenRead(indexPath);
            var index = JsonSerializer.Deserialize<VisionIndexDocument>(fs, options)
                        ?? throw new InvalidOperationException("视觉索引文件格式错误，无法反序列化。");

            var newProfiles = new Dictionary<string, LocatorProfile>(StringComparer.OrdinalIgnoreCase);
            var newTemplates = new Dictionary<string, VisionTemplateAsset>(StringComparer.OrdinalIgnoreCase);
            var newModels = new Dictionary<string, VisionModelAsset>(StringComparer.OrdinalIgnoreCase);

            foreach (var tpl in index.Templates)
            {
                var fullPath = ResolveRelativePath(tpl.File);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"模板文件缺失：{fullPath}");
                }

                var hash = ComputeSha256(fullPath).ToLowerInvariant();
                if (!hash.Equals(tpl.HashSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"模板 {tpl.Id} 的 SHA256 与 index 不一致，实际 {hash}，索引 {tpl.HashSha256}");
                }

                var (width, height) = TryReadPngSize(fullPath, tpl.Format);

                var asset = new VisionTemplateAsset(
                    tpl.Id,
                    fullPath,
                    hash,
                    width,
                    height,
                    tpl.Format,
                    tpl.Source ?? "unknown");

                newTemplates.Add(tpl.Id, asset);
            }

            foreach (var model in index.Models)
            {
                var fullPath = ResolveRelativePath(model.File);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"模型文件缺失：{fullPath}");
                }

                var hash = ComputeSha256(fullPath).ToLowerInvariant();
                if (!hash.Equals(model.HashSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"模型 {model.Id} 的 SHA256 与 index 不一致，实际 {hash}，索引 {model.HashSha256}");
                }

                var asset = new VisionModelAsset(
                    model.Id,
                    fullPath,
                    hash,
                    model.Framework ?? "onnx",
                    model.Source ?? "unknown");

                newModels.Add(model.Id, asset);
            }

            foreach (var profileRef in index.Profiles)
            {
                var fullPath = ResolveRelativePath(profileRef.File);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"定位配置缺失：{fullPath}");
                }

                var profile = JsonSerializer.Deserialize<LocatorProfile>(File.ReadAllBytes(fullPath), options)
                              ?? throw new InvalidDataException($"定位配置反序列化失败：{profileRef.Id}");

                profile.Validate();
                if (!profile.LocatorId.Equals(profileRef.Id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"定位配置 {profileRef.Id} 的 locatorId 与索引不一致。");
                }

                newProfiles.Add(profile.LocatorId, profile);
            }

            profiles = newProfiles;
            templates = newTemplates;
            models = newModels;

            logger?.LogInformation("视觉注册表已加载：模板 {TemplateCount} 个，模型 {ModelCount} 个，配置 {ProfileCount} 条。",
                templates.Count, models.Count, profiles.Count);
        }
        finally
        {
            gate.ExitWriteLock();
        }
    }

    private string ResolveRelativePath(string file)
    {
        if (Path.IsPathRooted(file))
        {
            return Path.GetFullPath(file);
        }

        return Path.GetFullPath(Path.Combine(rootDir, file));
    }

    private static string ComputeSha256(string file)
    {
        using var stream = File.OpenRead(file);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static (int width, int height) TryReadPngSize(string file, string format)
    {
        if (!string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
        {
            // 目前仅对 PNG 做宽高校验；其他格式保持 0 表示未知。
            return (0, 0);
        }

        using var stream = File.OpenRead(file);
        Span<byte> header = stackalloc byte[24];
        var read = stream.Read(header);
        if (read < 24)
        {
            throw new InvalidDataException("PNG 文件头长度不足，无法读取宽高。");
        }

        // PNG 标准签名
        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (!header.Slice(0, 8).SequenceEqual(signature))
        {
            throw new InvalidDataException("模板文件并非有效的 PNG 格式。");
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        return (width, height);
    }

    /// <summary>
    /// 中文：释放锁资源。
    /// </summary>
    public void Dispose()
    {
        gate.Dispose();
    }

    private sealed class VisionIndexDocument
    {
        [JsonPropertyName("templates")]
        public VisionTemplateReference[] Templates { get; init; } = Array.Empty<VisionTemplateReference>();

        [JsonPropertyName("models")]
        public VisionModelReference[] Models { get; init; } = Array.Empty<VisionModelReference>();

        [JsonPropertyName("profiles")]
        public VisionProfileReference[] Profiles { get; init; } = Array.Empty<VisionProfileReference>();
    }

    private sealed class VisionTemplateReference
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("file")]
        public string File { get; init; } = string.Empty;

        [JsonPropertyName("hashSha256")]
        public string HashSha256 { get; init; } = string.Empty;

        [JsonPropertyName("width")]
        public int Width { get; init; }
            = 0;

        [JsonPropertyName("height")]
        public int Height { get; init; }
            = 0;

        [JsonPropertyName("format")]
        public string Format { get; init; } = "png";

        [JsonPropertyName("source")]
        public string? Source { get; init; }
            = null;
    }

    private sealed class VisionModelReference
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("file")]
        public string File { get; init; } = string.Empty;

        [JsonPropertyName("hashSha256")]
        public string HashSha256 { get; init; } = string.Empty;

        [JsonPropertyName("framework")]
        public string? Framework { get; init; }
            = null;

        [JsonPropertyName("source")]
        public string? Source { get; init; }
            = null;
    }

    private sealed class VisionProfileReference
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("file")]
        public string File { get; init; } = string.Empty;
    }
}
