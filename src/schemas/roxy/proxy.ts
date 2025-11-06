/**
 * Roxy API 代理配置 Zod Schema 定义
 *
 * 提供运行时验证和类型推断能力。
 *
 * @packageDocumentation
 */

import { z } from "zod";

/**
 * 代理配置 Schema
 *
 * @remarks
 * 验证规则：
 * - proxyMethod: 0/1/2（不使用/使用/从代理池获取）
 * - proxyCategory: 枚举值（http/https/socks5/ssh）
 * - ipType: 枚举值（ipv4/ipv6）
 * - port: 端口号（1-65535）
 * - 所有字段都是可选的
 */
export const ProxyConfigSchema = z
	.object({
		proxyMethod: z
			.number()
			.int()
			.min(0)
			.max(2)
			.optional()
			.describe("代理方式（0: 不使用, 1: 使用, 2: 从代理池获取）"),
		proxyCategory: z.enum(["http", "https", "socks5", "ssh"]).optional().describe("代理类型"),
		ipType: z.enum(["ipv4", "ipv6"]).optional().describe("IP 类型"),
		host: z.string().optional().describe("代理主机地址"),
		port: z.number().int().min(1).max(65535).optional().describe("代理端口"),
		proxyUserName: z.string().optional().describe("代理用户名"),
		proxyPassword: z.string().optional().describe("代理密码"),
		refreshUrl: z.string().url().optional().describe("刷新 URL（代理池使用）"),
		checkChannel: z.string().optional().describe("检查通道"),
	})
	.passthrough(); // 允许额外字段，向后兼容

/**
 * 从 Schema 推断的代理配置类型
 */
export type ProxyConfig = z.infer<typeof ProxyConfigSchema>;
