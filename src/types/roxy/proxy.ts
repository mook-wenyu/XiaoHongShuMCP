/**
 * Roxy API 代理配置类型定义
 *
 * 定义浏览器窗口的代理配置选项。
 *
 * @remarks
 * 代理配置用于控制浏览器窗口的网络请求路由。
 * 支持多种代理方式和类型（HTTP、HTTPS、SOCKS5、SSH）。
 *
 * @packageDocumentation
 */

/**
 * 代理配置
 *
 * @remarks
 * 完整的代理配置参数，用于创建窗口时指定代理设置。
 *
 * 代理方式（proxyMethod）：
 * - 0: 不使用代理
 * - 1: 使用代理
 * - 2: 从代理池获取
 *
 * 代理类型（proxyCategory）：
 * - http: HTTP 代理
 * - https: HTTPS 代理
 * - socks5: SOCKS5 代理
 * - ssh: SSH 代理
 *
 * IP 类型（ipType）：
 * - ipv4: IPv4 地址
 * - ipv6: IPv6 地址
 */
export interface ProxyConfig {
	/** 代理方式（0: 不使用, 1: 使用, 2: 从代理池获取） */
	proxyMethod?: number;
	/** 代理类型（http, https, socks5, ssh） */
	proxyCategory?: string;
	/** IP 类型（ipv4, ipv6） */
	ipType?: string;
	/** 代理主机地址 */
	host?: string;
	/** 代理端口 */
	port?: number;
	/** 代理用户名 */
	proxyUserName?: string;
	/** 代理密码 */
	proxyPassword?: string;
	/** 刷新 URL（代理池使用） */
	refreshUrl?: string;
	/** 检查通道 */
	checkChannel?: string;
}
