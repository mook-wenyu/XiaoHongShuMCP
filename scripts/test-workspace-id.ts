/**
 * 测试脚本：验证 RoxyBrowser API 返回的 workspace ID 格式
 *
 * 问题：API 返回的 workspace ID 是 28255，而桌面应用显示的是 NJJ0028255
 *
 * 本脚本用于：
 * 1. 调用 /browser/workspace API 查看真实返回格式
 * 2. 分析 ID 字段的数据类型和格式
 * 3. 对比与桌面应用显示的差异
 */

// 加载 .env 文件
import { config as loadDotenv } from "dotenv";
loadDotenv();

import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import type { IRoxyClient } from "../src/contracts/IRoxyClient.js";

async function testWorkspaceIdFormat() {
	console.log("🔍 开始测试 RoxyBrowser workspace ID 格式...\n");

	try {
		// 1. 加载配置
		console.log("📋 步骤 1: 加载配置");
		const configProvider = ConfigProvider.load();
		const config = configProvider.getConfig();
		console.log(`   ✓ Roxy API URL: ${config.roxy.baseURL}`);
		console.log(`   ✓ Token 前缀: ${config.roxy.token.substring(0, 8)}...\n`);

		// 2. 创建客户端
		console.log("🔌 步骤 2: 创建 RoxyClient");
		const container = new ServiceContainer(config);
		const roxyClient: IRoxyClient = container.createRoxyClient();
		console.log("   ✓ RoxyClient 已创建\n");

		// 3. 调用 workspaces() API
		console.log("🌐 步骤 3: 调用 /browser/workspace API");
		const response = await roxyClient.workspaces();
		console.log("   ✓ API 调用成功\n");

		// 4. 分析响应结构
		console.log("📊 步骤 4: 分析响应数据");
		console.log("原始响应 (JSON):");
		console.log(JSON.stringify(response, null, 2));
		console.log("");

		// 5. 详细分析每个工作区的 ID
		if (response && typeof response === "object" && "data" in response) {
			const data = (response as any).data;

			// ⚠️ 注意：API 返回的是 data.rows，不是 data.list
			if (data && Array.isArray(data.rows)) {
				const workspaces = data.rows;
				console.log(`\n✅ 找到 ${workspaces.length} 个工作区\n`);

				console.log("┌─────────────────────────────────────────────────────────────┐");
				console.log("│                    工作区 ID 详细分析                        │");
				console.log("├─────────────────────────────────────────────────────────────┤");

				workspaces.forEach((ws: any, index: number) => {
					console.log(`\n工作区 #${index + 1}:`);
					console.log(`  - 原始对象: ${JSON.stringify(ws)}`);
					console.log(`  - ID 字段: ${ws.id}`);
					console.log(`  - ID 类型: ${typeof ws.id}`);
					console.log(`  - ID 是否为数字: ${typeof ws.id === "number"}`);
					console.log(`  - workspaceName 字段: ${ws.workspaceName || "N/A"}`);

					// 检查是否有其他可能包含前缀的字段
					const allKeys = Object.keys(ws);
					console.log(`  - 所有字段: ${allKeys.join(", ")}`);

					// 如果 ID 是数字，展示可能的前缀格式
					if (typeof ws.id === "number") {
						console.log(`  - 推测的带前缀格式 (NJJ0前缀): NJJ0${String(ws.id).padStart(6, "0")}`);
						console.log(`  - 推测的带前缀格式 (NJJ前缀): NJJ${String(ws.id).padStart(7, "0")}`);
					}
				});

				console.log("\n└─────────────────────────────────────────────────────────────┘\n");

				// 6. 问题分析
				console.log("🔎 问题分析:");
				console.log("┌─────────────────────────────────────────────────────────────┐");
				console.log("│ API 返回格式 vs 桌面应用格式                                │");
				console.log("├─────────────────────────────────────────────────────────────┤");
				console.log("│ API 返回:     28255 (number)                                │");
				console.log("│ 桌面应用:     NJJ0028255 (string with prefix)               │");
				console.log("│                                                             │");
				console.log("│ 可能原因:                                                    │");
				console.log("│ 1. 桌面应用做了前端展示转换 (仅用于显示)                     │");
				console.log("│ 2. 不同 API 版本返回不同格式                                 │");
				console.log("│ 3. 桌面应用使用的是不同的 API 端点                           │");
				console.log("│                                                             │");
				console.log("│ 建议:                                                        │");
				console.log("│ - 在调用 API 时使用数字 ID (28255)                           │");
				console.log("│ - 桌面应用的前缀可能仅用于用户界面显示                        │");
				console.log("│ - 确认 listWindows/open 等 API 接受的 workspaceId 格式      │");
				console.log("└─────────────────────────────────────────────────────────────┘\n");
			} else {
				console.log("❌ 响应数据格式异常: data.rows 不是数组\n");
				console.log("完整响应:", response);
			}
		} else {
			console.log("❌ 响应格式异常: 不包含 data 字段\n");
			console.log("完整响应:", response);
		}

		// 7. 测试使用数字 ID 调用其他 API
		console.log("\n🧪 步骤 5: 测试使用数字 ID 调用 listWindows API");
		if (response && typeof response === "object" && "data" in response) {
			const data = (response as any).data;
			if (data && Array.isArray(data.rows) && data.rows.length > 0) {
				const firstWorkspace = data.rows[0];
				const workspaceId = firstWorkspace.id;

				console.log(`   - 使用 workspaceId: ${workspaceId} (类型: ${typeof workspaceId})`);

				try {
					// 测试 1: 仅传 workspaceId
					console.log("\n   📝 测试 1: 仅传 workspaceId 参数");
					const windows1 = await roxyClient.listWindows({ workspaceId });
					console.log(`   返回窗口数: ${windows1.data?.rows?.length || 0}`);

					// 测试 2: 不传任何参数（可能显示所有窗口）
					console.log("\n   📝 测试 2: 不传 workspaceId（查询所有窗口）");
					let windows2: any = null;
					try {
						windows2 = await roxyClient.listWindows({} as any);
						console.log(`   返回窗口数: ${windows2.data?.rows?.length || 0}`);

						if (windows2.data?.rows?.length > 0) {
							console.log("\n   ⚠️ 发现: 不传 workspaceId 时能查到窗口！");
							console.log("   这些窗口的 workspaceId:");
							windows2.data.rows.forEach((win: any) => {
								console.log(
									`     - ${win.windowName}: workspaceId=${win.workspaceId} (${typeof win.workspaceId})`,
								);
							});
						}
					} catch (e) {
						console.log(`   查询失败: ${e}`);
					}

					// 测试 3: 添加更多参数
					console.log("\n   📝 测试 3: 添加 page_index 和 page_size 参数");
					const windows3 = await roxyClient.listWindows({
						workspaceId,
						page_index: 0,
						page_size: 100,
					});
					console.log(`   返回窗口数: ${windows3.data?.rows?.length || 0}`);

					// 测试 4: 检查是否有软删除的窗口
					console.log("\n   📝 测试 4: 包含软删除的窗口 (softDeleted=0)");
					const windows4 = await roxyClient.listWindows({
						workspaceId,
						softDeleted: 0,
						page_size: 100,
					});
					console.log(`   返回窗口数: ${windows4.data?.rows?.length || 0}`);

					// 选择有窗口的结果进行详细展示
					const windowsResults = [windows1, windows2, windows3, windows4].filter((w) => w !== null);
					let windowsList: any[] = [];

					for (const result of windowsResults) {
						// ⚠️ 修复: API 返回 data.rows，不是 data.list
						if (result?.data?.rows && result.data.rows.length > 0) {
							windowsList = result.data.rows;
							break;
						}
					}

					console.log(`\n   ✓ 最终找到 ${windowsList.length} 个窗口\n`);

					if (windowsList.length === 0) {
						console.log("⚠️ 所有测试都未找到窗口");
						console.log("\n🔍 调试信息 - 完整 API 响应:");
						console.log("测试1响应:", JSON.stringify(windows1, null, 2));
						if (windows2) {
							console.log("\n测试2响应:", JSON.stringify(windows2, null, 2));
						}
					}

					// 8. 详细展示窗口列表
					if (windowsList.length > 0) {
						console.log("📋 步骤 6: 窗口列表详细信息\n");
						console.log("┌─────────────────────────────────────────────────────────────┐");
						console.log("│                        窗口列表                              │");
						console.log("├─────────────────────────────────────────────────────────────┤\n");

						windowsList.forEach((win: any, index: number) => {
							console.log(`窗口 #${index + 1}:`);
							console.log(`  - dirId: ${win.dirId}`);
							console.log(`  - windowName: ${win.windowName || "N/A"}`);
							console.log(`  - os: ${win.os || "N/A"}`);
							console.log(`  - status: ${win.status} (${win.status === 1 ? "运行中" : "未运行"})`);
							console.log(`  - workspaceId: ${win.workspaceId} (类型: ${typeof win.workspaceId})`);
							console.log(`  - projectId: ${win.projectId || "N/A"}`);
							console.log(`  - windowRemark: ${win.windowRemark || "N/A"}`);
							console.log(`  - createTime: ${win.createTime || "N/A"}`);

							// 显示所有字段
							const allKeys = Object.keys(win);
							console.log(`  - 所有字段 (${allKeys.length}): ${allKeys.join(", ")}\n`);
						});

						console.log("└─────────────────────────────────────────────────────────────┘\n");

						// 9. 展示原始 JSON
						console.log("📄 原始窗口列表 JSON（前3个）:");
						console.log(JSON.stringify(windowsList.slice(0, 3), null, 2));
						console.log("");
					} else {
						console.log("\n⚠️ 确认无法找到窗口");
						console.log("   可能原因:");
						console.log("   1. 窗口在不同的工作空间下");
						console.log("   2. 窗口被软删除了");
						console.log("   3. API 需要其他参数");
						console.log("   4. RoxyBrowser 桌面应用需要重启\n");

						console.log("   建议操作:");
						console.log("   - 在 RoxyBrowser 桌面应用中确认窗口存在");
						console.log("   - 检查窗口所属的工作空间 ID");
						console.log("   - 尝试重启 RoxyBrowser 桌面应用\n");
					}
				} catch (error) {
					console.error(`   ✗ 失败: ${error}\n`);
				}
			}
		}

		console.log("✅ 测试完成!\n");
		process.exit(0);
	} catch (error) {
		console.error("\n❌ 测试失败:");
		console.error(error);
		console.log("\n请检查:");
		console.log("  1. .env 文件是否正确配置");
		console.log("  2. ROXY_API_TOKEN 是否有效");
		console.log("  3. RoxyBrowser 桌面应用是否正在运行");
		console.log("  4. API 地址是否正确 (默认 http://127.0.0.1:50000)");
		process.exit(1);
	}
}

testWorkspaceIdFormat();
