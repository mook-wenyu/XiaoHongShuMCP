using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 文本输入策略基础类。
/// - 职责：定义“是否适用”的探测与“如何输入”的具体实现；
/// - 约定：输入过程统一遵循“思考停顿 → 输入字符（带间隔） → 语义单位检查停顿”的节奏；
/// - 扩展：可按需要新增更多策略（如 iframe 内输入、第三方富文本等）。
/// </summary>
public abstract class BaseTextInputStrategy : ITextInputStrategy
{
    protected readonly IDelayManager delayManager;
    
    protected BaseTextInputStrategy(IDelayManager delayManager)
    {
        this.delayManager = delayManager;
    }
    
    /// <inheritdoc />
    public abstract Task<bool> IsApplicableAsync(IElementHandle element);
    
    /// <inheritdoc />
    public abstract Task InputTextAsync(IPage page, IElementHandle element, string text);
    
    /// <summary>
    /// 检查文本是否包含结束标点符号
    /// </summary>
    protected static bool ContainsEndPunctuation(string text)
    {
        return text.Contains('。') || text.Contains('！') || text.Contains('？') ||
               text.Contains('.') || text.Contains('!') || text.Contains('?');
    }
    
    /// <summary>
    /// 获取语义单位的检查延时
    /// </summary>
    protected int GetSemanticUnitReviewDelay(string unit)
    {
        return unit.Length > 4 || ContainsEndPunctuation(unit) 
            ? delayManager.GetReviewPauseDelay() 
            : delayManager.GetSemanticUnitDelay();
    }
}

/// <summary>
/// 普通输入框策略。
/// 适用于 input、textarea 等标准表单元素（非 contenteditable）。
/// </summary>
public class RegularInputStrategy : BaseTextInputStrategy
{
    public RegularInputStrategy(IDelayManager delayManager) : base(delayManager)
    {
    }
    
    /// <inheritdoc />
    public override async Task<bool> IsApplicableAsync(IElementHandle element)
    {
        try
        {
            var tagName = await element.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
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
    public override async Task InputTextAsync(IPage page, IElementHandle element, string text)
    {
        try
        {
            // 1. 点击输入框获得焦点
            await element.ClickAsync();
            
            // 2. 初始思考停顿
            await Task.Delay(delayManager.GetThinkingPauseDelay());
            
            // 3. 智能分割文本为语义单位
            var semanticUnits = SmartTextSplitter.SplitBySemanticUnits(text);
            
            // 4. 循环输入语义单位
            for (int i = 0; i < semanticUnits.Count; i++)
            {
                var unit = semanticUnits[i];
                
                // 思考停顿（每个语义单位前）
                if (i > 0)
                {
                    await Task.Delay(delayManager.GetThinkingPauseDelay());
                }
                
                // 快速连续输入整个语义单位
                foreach (var character in unit)
                {
                    await page.Keyboard.TypeAsync(character.ToString());
                    await Task.Delay(delayManager.GetCharacterTypingDelay());
                }
                
                // 检查停顿
                var reviewDelay = GetSemanticUnitReviewDelay(unit);
                await Task.Delay(reviewDelay);
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
    public ContentEditableInputStrategy(IDelayManager delayManager) : base(delayManager)
    {
    }
    
    /// <inheritdoc />
    public override async Task<bool> IsApplicableAsync(IElementHandle element)
    {
        try
        {
            var contentEditable = await element.GetAttributeAsync("contenteditable");
            var tagName = await element.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
            
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
    public override async Task InputTextAsync(IPage page, IElementHandle element, string text)
    {
        try
        {
            // 1. 点击元素获得焦点
            await element.ClickAsync();
            await Task.Delay(delayManager.GetThinkingPauseDelay());
            
            // 2. 确保元素处于可编辑状态
            await page.EvaluateAsync(@"(element) => {
                element.focus();
                // 如果是空的，清除placeholder内容
                if (element.querySelector('.is-empty.is-editor-empty')) {
                    element.innerHTML = '';
                }
            }", element);
            
            await Task.Delay(500); // 等待编辑器响应焦点事件
            
            // 3. 使用语义单位进行智能输入
            var semanticUnits = SmartTextSplitter.SplitBySemanticUnits(text);
            
            for (int i = 0; i < semanticUnits.Count; i++)
            {
                var unit = semanticUnits[i];
                
                // 思考停顿
                if (i > 0)
                {
                    await Task.Delay(delayManager.GetThinkingPauseDelay());
                }
                
                // 使用键盘输入替代过时的 ElementHandle.TypeAsync
                await element.FocusAsync();
                await page.Keyboard.TypeAsync(unit, new Microsoft.Playwright.KeyboardTypeOptions
                {
                    Delay = delayManager.GetCharacterTypingDelay()
                });
                
                // 检查停顿
                var reviewDelay = GetSemanticUnitReviewDelay(unit);
                await Task.Delay(reviewDelay);
            }
            
            // 4. 触发输入事件，确保Vue/TipTap检测到内容变化
            await page.EvaluateAsync(@"(element) => {
                element.dispatchEvent(new Event('input', {bubbles: true}));
                element.dispatchEvent(new Event('change', {bubbles: true}));
            }", element);
        }
        catch (Exception ex)
        {
            throw new Exception($"富文本编辑器输入失败: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 检查是否为富文本编辑器
    /// </summary>
    private async Task<bool> IsRichTextEditor(IElementHandle element)
    {
        try
        {
            var hasEditorClasses = await element.EvaluateAsync<bool>(@"element => {
                const classList = Array.from(element.classList);
                return classList.some(cls => 
                    cls.includes('editor') || 
                    cls.includes('tiptap') || 
                    cls.includes('prosemirror')
                );
            }");
            
            return hasEditorClasses;
        }
        catch
        {
            return false;
        }
    }
}
