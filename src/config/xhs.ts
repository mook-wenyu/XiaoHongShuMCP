/* 中文注释：小红书模块级参数配置（集中管理，便于调优与测试） */
export const XHS_CONF = {
	search: {
		waitUrlMs: Number(process.env.XHS_SEARCH_WAIT_URL_MS || 7000),
		waitApiMs: Number(process.env.XHS_SEARCH_WAIT_API_MS || 8000),
	},
	feed: {
		waitApiMs: Number(process.env.XHS_FEED_WAIT_API_MS || 7000),
	},
	scroll: {
		maxScrolls: Number(process.env.XHS_SCROLL_MAX_SCROLLS || 20),
		step: Number(process.env.XHS_SCROLL_STEP || 1400),
		settleMs: Number(process.env.XHS_SCROLL_SETTLE_MS || 250),
		// 智能批次确认参数（滚动后）
		useApiAfterScroll:
			String(process.env.XHS_SCROLL_USE_API_AFTER || "true").toLowerCase() !== "false",
		shortFeedWaitMs: Number(process.env.XHS_SCROLL_SHORT_FEED_WAIT_MS || 1500),
		shortSearchWaitMs: Number(process.env.XHS_SCROLL_SHORT_SEARCH_WAIT_MS || 1500),
		microScrollOnNoProgressPx: Number(process.env.XHS_SCROLL_MICRO_ON_NOPROGRESS_PX || 120),
		noProgressRoundsForBoost: Number(process.env.XHS_SCROLL_NOPROGRESS_ROUNDS || 2),
		boostScrollMinPx: Number(process.env.XHS_SCROLL_BOOST_MIN_PX || 1200),
		// 防跳过参数：重叠与保留率
		overlapAnchors: Number(process.env.XHS_SCROLL_OVERLAP_ANCHORS || 3),
		overlapRatio: Number(process.env.XHS_SCROLL_OVERLAP_RATIO || 0.25), // 视口重叠比例（0.05–0.6）
		retentionMin: Number(process.env.XHS_SCROLL_RETENTION_MIN || 0.6), // 可视卡片保留率阈值（触发回退）
		backtrackPx: Number(process.env.XHS_SCROLL_BACKTRACK_PX || 0), // 如为 0 则按视口比例计算
	},
	selector: {
		probeTimeoutMs: Number(process.env.XHS_SELECTOR_PROBE_MS || 250),
		resolveTimeoutMs: Number(process.env.XHS_SELECTOR_RESOLVE_MS || 3000),
		healthCheckIntervalMs: Number(process.env.XHS_SELECTOR_HEALTH_CHECK_MS || 60000),
	},
	capture: {
		minContentLength: Number(process.env.XHS_CAPTURE_MIN_LENGTH || 200),
		waitNetworkIdleMs: Number(process.env.XHS_CAPTURE_NETWORK_IDLE_MS || 10000),
		waitContentMs: Number(process.env.XHS_CAPTURE_CONTENT_MS || 5000),
	},
} as const;

export type SearchApiItem = {
	id?: string;
	note_card?: { display_title?: string };
};
