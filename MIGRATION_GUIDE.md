# 导航工具重构迁移指南

## 破坏性变更（Breaking Changes）

### 已删除的工具

- ❌ `xhs_navigate_explore` - 已移除，语义不明确且返回格式不一致

### 新增的工具

- ✅ `xhs.navigate.home` - 导航到小红书探索页（主页入口）
- ✅ `xhs.navigate.discover` - 导航到小红书发现页（个性化推荐流）

## 迁移步骤

### 场景1：导航到探索页主页

**旧代码：**
```typescript
// 使用 xhs_navigate_explore（已删除）
// 行为不明确：可能导航到首页或发现页
```

**新代码：**
```typescript
// 使用 xhs.navigate.home
{
  "tool": "xhs.navigate.home",
  "parameters": {
    "dirId": "user",
    "workspaceId": "optional"
  }
}
```

**返回格式：**
```json
{
  "ok": true,
  "data": {
    "target": "home",
    "url": "https://www.xiaohongshu.com/explore",
    "description": "已导航到探索页主页"
  }
}
```

### 场景2：导航到发现页（个性化推荐）

**旧代码：**
```typescript
// 使用 xhs_navigate_explore（已删除）
// 尝试点击"发现"链接，失败时回退到首页
```

**新代码：**
```typescript
// 使用 xhs.navigate.discover（使用拟人化点击）
{
  "tool": "xhs.navigate.discover",
  "parameters": {
    "dirId": "user",
    "workspaceId": "optional"
  }
}
```

**返回格式：**
```json
{
  "ok": true,
  "data": {
    "target": "discover",
    "url": "https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend",
    "description": "已通过模拟点击导航到发现页（推荐流）"
  }
}
```

**错误处理：**
```json
{
  "ok": false,
  "error": {
    "code": "NAVIGATE_DISCOVER_FAILED",
    "message": "错误信息",
    "screenshotPath": "artifacts/user/navigation/navigate-discover-error-1234567890.png"
  }
}
```

## 核心改进

### 1. 语义明确性

- **探索页（Home）**：`https://www.xiaohongshu.com/explore` - 主页入口，包含所有频道导航
- **发现页（Discover）**：`/explore?channel_id=homefeed_recommend` - 个性化推荐流

### 2. 命名规范一致性

- 采用点号命名：`xhs.navigate.home`、`xhs.navigate.discover`
- 与项目其他工具保持一致（如 `action.navigate`、`roxy.workspaces.list`）

### 3. 返回格式标准化

- 使用 `okRes/failRes` 模式
- 错误包含明确的错误码和消息
- 失败时提供截图路径便于调试

### 4. 拟人化行为

- `xhs.navigate.discover` 使用 `clickHuman` 模拟真实用户点击
- 避免直接URL导航，降低反检测风险

### 5. 错误处理增强

- 增加 `screenshotOnError` 辅助函数
- 失败时自动截图保存到 `artifacts/{dirId}/navigation/`
- 更详细的错误信息和调试支持

## 测试检查清单

- [ ] 测试 `xhs.navigate.home` 能否成功导航到探索页
- [ ] 验证返回的 URL 是否为 `https://www.xiaohongshu.com/explore`
- [ ] 测试 `xhs.navigate.discover` 能否通过点击导航到发现页
- [ ] 验证返回的 URL 包含 `channel_id=homefeed_recommend`
- [ ] 测试错误场景下是否生成截图
- [ ] 验证返回格式符合 `okRes/failRes` 规范

## 兼容性说明

- 本次重构为**破坏性变更**，旧工具 `xhs_navigate_explore` 已完全移除
- 建议更新所有依赖代码使用新工具
- 新工具遵循项目最佳实践，提供更好的可维护性和可读性

## 相关文件

- `src/mcp/tools/xhsShortcuts.ts` - 工具实现
- `src/mcp/utils/result.ts` - okRes/failRes 实用函数
- `src/humanization/actions.ts` - 拟人化行为函数
