using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;

// 说明：命名空间迁移至 HushOps.Services。
namespace XiaoHongShuMCP.Services;

/// <summary>
/// 文本输入策略基础类。
/// - 职责：定义“是否适用”的探测与“如何输入”的具体实现；
/// - 约定：输入过程统一遵循“思考停顿 → 输入字符（带间隔） → 语义单位检查停顿”的节奏；
/// - 扩展：可按需要新增更多策略（如 iframe 内输入、第三方富文本等）。
/// </summary>
public abstract class BaseTextInputStrategy : ITextInputStrategy
{
    protected readonly HushOps.Core.Humanization.IDelayManager delayManager;
    
    protected BaseTextInputStrategy(HushOps.Core.Humanization.IDelayManager delayManager)
    {
        this.delayManager = delayManager;
    }
    
    /// <inheritdoc />
    public abstract Task<bool> IsApplicableAsync(IAutoElement element);
    
    /// <inheritdoc />
    public abstract Task InputTextAsync(IAutoPage page, IAutoElement element, string text);
    
    /// <summary>
    /// 检查文本是否包含结束标点符号
    /// </summary>
    protected static bool ContainsEndPunctuation(string text)
    {
        return text.Contains('。') || text.Contains('！') || text.Contains('？') ||
               text.Contains('.') || text.Contains('!') || text.Contains('?');
    }
    
    /// <summary>
    /// 等待语义单位的检查停顿（按单位长度/结束标点分类）
    /// </summary>
    protected async Task WaitSemanticUnitReviewAsync(string unit)
    {
        if (unit.Length > 4 || ContainsEndPunctuation(unit))
            await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ReviewPause);
        else
            await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.TypingSemanticUnit);
    }
}

/// <summary>
/// 普通输入框策略。
/// 适用于 input、textarea 等标准表单元素（非 contenteditable）。
/// </summary>
public class RegularInputStrategy : BaseTextInputStrategy
{
    public RegularInputStrategy(HushOps.Core.Humanization.IDelayManager delayManager) : base(delayManager) { }
    
    /// <inheritdoc />
    public override async Task<bool> IsApplicableAsync(IAutoElement element)
    {
        try
        {
            var tagName = await element.GetTagNameAsync();
            var contentEditable = await element.GetAttributeAsync("contenteditable");
            
            // 适用于标准输入元素，且不是contenteditable
            return tagName is "input" or "textarea" && 
                   contentEditable != "true";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检查普通输入框策略适用性异常: {ex.Message}");
            return false;
        }
    }
    
    /// <inheritdoc />
    public override async Task InputTextAsync(IAutoPage page, IAutoElement element, string text)
    {
        try
        {
            // 1. 点击输入框获得焦点
            await element.ClickAsync();
            
            // 2. 初始思考停顿
            await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ThinkingPause);
            
            // 3. 智能分割文本为语义单位
            var semanticUnits = SmartTextSplitter.SplitBySemanticUnits(text);
            
            // 4. 循环输入语义单位
            for (int i = 0; i < semanticUnits.Count; i++)
            {
                var unit = semanticUnits[i];
                
                // 思考停顿（每个语义单位前）
                if (i > 0)
                {
                    await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ThinkingPause);
                }
                
                // 快速连续输入整个语义单位
                await page.Keyboard.TypeAsync(unit);
                await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.TypingCharacter);
                
                // 检查停顿
                await WaitSemanticUnitReviewAsync(unit);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"普通输入框文本输入失败: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// 富文本编辑器输入策略。
/// 适用于 contenteditable 元素（例如 TipTap/ProseMirror 等），通过 Keyboard.Type 实现更自然的事件序列。
/// </summary>
public class ContentEditableInputStrategy : BaseTextInputStrategy
{
    public ContentEditableInputStrategy(HushOps.Core.Humanization.IDelayManager delayManager) : base(delayManager) { }
    
    /// <inheritdoc />
    public override async Task<bool> IsApplicableAsync(IAutoElement element)
    {
        try
        {
            var contentEditable = await element.GetAttributeAsync("contenteditable");
            var tagName = await element.GetTagNameAsync();
            
            // 适用于contenteditable元素或特定的div元素
            return contentEditable == "true" || 
                   (tagName == "div" && await IsRichTextEditor(element));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检查富文本编辑器策略适用性异常: {ex.Message}");
            return false;
        }
    }
    
    /// <inheritdoc />
    public override async Task InputTextAsync(IAutoPage page, IAutoElement element, string text)
    {
        try
        {
            // 1. 点击元素获得焦点
            await element.ClickAsync();
            await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ThinkingPause);

            // 2. 使用键盘序列确保可编辑并清空（更拟人、禁注入）：Ctrl/Meta+A + Backspace
            // 注：Playwright 将自动触发 input 相关事件，无需显式 dispatchEvent
            await page.Keyboard.PressAsync(OperatingSystem.IsMacOS() ? "Meta+A" : "Control+A");
            await page.Keyboard.PressAsync("Backspace");
            await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ContentLoading); // 等待编辑器响应焦点/清空
            
            // 3. 使用语义单位进行智能输入
            var semanticUnits = SmartTextSplitter.SplitBySemanticUnits(text);
            
            for (int i = 0; i < semanticUnits.Count; i++)
            {
                var unit = semanticUnits[i];
                
                // 思考停顿
                if (i > 0)
                {
                    await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ThinkingPause);
                }
                
                // 使用键盘输入替代过时的 ElementHandle.TypeAsync
                await page.Keyboard.TypeAsync(unit);
                await delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.TypingCharacter);
                
                // 检查停顿
                await WaitSemanticUnitReviewAsync(unit);
            }
            
            // 4. 无需 JS 注入事件：逐键输入已自然触发 input/keydown/keyup
        }
        catch (Exception ex)
        {
            throw new Exception($"富文本编辑器输入失败: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 检查是否为富文本编辑器
    /// </summary>
    private async Task<bool> IsRichTextEditor(IAutoElement element)
    {
        try
        {
            // 使用 class 属性判断常见富文本标记，避免 JS Evaluate
            var cls = (await element.GetAttributeAsync("class")) ?? string.Empty;
            var hasEditorClasses = cls.Contains("editor", StringComparison.OrdinalIgnoreCase)
                                   || cls.Contains("tiptap", StringComparison.OrdinalIgnoreCase)
                                   || cls.Contains("prosemirror", StringComparison.OrdinalIgnoreCase);
            
            return hasEditorClasses;
        }
        catch
        {
            return false;
        }
    }
}
