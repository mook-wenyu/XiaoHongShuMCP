/* 中文注释：MCP 工具参数解析统一入口
 * 兼容三种形态：
 * 1) tools/call 规范中的 params.arguments
 * 2) 历史/客户端兼容的 params.params（个别客户端/SDK 版本）
 * 3) TS SDK registerTool 的 handler 直接接收“已解析参数对象”（入参本体）
 */
export function getParams<T extends Record<string, unknown> = Record<string, unknown>>(
	input: unknown,
): T {
	if (input == null) return {} as T;
	const anyIn = input as any;
	if (anyIn && typeof anyIn === "object") {
		if (anyIn.arguments && typeof anyIn.arguments === "object") return anyIn.arguments as T;
		if (anyIn.params && typeof anyIn.params === "object") return anyIn.params as T;
		return anyIn as T; // handler 直收参数对象的形态
	}
	return {} as T;
}
