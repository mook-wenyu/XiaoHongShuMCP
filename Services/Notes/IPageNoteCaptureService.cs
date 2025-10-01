using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Servers.XiaoHongShu.Services.Notes;

/// <summary>
/// 中文：页面笔记采集服务接口，从当前浏览器页面提取笔记数据。
/// English: Service interface for capturing notes from the current browser page DOM.
/// </summary>
public interface IPageNoteCaptureService
{
    /// <summary>
    /// 中文：从当前页面采集指定数量的笔记并导出为 CSV。
    /// English: Captures specified number of notes from current page and exports to CSV.
    /// </summary>
    /// <param name="context">采集上下文，包含目标数量和输出配置 | Capture context with target count and output config.</param>
    /// <param name="cancellationToken">取消令牌 | Cancellation token.</param>
    /// <returns>采集结果，包含 CSV 路径和采集数量 | Capture result with CSV path and collected count.</returns>
    Task<PageNoteCaptureResult> CaptureAsync(PageNoteCaptureContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 中文：页面笔记采集上下文。
/// English: Context for page note capture operation.
/// </summary>
/// <param name="BrowserKey">浏览器配置键 | Browser profile key.</param>
/// <param name="TargetCount">目标采集数量 | Target number of notes to collect.</param>
/// <param name="OutputDirectory">输出目录 | Output directory for CSV file.</param>
public sealed record PageNoteCaptureContext(
    string BrowserKey,
    int TargetCount,
    string OutputDirectory);

/// <summary>
/// 中文：页面笔记采集结果。
/// English: Result of page note capture operation.
/// </summary>
/// <param name="CsvPath">CSV 文件路径 | CSV file path.</param>
/// <param name="CollectedCount">实际采集数量 | Actual number of notes collected.</param>
/// <param name="Metadata">元数据 | Metadata.</param>
public sealed record PageNoteCaptureResult(
    string CsvPath,
    int CollectedCount,
    IReadOnlyDictionary<string, string> Metadata);
