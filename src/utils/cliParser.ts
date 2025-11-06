import { readFile } from "node:fs/promises";

/**
 * 解析 payload 参数
 *
 * 支持两种格式：
 * - JSON 字符串：直接解析
 * - @文件路径：读取文件内容并解析为 JSON
 *
 * @param raw 原始参数值
 * @returns 解析后的 JSON 对象
 * @throws 文件不存在或 JSON 解析失败时抛出错误
 *
 * @example
 * ```typescript
 * // JSON 字符串
 * const payload1 = await parsePayload('{"url": "https://example.com"}');
 *
 * // 文件引用
 * const payload2 = await parsePayload('@path/to/payload.json');
 * ```
 */
export async function parsePayload(raw: string): Promise<unknown> {
	if (raw.startsWith("@")) {
		const path = raw.slice(1);
		const text = await readFile(path, "utf-8");
		return JSON.parse(text);
	}
	return JSON.parse(raw);
}

/**
 * 解析 dirIds 参数
 *
 * 支持两种格式：
 * - --dir-ids=a,b,c（逗号分隔）
 * - --dirId=a --dirId=b --dirId=c（多次参数）
 *
 * @param argv 命令行参数数组（通常是 process.argv）
 * @returns dirId 列表
 *
 * @example
 * ```typescript
 * const dirIds1 = parseDirIds(['--dir-ids=dir1,dir2,dir3']);
 * // => ['dir1', 'dir2', 'dir3']
 *
 * const dirIds2 = parseDirIds(['--dirId=dir1', '--dirId=dir2']);
 * // => ['dir1', 'dir2']
 * ```
 */
export function parseDirIds(argv: string[]): string[] {
	const dirIdsArg = parseArg("dir-ids", argv) || process.env.ROXY_DIR_IDS;
	const dirIdSingles = argv.filter((a) => a.startsWith("--dirId=")).map((a) => a.split("=")[1]);
	return [
		...(dirIdsArg
			? dirIdsArg
					.split(",")
					.map((x) => x.trim())
					.filter(Boolean)
			: []),
		...dirIdSingles,
	];
}

/**
 * 解析通用参数
 *
 * 从命令行参数中提取指定名称的参数值。
 *
 * @param name 参数名（不含 -- 前缀）
 * @param argv 命令行参数数组
 * @param defaultValue 默认值（参数不存在时返回）
 * @returns 参数值或默认值
 *
 * @example
 * ```typescript
 * const task = parseArg('task', process.argv, 'defaultTask');
 * const url = parseArg('url', process.argv);
 * ```
 */
export function parseArg(name: string, argv: string[], defaultValue?: string): string | undefined {
	const found = argv.find((a) => a.startsWith(`--${name}=`));
	return found ? found.split("=")[1] : defaultValue;
}

/**
 * 解析布尔标志
 *
 * 检查命令行参数中是否存在指定标志。
 *
 * @param name 标志名（不含 -- 前缀）
 * @param argv 命令行参数数组
 * @returns 标志是否存在
 *
 * @example
 * ```typescript
 * const verbose = parseFlag('verbose', process.argv);
 * const debug = parseFlag('debug', process.argv);
 * ```
 */
export function parseFlag(name: string, argv: string[]): boolean {
	return argv.some((a) => a === `--${name}`);
}
