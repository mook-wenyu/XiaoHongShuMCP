/* 中文注释：Roxy 管理工具（工作区与窗口管理）
 * MCP 工具入参说明：
 * - MCP 协议 tools/call 使用字段名 `arguments` 承载参数；
 * - 兼容性考虑：本模块统一通过 getParams(input) 兼容读取 input.arguments / input.params；
 * - 参考：modelcontextprotocol.io 规范中 tools/call 请求体包含 `arguments`。
 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { PolicyEnforcer } from "../../services/policy.js";
import type { IRoxyClient } from "../../contracts/IRoxyClient.js";
import { getParams } from "../utils/params.js";

export function registerRoxyAdminTools(server: McpServer, roxy: IRoxyClient, policy: PolicyEnforcer) {
  // GET /browser/workspace
  const WorkspaceList = z.object({ page_index: z.number().int().nonnegative().optional(), page_size: z.number().int().positive().max(200).optional() });
  server.registerTool("roxy.workspaces.list", { description: "获取空间项目列表（分页）", inputSchema: { page_index: z.number().optional(), page_size: z.number().optional() } },
    async (input: any) => {
      const { page_index, page_size } = WorkspaceList.parse(getParams(input));
      const res = await policy.use("admin:workspace", async () => roxy.workspaces({ page_index, page_size }));
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // GET /browser/list_v3
  const WindowsList = z.object({
    workspaceId: z.union([z.number(), z.string()]),
    dirIds: z.string().optional(),
    windowName: z.string().optional(),
    sortNums: z.string().optional(),
    os: z.string().optional(),
    projectIds: z.string().optional(),
    windowRemark: z.string().optional(),
    page_index: z.number().int().nonnegative().default(1),
    page_size: z.number().int().positive().max(200).default(15),
    status: z.number().int().optional(),
    labelIds: z.string().optional(),
    softDeleted: z.number().int().optional(),
    createTimeBegin: z.string().optional(),
    createTimeEnd: z.string().optional(),
    isMultiLogin: z.number().int().optional(),
    is_not_proxy: z.number().int().optional()
  });
  server.registerTool("roxy.windows.list", {
    description: "获取浏览器窗口列表（v3）",
    inputSchema: {
      workspaceId: z.union([z.number(), z.string()]),
      dirIds: z.string().optional(),
      windowName: z.string().optional(),
      sortNums: z.string().optional(),
      os: z.string().optional(),
      projectIds: z.string().optional(),
      windowRemark: z.string().optional(),
      page_index: z.number().int().nonnegative().optional(),
      page_size: z.number().int().positive().max(200).optional(),
      status: z.number().int().optional(),
      labelIds: z.string().optional(),
      softDeleted: z.number().int().optional(),
      createTimeBegin: z.string().optional(),
      createTimeEnd: z.string().optional(),
      isMultiLogin: z.number().int().optional(),
      is_not_proxy: z.number().int().optional()
    }
  },
    async (input: any) => {
      const params = getParams(input);
      const parsed = WindowsList.parse(params);
      const res = await policy.use(`admin:list:${String(parsed.workspaceId)}`,
        async () => roxy.listWindows(parsed as any)
      );
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // POST /browser/create
  const WindowCreate = z.object({
    workspaceId: z.union([z.number(), z.string()]),
    windowName: z.string().optional(),
    os: z.string().optional(),
    osVersion: z.string().optional(),
    coreVersion: z.string().optional(),
    projectId: z.union([z.number(), z.string()]).optional(),
    windowRemark: z.string().optional(),
    defaultOpenUrl: z.array(z.string()).optional(),
    proxyInfo: z.record(z.any()).optional(),
    // 允许透传文档中的其他字段：tags/labelIds/.. 等
  }).passthrough();
  server.registerTool("roxy.window.create", {
    description: "创建浏览器窗口（POST /browser/create）",
    inputSchema: {
      workspaceId: z.union([z.number(), z.string()]),
      windowName: z.string().optional(),
      os: z.string().optional(),
      osVersion: z.string().optional(),
      coreVersion: z.string().optional(),
      projectId: z.union([z.number(), z.string()]).optional(),
      windowRemark: z.string().optional(),
      defaultOpenUrl: z.array(z.string()).optional(),
      proxyInfo: z.record(z.any()).optional()
    }
  },
    async (input: any) => {
      const body = WindowCreate.parse(getParams(input));
      const res = await policy.use(`admin:create:${String(body.workspaceId)}`, async () => roxy.createWindow(body as any));
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // （移除）roxy.window.detail：窗口详情接口不再通过 MCP 工具暴露。
}
