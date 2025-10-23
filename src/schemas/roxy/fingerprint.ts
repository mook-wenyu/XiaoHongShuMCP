/**
 * Roxy API 指纹配置 Zod Schema 定义
 *
 * 提供运行时验证和类型推断能力。
 *
 * @packageDocumentation
 */

import { z } from "zod";

/**
 * 浏览器指纹配置 Schema
 *
 * @remarks
 * 验证规则：
 * - 布尔值字段：使用 z.boolean()
 * - 数值字段：使用 z.number() 并添加范围限制
 * - 字符串字段：使用 z.string()
 * - 数组字段：使用 z.array()
 * - 所有字段都是可选的（.optional()）
 */
export const FingerprintConfigSchema = z
	.object({
		// ===== 语言配置 =====
		isLanguageBaseIp: z.boolean().optional().describe("是否根据 IP 自动设置语言"),
		language: z.string().optional().describe("浏览器语言（如 en-US, zh-CN）"),
		isDisplayLanguageBaseIp: z.boolean().optional().describe("是否根据 IP 自动设置显示语言"),
		displayLanguage: z.string().optional().describe("显示语言列表"),

		// ===== 时区配置 =====
		isTimeZone: z.boolean().optional().describe("是否自动设置时区"),
		timeZone: z.string().optional().describe("时区（如 Asia/Shanghai, America/New_York）"),

		// ===== 地理位置配置 =====
		position: z.string().optional().describe("地理位置模式"),
		isPositionBaseIp: z.boolean().optional().describe("是否根据 IP 自动设置地理位置"),
		longitude: z.string().optional().describe("经度"),
		latitude: z.string().optional().describe("纬度"),
		precisionPos: z.number().optional().describe("地理位置精度"),

		// ===== 媒体权限 =====
		forbidAudio: z.boolean().optional().describe("是否禁用音频"),
		forbidImage: z.boolean().optional().describe("是否禁用图片"),
		forbidMedia: z.boolean().optional().describe("是否禁用媒体"),

		// ===== 窗口尺寸 =====
		openWidth: z.number().int().positive().optional().describe("打开窗口宽度"),
		openHeight: z.number().int().positive().optional().describe("打开窗口高度"),

		// ===== 书签和位置 =====
		openBookmarks: z.boolean().optional().describe("打开书签"),
		positionSwitch: z.string().optional().describe("位置开关"),
		windowRatioPosition: z.string().optional().describe("窗口比例位置"),

		// ===== 显示和同步 =====
		isDisplayName: z.boolean().optional().describe("是否显示名称"),
		syncBookmarks: z.boolean().optional().describe("同步书签"),
		syncHistory: z.boolean().optional().describe("同步历史记录"),
		syncExtensions: z.boolean().optional().describe("同步扩展"),
		syncAutofill: z.boolean().optional().describe("同步自动填充"),
		syncPasswords: z.boolean().optional().describe("同步密码"),

		// ===== 清除选项 =====
		clearCacheFilesBeforeLaunch: z.boolean().optional().describe("清除缓存文件"),
		clearCookiesBeforeLaunch: z.boolean().optional().describe("清除 Cookie"),
		clearHistoryBeforeLaunch: z.boolean().optional().describe("清除历史记录"),

		// ===== 指纹和密码 =====
		randomFingerprint: z.boolean().optional().describe("随机指纹"),
		forbidSavePassword: z.boolean().optional().describe("禁止保存密码"),

		// ===== 启动和工作台 =====
		stopOpenTabs: z.boolean().optional().describe("停止打开标签页"),
		stopOpenUrls: z.boolean().optional().describe("停止打开URL"),
		openWorkbench: z.boolean().optional().describe("打开工作台"),

		// ===== 分辨率配置 =====
		resolutionType: z.string().optional().describe("分辨率类型"),
		resolutionWidth: z.number().int().positive().optional().describe("分辨率宽度"),
		resolutionHeight: z.number().int().positive().optional().describe("分辨率高度"),

		// ===== 字体配置 =====
		fontType: z.string().optional().describe("字体类型"),

		// ===== WebRTC 配置 =====
		webRTC: z.string().optional().describe("WebRTC 设置"),

		// ===== WebGL 配置 =====
		webGL: z.string().optional().describe("WebGL 供应商"),
		webGLInfo: z.string().optional().describe("WebGL 信息"),
		webGLVendor: z.string().optional().describe("WebGL 供应商（备选字段）"),
		webGLRenderer: z.string().optional().describe("WebGL 渲染器"),

		// ===== WebGPU 配置 =====
		webGpu: z.string().optional().describe("WebGPU 设置"),

		// ===== Canvas 配置 =====
		canvas: z.string().optional().describe("Canvas 指纹"),

		// ===== AudioContext 配置 =====
		audioContext: z.string().optional().describe("AudioContext 指纹"),

		// ===== 语音合成 =====
		speechVoices: z.string().optional().describe("语音列表"),

		// ===== 隐私配置 =====
		doNotTrack: z.string().optional().describe("Do Not Track 设置"),

		// ===== ClientRects 配置 =====
		clientRects: z.string().optional().describe("ClientRects 噪声"),

		// ===== 设备信息 =====
		deviceInfo: z.string().optional().describe("设备信息"),
		deviceNameSwitch: z.boolean().optional().describe("设备名称开关"),

		// ===== 硬件信息 =====
		macInfo: z.string().optional().describe("MAC 地址信息"),
		hardwareConcurrent: z.number().int().positive().optional().describe("硬件并发数"),
		deviceMemory: z.number().positive().optional().describe("设备内存（GB）"),

		// ===== SSL 配置 =====
		disableSsl: z.boolean().optional().describe("禁用 SSL"),
		disableSslList: z.array(z.string()).optional().describe("禁用 SSL 列表"),

		// ===== 端口扫描保护 =====
		portScanProtect: z.boolean().optional().describe("端口扫描保护"),
		portScanList: z.array(z.string()).optional().describe("端口扫描列表"),

		// ===== GPU 和沙箱 =====
		useGpu: z.boolean().optional().describe("使用 GPU"),
		sandboxPermission: z.boolean().optional().describe("沙箱权限"),

		// ===== 启动参数 =====
		startupParam: z.string().optional().describe("启动参数"),
	})
	.passthrough(); // 允许额外字段，向后兼容

/**
 * 从 Schema 推断的指纹配置类型
 */
export type FingerprintConfig = z.infer<typeof FingerprintConfigSchema>;
