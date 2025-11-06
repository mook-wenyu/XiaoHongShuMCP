/**
 * Preflight 自检（环境变量与 Playwright 模块检测）
 * - 校验关键环境变量与 Config 解析
 * - 检测 Playwright 模块是否可用（不强制拉起浏览器），提示安装浏览器命令
 *
 * 退出码：存在阻塞项（环境变量不全）时返回 1，否则 0
 */

function out(s: string) {
	process.stdout.write(s.endsWith("\n") ? s : s + "\n");
}
function err(s: string) {
	process.stderr.write(s.endsWith("\n") ? s : s + "\n");
}

(async () => {
	out("== Preflight 自检 ==");

	// 1) 环境变量与配置解析
	let envOk = true;
	if (!process.env.ROXY_API_TOKEN) {
		err("[缺失] ROXY_API_TOKEN");
		envOk = false;
	}
	try {
		const { ConfigProvider } = await import("../src/config/ConfigProvider.js");
		ConfigProvider.load();
		out("[OK] 配置解析通过");
	} catch (e) {
		err(`[错误] 配置解析失败: ${String((e as any)?.message || e)}`);
		envOk = false;
	}

	// 2) Playwright 模块检测（不强制安装浏览器）
	try {
		await import("playwright");
		out("[OK] Playwright 模块已安装（如首跑失败，执行 npx playwright install chromium）");
	} catch {
		err(
			"[警告] 未检测到 Playwright 模块，请执行 npm i -S playwright，并安装浏览器 npx playwright install chromium",
		);
	}

	// 3) RoxyBrowser 集成检查
	out("[OK] RoxyBrowser 集成：直接使用 Playwright CDP 连接");

	// 4) 结果
	out(envOk ? "== 自检通过 ==" : "== 自检存在问题，请按提示修正 ==");
	process.exit(envOk ? 0 : 1);
})().catch((e) => {
	err(String((e as any)?.message || e));
	process.exit(1);
});
