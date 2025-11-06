// 离线枚举工具：避免依赖实际 MCP 启动（无官方桥包时也可运行）
// 通过构建一个最小 FakeServer，调用各工具注册器收集名称

import { registerBrowserTools } from "../src/mcp/tools/browser.js";
import { registerPageTools } from "../src/mcp/tools/page.js";
import { registerXhsTools } from "../src/mcp/tools/xhs.js";
import { registerResourceTools } from "../src/mcp/tools/resources.js";
import { registerRoxyAdminTools } from "../src/mcp/tools/roxyAdmin.js";

class FakeServer {
  public tools: { name: string; description?: string }[] = [];
  registerTool(name: string, meta: any, _handler: any) {
    this.tools.push({ name, description: meta?.description });
  }
  // 兼容接口占位（未使用）
  registerResource() {}
}

async function main() {
  const server = new FakeServer();
  const container: any = {};
  const adapter: any = {};
  const roxy: any = {};
  const policy: any = {};

  // 官方命名
  registerBrowserTools(server as any, container, adapter);
  registerPageTools(server as any, container, adapter);
  registerXhsTools(server as any, container, adapter);
  registerResourceTools(server as any, container, adapter);
  // 高权限管理工具
  registerRoxyAdminTools(server as any, roxy, policy);

  const expected = [
    // 官方命名（唯一标准）
    "browser.open","browser.close",
    "page.create","page.list","page.close","page.navigate","page.click","page.hover","page.scroll","page.screenshot",
    "page.type","page.input.clear",
    "xhs.session.check","xhs.navigate.home",
    "resources.listArtifacts","resources.readArtifact","page.snapshot",
    // 管理工具（保留 roxy 命名空间，仅用于管理）
    "roxy.workspaces.list","roxy.windows.list","roxy.window.create"
  ];
  const optional = ["server.ping","server.capabilities"];

  const names = server.tools.map(t => t.name);
  const missing = expected.filter((n) => !names.includes(n));
  const extras = names.filter((n) => !expected.includes(n) && !optional.includes(n));

  if (missing.length === 0 && extras.length === 0) {
    console.log(JSON.stringify({ ok: true, total: names.length, note: "tools surface matches expected" }));
    process.exit(0);
  } else {
    console.log(JSON.stringify({ ok: false, total: names.length, missing, extras }));
    process.exit(1);
  }
}
main().catch((e) => { console.error("check-mcp-tools failed:", e); process.exit(2); });
