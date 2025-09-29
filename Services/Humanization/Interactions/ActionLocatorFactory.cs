using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：辅助生成 <see cref="ActionLocator"/>，将字符串角色转换为枚举并记录日志。
/// </summary>
public sealed class ActionLocatorFactory
{
    private readonly ILogger<ActionLocatorFactory> _logger;

    public ActionLocatorFactory(ILogger<ActionLocatorFactory> logger)
    {
        _logger = logger;
    }

    public ActionLocator Create(
        string? role = null,
        string? text = null,
        string? label = null,
        string? placeholder = null,
        string? altText = null,
        string? title = null,
        string? testId = null)
    {
        AriaRole? parsedRole = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (Enum.TryParse(role, true, out AriaRole ariaRole))
            {
                parsedRole = ariaRole;
            }
            else
            {
                _logger.LogDebug("[ActionLocatorFactory] 无法解析的角色：{Role}", role);
            }
        }

        return new ActionLocator(
            parsedRole,
            text,
            label,
            placeholder,
            altText,
            title,
            testId);
    }
}
