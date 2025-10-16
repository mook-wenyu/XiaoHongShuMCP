# Repository Guidelines

## 项目结构与模块组织
- 根目录：`Program.cs`、`ServiceCollectionExtensions.cs`、`HushOps.Servers.XiaoHongShu.csproj`、`HushOps.Servers.XiaoHongShu.sln`。
- 业务代码：`Services/`、`Tools/`、`Infrastructure/`、`Diagnostics/`、`Configuration/`。
- 测试：`Tests/`（主测试项目在 `Tests/HushOps.Servers.XiaoHongShu.Tests/`）。
- 文档与脚本：`docs/`、`Tools/`。依赖 DLL：`libs/`（`FingerprintBrowser.dll`）。
- 缓存：`storage/playwright-cache/`（不应提交）。

## 构建、测试与本地运行
- 首次准备（安装浏览器）：Windows 执行 `Tools/install-playwright.ps1`；Linux/macOS 执行 `Tools/install-playwright.sh`。
- 构建：`dotnet build -c Debug`（或 `Release`）。
- 运行：`dotnet run --project HushOps.Servers.XiaoHongShu.csproj`。
- 测试：`dotnet test Tests/HushOps.Servers.XiaoHongShu.Tests/HushOps.Servers.XiaoHongShu.Tests.csproj -l "trx;LogFileName=test-results.trx"`。
- 发布：`dotnet publish -c Release -o publish`。

## 编码风格与命名规范
- 语言：C#（.NET 8，开启可空 `nullable`，`TreatWarningsAsErrors=true`）。
- 缩进与格式：4 空格；提交前运行 `dotnet format`（遵循根目录 `.editorconfig`）。
- 命名：类型/方法用 PascalCase；局部变量/参数用 camelCase；常量用 UPPER_CASE。
- 约定：配置类以 `*Options` 结尾（见 `Configuration/`）；工具类以 `*Tool`；服务以 `*Service`。

## 测试规范
- 框架：xUnit（`[Fact]` / `[Theory]`）。测试文件以 `*Tests.cs` 结尾，集中于 `Tests/HushOps.Servers.XiaoHongShu.Tests/`。
- 要求：新增功能需配套单测，覆盖正常路径与至少一个失败分支；与 Playwright 相关的测试需先完成浏览器安装。
- 产物：TRX 报告默认输出到 `Tests/.../TestResults/`。

## 提交与合并请求
- 提交信息：建议采用 Conventional Commits（示例：`feat: 增加人性化滚动策略`、`fix: 修复登录态丢失`、`test: 增补 NoteCaptureTool 用例`）。
- PR 要求：
  - 清晰描述动机、范围与影响，并关联 Issue（如 `#123`）。
  - 附上关键命令与本地验证结果（构建/测试输出或 TRX 摘要）。
  - 变更第三方 DLL 请说明来源与版本。

## 安全与配置提示（重要）
- 禁止提交账号、Cookie、代理与令牌；机密通过环境变量（前缀 `HUSHOPS_XHS_SERVER_`）或 `appsettings.*.json` 注入。
- 使用 `DOTNET_ENVIRONMENT` 控制环境；开发默认读取 `appsettings.Development.json`。
- `libs/` 为预编译依赖目录，更新须在 PR 说明。