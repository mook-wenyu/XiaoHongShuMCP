/* 中文注释：选择器 ID 生成（用于健康度与追踪） */
export function selectorIdFromTarget(target: unknown): string {
	try {
		const s = typeof target === "string" ? target : JSON.stringify(target);
		let h = 0;
		for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0;
		return "sel:" + (h >>> 0).toString(16);
	} catch {
		return "sel:unknown";
	}
}
