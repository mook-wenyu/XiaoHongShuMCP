/* 中文注释：选择器健康度报表生成脚本（可读取 JSON/NDJSON 输入） */
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { generateHealthReport } from "../src/selectors/report.js";
import { healthMonitor } from "../src/selectors/health.js";

interface SampleRec { selectorId: string; success: boolean; durationMs: number }

function parseArgs(argv: string[]) {
  const args: Record<string, string | boolean> = {};
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith("--")) {
      const [k, v] = a.split("=");
      args[k.slice(2)] = v ?? true;
    }
  }
  return args;
}

async function ingestFile(p: string) {
  const full = resolve(process.cwd(), p);
  const raw = await readFile(full, "utf-8");
  const lines = raw.split(/\r?\n/).filter(Boolean);
  if (lines.length > 0 && lines[0].trim().startsWith("{")) {
    // NDJSON 逐行 JSON
    for (const line of lines) {
      try {
        const rec = JSON.parse(line) as Partial<SampleRec>;
        if (rec.selectorId && typeof rec.success === "boolean" && typeof rec.durationMs === "number") {
          healthMonitor.record(rec.selectorId, rec.success, rec.durationMs);
        }
      } catch {}
    }
    return;
  }
  // 尝试整体 JSON
  try {
    const arr = JSON.parse(raw) as any[];
    for (const r of arr) {
      if (r && r.selectorId && typeof r.success === "boolean" && typeof r.durationMs === "number") {
        healthMonitor.record(r.selectorId, r.success, r.durationMs);
      }
    }
  } catch {}
}

function toCsv(rows: any[]) {
  const header = ["selectorId","totalCount","successCount","failureCount","successRate","avgDurationMs","lastUsed"];
  const body = rows.map(r => [r.selectorId, r.totalCount, r.successCount, r.failureCount, r.successRate, r.avgDurationMs, r.lastUsed].join(","));
  return [header.join(","), ...body].join("\n");
}

async function main() {
  const args = parseArgs(process.argv);
  const inputs = (typeof args.input === "string" ? (args.input as string).split(",") : []).filter(Boolean);
  for (const p of inputs) await ingestFile(p);

  const report = generateHealthReport({});
  const outDir = resolve("reports");
  await mkdir(outDir, { recursive: true });
  const jsonPath = resolve(outDir, "selector-health.json");
  await writeFile(jsonPath, JSON.stringify(report, null, 2), "utf-8");

  if (args.csv) {
    const csvPath = resolve(outDir, "selector-health.csv");
    await writeFile(csvPath, toCsv(report.selectors), "utf-8");
  }
  // 控制台简报
  // eslint-disable-next-line no-console
  console.log(JSON.stringify({ ok: true, jsonPath, csv: !!args.csv }, null, 2));
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exit(1);
});
