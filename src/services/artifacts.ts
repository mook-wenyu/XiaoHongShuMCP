/* 中文注释：产物写入工具 */
import { promises as fs } from "node:fs";
import { join } from "node:path";

export async function ensureDir(p: string) {
	await fs.mkdir(p, { recursive: true });
}
export async function writeJson(p: string, data: any) {
	await fs.writeFile(p, JSON.stringify(data, null, 2), "utf-8");
}
export function pathJoin(...p: string[]) {
	return join(...p);
}

export async function writeManifest(root: string, dirId: string, entry: any) {
	const dir = pathJoin(root, dirId);
	await ensureDir(dir);
	const file = pathJoin(dir, `manifest-${Date.now()}.json`);
	await writeJson(file, entry);
	return file;
}
