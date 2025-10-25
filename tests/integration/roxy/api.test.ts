/**
 * RoxyClient API 集成测试
 *
 * 测试 RoxyBrowser API 的核心功能：
 * - 健康检查
 * - 工作区查询
 * - 窗口管理
 * - 窗口详情
 */

import { describe, it, expect, beforeAll } from "vitest";
import { roxyReady } from "../../helpers/roxy.js";
const ready = await roxyReady();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { ConfigProvider } from "../../../src/config/ConfigProvider.js";
import { ServiceContainer } from "../../../src/core/container.js";
import type { IRoxyClient } from "../../../src/contracts/IRoxyClient.js";
import type {
	WorkspaceListResponse,
	WindowListResponse,
	Workspace,
	Window,
} from "../../../src/types/roxy/index.js";
import {
	WorkspaceListResponseSchema,
	WindowListResponseSchema,
} from "../../../src/schemas/roxy/index.js";

describeIf("RoxyClient API 集成测试", () => {
	let roxyClient: IRoxyClient;

	beforeAll(() => {
		const configProvider = ConfigProvider.load();
		const config = configProvider.getConfig();
		const container = new ServiceContainer(config);
		roxyClient = container.createRoxyClient();
	});

	it("应该通过健康检查", async () => {
		const result = await roxyClient.health();
		expect(result).toBeDefined();
		// API 返回标准响应格式 { code: 0, msg: '成功', data: ... } 或字符串
		if (typeof result === "object") {
			expect(result).toHaveProperty("code");
			expect(result).toHaveProperty("msg");
		}
		console.log("✅ 健康检查通过:", result);
	}, 10000);

	it("应该能查询工作区列表", async () => {
		const workspaces: WorkspaceListResponse = await roxyClient.workspaces();
		expect(workspaces).toBeDefined();
		// ⚠️ 注意：API 返回的是 data.rows，不是 data.list
		expect(Array.isArray(workspaces.data?.rows)).toBe(true);
		console.log(`✅ 查询到 ${workspaces.data?.rows?.length || 0} 个工作区`);
		console.log("工作区列表:", JSON.stringify(workspaces.data?.rows, null, 2));

		// 显示每个工作区的 ID 格式
		if (workspaces.data?.rows) {
			workspaces.data.rows.forEach((ws: Workspace) => {
				console.log(
					`  - 工作区 ID: ${ws.id}, 类型: ${typeof ws.id}, 名称: ${ws.workspaceName || "N/A"}`
				);
			});
		}

		// Zod 验证测试：验证响应符合 Schema
		const validation = WorkspaceListResponseSchema.safeParse(workspaces);
		expect(validation.success).toBe(true);
		if (!validation.success) {
			console.error("Zod 验证失败:", validation.error.format());
		}
	}, 10000);

	it("应该能查询窗口列表", async () => {
		const windows: WindowListResponse = await roxyClient.listWindows({ workspaceId: 28255 });
		expect(windows).toBeDefined();
		expect(windows).toHaveProperty("code");
		expect(windows).toHaveProperty("msg");

		// data 可能为 null（用户没有窗口时）或包含 rows 数组
		if (windows.data !== null) {
			expect(Array.isArray(windows.data.rows)).toBe(true);
			console.log(`✅ 查询到 ${windows.data.rows?.length || 0} 个窗口`);

			// 显示前 3 个窗口信息
			const first3 = windows.data.rows?.slice(0, 3) || [];
			first3.forEach((win: Window) => {
				console.log(`  - dirId: ${win.dirId}, 名称: ${win.windowName}, 系统: ${win.os}`);
			});
		} else {
			console.log("✅ 查询窗口列表成功（无窗口数据）");
		}

		// Zod 验证测试：验证响应符合 Schema
		const validation = WindowListResponseSchema.safeParse(windows);
		expect(validation.success).toBe(true);
		if (!validation.success) {
			console.error("Zod 验证失败:", validation.error.format());
		}
	}, 10000);
});