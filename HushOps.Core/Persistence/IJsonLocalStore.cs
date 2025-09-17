using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Persistence;

/// <summary>
/// JSON 本地存储抽象，统一封装原子落盘、校验和与目录管理。
/// </summary>
public interface IJsonLocalStore
{
    /// <summary>根目录的绝对路径。</summary>
    string RootDirectory { get; }

    /// <summary>
    /// 保存指定对象为 JSON 文件，并返回写入元数据。
    /// </summary>
    Task<JsonStoreEntry> SaveAsync<T>(string relativePath, T payload, CancellationToken ct = default);

    /// <summary>
    /// 读取指定路径的 JSON 内容；如不存在则返回 <c>null</c>。
    /// </summary>
    Task<T?> LoadAsync<T>(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// 删除路径对应文件，返回是否删除成功。
    /// </summary>
    Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// 判断文件是否存在。
    /// </summary>
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// 列出目录下所有 JSON 存档（默认遍历根目录）。
    /// </summary>
    Task<IReadOnlyList<JsonStoreEntry>> ListAsync(string? relativeDirectory = null, CancellationToken ct = default);
}

/// <summary>
/// JSON 存储条目的元数据。
/// </summary>
public sealed record JsonStoreEntry(
    string RelativePath,
    string FullPath,
    DateTime SavedAtUtc,
    string ChecksumSha256,
    long ContentLengthBytes);

/// <summary>
/// JSON 本地存储选项。
/// </summary>
public sealed record JsonLocalStoreOptions(
    string RootDirectory,
    JsonSerializerOptions? SerializerOptions = null,
    bool WriteIndented = true);
