# 配置指南
> 更新时间：2025-10-02 13:11 (UTC+8) · 执行：Codex

## 配置加载优先级

配置按以下优先级加载（后者覆盖前者）：
1. 代码默认值
2. `appsettings.json`（可选）
3. `config/xiao-hong-shu.json`（可选）
4. 环境变量（前缀 `HUSHOPS_XHS_SERVER_`）

## 配置节说明

| 配置节 | 环境变量前缀 | 描述 |
|-------|-------------|------|
| `xhs` | `HUSHOPS_XHS_SERVER_XHS__` | 默认关键词、画像、人性化节奏 |
| `humanBehavior` | `HUSHOPS_XHS_SERVER_HumanBehavior__` | 行为档案配置 |
| `fingerprint` | `HUSHOPS_XHS_SERVER_Fingerprint__` | 浏览器指纹配置 |
| `networkStrategy` | `HUSHOPS_XHS_SERVER_NetworkStrategy__` | 网络策略配置 |
| `playwrightInstallation` | `HUSHOPS_XHS_SERVER_PlaywrightInstallation__` | Playwright 安装配置 |
| `verification` | `HUSHOPS_XHS_SERVER_Verification__` | 验证运行配置 |

## 核心配置示例

### 1. 基础配置（xhs 节）

```json
{
  "xhs": {
    "defaultKeyword": "旅行攻略",
    "humanized": {
      "minDelayMs": 800,
      "maxDelayMs": 2600,
      "jitter": 0.2
    },
    "portraits": [
      {
        "id": "travel-lover",
        "tags": ["旅行", "美食", "摄影"],
        "metadata": {
          "category": "lifestyle",
          "region": "asia"
        }
      }
    ]
  }
}
```

### 2. 行为档案配置（humanBehavior 节）

```json
{
  "humanBehavior": {
    "defaultProfile": "default",
    "profiles": {
      "default": {
        "preActionDelay": { "minMs": 250, "maxMs": 600 },
        "postActionDelay": { "minMs": 220, "maxMs": 520 },
        "typingInterval": { "minMs": 80, "maxMs": 200 },
        "scrollDelay": { "minMs": 260, "maxMs": 720 },
        "maxScrollSegments": 2,
        "hesitationProbability": 0.12,
        "clickJitter": { "minPx": 1, "maxPx": 4 },
        "mouseMoveSteps": { "min": 12, "max": 28 },
        "mouseVelocity": { "min": 280, "max": 820 },
        "randomIdleProbability": 0.1,
        "randomIdleDuration": { "minMs": 420, "maxMs": 960 },
        "requireProxy": false,
        "allowAutomationIndicators": false
      },
      "cautious": {
        "preActionDelay": { "minMs": 420, "maxMs": 820 },
        "postActionDelay": { "minMs": 360, "maxMs": 780 },
        "hesitationProbability": 0.22,
        "randomIdleProbability": 0.2
      },
      "aggressive": {
        "preActionDelay": { "minMs": 120, "maxMs": 280 },
        "postActionDelay": { "minMs": 140, "maxMs": 320 },
        "hesitationProbability": 0.05,
        "randomIdleProbability": 0.05
      }
    }
  }
}
```

### 3. 浏览器指纹配置（fingerprint 节）

```json
{
  "fingerprint": {
    "defaultProfileKey": "user",
    "profiles": {
      "user": {
        "profileType": "User",
        "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
        "platform": "Win32",
        "viewportWidth": 1440,
        "viewportHeight": 900,
        "locale": "zh-CN",
        "timezoneId": "Asia/Shanghai",
        "hardwareConcurrency": 8,
        "vendor": "Google Inc.",
        "webglVendor": "Intel Inc.",
        "webglRenderer": "Intel(R) UHD Graphics 770",
        "canvasSeed": "stable-user-seed",
        "webglSeed": "stable-user-seed",
        "browserPath": "%LOCALAPPDATA%/Google/Chrome/Application/chrome.exe",
        "userDataDir": "%LOCALAPPDATA%/Google/Chrome/User Data"
      },
      "creator-profile": {
        "profileType": "Synthetic",
        "userAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 13_5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "platform": "MacIntel",
        "viewportWidth": 1680,
        "viewportHeight": 1050,
        "locale": "zh-CN",
        "timezoneId": "Asia/Shanghai",
        "hardwareConcurrency": 8,
        "vendor": "Apple Computer, Inc.",
        "webglVendor": "Apple Inc.",
        "webglRenderer": "Apple M2",
        "canvasSeed": "creator-seed",
        "webglSeed": "creator-seed"
      }
    }
  }
}
```

### 4. 网络策略配置（networkStrategy 节）

```json
{
  "networkStrategy": {
    "defaultTemplate": "default",
    "templates": {
      "default": {
        "proxyPool": "direct",
        "proxyAddress": null,
        "requestDelay": { "minMs": 120, "maxMs": 350 },
        "maxRetryAttempts": 3,
        "retryBaseDelayMs": 200,
        "simulateBandwidth": false,
        "downstreamKbps": 2000,
        "upstreamKbps": 800
      },
      "residential-sg": {
        "proxyPool": "residential",
        "proxyAddress": "http://127.0.0.1:24000",
        "requestDelay": { "minMs": 260, "maxMs": 620 },
        "maxRetryAttempts": 2,
        "retryBaseDelayMs": 350,
        "simulateBandwidth": true,
        "downstreamKbps": 1800,
        "upstreamKbps": 600
      }
    }
  }
}
```

### 5. Playwright 安装配置（playwrightInstallation 节）

```json
{
  "playwrightInstallation": {
    "browsersPath": "D:/Playwright/Cache",
    "browsers": ["chromium"],
    "arguments": ["--with-deps"],
    "downloadHost": "https://playwright.azureedge.net",
    "ignoreFailures": false,
    "skipIfBrowsersPresent": true
  }
}
```

## 行为档案参数详解

| 参数 | 说明 | 默认档案参考值 |
|------|------|----------------|
| `preActionDelay` / `postActionDelay` | 动作前后延迟范围（毫秒），控制整体节奏 | 250-600 / 220-520 |
| `typingInterval` | 输入字符间隔（毫秒），越小越接近机器输入 | 80-200 |
| `scrollDelay` | 滚动间隔（毫秒），影响翻页速度 | 260-720 |
| `maxScrollSegments` | 单次滚动分段数，决定动作拆分粒度 | 2 |
| `hesitationProbability` | 随机犹豫概率（0-1），提升拟人化效果 | 0.12 |
| `clickJitter` | 点击抖动像素范围，减少同点重复痕迹 | 1-4 |
| `mouseMoveSteps` | 鼠标路径分步数，越高越平滑 | 12-28 |
| `mouseVelocity` | 鼠标速度（像素/秒），影响移动时长 | 280-820 |
| `randomIdleProbability` / `randomIdleDuration` | 随机停顿概率与时长（毫秒） | 0.1 / 420-960 |
| `likeProbability` / `favoriteProbability` | 浏览笔记时的互动概率 | 0.3 / 0.2 |
| `requireProxy` | 是否强制使用代理（true 将阻止无代理运行） | false |
| `allowAutomationIndicators` | 是否允许暴露自动化特征 | false |

## 环境变量配置

### Windows 示例

```pwsh
set HUSHOPS_XHS_SERVER_XHS__DefaultKeyword=旅行攻略
set HUSHOPS_XHS_SERVER_XHS__Humanized__MinDelayMs=800
set HUSHOPS_XHS_SERVER_XHS__Humanized__MaxDelayMs=2600
set HUSHOPS_XHS_SERVER_HumanBehavior__DefaultProfile=cautious
set HUSHOPS_XHS_SERVER_NetworkStrategy__DefaultTemplate=residential-sg
set HUSHOPS_XHS_SERVER_PlaywrightInstallation__DownloadHost=https://playwright.azureedge.net
```

### Linux/macOS 示例

```bash
export HUSHOPS_XHS_SERVER_XHS__DefaultKeyword="旅行攻略"
export HUSHOPS_XHS_SERVER_XHS__Humanized__MinDelayMs=800
export HUSHOPS_XHS_SERVER_XHS__Humanized__MaxDelayMs=2600
export HUSHOPS_XHS_SERVER_HumanBehavior__DefaultProfile=cautious
export HUSHOPS_XHS_SERVER_NetworkStrategy__DefaultTemplate=residential-sg
export HUSHOPS_XHS_SERVER_PlaywrightInstallation__DownloadHost=https://playwright.azureedge.net
```

## 高级配置场景

### 多账号隔离配置

- 为每个账号分配独立的 `xhs.portraits` 条目，并与 `humanBehavior.profiles` 中的节奏模板做一一映射。
- 调用工具前先执行 `xhs_browser_open` 指定 `profileKey`，系统会在 `storage/browser-profiles/<profileKey>` 下创建隔离目录。
- 网络层建议为不同账号指定 `networkStrategy.templates`，配合代理池避免同出口 IP。

### 代理策略配置

- 在 `networkStrategy.templates` 中为不同业务场景定义延迟、重试和带宽参数，使用 `defaultTemplate` 切换主策略。
- 若代理需要认证，可在 `proxyAddress` 中写入完整的 `http://user:pass@host:port`，并在环境变量中传递加密后的凭证。
- 通过 `simulateBandwidth` 与上下行带宽限制模拟低速网络，帮助触发平台的慢速容忍逻辑。

### 自定义画像配置

- `xhs.portraits` 支持附加 `metadata` 字段，例如 `ageGroup`、`interests`，供工具层做二次解析。
- 配合 `humanBehavior.defaultProfile` 可让定制画像自动切换到更谨慎或激进的操作节奏。
- 若需要加载外部画像文件，可在部署流程中以脚本合并多个 JSON，再写入 `config/xiao-hong-shu.json`。

## 配置验证

- 执行 `dotnet run -- --tools-list`，确认服务器成功读取配置并暴露全部 MCP 工具。
- 运行 `dotnet run -- --verification-run`，验证浏览器、指纹与网络策略组合是否正常，并检查日志是否包含 `STATUS: ok`。
- 若使用环境变量覆盖参数，可结合 `set`/`export` 后再次执行上述命令确认生效。

### 常见配置错误与解决方案

| 错误现象 | 常见原因 | 解决方案 |
|----------|----------|----------|
| 工具列表为空 | 命令路径或环境变量未配置，服务器未启动 | 重新确认 `command` 与 `args` 指向 `.csproj`，并执行 `dotnet run -- --tools-list` 检查日志 |
| 字符串参数被解析为 `null` | 旧版配置仍使用 `null` 而非空字符串 | 将相关字段替换为 `""`，再次运行 `dotnet run -- --tools-list` 验证 |
| Playwright 反复下载 | 缺少缓存目录或未设置镜像 | 设定 `playwrightInstallation.browsersPath` 与 `downloadHost`，必要时手动运行安装脚本 |
| FingerprintBrowser 未注册 | `libs/FingerprintBrowser.dll` 缺失或 `.csproj` 引用被移除 | 确认 `libs/FingerprintBrowser.dll` 及配套文件存在，并保持 `.csproj` 中 `<Reference Include="FingerprintBrowser">` 与 `<HintPath>libs\\FingerprintBrowser.dll</HintPath>`；缺失时重新复制交付包提供的 DLL |
| 代理策略未生效 | `networkStrategy` 模板未命中或缺少代理地址 | 检查 `defaultTemplate` 与模板键是否一致，并确认代理地址格式正确 |

