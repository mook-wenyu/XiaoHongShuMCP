# 设计文档 - 全面Bug修复

- **任务 ID**: TASK-20250201-001
- **来源**: 用户报告NavigateExplore超时 + 浏览器配置不保留登录状态
- **更新时间**: 2025-02-01
- **责任人**: Claude
- **关联提交**: 待提交
- **状态**: 已完成

## 设计目标

基于研究分析的5个严重bug，设计最小侵入、最大收益的修复方案，优先解决阻塞性问题。

## 方案选择

### 问题1: 浏览器配置未保留登录状态

#### 方案对比

**方案A: 使用LaunchPersistentContextAsync（选中）**
- ✅ 优点: Playwright官方推荐，完整保留用户数据
- ✅ 优点: 一次性解决cookies、localStorage、登录状态
- ✅ 优点: 代码改动最小（仅修改CreateSessionAsync）
- ⚠️ 约束: 必须`Headless = false`
- ⚠️ 约束: 仅对User模式启用
- ⚠️ 风险: 可能与现有指纹配置冲突 → 缓解：保留所有指纹参数

**方案B: 手动复制cookies**
- ❌ 缺点: 需要额外读取Chrome/Edge用户数据
- ❌ 缺点: localStorage无法复制
- ❌ 缺点: 复杂度高，容易出错

**方案C: 使用CDP直连真实浏览器**
- ❌ 缺点: 需要大量重构
- ❌ 缺点: 破坏现有架构
- ❌ 缺点: 用户必须手动启动浏览器

**决策**: 选择方案A，理由：
- 官方支持
- 改动最小
- 效果最好
- 风险可控

#### 实现设计

```csharp
// 核心逻辑：在CreateSessionAsync中检测User模式
if (openResult.Kind == BrowserProfileKind.User && !string.IsNullOrWhiteSpace(openResult.ProfilePath))
{
    // 使用持久化上下文
    var launchOptions = new BrowserTypeLaunchPersistentContextOptions
    {
        UserAgent = fingerprintContext.UserAgent,
        Locale = fingerprintContext.Language,
        TimezoneId = fingerprintContext.Timezone,
        Headless = false, // 必须！
        ViewportSize = new ViewportSize { Width = ..., Height = ... },
        DeviceScaleFactor = ...,
        IsMobile = ...,
        HasTouch = ...
    };

    context = await playwright.Chromium.LaunchPersistentContextAsync(
        openResult.ProfilePath,
        launchOptions);
}
else
{
    // 独立配置继续使用临时上下文
    context = await browser.NewContextAsync(contextOptions);
}
```

**关键点**:
- User模式自动切换到持久化上下文
- 独立配置不受影响
- 所有指纹配置完整传递
- 保持向后兼容

---

### 问题4: 自动化检测未隐藏

#### 方案对比

**方案A: 注入JavaScript脚本（选中）**
- ✅ 优点: Playwright原生支持`AddInitScriptAsync`
- ✅ 优点: 脚本在页面加载前执行
- ✅ 优点: 对所有页面生效
- ✅ 优点: 无需修改每个页面操作

**方案B: 使用浏览器启动参数**
- ❌ 缺点: 参数容易被检测
- ❌ 缺点: 效果不如脚本注入

**方案C: 使用Stealth插件**
- ❌ 缺点: 第三方依赖
- ❌ 缺点: 可能与Playwright版本冲突

**决策**: 选择方案A，理由：
- 官方推荐
- 效果最好
- 无额外依赖
- 易于维护

#### 实现设计

```csharp
// 在CreateSessionAsync中注入反检测脚本
await context.AddInitScriptAsync(WebdriverHideScript).ConfigureAwait(false);

// 脚本内容
private const string WebdriverHideScript = @"
(() => {
    Object.defineProperty(navigator, 'webdriver', {get: () => false});
    Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]});
    Object.defineProperty(navigator, 'languages', {get: () => ['zh-CN', 'zh', 'en']});
    window.chrome = {runtime: {}};
})();";
```

**隐藏特征**:
- `navigator.webdriver`: 最明显标志 → 改为false
- `navigator.plugins`: Headless通常为空 → 添加假数据
- `navigator.languages`: 真实用户有多语言 → 设置合理值
- `window.chrome`: 真实Chrome有此对象 → 添加假对象

**风险缓解**:
- 脚本立即执行函数，避免全局污染
- 只修改检测点，不破坏功能
- 与现有Canvas/WebGL混淆兼容

---

### 问题2: NavigateExplore被模态遮罩拦截

#### 方案对比

**方案A: 使用ESC键关闭模态（选中）**
- ✅ 优点: 通用，适用所有模态类型
- ✅ 优点: 代码改动最小
- ✅ 优点: 符合用户操作习惯
- ⚠️ 风险: 某些模态可能不响应ESC → 缓解：添加MoveRandom等待

**方案B: 检测并点击关闭按钮**
- ❌ 缺点: 不同模态关闭按钮不同
- ❌ 缺点: 需要复杂的选择器逻辑

**方案C: 等待模态自动消失**
- ❌ 缺点: 可能永远不消失
- ❌ 缺点: 增加等待时间

**方案D: Force点击穿透模态**
- ❌ 缺点: 违反真实用户行为
- ❌ 缺点: 可能触发反爬虫

**决策**: 选择方案A，理由：
- 最通用
- 最符合真实用户行为
- 改动最小
- 风险最低

#### 实现设计

```csharp
private static void BuildNavigateExplore(ICollection<HumanizedAction> actions, string profile)
{
    // 步骤1: 尝试关闭可能存在的模态遮罩
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.PressKey,
        ActionLocator.Empty,
        parameters: new HumanizedActionParameters(text: "Escape"),
        behaviorProfile: profile));

    // 步骤2: 短暂等待模态关闭动画
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.MoveRandom,
        behaviorProfile: profile));

    // 步骤3: 点击"发现"链接
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.Click,
        new ActionLocator(Text: "发现"),
        behaviorProfile: profile));

    // 步骤4: 随机鼠标移动（模拟等待页面加载）
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.MoveRandom,
        behaviorProfile: profile));

    // 步骤5: 滚动浏览内容
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.Wheel,
        ActionLocator.Empty,
        parameters: new HumanizedActionParameters(wheelDeltaY: 300),
        behaviorProfile: profile));
}
```

**流程优化**:
- 添加2个新步骤（PressKey + MoveRandom）
- 保持原有5步骤结构
- 总共7步骤，兼顾模态处理和页面加载

**风险缓解**:
- 即使没有模态，ESC键也不会影响页面
- MoveRandom等待300-800ms，足够动画完成
- 后续步骤保持不变，确保兼容性

---

## 未修复问题的缓解策略

### 问题3: PublishNote使用GotoAsync可能破坏会话

**风险等级**: 中
**影响范围**: 仅PublishNote功能
**缓解策略**:
- 问题1修复后，持久化上下文应该能跨域保持登录
- 如仍有问题，后续可改为点击导航
- 暂时保持现状，优先验证问题1修复效果

### 问题5: 指纹配置不一致

**风险等级**: 中
**影响范围**: 所有功能
**缓解策略**:
- 问题1修复后，持久化上下文可能自动修正
- 添加日志监控UserAgent/Timezone实际值
- 后续可考虑使用CDP强制覆盖

### 问题6-8: 其他优化

**风险等级**: 低
**影响范围**: 边缘场景
**策略**: 留待后续优化，不影响核心功能

---

## 架构影响分析

### 修改范围

- **核心文件**: `PlaywrightSessionManager.cs` (1个文件)
- **脚本构建**: `DefaultHumanizedActionScriptBuilder.cs` (1个文件)
- **影响服务**: 所有使用Playwright的功能
- **向后兼容**: 完全兼容，独立配置不受影响

### 依赖关系

```
BrowserAutomationService
    └── PlaywrightSessionManager (修改)
            └── IPlaywright
            └── IBrowserContext

HumanizedActionService
    └── DefaultHumanizedActionScriptBuilder (修改)
            └── HumanizedAction
            └── ActionLocator
```

**关键点**:
- 修改集中在2个文件
- 不破坏现有接口
- 不需要修改调用方
- 不需要修改测试

---

## 质量保证

### 测试策略

1. **单元测试**: 所有现有测试必须通过（51个）
2. **编译检查**: 0 warning, 0 error
3. **功能验证**: 手动测试NavigateExplore流程
4. **回归测试**: 验证独立配置不受影响

### 成功标准

- ✅ User模式保留登录状态
- ✅ NavigateExplore不再超时
- ✅ 自动化检测隐藏
- ✅ 所有测试通过
- ✅ 编译无错误
- ✅ 向后兼容

---

## 回滚方案

### 如何回滚

如果修复导致新问题：

1. 恢复 `PlaywrightSessionManager.cs` 到上一版本
2. 恢复 `DefaultHumanizedActionScriptBuilder.cs` 到上一版本
3. 重新编译和测试

### 回滚风险

- **低**: 修改集中且可逆
- **低**: 独立配置不受影响
- **低**: 无破坏性变更

---

## 决策记录

| 决策ID | 决策内容 | 理由 | 风险缓解 |
|--------|---------|------|---------|
| D-001 | 使用LaunchPersistentContextAsync | Playwright官方推荐，完整保留用户数据 | 仅User模式启用，保留所有指纹配置 |
| D-002 | 注入WebdriverHideScript | 原生支持，脚本在页面加载前执行 | 立即执行函数，避免全局污染 |
| D-003 | ESC键关闭模态遮罩 | 通用，符合真实用户行为 | 添加MoveRandom等待动画完成 |
| D-004 | 不修复问题3、5、6-8 | 问题1修复可能自动解决，优先验证核心修复 | 添加日志监控，留待后续优化 |

---

## 参考资料

- Playwright .NET Documentation: https://playwright.dev/dotnet/
- Browser Contexts: https://playwright.dev/dotnet/docs/browser-contexts
- Launch Persistent Context API: https://playwright.dev/dotnet/docs/api/class-browsertype#browser-type-launch-persistent-context
- Anti-Detection: https://playwright.dev/docs/test-configuration#anti-bot-detection