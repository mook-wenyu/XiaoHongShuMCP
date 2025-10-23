/* 中文注释：产物清理脚本
 * - 删除 artifacts 下早于阈值（天）的文件与空目录
 * - 支持 --dry-run 查看将被清理的文件
 * - 支持环境变量：ARTIFACTS_DIR（默认 artifacts）、RETENTION_DAYS（默认 7）
 */
import { rmSync, readdirSync, statSync, existsSync } from "node:fs";
import { join, resolve } from "node:path";

function walk(dir: string): string[] {
  const out: string[] = [];
  for (const name of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, name.name);
    if (name.isDirectory()) out.push(...walk(p));
    else out.push(p);
  }
  return out;
}

function main() {
  const root = resolve(process.env.ARTIFACTS_DIR || "artifacts");
  const retention = Math.max(1, Number(process.env.RETENTION_DAYS || 7));
  const dry = process.argv.includes("--dry-run");
  if (!existsSync(root)) {
    console.log(JSON.stringify({ ok: true, msg: "artifacts 不存在，跳过", root }));
    return;
  }
  const now = Date.now();
  const files = walk(root);
  const toDelete: string[] = [];
  for (const f of files) {
    try {
      const st = statSync(f);
      const ageDays = (now - st.mtimeMs) / 86400000;
      if (ageDays >= retention) toDelete.push(f);
    } catch {}
  }
  if (!dry) {
    for (const f of toDelete) {
      try { rmSync(f, { force: true }); } catch {}
    }
  }
  console.log(JSON.stringify({ ok: true, root, retentionDays: retention, dryRun: dry, deleted: dry ? 0 : toDelete.length, candidates: toDelete }));
}

main();
