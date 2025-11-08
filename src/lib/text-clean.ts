/* 文本清洗策略：按环境变量与配置执行字符移除与空白归一化
 * 环境变量：
 *  - XHS_TEXT_CLEAN_REMOVE_CHARS: 要移除的字符集合（按字符逐个移除），如 ",.!?，。！？·—–-:;()[]{}\"'`|<>~@#$%^&*+=_"
 *  - XHS_TEXT_CLEAN_REMOVE_REGEX: 自定义正则（字符串形式，不含 / / 标记），如 "[\\p{Emoji_Presentation}]+"（注意转义）
 *  - XHS_TEXT_CLEAN_COLLAPSE_WS: 是否压缩空白为单空格（默认 true）
 */

import type { Page } from "playwright";
// 外部清洗配置已移除，保留环境变量驱动与默认策略（Unicode 标点/符号剔除 + 压缩空白）

const DEFAULT_REMOVE_CHARS = ",.!?，。！？·—–-:;()[]{}\"'`|<>~@#$%^&*+=_";

function buildRegexFromEnv(): RegExp | null {
	// 每次根据当前 env 重新构建，避免跨测试缓存导致的不可预期行为
	const removeChars = process.env.XHS_TEXT_CLEAN_REMOVE_CHARS ?? DEFAULT_REMOVE_CHARS;
	const removeRegex = process.env.XHS_TEXT_CLEAN_REMOVE_REGEX; // e.g., "[A-Z]" or "[\\p{Emoji_Presentation}]"
	try {
		if (removeRegex && removeRegex.trim().length > 0) {
			return new RegExp(removeRegex, "gu");
		}
		if (removeChars && removeChars.length > 0) {
			const escaped = removeChars.replace(/[\\^$.*+?()[\]{}|]/g, "\\$&");
			return new RegExp(`[${escaped}]`, "gu");
		}
		return null;
	} catch {
		return null;
	}
}

function applyRule(
	input: string,
	rule?: { removeChars?: string; removeRegex?: string; collapseWs?: boolean },
): string {
	let s = String(input || "");
	try {
		s = s.normalize("NFKC");
	} catch {}
	try {
		if (rule?.removeRegex) {
			const re = new RegExp(rule.removeRegex, "gu");
			s = s.replace(re, "");
		} else if (rule?.removeChars) {
			const escaped = rule.removeChars.replace(/[\\^$.*+?()[\]{}|]/g, "\\$&");
			const re = new RegExp(`[${escaped}]`, "gu");
			s = s.replace(re, "");
		} else {
			const re = buildRegexFromEnv();
			if (re) s = s.replace(re, "");
			// 保险兜底：移除所有 Unicode 标点和符号（默认行为），避免环境缓存导致的失效
			s = s.replace(/[\p{P}\p{S}]+/gu, "");
		}
	} catch {
		const re = buildRegexFromEnv();
		if (re) s = s.replace(re, "");
		s = s.replace(/[\p{P}\p{S}]+/gu, "");
	}
	const collapse =
		rule?.collapseWs ??
		String(process.env.XHS_TEXT_CLEAN_COLLAPSE_WS || "true").toLowerCase() !== "false";
	if (collapse) s = s.replace(/\s+/g, " ").trim();
	return s;
}

export function cleanText(input: string | null | undefined): string {
	return applyRule(String(input || ""), undefined);
}

export async function cleanTextFor(
	_page: Page,
	_pageType: string | undefined,
	input: string | null | undefined,
): Promise<string> {
	// 直接使用环境变量与默认规则；如需差异化按页面类型处理，可在此处按 _pageType 做条件分支。
	return applyRule(String(input || ""), undefined);
}
