import { defineConfig } from "vitest/config";

export default defineConfig({
	test: {
		globals: true,
		environment: "node",
		coverage: {
			provider: "v8",
			reporter: ["text", "json", "html"],
			include: [
				"src/mcp/tools/page.ts",
				"src/mcp/tools/browser.ts",
				"src/mcp/tools/xhsShortcuts.ts",
				"src/mcp/tools/xhs.ts",
			],
			thresholds: {
				lines: 85,
				branches: 75,
			},
		},
	},
});
