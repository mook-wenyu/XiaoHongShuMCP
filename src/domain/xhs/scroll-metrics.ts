/* 中文注释：滚动跳过检测与指标记录
 * 目标：在每次滚动后，比较滚动前/后可见卡片集合的“保留率/重叠率”，辅助判断是否出现跳过（over-skip）。
 * 输出：写入 selector-health NDJSON（selectorId: 'nav-scroll-metrics'），字段含保留率、集合规模、阈值与是否疑似跳过。
 */
import { appendHealth } from "../../selectors/health-sink.js";
import type { CardInfo } from "../../selectors/card.js";

function normalizeText(s: string): string {
  return (s || "").toLowerCase().replace(/\s+/g, " ").slice(0, 200);
}

function hash32(str: string): number {
  let h = 2166136261 >>> 0; // FNV-ish
  for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); }
  return (h >>> 0);
}

function cardKey(c: CardInfo): string {
  if (c.noteId) return `id:${c.noteId}`;
  const basis = normalizeText(c.text || c.title || "");
  return `t:${hash32(basis)}`;
}

export function computeRetention(prev: CardInfo[], curr: CardInfo[]) {
  const prevKeys = new Set(prev.map(cardKey));
  const currKeys = new Set(curr.map(cardKey));
  if (prevKeys.size === 0 || currKeys.size === 0) {
    return { prevCount: prevKeys.size, currCount: currKeys.size, shared: 0, retention: 0 };
  }
  let shared = 0;
  for (const k of prevKeys) if (currKeys.has(k)) shared++;
  const retention = shared / Math.max(1, prevKeys.size);
  return { prevCount: prevKeys.size, currCount: currKeys.size, shared, retention };
}

export async function recordScrollMetrics(opts: {
  slug?: string;
  url?: string;
  round: number;
  stepPx: number;
  progressed: boolean;
  prev: CardInfo[];
  curr: CardInfo[];
  ratioEnv?: number; // XHS_SCROLL_STEP_RATIO（用于给出期望保留率参考）
  screenshotPath?: string;
}) {
  const enabled = String(process.env.XHS_SCROLL_METRICS ?? "true").toLowerCase();
  if (enabled === "false" || enabled === "0") return;
  const { prev, curr } = opts;
  const { prevCount, currCount, shared, retention } = computeRetention(prev, curr);
  // 经验阈值：步长比例 r → 期望保留率 ~ (1 - r)（留一些富余）
  const r = Number.isFinite(opts.ratioEnv || NaN) ? Math.min(0.9, Math.max(0.1, opts.ratioEnv as number)) : 0.55;
  const expected = Math.max(0.15, Math.min(0.85, 1 - r));
  const minAccept = Math.max(0.12, expected * 0.6); // 放宽 40%
  const suspectedSkip = retention < minAccept;
  await appendHealth({
    ts: Date.now(),
    slug: opts.slug,
    url: opts.url,
    selectorId: "nav-scroll-metrics",
    ok: !suspectedSkip,
    durationMs: 0,
    metric: {
      round: opts.round,
      anchors: currCount,
      visited: prevCount, // 这里复用字段名记录 prev 集合规模
      progressed: opts.progressed,
    },
    errorCode: suspectedSkip ? "SCROLL_SKIP_SUSPECT" : undefined,
  });
  // 追加一条带详细度量的扩展示例（不影响上条的结构化报表）
  await appendHealth({
    ts: Date.now(), slug: opts.slug, url: opts.url, selectorId: "nav-scroll-metrics-detail", ok: true, durationMs: 0,
    metric: { round: opts.round, anchors: currCount, visited: prevCount, progressed: opts.progressed },
    errorCode: undefined as any,
  });
  // 为了简洁，详细值写到 stderr（可选），避免 NDJSON 过大；如需落盘，可扩展 health-sink。
  try {
    const line = `[scroll-metrics] round=${opts.round} step=${opts.stepPx} prev=${prevCount} curr=${currCount} shared=${shared} retention=${retention.toFixed(3)} expected≈${expected.toFixed(2)} suspect=${suspectedSkip} snap=${opts.screenshotPath || "-"}\n`;
    process.stderr.write(line);
  } catch {}
}
