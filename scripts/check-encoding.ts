/**
 * 中文编码巡检脚本（CI/本地）
 * - 检查 UTF-8 BOM
 * - 检测常见中文乱码片段（UTF-8 被错误解码产生的“鎵/绐/娴/鍝/璇/杩/淇/楠/瀹/杞/鑾/鏌/鎿/閿/鍘/瀹/閲/閫/绔/鎸/闅/寤/鏈/灏/鑷/鍔/鎶/鈥/锛 等）
 * - 发现问题时以非零退出码结束，便于 CI 拦截
 */

import fs from "fs";
import path from "path";

type Issue = { file: string; type: "BOM" | "Garbled"; detail?: string; line?: number };

const EXCLUDE_DIR = new Set(["node_modules", ".git", "dist", "artifacts", ".turbo", ".next"]);
const ALLOW_EXT = new Set([".ts", ".tsx", ".js", ".mjs", ".cjs", ".json", ".md", ".yml", ".yaml"]);
const DEFAULT_ROOTS = ["src", "tests", "scripts", "README.md", "package.json", "AGENTS.md"];

// 典型乱码词（由 UTF-8 误按其他编码解码产生）
const BAD_TOKENS = [
	// 词组（优先命中）
	"鎵撳紑",
	"绐楀彛",
	"娴忚鍣",
	"鍝嶅簲",
	"璇锋眰",
	"宸ヤ綔绌洪棿",
	"杩炴帴",
	"淇℃伅",
	"楠岃瘉",
	"瀹藉",
	"杞",
	"鑾峰彇",
	"鏌ヨ",
	"鎿嶄綔",
	"閿欒",
	"鍘熷",
	"瀹藉",
	"閲嶈瘯",
	"閫氳繃",
	"绔偣",
	"鎸囩汗",
	"闅忔満",
	"寤虹珛",
	"鏈繑鍥",
	"灏忕孩涔",
	"鑷姩鍖",
	"鏈嶅姟",
	"浜烘満",
	"鍔犺浇",
	"鎶ラ敊",
	"鎶涘嚭",
	// 单字高频
	"鈥",
	"锛",
	"锟",
	"鑳",
	"鐜",
	"鐢",
	"鐧",
	"缁",
	"鍙",
	"绠",
	"璁",
	"闈",
];

function isTextFile(p: string): boolean {
	const ext = path.extname(p).toLowerCase();
	if (!ext) return false;
	return ALLOW_EXT.has(ext);
}

function shouldSkip(full: string): boolean {
	const parts = full.split(path.sep);
	return parts.some((seg) => EXCLUDE_DIR.has(seg));
}

function walk(dir: string): string[] {
	const out: string[] = [];
	for (const it of fs.readdirSync(dir, { withFileTypes: true })) {
		const full = path.join(dir, it.name);
		if (shouldSkip(full)) continue;
		if (it.isDirectory()) out.push(...walk(full));
		else if (it.isFile() && isTextFile(full)) out.push(full);
	}
	return out;
}

function checkFileBom(file: string, issues: Issue[]) {
	const buf = fs.readFileSync(file);
	if (buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) {
		issues.push({ file, type: "BOM" });
	}
}

function checkFileGarbled(file: string, issues: Issue[]) {
	// 跳过对本脚本自身的乱码扫描（否则常量表会被命中）
	const self = path.normalize(path.resolve(process.cwd(), "scripts/check-encoding.ts"));
	const target = path.normalize(path.resolve(file));
	if (target === self) return;
	const content = fs.readFileSync(file, "utf8");
	const lines = content.split(/\r?\n/);
	for (let i = 0; i < lines.length; i++) {
		const line = lines[i];
		for (const tok of BAD_TOKENS) {
			if (line.includes(tok)) {
				issues.push({ file, type: "Garbled", line: i + 1, detail: tok });
			}
		}
	}
}

function main() {
	const roots = (process.env.CHECK_ENCODING_PATHS?.split(",").filter(Boolean) || DEFAULT_ROOTS).map(
		(p) => path.resolve(process.cwd(), p),
	);

	const files: string[] = [];
	for (const r of roots) {
		if (!fs.existsSync(r)) continue;
		const stat = fs.statSync(r);
		if (stat.isDirectory()) files.push(...walk(r));
		else if (stat.isFile() && isTextFile(r)) files.push(r);
	}

	const issues: Issue[] = [];
	for (const f of files) {
		checkFileBom(f, issues);
		checkFileGarbled(f, issues);
	}

	if (issues.length === 0) {
		console.error("[check-encoding] ✅ 未发现编码问题（UTF-8 无 BOM，未命中常见乱码片段）");
		return;
	}

	const bom = issues.filter((x) => x.type === "BOM");
	const garbled = issues.filter((x) => x.type === "Garbled");

	if (bom.length) {
		console.error("[check-encoding] ❌ 发现 BOM 文件：");
		for (const it of bom) console.error(`  - ${it.file}`);
	}
	if (garbled.length) {
		console.error("[check-encoding] ❌ 发现疑似中文乱码片段：");
		for (const it of garbled) console.error(`  - ${it.file}:${it.line} 命中片段: ${it.detail}`);
	}

	process.exitCode = 2;
}

main();
