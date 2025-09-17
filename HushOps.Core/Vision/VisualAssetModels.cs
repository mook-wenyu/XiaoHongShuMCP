using System;

namespace HushOps.Core.Vision;

/// <summary>
/// 中文：模板资源的元信息，用于驱动视觉定位流程。
/// </summary>
public sealed class VisionTemplateAsset
{
    public VisionTemplateAsset(string templateId, string filePath, string hashSha256, int width, int height, string format, string source)
    {
        TemplateId = templateId;
        FilePath = filePath;
        HashSha256 = hashSha256;
        Width = width;
        Height = height;
        Format = format;
        Source = source;
    }

    /// <summary>模板标识符（需与 index.json 对应）。</summary>
    public string TemplateId { get; }

    /// <summary>模板文件的绝对路径（UTF-8 文件）。</summary>
    public string FilePath { get; }

    /// <summary>SHA256 哈希（十六进制小写），用于完整性校验。</summary>
    public string HashSha256 { get; }

    /// <summary>图像宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>图像高度（像素）。</summary>
    public int Height { get; }

    /// <summary>文件格式（png/jpeg 等）。</summary>
    public string Format { get; }

    /// <summary>来源标记，便于审计。</summary>
    public string Source { get; }
}

/// <summary>
/// 中文：模型资源元数据（适用于 DNN/ONNX } 流程）。
/// </summary>
public sealed class VisionModelAsset
{
    public VisionModelAsset(string modelId, string filePath, string hashSha256, string framework, string source)
    {
        ModelId = modelId;
        FilePath = filePath;
        HashSha256 = hashSha256;
        Framework = framework;
        Source = source;
    }

    /// <summary>模型标识符。</summary>
    public string ModelId { get; }

    /// <summary>模型文件绝对路径。</summary>
    public string FilePath { get; }

    /// <summary>SHA256 哈希（十六进制小写）。</summary>
    public string HashSha256 { get; }

    /// <summary>模型框架/类型（例如 onnx/yolov8）。</summary>
    public string Framework { get; }

    /// <summary>来源标记。</summary>
    public string Source { get; }
}

/// <summary>
/// 中文：视觉定位候选信息（位置与置信度）。
/// </summary>
public sealed class VisualCandidate
{
    public VisualCandidate(string locatorId, double score, double x, double y, double width, double height)
    {
        LocatorId = locatorId;
        Score = score;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>对应的定位 ID。</summary>
    public string LocatorId { get; }

    /// <summary>匹配置信度。</summary>
    public double Score { get; }

    /// <summary>候选框左上角 X 像素坐标（相对 viewport）。</summary>
    public double X { get; }

    /// <summary>候选框左上角 Y 像素坐标。</summary>
    public double Y { get; }

    /// <summary>候选框宽度。</summary>
    public double Width { get; }

    /// <summary>候选框高度。</summary>
    public double Height { get; }

    /// <summary>计算候选中心点 X。</summary>
    public double CenterX => X + Width / 2d;

    /// <summary>计算候选中心点 Y。</summary>
    public double CenterY => Y + Height / 2d;
}
