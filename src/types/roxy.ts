/* 中文注释：RoxyBrowser 类型定义 */
export interface RoxyOpenRequest {
	dirId: string;
	workspaceId?: string;
	args?: string[];
}

export interface RoxyOpenResponse {
	data: { id: string; ws: string; http: string };
}

export interface RoxyConnInfoResponse {
	data: { id: string; ws: string; http: string }[];
}
