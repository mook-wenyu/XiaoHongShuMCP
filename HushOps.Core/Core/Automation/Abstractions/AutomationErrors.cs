using System;

namespace HushOps.Core.Automation.Abstractions;

/// <summary>
/// 自动化统一错误模型（中文注释）：
/// - 目的：将不同驱动的异常（如 Playwright、CDP 等）统一为稳定的领域错误，便于上层重试与审计。
/// - 分类：可重试（Retryable）与不可重试（NonRetryable），并附带建议退避时间。
/// </summary>
public abstract class AutomationError : Exception
{
    /// <summary>是否建议进行自动重试。</summary>
    public bool IsRetryable { get; }

    /// <summary>建议的退避时间（毫秒）。</summary>
    public int SuggestedBackoffMs { get; }

    protected AutomationError(string message, bool retryable, int suggestedBackoffMs, Exception? inner = null)
        : base(message, inner)
    {
        IsRetryable = retryable;
        SuggestedBackoffMs = suggestedBackoffMs;
    }
}

/// <summary>页面加载或导航相关错误（多为可重试）。</summary>
public sealed class NavigationError : AutomationError
{
    public NavigationError(string message, int backoffMs = 1000, Exception? inner = null)
        : base(message, retryable: true, suggestedBackoffMs: backoffMs, inner) { }
}

/// <summary>选择器/元素查找失败错误。</summary>
public sealed class SelectorNotFoundError : AutomationError
{
    public string Selector { get; }
    public SelectorNotFoundError(string selector, string message, int backoffMs = 300, Exception? inner = null)
        : base(message, retryable: true, suggestedBackoffMs: backoffMs, inner)
    {
        Selector = selector;
    }
}

/// <summary>点击/输入等交互失败错误。</summary>
public sealed class InteractionError : AutomationError
{
    public InteractionError(string message, int backoffMs = 300, Exception? inner = null)
        : base(message, retryable: true, suggestedBackoffMs: backoffMs, inner) { }
}

/// <summary>网络层/代理等错误，可重试或不可重试取决于具体情况。</summary>
public sealed class NetworkError : AutomationError
{
    public NetworkError(string message, bool retryable = true, int backoffMs = 1000, Exception? inner = null)
        : base(message, retryable, backoffMs, inner) { }
}
