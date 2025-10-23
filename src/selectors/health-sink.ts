import { appendFile, mkdir } from "node:fs/promises";
import { dirname } from "node:path";

const DISABLED = /^true$/i.test(process.env.SELECTOR_HEALTH_DISABLED || "false");
const OUT_PATH = process.env.SELECTOR_HEALTH_PATH || "artifacts/selector-health.ndjson";

export type SelectorHealthRecord = {
  ts: number;
  slug?: string;
  url?: string;
  selectorId: string;
  ok: boolean;
  durationMs: number;
  retries?: number;
  errorCode?: string;
};

async function ensureParent(file: string) {
  try { await mkdir(dirname(file), { recursive: true }); } catch {}
}

export async function appendHealth(record: SelectorHealthRecord): Promise<void> {
  if (DISABLED) return;
  try {
    await ensureParent(OUT_PATH);
    const line = JSON.stringify(record) + "\n";
    await appendFile(OUT_PATH, line, { encoding: "utf-8" });
  } catch (e) {
    // 降级：不影响主流程，记录到 stderr 以便诊断
    try { process.stderr.write(`[selector-health-sink] ${String((e as any)?.message || e)}\n`); } catch {}
  }
}
