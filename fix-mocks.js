/* 批量修复 mock 响应 */
import { readFileSync, writeFileSync } from 'fs';

const file = 'tests/unit/services/roxyClient.test.ts';
let content = readFileSync(file, 'utf-8');

// 替换模式：mockFetch.mockResolvedValue({ ok: true, status: 200, json: () => Promise.resolve(DATA) })
// 替换为：mockFetch.mockResolvedValue(createMockResponse(DATA))
content = content.replace(
	/mockFetch\.mockResolvedValue\(\{\s*ok:\s*true,\s*status:\s*\d+,\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(([^)]+)\),?\s*\}\)/g,
	'mockFetch.mockResolvedValue(createMockResponse($1))'
);

// 处理字符串响应的情况
content = content.replace(
	/mockFetch\.mockResolvedValue\(\{\s*ok:\s*true,\s*status:\s*200,\s*json:\s*\(\)\s*=>\s*Promise\.resolve\("([^"]+)"\),?\s*\}\)/g,
	'mockFetch.mockResolvedValue(createMockResponse("$1"))'
);

writeFileSync(file, content, 'utf-8');
console.log('✅ Fixed mock responses');
