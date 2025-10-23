/* 中文注释：ESLint 基线（与 Prettier 协同），Tab + 双引号 */
module.exports = {
	env: { es2022: true, node: true },
	parser: "@typescript-eslint/parser",
	parserOptions: { sourceType: "module", ecmaVersion: "latest" },
	plugins: ["@typescript-eslint"],
	extends: [
		"eslint:recommended",
		"plugin:@typescript-eslint/recommended",
		"prettier"
	],
	rules: {
		quotes: ["error", "double"],
		"@typescript-eslint/no-unused-vars": ["warn", { argsIgnorePattern: "^_" }]
	}
};
