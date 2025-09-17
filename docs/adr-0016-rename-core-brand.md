标题：Core 品牌与命名空间改名（XiaoHongShuMCP.Core → HushOps.Core）
状态：已通过
背景：
- 需要进行品牌抽象与中性化，删除与业务平台强绑定的命名；
- 现有代码/脚本/文档广泛引用 `XiaoHongShuMCP.Core`，涉及命名空间、项目名、目录名与 CI 脚本；
- 根目录应用工程 `XiaoHongShuMCP` 保持不变（仅更新引用）。
方案：
- A：仅在 csproj 层修改 Assembly/PackageId，不改命名空间与目录（兼容性强，但“品牌不彻底”）。
- B：全量改名（目录/项目文件/命名空间/引用/脚本/文档），一次性收敛（破坏式，不保留兼容）。
决策：
- 选择 B：一次性彻底改名为 `HushOps.Core`，删除旧名引用；主应用与测试同步更新。
影响：
- 代码：新增/修改涉及 Core 的 using 与 namespace；解决方案与 ProjectReference 路径更新；CI 覆盖率脚本改为 `[HushOps.Core]*`；
- 数据/运维：无数据迁移；CI 路径更新；阅读文档与开发脚本中的路径改名；
- 向后兼容：不保持；外部消费方需更新包名/命名空间。
回滚：
- 触发条件：发布前构建阻断或依赖方无法及时迁移；
- 步骤：将目录 `HushOps.Core` 还原为 `XiaoHongShuMCP.Core`，将命名空间/项目名/脚本/文档中 `HushOps.Core` 全量替换回 `XiaoHongShuMCP.Core`（保留本 ADR 以备后续再次推进）。

