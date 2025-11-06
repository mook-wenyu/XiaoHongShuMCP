/* 中文注释：按提示推断定位器，优先无歧义的语义定位 */
import type { Page, Locator } from "playwright";
import type { TargetHints } from "./types.js";
import { XHS_CONF } from "../config/xhs.js";

export interface ResolveOptions {
	aliases?: Record<string, Locator>;
	probeTimeoutMs?: number;
}

// 将文本条件（string/regex 对象）转为 Playwright 可用的 name/text 参数
function normalizeTextExpr(v: any): any {
	if (typeof v === "string") return v.trim();
	if (!v || typeof v !== "object") return undefined;
	if ("exact" in v)
		return new RegExp(`^${escapeRegExp(v.exact)}$`, v.caseSensitive ? undefined : "i");
	if ("contains" in v)
		return new RegExp(escapeRegExp(v.contains), v.caseSensitive ? undefined : "i");
	if ("regex" in v) return new RegExp(v.regex);
	return undefined;
}
function escapeRegExp(s: string) {
	return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function applyFilters(base: Locator, hints: any): Locator {
	let loc = base;
	if (hints.hasText) {
		const ht =
			typeof hints.hasText === "string" ? hints.hasText : new RegExp(String(hints.hasText.regex));
		loc = loc.filter({ hasText: ht as any });
	}
	if (hints.has) {
		// 递归编译子定位器作为 has 过滤器
		// 注意：此处不支持嵌套 alternatives，仅取第一候选
		const sub = compileLocator(loc.page(), hints.has, undefined);
		loc = loc.filter({ has: sub });
	}
	if (hints.nth !== undefined) {
		if (hints.nth === "first") loc = loc.first();
		else if (hints.nth === "last") loc = loc.last();
		else loc = loc.nth(hints.nth);
	}
	return loc;
}

function compileLocator(page: Page, hints: any, scope?: Locator): Locator {
	const prefer: string[] =
		Array.isArray(hints.prefer) && hints.prefer?.length
			? hints.prefer
			: ["role", "label", "placeholder", "testId", "text", "selector"];
	let base: Locator | undefined;
	for (const key of prefer) {
		switch (key) {
			case "role":
				if (hints.role) {
					const name = normalizeTextExpr(hints.name);
					base = (scope ?? page).getByRole(hints.role as any, name ? { name } : (undefined as any));
				}
				break;
			case "label":
				if (hints.label) base = (scope ?? page).getByLabel(hints.label);
				break;
			case "placeholder":
				if (hints.placeholder) base = (scope ?? page).getByPlaceholder(hints.placeholder);
				break;
			case "testId":
				if (hints.testId) base = (scope ?? page).getByTestId(hints.testId);
				break;
			case "text":
				if (hints.text) {
					const te = normalizeTextExpr(hints.text) ?? hints.text;
					base = (scope ?? page).getByText(te as any);
				}
				break;
			case "selector":
				if (hints.selector) base = (scope ?? page).locator(hints.selector);
				break;
		}
		if (base) break;
	}
	if (!base)
		throw new Error(
			"无法从 TargetHints 生成基础定位器（请提供 role/label/placeholder/testId/text/selector 之一）",
		);
	return applyFilters(base, hints);
}

async function probeFirstResolvable(
	page: Page,
	candidates: (() => Locator)[],
	timeout: number,
): Promise<Locator> {
	for (const make of candidates) {
		const loc = make();
		try {
			await loc.first().waitFor({ state: "attached", timeout });
			return loc;
		} catch {} // 尝试下一个
	}
	// 全部失败则返回第一个候选（便于上层统一错误处理与截图）
	return candidates[0]();
}

export async function resolveLocatorAsync(
	page: Page,
	hints: TargetHints,
	opts?: ResolveOptions,
): Promise<Locator> {
	const aliases = opts?.aliases ?? {};
	const scope = hints.within ? aliases[hints.within] : undefined;
	const probe = Math.max(
		50,
		Math.min(2000, opts?.probeTimeoutMs ?? XHS_CONF.selector.probeTimeoutMs),
	);
	const alternatives = (hints as any).alternatives as any[] | undefined;
	if (alternatives?.length) {
		const cands = alternatives.map((h) => () => compileLocator(page, h, scope));
		return probeFirstResolvable(page, cands, probe);
	}
	return compileLocator(page, hints, scope);
}
