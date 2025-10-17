# CDP 连接功能集成测试指南

## 概述

本文档提供 Chrome DevTools Protocol (CDP) 连接功能的手动集成测试指南，包括三种连接模式的测试方法。

## 连接模式说明

### 1. Auto 模式（默认）
- **行为**：优先尝试 CDP 连接，失败则自动回退到 Launch 模式
- **适用场景**：用户浏览器配置，浏览器可能已打开或未打开
- **优势**：最灵活，自动适应浏览器状态

### 2. Launch 模式
- **行为**：直接启动新的浏览器实例
- **适用场景**：独立配置，或明确需要新浏览器实例
- **优势**：可预测，不依赖外部状态

### 3. ConnectCdp 模式
- **行为**：仅尝试 CDP 连接，失败则抛出错误
- **适用场景**：确定浏览器已启用远程调试
- **优势**：明确失败，便于诊断问题

## 前置准备

### 启动带远程调试的浏览器

Windows (Edge):
```powershell
# 方法 1：命令行启动
& "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" `
  --remote-debugging-port=9222 `
  --user-data-dir="C:\Users\YourUsername\AppData\Local\Microsoft\Edge\User Data"

# 方法 2：快捷方式启动
# 右键桌面 Edge 快捷方式 -> 属性 -> 目标，添加参数：
# "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --remote-debugging-port=9222
```

Windows (Chrome):
```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" `
  --remote-debugging-port=9222 `
  --user-data-dir="C:\Users\YourUsername\AppData\Local\Google\Chrome\User Data"
```

macOS (Chrome):
```bash
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome \
  --remote-debugging-port=9222 \
  --user-data-dir="$HOME/Library/Application Support/Google/Chrome"
```

Linux (Chrome):
```bash
google-chrome \
  --remote-debugging-port=9222 \
  --user-data-dir="$HOME/.config/google-chrome"
```

### 验证远程调试已启用

浏览器启动后访问：
```
http://localhost:9222/json/version
```

应该看到类似以下 JSON 响应：
```json
{
  "Browser": "Chrome/120.0.0.0",
  "Protocol-Version": "1.3",
  "User-Agent": "Mozilla/5.0...",
  "V8-Version": "12.0.267.8",
  "WebKit-Version": "537.36",
  "webSocketDebuggerUrl": "ws://localhost:9222/devtools/browser/..."
}
```

## 测试场景

### 场景 1：Auto 模式 + 浏览器已启动

**测试步骤**：
1. 启动带远程调试的浏览器（参考前置准备）
2. 使用 xhs MCP 工具调用 `browser_open`，参数为空或设置 `connectionMode: "Auto"`
3. 观察日志输出

**预期结果**：
```
[Playwright] 尝试 CDP 连接 profile=user endpoint=http://localhost:9222
[Playwright] CDP 连接成功 profile=user contexts=1
[Playwright] CDP 会话创建成功 profile=user pages=1
```

**验证点**：
- 不会启动新的浏览器窗口
- 复用已打开的浏览器实例
- 可以看到现有页面

---

### 场景 2：Auto 模式 + 浏览器未启动

**测试步骤**：
1. 确保没有浏览器在 9222 端口运行
2. 使用 xhs MCP 工具调用 `browser_open`
3. 观察日志输出

**预期结果**：
```
[Playwright] 尝试 CDP 连接 profile=user endpoint=http://localhost:9222
[Playwright] CDP 连接失败，回退到 Launch 模式 profile=user
[Playwright] 启动持久化上下文 profile=user path=C:\Users\...
```

**验证点**：
- CDP 连接失败后自动回退
- 启动新的浏览器实例
- 操作正常完成

---

### 场景 3：ConnectCdp 模式 + 浏览器已启动

**测试步骤**：
1. 启动带远程调试的浏览器
2. 调用 `browser_open`，参数设置 `connectionMode: "ConnectCdp"`
3. 观察结果

**预期结果**：
```
[Playwright] 尝试 CDP 连接 profile=user endpoint=http://localhost:9222
[Playwright] CDP 连接成功 profile=user contexts=1
```

**验证点**：
- 成功连接到现有浏览器
- 不启动新实例

---

### 场景 4：ConnectCdp 模式 + 浏览器未启动

**测试步骤**：
1. 确保没有浏览器在 9222 端口运行
2. 调用 `browser_open`，参数设置 `connectionMode: "ConnectCdp"`
3. 观察错误信息

**预期结果**：
```
[Playwright] CDP 连接失败 profile=user port=9222。
请确保浏览器已启动且启用了远程调试参数 --remote-debugging-port=9222

错误：CDP 连接失败。请启动浏览器并添加启动参数：--remote-debugging-port=9222。
```

**验证点**：
- 抛出明确的错误信息
- 提供启动参数指导
- 不尝试回退到 Launch 模式

---

### 场景 5：Launch 模式

**测试步骤**：
1. 调用 `browser_open`，参数设置 `connectionMode: "Launch"`
2. 观察行为

**预期结果**：
```
[Playwright] 启动持久化上下文 profile=user path=C:\Users\...
```

**验证点**：
- 直接启动新浏览器实例
- 不尝试 CDP 连接
- 即使有浏览器在 9222 端口运行也不连接

---

### 场景 6：自定义 CDP 端口

**测试步骤**：
1. 启动浏览器时使用自定义端口（如 9333）：
   ```powershell
   msedge.exe --remote-debugging-port=9333
   ```
2. 调用 `browser_open`，参数设置 `cdpPort: 9333`
3. 观察连接是否成功

**预期结果**：
```
[Playwright] 尝试 CDP 连接 profile=user endpoint=http://localhost:9333
[Playwright] CDP 连接成功 profile=user contexts=1
```

**验证点**：
- 可以使用非默认端口
- 端口配置正确传递

---

### 场景 7：独立配置强制 Launch

**测试步骤**：
1. 调用 `browser_open`，使用独立配置（非 "user" profile）
2. 观察连接模式

**预期结果**：
```
[Playwright] 启动持久化上下文 profile=isolated-test path=...
```

**验证点**：
- 独立配置始终使用 Launch 模式
- 即使设置 ConnectCdp 也会被强制改为 Launch
- 创建独立的用户数据目录

---

## 参数验证测试

### 无效 CDP 端口

**测试用例**：
```csharp
// 端口 < 1
var request = BrowserOpenRequest.UseUserProfile(
    profilePath: null,
    profileKey: "test",
    connectionMode: BrowserConnectionMode.Auto,
    cdpPort: 0);  // 应该抛出异常
```

**预期错误**：
```
CDP 端口必须在 1-65535 范围内，当前值：0。
```

**其他测试值**：
- `-1` ✗
- `0` ✗
- `1` ✓
- `9222` ✓
- `65535` ✓
- `65536` ✗
- `100000` ✗

---

## 故障排查

### 问题：CDP 连接失败

**可能原因**：
1. 浏览器未启动
2. 浏览器未启用远程调试
3. 端口号不匹配
4. 防火墙阻止连接
5. 浏览器在不同的配置目录

**诊断步骤**：
1. 访问 `http://localhost:9222/json/version` 验证端口
2. 检查浏览器启动参数
3. 使用任务管理器确认浏览器进程存在
4. 检查端口是否被占用：`netstat -ano | findstr 9222`

---

### 问题：Auto 模式未回退

**可能原因**：
- 端口被其他程序占用（非浏览器）
- 网络配置问题

**诊断步骤**：
1. 检查日志中是否有 "CDP 连接失败，回退到 Launch 模式" 消息
2. 确认异常类型和消息
3. 验证 LaunchPersistentContext 是否被调用

---

### 问题：浏览器配置目录冲突

**症状**：
- CDP 连接后看不到预期的页面
- 浏览器状态不一致

**解决方案**：
1. 确保 `--user-data-dir` 参数与 `profilePath` 一致
2. 检查是否有多个浏览器实例使用不同配置
3. 关闭所有浏览器实例重新测试

---

## 单元测试覆盖

已完成的单元测试（位于 `BrowserOpenModelsTests.cs`）：

✓ 基础创建测试
  - ForUser_ShouldCreateUserProfileRequest
  - ForUser_WithProfilePath_ShouldSetPath
  - ForIsolated_ShouldCreateIsolatedProfileRequest

✓ CDP 连接模式测试
  - UseUserProfile_WithAutoMode_ShouldAllowAutoConnection
  - UseUserProfile_WithConnectCdpMode_ShouldAllowCdpConnection
  - UseUserProfile_WithLaunchMode_ShouldAllowLaunchOnly

✓ 验证逻辑测试
  - EnsureValid_WithEmptyProfileKey_ShouldThrow
  - EnsureValid_WithWhitespaceProfileKey_ShouldThrow
  - EnsureValid_WithInvalidCdpPort_ShouldThrow (多个边界值)
  - EnsureValid_WithValidCdpPort_ShouldNotThrow (多个有效值)

✓ 参数规范化测试
  - EnsureValid_ShouldTrimWhitespace
  - EnsureValid_ShouldHandleNullProfilePath

**测试统计**：17 个测试全部通过

---

## 性能考虑

### CDP 连接 vs Launch 模式

| 指标 | CDP 连接 | Launch 模式 |
|------|---------|------------|
| 启动时间 | ~100-200ms | ~2-3s |
| 内存占用 | 0（复用） | +200-500MB |
| 状态保持 | ✓ 保留 | ✗ 重新开始 |
| 并发限制 | 受浏览器限制 | 仅受系统资源限制 |

### 建议

- **开发环境**：使用 Auto 模式，方便调试
- **生产环境**：使用 Launch 模式，确保隔离性
- **CI/CD**：使用 Launch 模式，避免外部依赖
- **人工测试**：使用 ConnectCdp 模式，快速迭代

---

## 安全考虑

### 远程调试端口

⚠️ **警告**：远程调试端口允许完全控制浏览器，包括：
- 执行任意 JavaScript
- 访问所有页面内容
- 读取 Cookie 和本地存储
- 截获网络请求

### 安全建议

1. **仅本地绑定**：确保端口仅监听 `localhost`
2. **防火墙规则**：阻止外部访问调试端口
3. **生产禁用**：生产环境禁用远程调试
4. **端口随机化**：考虑使用随机端口而非固定 9222

---

## 总结

CDP 连接功能提供了灵活的浏览器自动化方式：

✅ **优势**：
- 复用已打开的浏览器实例
- 保留浏览器状态和登录信息
- 启动速度快
- 资源占用低

⚠️ **限制**：
- 需要手动启动浏览器并启用远程调试
- 端口管理复杂度增加
- 潜在的安全风险

🎯 **最佳实践**：
- 默认使用 Auto 模式获得最佳灵活性
- 明确场景使用 Launch 或 ConnectCdp 模式
- 独立配置始终使用 Launch 确保隔离
- 生产环境谨慎使用 CDP 连接

---

## 相关文件

- `BrowserConnectionMode.cs` - 连接模式枚举定义
- `BrowserOpenModels.cs` - 请求和结果模型
- `PlaywrightSessionManager.cs` - CDP 连接实现
- `BrowserOpenModelsTests.cs` - 单元测试
