using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Persistence;

/// <summary>
/// JSON 本地存储实现，负责统一的目录管理、原子落盘与校验和计算。
/// </summary>
public sealed class JsonLocalStore : IJsonLocalStore
{
    private readonly JsonLocalStoreOptions options;
    private readonly JsonSerializerOptions serializer;

    /// <summary>
    /// 使用指定配置构造 JSON 存储。
    /// </summary>
    public JsonLocalStore(JsonLocalStoreOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.RootDirectory))
        {
            throw new ArgumentException("根目录不能为空", nameof(options));
        }

        RootDirectory = Path.GetFullPath(options.RootDirectory);
        Directory.CreateDirectory(RootDirectory);

        serializer = options.SerializerOptions != null
            ? new JsonSerializerOptions(options.SerializerOptions)
            : new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializer.WriteIndented = options.WriteIndented;
    }

    /// <inheritdoc />
    public string RootDirectory { get; }

    /// <inheritdoc />
    public async Task<JsonStoreEntry> SaveAsync<T>(string relativePath, T payload, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var json = JsonSerializer.Serialize(payload, serializer);
        var bytes = Encoding.UTF8.GetBytes(json);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));

        var tempFile = fullPath + $".tmp-{Guid.NewGuid():N}";
        await File.WriteAllBytesAsync(tempFile, bytes, ct).ConfigureAwait(false);
        File.Move(tempFile, fullPath, true);

        var info = new FileInfo(fullPath);
        return new JsonStoreEntry(
            RelativePath: GetRelativeFromRoot(fullPath),
            FullPath: fullPath,
            SavedAtUtc: DateTime.UtcNow,
            ChecksumSha256: checksum,
            ContentLengthBytes: info.Length);
    }

    /// <inheritdoc />
    public async Task<T?> LoadAsync<T>(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath)) return default;

        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<T>(stream, serializer, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath)) return Task.FromResult(false);

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JsonStoreEntry>> ListAsync(string? relativeDirectory = null, CancellationToken ct = default)
    {
        var directory = string.IsNullOrWhiteSpace(relativeDirectory)
            ? RootDirectory
            : ResolveDirectory(relativeDirectory!);

        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<JsonStoreEntry>>(Array.Empty<JsonStoreEntry>());
        }

        var entries = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Select(fullPath =>
            {
                var info = new FileInfo(fullPath);
                return new JsonStoreEntry(
                    RelativePath: GetRelativeFromRoot(fullPath),
                    FullPath: fullPath,
                    SavedAtUtc: info.LastWriteTimeUtc,
                    ChecksumSha256: string.Empty,
                    ContentLengthBytes: info.Length);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<JsonStoreEntry>>(entries);
    }

    /// <summary>
    /// 归一化路径并返回根目录内的绝对路径。
    /// </summary>
    private string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("路径不能为空", nameof(relativePath));
        }

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace("\\", Path.DirectorySeparatorChar.ToString());

        var fullPath = Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(RootDirectory, normalized));

        if (!fullPath.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"路径 {relativePath} 不在存储根目录内");
        }

        return fullPath;
    }

    private string ResolveDirectory(string relativeDirectory)
    {
        var path = ResolvePath(relativeDirectory);
        Directory.CreateDirectory(path);
        return path;
    }

    private string GetRelativeFromRoot(string fullPath)
        => Path.GetRelativePath(RootDirectory, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
}
