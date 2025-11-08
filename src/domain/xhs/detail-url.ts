// 统一维护小红书“笔记详情”路由识别规则
// 覆盖搜索结果、发现页详情、历史路径与问答/视频等形态
export const DETAIL_URL_RE =
	/(\/explore\/|\/search_result\/|\/discovery\/item\/|\/question\/|\/p\/|\/zvideo\/)/i;

export function isDetailUrl(url: string): boolean {
	try {
		return DETAIL_URL_RE.test(String(url || ""));
	} catch {
		return false;
	}
}
