/* 中文注释：统一 NO_WS 错误构造与检测（减少重复分支，提升可维护性与覆盖率） */
import { err } from "./errors.js";

export function isNoWsError(e: any): boolean {
	const msg = String(e?.message ?? e ?? "");
	return /未返回\s*CDP\s*endpoint|未返回\s*ws\s*端点|connectOverCDP|ECONNREFUSED|ENOTFOUND/i.test(msg);
}

export function noWsErr(dirId: string, workspaceId?: string) {
	return err("NO_WS_ENDPOINT", "未获取到 Roxy ws 端点", {
		dirId,
		workspaceId,
		suggest: [
			"call xhs_open_context",
			"call browser_open",
			"check ROXY_* env",
			"start window or allow open",
		],
	});
}

