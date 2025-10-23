// 中文注释：ESLint v9 Flat Config（与 Prettier 协同），ESM 格式
// - 迁移自 .eslintrc.*；使用 files/ignores 精准匹配
// - TypeScript 解析与最小推荐规则，避免与 Prettier 冲突
import tseslint from "@typescript-eslint/eslint-plugin";
import tsparser from "@typescript-eslint/parser";

export default [
  {
    ignores: [
      "node_modules/**",
      "dist/**",
      "artifacts/**",
      "legacy/**"
    ]
  },
  {
    files: ["src/**/*.ts", "tests/**/*.ts"],
    languageOptions: {
      parser: tsparser,
      ecmaVersion: 2022,
      sourceType: "module"
    },
    plugins: { "@typescript-eslint": tseslint },
    rules: {
      // 最小化规则集，先与 Prettier 保持相容，再逐步加严
      "@typescript-eslint/no-unused-vars": ["warn", { argsIgnorePattern: "^_" }],
      quotes: ["error", "double"]
    }
  }
];