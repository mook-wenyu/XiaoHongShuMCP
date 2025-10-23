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
