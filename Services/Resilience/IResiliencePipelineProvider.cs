using Polly;

namespace HushOps.Servers.XiaoHongShu.Services.Resilience;

/// <summary>
/// 中文：弹性管道提供者接口，用于获取不同场景的弹性策略管道。
/// English: Resilience pipeline provider interface for obtaining resilience pipelines for different scenarios.
/// </summary>
public interface IResiliencePipelineProvider
{
    /// <summary>
    /// 中文：获取网络调用的弹性管道（包含 Retry + Circuit Breaker + Timeout）。
    /// English: Get resilience pipeline for network calls (includes Retry + Circuit Breaker + Timeout).
    /// </summary>
    ResiliencePipeline GetNetworkPipeline();

    /// <summary>
    /// 中文：获取浏览器操作的弹性管道（包含 Retry + Timeout）。
    /// English: Get resilience pipeline for browser operations (includes Retry + Timeout).
    /// </summary>
    ResiliencePipeline GetBrowserPipeline();

    /// <summary>
    /// 中文：获取数据访问的弹性管道（包含 Retry）。
    /// English: Get resilience pipeline for data access (includes Retry).
    /// </summary>
    ResiliencePipeline GetDataAccessPipeline();
}
