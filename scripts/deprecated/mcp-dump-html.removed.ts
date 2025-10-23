import { spawn } from 'node:child_process';
import { resolve } from 'node:path';
import process from 'node:process';

// 简易 MCP 调用脚本：调用 xhs_dump_html
// 用法：tsx scripts/mcp-dump-html.ts --dirId=user --tag=discover

function getArg(name: string, def?: string) {
  const idx = process.argv.findIndex(a => a === `--${name}`);
  if (idx >= 0 && process.argv[idx+1]) return process.argv[idx+1];
  return def;
}

const dirId = getArg('dirId', 'user')!;
const tag = getArg('tag', 'page')!;

const serverPath = resolve('dist/mcp/server.js');

const child = spawn(process.execPath, [serverPath], { stdio: ['pipe','pipe','pipe'] });

const req = {
  jsonrpc: '2.0', id: 1, method: 'tools/call', params: {
    name: 'xhs_dump_html', arguments: { dirId, tag }
  }
};

child.stdout.setEncoding('utf-8');
child.stderr.setEncoding('utf-8');
child.stdout.on('data', d => process.stdout.write(d));
child.stderr.on('data', d => process.stderr.write(d));

child.stdin.write(JSON.stringify(req) + '\n');
