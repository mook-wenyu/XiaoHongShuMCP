/* 全面修复 roxyClient.test.ts 中所有 mock 响应 */
import { readFileSync, writeFileSync } from 'fs';

const file = 'tests/unit/services/roxyClient.test.ts';
let content = readFileSync(file, 'utf-8');

// 修复 mockImplementation 中的响应（复杂多行）
// 模式 1: return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(DATA) })
content = content.replace(
	/return Promise\.resolve\(\{\s*ok:\s*true,\s*status:\s*200,\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(([^}]+\}[^}]*)\),?\s*\}\);/g,
	'return Promise.resolve(createMockResponse($1));'
);

// 修复跨多行的情况
content = content.replace(
	/return Promise\.resolve\(\{\s*ok:\s*true,\s*status:\s*200,\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(\{[\s\S]*?data:\s*\{[\s\S]*?\},?\s*\}\),?\s*\}\);/g,
	(match) => {
		// 提取 data 对象
		const dataMatch = match.match(/data:\s*(\{[\s\S]*?\})\s*\}/);
		if (dataMatch) {
			return `return Promise.resolve(createMockResponse({ data: ${dataMatch[1]} }));`;
		}
		return match;
	}
);

writeFileSync(file, content, 'utf-8');
console.log('✅ Fixed roxyClient.test.ts mockImplementation responses');
