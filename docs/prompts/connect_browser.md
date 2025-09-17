目标：通过 MCP 工具 ConnectToBrowser 连接浏览器并校验登录，严格只读与审计规范。

使用建议
- 默认 waitUntilLoggedIn=false，先连后查；需要强制等待时设置 true，注意超时退避。
- 严禁在 Prompt 中诱导注入脚本，所有注入仅由 AntiDetectionPipeline 兜底、并强审计。

示例（英文提示，适合多宿主）
- Task: Connect to an existing browser session for Xiaohongshu, then verify login state.
- Constraints: No JS injection; respect read-only evaluation policy; timeout <= 120s.
- Output: JSON with fields {IsConnected, IsLoggedIn, Message, ErrorCode}。
