/* 批量修复 errorRecovery.test.ts mock 响应 */
import { readFileSync, writeFileSync } from 'fs';

const file = 'tests/integration/errorRecovery.test.ts';
let content = readFileSync(file, 'utf-8');

// 替换成功响应
content = content.replace(
	/return Promise\.resolve\(\{\s*ok:\s*true,\s*status:\s*200,\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(([^)]+)\)\s*\}\s*as\s*any\);/g,
	'return Promise.resolve(createMockResponse($1));'
);

// 替换失败响应（5xx错误）
content = content.replace(
	/return Promise\.resolve\(\{\s*ok:\s*false,\s*status:\s*(\d+),\s*statusText:\s*"([^"]+)",\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(([^)]+)\)\s*\}\);/g,
	(match, status, statusText, data) => {
		return `return Promise.resolve(createMockResponse(${data}, { ok: false, status: ${status}, statusText: "${statusText}" }));`;
	}
);

// 替换简单的 mockResolvedValue
content = content.replace(
	/mockFetch\.mockResolvedValue\(\{\s*ok:\s*(true|false),\s*status:\s*(\d+),\s*statusText:\s*"([^"]+)",\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(([^)]+)\)\s*\}\);/g,
	(match, ok, status, statusText, data) => {
		return `mockFetch.mockResolvedValue(createMockResponse(${data}, { ok: ${ok}, status: ${status}, statusText: "${statusText}" }));`;
	}
);

// 处理简单的成功响应
content = content.replace(
	/mockFetch\.mockResolvedValue\(\{\s*ok:\s*true,\s*status:\s*200,\s*json:\s*\(\)\s*=>\s*Promise\.resolve\(([^)]+)\)\s*\}\);/g,
	'mockFetch.mockResolvedValue(createMockResponse($1));'
);

writeFileSync(file, content, 'utf-8');
console.log('✅ Fixed errorRecovery.test.ts mock responses');
