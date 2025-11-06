/* 中文说明：浏览器工具（可复用前缀） */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { IAdapter } from "../../adapter/IAdapter.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();

export function registerBrowserToolsWithPrefix(server: McpServer, _container: ServiceContainer, adapter: IAdapter, prefix = "browser") {
  const name = (n: string) => `${prefix}.${n}`;

  server.registerTool(name("open"), {
    description: "打开浏览器窗口（dirId=账号窗口，workspaceId 可选）",
    inputSchema: { dirId: DirId, workspaceId: WorkspaceId }
  }, async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const { context } = await adapter.open(dirId, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok({ dirId, pages: context.pages().length })) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(err("CONNECTION_FAILED", String(e?.message || e))) }] };
    }
  });

  server.registerTool(name("close"), {
    description: "关闭浏览器窗口（按 dirId）",
    inputSchema: { dirId: DirId }
  }, async (input: any) => {
    try { await adapter.close((input as any).dirId); return { content: [{ type: "text", text: JSON.stringify(ok({ dirId: (input as any).dirId })) }] }; }
    catch (e: any) { return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] }; }
  });
}

export function registerBrowserTools(server: McpServer, container: ServiceContainer, adapter: IAdapter) {
  return registerBrowserToolsWithPrefix(server, container, adapter, "browser");
}
