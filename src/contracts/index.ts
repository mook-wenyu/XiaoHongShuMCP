/**
 * 接口定义层（Contracts）
 *
 * 定义核心服务的契约接口，实现接口隔离原则和依赖倒置。
 * 所有接口采用 TypeScript 泛型和 Playwright 类型确保类型安全。
 *
 * @packageDocumentation
 */

export * from "./IRoxyClient.js";
// 官方唯一：移除本地 PlaywrightConnector/ConnectionManager 接口
export * from "./ILogger.js";
export * from "./ITaskRegistry.js";
