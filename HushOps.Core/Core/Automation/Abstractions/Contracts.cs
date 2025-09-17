using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Automation.Abstractions;

/// <summary>
/// 运行时入口：管理浏览器驱动与会话生命周期（中文注释）。
/// 说明：平台层只能依赖本抽象，不得直接依赖底层驱动（如 Microsoft.Playwright）。
/// </summary>
public interface IAutomationRuntime
{
    /// <summary>获取默认浏览器驱动。</summary>
    IBrowserDriver DefaultDriver { get; }
}

/// <summary>浏览器驱动接口：创建会话（包含浏览器/上下文）并提供资源清理。</summary>
public interface IBrowserDriver
{
    Task<IAutoSession> CreateSessionAsync(BrowserLaunchOptions options, CancellationToken ct = default);
}

/// <summary>浏览器会话：封装 Browser/Context 的生命周期。</summary>
public interface IAutoSession : IAsyncDisposable
{
    string SessionId { get; }
    Task<IAutoPage> NewPageAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IAutoPage>> GetPagesAsync(CancellationToken ct = default);
}

/// <summary>抽象页面对象。</summary>
public interface IAutoPage : IAsyncDisposable
{
    string PageId { get; }
    INavigator Navigator { get; }
    IInput Input { get; }
    /// <summary>
    /// 键盘抽象，封装按键与文本输入，避免直接依赖具体驱动的 Keyboard 对象。
    /// </summary>
    IKeyboard Keyboard { get; }
    /// <summary>
    /// 剪贴板抽象（可能受权限限制）。
    /// </summary>
    IClipboard Clipboard { get; }
    /// <summary>
    /// 文件选择/上传抽象，统一文件输入能力。
    /// </summary>
    IFilePicker FilePicker { get; }

    Task<string> ContentAsync(CancellationToken ct = default);
    /// <summary>
    /// 获取当前页面 URL（优先使用底层驱动能力，避免 JS Evaluate）。
    /// </summary>
    Task<string> GetUrlAsync(CancellationToken ct = default);
    /// <summary>
    /// 在页面上下文执行脚本并返回结果（与具体驱动无关）。
    /// 用途：读取 location/快速 DOM 判定/轻量属性查询等。
    /// </summary>
    Task<T> EvaluateAsync<T>(string script, CancellationToken ct = default);
    Task<IAutoElement?> QueryAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default);
    Task<IReadOnlyList<IAutoElement>> QueryAllAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default);
    Task MouseClickAsync(double x, double y, CancellationToken ct = default);
    /// <summary>
    /// 模拟鼠标移动到绝对坐标（像素，视口坐标系）。
    /// 说明：用于“人类化轨迹”按步移动指针，禁止在业务层通过 JS 注入移动。
    /// </summary>
    Task MouseMoveAsync(double x, double y, CancellationToken ct = default);
    /// <summary>
    /// 使用鼠标滚轮进行滚动（拟人交互，禁用 JS 注入）。
    /// 正值表示向下滚动，负值表示向上滚动；单位为像素近似。
    /// </summary>
    Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken ct = default);
}

/// <summary>抽象元素对象。</summary>
public interface IAutoElement
{
    Task ClickAsync(CancellationToken ct = default);
    Task TypeAsync(string text, CancellationToken ct = default);
    Task<bool> IsVisibleAsync(CancellationToken ct = default);
    Task HoverAsync(CancellationToken ct = default);
    Task ScrollIntoViewIfNeededAsync(CancellationToken ct = default);
    Task<T?> EvaluateAsync<T>(string script, CancellationToken ct = default);
    Task<BoundingBox?> GetBoundingBoxAsync(CancellationToken ct = default);
    Task<(double x, double y)?> GetCenterAsync(CancellationToken ct = default);
    /// <summary>读取元素属性（若不存在返回 null）。</summary>
    Task<string?> GetAttributeAsync(string name, CancellationToken ct = default);
    /// <summary>读取元素内联文本（去除首尾空白）。</summary>
    Task<string> InnerTextAsync(CancellationToken ct = default);
    /// <summary>读取元素标签名（小写）。</summary>
    Task<string> GetTagNameAsync(CancellationToken ct = default);
    // 已废弃：outerHTML 审计采样接口已与业务解耦，改由 Internal 审计服务提供；此处删除以收紧 API 面。
    /// <summary>在元素作用域内查找后代元素。</summary>
    Task<IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, CancellationToken ct = default);
    /// <summary>
    /// 可见性/遮挡/样式探针：在适配器层进行只读检测，业务层不执行 JS 评估。
    /// </summary>
    Task<ElementVisibilityProbe> ProbeVisibilityAsync(CancellationToken ct = default);

    /// <summary>
    /// 计算样式探针（只读）：在适配器内部以最小脚本评估 getComputedStyle，返回关键低基数字段。
    /// 说明：返回固定字段以控制指标与日志的基数；禁止返回大字段或完整样式表。
    /// </summary>
    Task<ElementComputedStyleProbe> GetComputedStyleProbeAsync(CancellationToken ct = default);

    /// <summary>
    /// 文本探针（只读）：一次性返回 innerText 与 textContent 的规整版本及长度信息，便于上层比较与策略判断。
    /// </summary>
    Task<ElementTextProbe> TextProbeAsync(CancellationToken ct = default);

    /// <summary>
    /// 可点击性探针（强类型聚合）：在适配器内部完成尺寸/样式/可见性/遮挡等判断的只读检测，
    /// 统一收敛脚本与 API 调用，避免业务层组合判断与脚本片段。
    /// </summary>
    Task<ElementClickabilityProbe> GetClickabilityProbeAsync(CancellationToken ct = default);
}

/// <summary>
/// 元素可见性探针结果（抽象），避免业务层直接脚本评估。
/// </summary>
public sealed class ElementVisibilityProbe
{
    /// <summary>是否位于视口范围内。</summary>
    public bool InViewport { get; init; }
    /// <summary>样式层面可见（visibility/display/opacity）。</summary>
    public bool VisibleByStyle { get; init; }
    /// <summary>pointer-events 是否允许。</summary>
    public bool PointerEventsEnabled { get; init; }
    /// <summary>中心点是否被其他元素遮挡。</summary>
    public bool CenterOccluded { get; init; }
}

/// <summary>
/// 计算样式探针（低基数字段）。
/// </summary>
public sealed class ElementComputedStyleProbe
{
    /// <summary>display 值（如 block/none 等）。</summary>
    public string? Display { get; set; }
    /// <summary>visibility 值（如 visible/hidden）。</summary>
    public string? Visibility { get; set; }
    /// <summary>pointer-events 值。</summary>
    public string? PointerEvents { get; set; }
    /// <summary>opacity 数值（0..1）。</summary>
    public double Opacity { get; set; }
    /// <summary>position（如 static/fixed/sticky 等）。</summary>
    public string? Position { get; set; }
    /// <summary>overflow-x。</summary>
    public string? OverflowX { get; set; }
    /// <summary>overflow-y。</summary>
    public string? OverflowY { get; set; }
}

/// <summary>
/// 文本探针：包含 innerText 与 textContent 的裁剪结果与长度，用于更鲁棒的文本判定。
/// </summary>
public sealed class ElementTextProbe
{
    /// <summary>innerText（已 Trim）。</summary>
    public string InnerText { get; set; } = string.Empty;
    /// <summary>textContent（已 Trim 与压缩空白）。</summary>
    public string TextContent { get; set; } = string.Empty;
    /// <summary>innerText 长度。</summary>
    public int InnerTextLength { get; set; }
    /// <summary>textContent 长度。</summary>
    public int TextContentLength { get; set; }
}

/// <summary>
/// 可点击性探针聚合结果：包含尺寸、样式、可见性、遮挡等关键维度与最终可点击性建议。
/// </summary>
public sealed class ElementClickabilityProbe
{
    /// <summary>是否存在有效尺寸（>0）。</summary>
    public bool HasBox { get; set; }
    /// <summary>宽度（像素）。</summary>
    public double Width { get; set; }
    /// <summary>高度（像素）。</summary>
    public double Height { get; set; }
    /// <summary>是否在视口内。</summary>
    public bool InViewport { get; set; }
    /// <summary>样式层面是否可见（display/visibility/opacity）。</summary>
    public bool VisibleByStyle { get; set; }
    /// <summary>pointer-events 是否允许。</summary>
    public bool PointerEventsEnabled { get; set; }
    /// <summary>中心点可能被遮挡。</summary>
    public bool CenterOccluded { get; set; }
    /// <summary>综合建议是否可点击（HasBox && InViewport && VisibleByStyle && PointerEventsEnabled && !CenterOccluded）。</summary>
    public bool Clickable { get; set; }
}

/// <summary>导航能力。</summary>
public interface INavigator
{
    Task GoToAsync(string url, PageGotoOptions? options = null, CancellationToken ct = default);
}

/// <summary>输入/交互能力（人类化可在适配器内实现）。</summary>
public interface IInput
{
    Task ClickAsync(string selector, CancellationToken ct = default);
    Task TypeAsync(string selector, string text, CancellationToken ct = default);
}

/// <summary>
/// 键盘抽象：用于模拟人类键盘操作（中文注释）。
/// 说明：上层统一使用该接口，而非直接引用具体驱动 API。
/// </summary>
public interface IKeyboard
{
    /// <summary>按序输入文本，支持可选延迟（毫秒）。</summary>
    Task TypeAsync(string text, int? delayMs = null, CancellationToken ct = default);
    /// <summary>按下组合键或单键，如 "Control+V"、"Enter"。</summary>
    Task PressAsync(string key, int? delayMs = null, CancellationToken ct = default);
}

/// <summary>
/// 剪贴板抽象：读写浏览器/系统剪贴板（可能受权限限制）。
/// </summary>
public interface IClipboard
{
    Task WriteTextAsync(string text, CancellationToken ct = default);
    Task<string> ReadTextAsync(CancellationToken ct = default);
}

/// <summary>
/// 文件选择器/上传抽象：统一文件上传能力，避免直接操作底层句柄。
/// </summary>
public interface IFilePicker
{
    /// <summary>
    /// 通过选择器定位 input[type=file] 并设置文件。
    /// </summary>
    Task SetFilesAsync(string selector, IEnumerable<string> filePaths, CancellationToken ct = default);
    /// <summary>
    /// 针对已获取的抽象元素设置文件。
    /// </summary>
    Task SetFilesAsync(IAutoElement element, IEnumerable<string> filePaths, CancellationToken ct = default);
}

/// <summary>页面导航参数。</summary>
public sealed class PageGotoOptions
{
    /// <summary>等待条件，默认 DOMContentLoaded。</summary>
    public WaitUntilState WaitUntil { get; init; } = WaitUntilState.DomContentLoaded;
    /// <summary>超时毫秒。</summary>
    public int? TimeoutMs { get; init; }
}

/// <summary>等待枚举，与具体驱动解耦。</summary>
public enum WaitUntilState
{
    Load,
    DomContentLoaded,
    NetworkIdle
}

/// <summary>浏览器启动参数。</summary>
public sealed class BrowserLaunchOptions
{
    public bool Headless { get; init; } = true;
    public string? ExecutablePath { get; init; }
    public string? UserDataDir { get; init; }
    public string? RemoteDebuggingUrl { get; init; }
    public string? ProxyServer { get; init; }
    public (int width, int height)? Viewport { get; init; }
    public string? Locale { get; init; }
    public string? TimezoneId { get; init; }
}

/// <summary>矩形包围盒（用于坐标与可见性评估）。</summary>
public sealed class BoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}
