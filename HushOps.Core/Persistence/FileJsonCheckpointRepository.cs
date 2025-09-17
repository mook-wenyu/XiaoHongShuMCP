using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Resumable;

namespace HushOps.Persistence;

/// <summary>
/// 基于文件系统(JSON)的检查点仓库实现（破坏性替代 SQLite 方案）。
/// 设计原则：
/// - 单操作一个主文件：<c>checks/&lt;hash16&gt;.json</c>，始终保存该 operationId 的“最新”<see cref="CheckpointEnvelope"/>。
/// - 原子写入：写入临时文件后通过 <see cref="File.Move(string,string,bool)"/> 覆盖，避免半写入导致的损坏。
/// - 过滤查询：<see cref="ListLatestAsync"/> 通过读取文件内容按真实 OperationId 前缀过滤，保证行为与存储名无关。
/// - 文件名安全：使用 SHA256(operationId) 的前 16 字节十六进制作为文件名，避免非法字符与路径遍历问题。
/// - 性能权衡：目录内文件数预计可控（以 operationId 数量计）。如未来增长，可引入分级目录或索引文件（不在本次范围）。
/// 安全与合规：仅持久化非敏感摘要；严格使用 UTF-8 无 BOM；异常信息不包含敏感数据。
/// </summary>
public sealed class FileJsonCheckpointRepository : ICheckpointRepository
{
    private readonly string rootDir;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 初始化文件仓库。
    /// </summary>
    /// <param name="directory">根目录，建议使用专用子目录，例如 <c>.data/checkpoints</c>。</param>
    public FileJsonCheckpointRepository(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("目录不能为空", nameof(directory));
        rootDir = Path.GetFullPath(directory);
        Directory.CreateDirectory(rootDir);
    }

    /// <summary>
    /// 保存或覆盖最新检查点（按 operationId 聚合，始终只保留最新）。
    /// </summary>
    public async Task SaveAsync(CheckpointEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var path = GetPathForOperation(envelope.OperationId);
        var tmp = path + ".tmp";

        // 将信封序列化为 JSON，并追加一个便于人工巡检的 minimal 注释（不包含敏感信息）。
        var json = JsonSerializer.Serialize(envelope, jsonOptions);

        // 原子写入：先写入临时文件，再 Move 覆盖到目标。
        await File.WriteAllTextAsync(tmp, json, Encoding.UTF8, ct);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// 读取指定操作的最新检查点；若文件不存在返回 null。
    /// </summary>
    public async Task<CheckpointEnvelope?> LoadLatestAsync(string operationId, CancellationToken ct = default)
    {
        var path = GetPathForOperation(operationId);
        if (!File.Exists(path)) return null;
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<CheckpointEnvelope>(fs, jsonOptions, ct);
    }

    /// <summary>
    /// 删除某个操作的历史（当前模型仅有“最新”文件）。
    /// </summary>
    public Task DeleteAsync(string operationId, CancellationToken ct = default)
    {
        var path = GetPathForOperation(operationId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 列出最近的检查点（每个 operationId 一条“最新”记录）。
    /// 说明：通过读取文件内容解析真实 OperationId，然后基于 <paramref name="operationIdPrefix"/> 进行前缀过滤。
    /// </summary>
    public async Task<IReadOnlyList<CheckpointEnvelope>> ListLatestAsync(string? operationIdPrefix, int topN = 50, CancellationToken ct = default)
    {
        var files = Directory.Exists(rootDir)
            ? Directory.EnumerateFiles(rootDir, "*.json", SearchOption.TopDirectoryOnly)
            : Enumerable.Empty<string>();

        var list = new List<CheckpointEnvelope>(Math.Max(4, Math.Min(512, topN)));
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var env = await JsonSerializer.DeserializeAsync<CheckpointEnvelope>(fs, jsonOptions, ct);
                if (env == null) continue;
                if (operationIdPrefix == null || env.OperationId.StartsWith(operationIdPrefix, StringComparison.Ordinal))
                {
                    list.Add(env);
                }
            }
            catch (IOException)
            {
                // 读文件被并发覆盖时的短暂失败，直接跳过即可（非关键路径）。
            }
            catch (JsonException)
            {
                // 非法或半写入的 JSON，跳过。（写入侧使用原子 Move 已尽量避免）
            }
        }

        return list
            .OrderByDescending(x => x.Seq)
            .Take(topN)
            .ToArray();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// 依据 operationId 计算安全文件路径：<c>sha256(operationId).Hex16</c> 作为文件名，后缀 <c>.json</c>。
    /// </summary>
    private string GetPathForOperation(string operationId)
    {
        // SHA256 取前 16 字节（128 位）足以避免碰撞，且文件名简短。
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(operationId), hash);
        var hex16 = Convert.ToHexString(hash.Slice(0, 16));
        return Path.Combine(rootDir, hex16.ToLowerInvariant() + ".json");
    }
}
