# XiaoHongShuMCP 项目概览

## 项目简介
XiaoHongShuMCP 是基于 .NET 9.0 和 Model Context Protocol (MCP) 的小红书智能自动化服务器，提供安全的小红书运营工具。

## 核心特性
- 🔐 安全优先：所有内容操作仅保存为草稿
- 🚀 启动即用：MCP服务器启动时自动连接浏览器并验证登录状态
- 🤖 智能搜索：支持多维度筛选的增强搜索功能
- 📊 数据分析：自动统计分析和 Excel 导出
- 👤 拟人化交互：模拟真人操作模式，防检测机制
- 🧪 完整测试：74 个单元测试，100% 通过率

## 技术栈
- **.NET 9.0**: 主开发框架
- **Model Context Protocol 0.3.0-preview.4**: MCP 协议实现
- **Microsoft Playwright 1.54.0**: 浏览器自动化引擎
- **Serilog**: 结构化日志记录
- **NPOI 2.7.4**: Excel 文件操作
- **NUnit 4.4.0**: 单元测试框架
- **Moq 4.20.72**: Mock 框架

## 架构设计
采用现代 .NET 架构模式：
- 依赖注入 (Microsoft.Extensions.DependencyInjection)
- 接口隔离原则
- 统一错误处理 (OperationResult<T>)
- 异步编程模式
- 拟人化交互系统

## 安全特性
- 仅草稿模式：确保用户完全控制发布时机
- 本地数据处理：不上传第三方服务
- 防检测机制：智能拟人化操作
- 日志脱敏：敏感信息自动处理