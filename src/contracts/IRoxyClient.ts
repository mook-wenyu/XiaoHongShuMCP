/**
 * Roxy API 客户端接口
 *
 * 封装与 RoxyBrowser API 的交互，包括窗口打开、关闭、连接信息查询等。
 *
 * @example
 * ```typescript
 * const client: IRoxyClient = new RoxyClient(baseURL, token);
 * await client.health(); // 健康检查
 * const { id, ws } = await client.open('dirId123'); // 打开窗口
 * await client.close('dirId123'); // 关闭窗口
 * ```
 */

import type {
	WorkspaceListParams,
	WorkspaceListResponse,
	WindowListParams,
	WindowListResponse,
	WindowCreateRequest,
	WindowCreateResponse,
	ConnectionInfo,
	ConnectionInfoResponse,
	ApiResponse,
} from "../types/roxy/index.js";

export interface IRoxyClient {
	/**
	 * 健康检查
	 * @returns 健康状态信息
	 */
	health(): Promise<{ code?: number; msg?: unknown } | string>;

	/**
	 * 打开浏览器窗口
	 * @param dirId 窗口标识符
	 * @param args 启动参数
	 * @param workspaceId 工作空间 ID
	 * @returns 窗口连接信息（包含 WebSocket 端点）
	 */
	open(dirId: string, args?: string[], workspaceId?: string | number): Promise<ConnectionInfo>;

	/**
	 * 关闭浏览器窗口
	 * @param dirId 窗口标识符
	 */
	close(dirId: string): Promise<void>;

	/**
	 * 查询窗口连接信息
	 * @param dirIds 窗口标识符列表
	 * @returns 连接信息响应
	 */
	connectionInfo(dirIds: string[]): Promise<ConnectionInfoResponse>;

	/**
	 * 获取工作空间列表
	 * @param params 分页查询参数
	 * @returns 工作空间列表响应
	 */
	workspaces(params?: WorkspaceListParams): Promise<WorkspaceListResponse>;

	/**
	 * 获取浏览器窗口列表
	 * @param params 窗口查询参数（必须包含 workspaceId）
	 * @returns 窗口列表响应
	 */
	listWindows(params: WindowListParams): Promise<WindowListResponse>;

	/**
	 * 创建浏览器窗口
	 * @param body 窗口创建请求参数
	 * @returns 创建的窗口信息响应
	 */
	createWindow(body: WindowCreateRequest): Promise<WindowCreateResponse>;

	// detailWindow 已移除：不再暴露窗口详情接口，避免与非关键 API 的耦合

	/**
	 * 生成窗口随机指纹
	 *
	 * @param workspaceId 工作空间 ID
	 * @param dirId 窗口 ID
	 * @returns 操作结果
	 *
	 * @remarks
	 * 为指定窗口生成随机指纹配置，增强隐私保护和反检测能力。
	 */
	randomFingerprint(workspaceId: number | string, dirId: string): Promise<ApiResponse<null>>;

	/**
	 * 确保窗口已打开（先查询运行中窗口，若不存在再打开）
	 * @param dirId 窗口标识符
	 * @param workspaceId 工作空间 ID
	 * @param args 启动参数
	 * @returns 窗口连接信息
	 */
	ensureOpen(
		dirId: string,
		workspaceId?: string | number,
		args?: string[],
	): Promise<ConnectionInfo>;
}
