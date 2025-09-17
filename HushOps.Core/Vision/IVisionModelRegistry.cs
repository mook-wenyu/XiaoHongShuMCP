using System.Collections.Generic;
using System.IO;

namespace HushOps.Core.Vision;

/// <summary>
/// 中文：视觉模型/模板注册表，负责从本地 JSON 中加载并校验资源。
/// </summary>
public interface IVisionModelRegistry
{
    /// <summary>根据定位 ID 获取配置；若不存在抛出异常。</summary>
    LocatorProfile GetProfile(string locatorId);

    /// <summary>尝试获取配置。</summary>
    bool TryGetProfile(string locatorId, out LocatorProfile? profile);

    /// <summary>根据模板 ID 获取模板资源。</summary>
    VisionTemplateAsset GetTemplate(string templateId);

    /// <summary>尝试获取模板资源。</summary>
    bool TryGetTemplate(string templateId, out VisionTemplateAsset? template);

    /// <summary>根据模型 ID 获取模型资源（若存在）。</summary>
    VisionModelAsset GetModel(string modelId);

    /// <summary>尝试获取模型资源。</summary>
    bool TryGetModel(string modelId, out VisionModelAsset? model);

    /// <summary>列出当前所有定位配置。</summary>
    IReadOnlyCollection<LocatorProfile> ListProfiles();

    /// <summary>打开模板文件的只读流，由调用方负责释放。</summary>
    Stream OpenTemplateStream(string templateId);

    /// <summary>重新加载 index 与配置，确保热更新生效。</summary>
    void Reload();
}
