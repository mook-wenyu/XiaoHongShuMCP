目标：采集反检测只读快照，进行白名单校验与审计。

使用建议
- 仅使用 AntiDetectionTools.GetAntiDetectionSnapshot；不要自行 Evaluate；
- 提供 whitelist.json 时，将返回 Violations 与 DegradeRecommended；
- 审计文件默认写入 .audit/，命名含时间戳。

示例提示
- Task: Collect anti-detection snapshot and validate with the provided whitelist path.
- Constraints: Read-only evaluate only; no script injection; record audit file path.
- Output: JSON {Success, Message, AuditPath, Violations[], DegradeRecommended}。
