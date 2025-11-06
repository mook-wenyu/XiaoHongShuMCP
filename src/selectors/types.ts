/* 中文注释：选择器提示类型（Selector-only 模式） */

// 说明：这是运行时无依赖的 TypeScript 类型定义，
// 与历史 DSL(zod) 字段保持名称和含义一致，供 selector.ts 与调用方复用。

export type TextExpr =
	| string
	| { exact: string; caseSensitive?: boolean }
	| { contains: string; caseSensitive?: boolean }
	| { regex: string };

export interface TargetHints {
	// 可选：逻辑选择器 ID（用于从配置 JSON 取候选）
	id?: string;

	// 基础策略（按优先级探测 role/label/placeholder/testId/text/selector）
	selector?: string;
	role?: string; // ARIA role，如 'button'
	label?: string; // getByLabel
	placeholder?: string; // getByPlaceholder
	text?: TextExpr; // getByText
	testId?: string; // getByTestId
	name?: TextExpr; // role 的 name（可与 role 联用）

	// 过滤与范围
	hasText?: string | { regex: string };
	has?: TargetHints; // 作为 has 过滤器的子定位器

	// 作用域与序（容器别名 + 索引）
	within?: string; // 先前命名的容器别名
	nth?: number | "first" | "last";

	// 可见性与等待
	visible?: boolean;
	timeoutMs?: number; // 单次定位等待上限（毫秒）

	// 策略偏好与回退
	prefer?: Array<
		"role" | "label" | "placeholder" | "testId" | "title" | "alt" | "text" | "selector"
	>;
	alternatives?: TargetHints[]; // 候选链，按序探测
}
