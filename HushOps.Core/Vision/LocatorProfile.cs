using System;
using System.Text.Json.Serialization;

namespace HushOps.Core.Vision;

/// <summary>
/// 中文：视觉定位配置文件的强类型表示，描述某一业务意图的检测策略。
/// </summary>
public sealed class LocatorProfile
{
    /// <summary>定位标识符，需与业务侧别名一一对应。</summary>
    [JsonPropertyName("locatorId")]
    public string LocatorId { get; init; } = string.Empty;

    /// <summary>策略用途说明，便于审计与回溯。</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>配置版本号，破坏性变更需递增。</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>主定位策略（模板、DNN 等）。</summary>
    [JsonPropertyName("strategy")]
    public LocatorStrategy Strategy { get; init; } = LocatorStrategy.Default;

    /// <summary>可选：限制搜索区域，加速匹配并避免误报。</summary>
    [JsonPropertyName("roi")]
    public RegionOfInterest Roi { get; init; } = RegionOfInterest.Default;

    /// <summary>后处理参数，用于候选抑制与排序。</summary>
    [JsonPropertyName("postProcessing")]
    public PostProcessingConfig PostProcessing { get; init; } = PostProcessingConfig.Default;

    /// <summary>元数据（创建时间、来源等），支持审计。</summary>
    [JsonPropertyName("metadata")]
    public LocatorMetadata Metadata { get; init; } = LocatorMetadata.Default;

    /// <summary>
    /// 中文：验证配置是否完整有效，发现异常直接抛出，以阻止不一致配置上线。
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LocatorId))
        {
            throw new InvalidOperationException("视觉定位配置缺少 locatorId，不允许加载。");
        }

        if (Version <= 0)
        {
            throw new InvalidOperationException($"视觉定位配置 {LocatorId} 的 version 必须为正整数。");
        }

        Strategy.Validate($"locator:{LocatorId}");
        Roi.Validate($"locator:{LocatorId}");
        PostProcessing.Validate($"locator:{LocatorId}");
    }
}

/// <summary>中文：定位策略类型。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LocatorStrategyType
{
    /// <summary>模板匹配（OpenCV MatchTemplate）。</summary>
    Template,

    /// <summary>基于 DNN 的通用检测（如 YOLO/ONNX）。</summary>
    Dnn
}

/// <summary>中文：定位策略定义。</summary>
public sealed class LocatorStrategy
{
    public static LocatorStrategy Default { get; } = new();

    [JsonPropertyName("type")]
    public LocatorStrategyType Type { get; init; } = LocatorStrategyType.Template;

    [JsonPropertyName("templateId")]
    public string? TemplateId { get; init; }
        = null;

    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }
        = null;

    [JsonPropertyName("matchThreshold")]
    public double MatchThreshold { get; init; } = 0.9d;

    [JsonPropertyName("scoreThreshold")]
    public double ScoreThreshold { get; init; } = 0.35d;

    [JsonPropertyName("preprocess")]
    public PreprocessOptions Preprocess { get; init; } = PreprocessOptions.Default;

    public void Validate(string scope)
    {
        if (Type == LocatorStrategyType.Template)
        {
            if (string.IsNullOrWhiteSpace(TemplateId))
            {
                throw new InvalidOperationException($"{scope} 模板策略缺少 templateId。");
            }

            if (MatchThreshold is < 0.5 or > 0.999)
            {
                throw new InvalidOperationException($"{scope} 模板匹配阈值需位于 [0.5,0.999] 区间。");
            }
        }

        if (Type == LocatorStrategyType.Dnn)
        {
            if (string.IsNullOrWhiteSpace(ModelId))
            {
                throw new InvalidOperationException($"{scope} DNN 策略缺少 modelId。");
            }

            if (ScoreThreshold is < 0.05 or > 0.99)
            {
                throw new InvalidOperationException($"{scope} DNN 置信度阈值需位于 [0.05,0.99] 区间。");
            }
        }

        Preprocess.Validate(scope);
    }
}

/// <summary>中文：预处理配置，控制灰度化、模糊等步骤。</summary>
public sealed class PreprocessOptions
{
    public static PreprocessOptions Default { get; } = new();

    [JsonPropertyName("grayscale")]
    public bool Grayscale { get; init; }
        = true;

    [JsonPropertyName("blurKernel")]
    public int BlurKernel { get; init; } = 1;

    [JsonPropertyName("cannyThreshold1")]
    public double? CannyThreshold1 { get; init; }
        = null;

    [JsonPropertyName("cannyThreshold2")]
    public double? CannyThreshold2 { get; init; }
        = null;

    public void Validate(string scope)
    {
        if (BlurKernel < 1)
        {
            throw new InvalidOperationException($"{scope} 预处理模糊核大小必须 ≥ 1。");
        }

        if (CannyThreshold1 is < 0)
        {
            throw new InvalidOperationException($"{scope} Canny 阈值 1 不可为负数。");
        }

        if (CannyThreshold2 is < 0)
        {
            throw new InvalidOperationException($"{scope} Canny 阈值 2 不可为负数。");
        }

        if (CannyThreshold1.HasValue && CannyThreshold2.HasValue && CannyThreshold1 > CannyThreshold2)
        {
            throw new InvalidOperationException($"{scope} Canny 阈值配置应满足 threshold1 ≤ threshold2。");
        }
    }
}

/// <summary>中文：搜索区域定义。</summary>
public sealed class RegionOfInterest
{
    public static RegionOfInterest Default { get; } = new();

    [JsonPropertyName("x")]
    public int X { get; init; } = 0;

    [JsonPropertyName("y")]
    public int Y { get; init; } = 0;

    [JsonPropertyName("width")]
    public int Width { get; init; } = 0;

    [JsonPropertyName("height")]
    public int Height { get; init; } = 0;

    public void Validate(string scope)
    {
        if (Width < 0 || Height < 0)
        {
            throw new InvalidOperationException($"{scope} ROI 宽高必须为非负数。");
        }
    }
}

/// <summary>中文：后处理参数。</summary>
public sealed class PostProcessingConfig
{
    public static PostProcessingConfig Default { get; } = new();

    [JsonPropertyName("maxCandidates")]
    public int MaxCandidates { get; init; } = 3;

    [JsonPropertyName("scoreSuppression")]
    public double ScoreSuppression { get; init; } = 0.1d;

    public void Validate(string scope)
    {
        if (MaxCandidates <= 0)
        {
            throw new InvalidOperationException($"{scope} 后处理候选数量必须大于 0。");
        }

        if (ScoreSuppression is < 0 or > 1)
        {
            throw new InvalidOperationException($"{scope} 后处理分数抑制系数需位于 [0,1] 区间。");
        }
    }
}

/// <summary>中文：元数据字段。</summary>
public sealed class LocatorMetadata
{
    public static LocatorMetadata Default { get; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UnixEpoch;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UnixEpoch;

    [JsonPropertyName("source")]
    public string Source { get; init; } = "unknown";
}
