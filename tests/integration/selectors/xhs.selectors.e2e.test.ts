/* 中文注释：XhsSelectors 语义映射定位验证（集成） */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../helpers/roxyHarness.js";
import { installXhsRoutes } from "../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";
import { XhsSelectors } from "../../../src/selectors/xhs.js";
import { resolveLocatorResilient } from "../../../src/selectors/index.js";

describeIf("XhsSelectors 语义映射定位验证", () => {
	let dirId: string;
	beforeAll(async () => {
		const { manager } = await getContainerAndManager();
		dirId = await pickDirId("xhs_selectors_e2e");
		const ctx = await manager.getContext(dirId);
		const fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
		await installXhsRoutes(ctx, { root: fixturesRoot });
		const page = await ctx.newPage();
		await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
		await page.close();
	});

	it("searchInput/searchSubmit/navDiscover 可定位", async () => {
		const { manager } = await getContainerAndManager();
		const ctx = await manager.getContext(dirId);
		const page = await ctx.newPage();
		await page.goto("https://www.xiaohongshu.com/explore");
		const inLoc = await resolveLocatorResilient(page as any, XhsSelectors.searchInput() as any);
		expect(await inLoc.count()).toBeGreaterThanOrEqual(1);
		const btnLoc = await resolveLocatorResilient(page as any, XhsSelectors.searchSubmit() as any);
		expect(await btnLoc.count()).toBeGreaterThanOrEqual(1);
		const navLoc = await resolveLocatorResilient(page as any, XhsSelectors.navDiscover() as any);
		expect(await navLoc.count()).toBeGreaterThanOrEqual(1);
		// 额外覆盖：常用 note 相关选择器
		const anchor = await resolveLocatorResilient(page as any, XhsSelectors.noteAnchor() as any);
		expect(await anchor.count()).toBeGreaterThanOrEqual(1);
		// noteModalMask 存在，但关闭按钮可能缺失，此处仅验证 mask
		const mask = await resolveLocatorResilient(page as any, XhsSelectors.noteModalMask() as any);
		expect(await mask.count()).toBeGreaterThanOrEqual(1);
		await page.close();
	});
});

