/**
 * Roxy API 指纹配置类型定义
 *
 * 定义浏览器窗口的完整指纹配置选项（50+ 配置项）。
 *
 * @remarks
 * 指纹配置用于控制浏览器的各种特征，实现反检测和隐私保护。
 * 包括语言、时区、地理位置、分辨率、WebGL、Canvas、AudioContext、字体等。
 *
 * @packageDocumentation
 */

/**
 * 浏览器指纹配置
 *
 * @remarks
 * 完整的指纹配置参数，用于创建窗口时自定义浏览器特征。
 *
 * 配置项分类：
 * - 语言和时区：language, displayLanguage, timeZone
 * - 地理位置：position, longitude, latitude
 * - 分辨率：openWidth, openHeight, resolutionType, resolution*
 * - WebGL/Canvas：webGL, webGLInfo, canvas
 * - 设备信息：deviceInfo, hardwareConcurrent, deviceMemory
 * - 权限和限制：forbidAudio, forbidImage, forbidMedia, disableSsl
 */
export interface FingerprintConfig {
	// ===== 语言配置 =====
	/** 是否根据 IP 自动设置语言（true: 自动, false: 手动） */
	isLanguageBaseIp?: boolean;
	/** 浏览器语言（如 "en-US", "zh-CN"） */
	language?: string;
	/** 是否根据 IP 自动设置显示语言 */
	isDisplayLanguageBaseIp?: boolean;
	/** 显示语言列表 */
	displayLanguage?: string;

	// ===== 时区配置 =====
	/** 是否自动设置时区 */
	isTimeZone?: boolean;
	/** 时区（如 "Asia/Shanghai", "America/New_York"） */
	timeZone?: string;

	// ===== 地理位置配置 =====
	/** 地理位置模式 */
	position?: string;
	/** 是否根据 IP 自动设置地理位置 */
	isPositionBaseIp?: boolean;
	/** 经度 */
	longitude?: string;
	/** 纬度 */
	latitude?: string;
	/** 地理位置精度 */
	precisionPos?: number;

	// ===== 媒体权限 =====
	/** 是否禁用音频（true: 禁用, false: 启用） */
	forbidAudio?: boolean;
	/** 是否禁用图片（true: 禁用, false: 启用） */
	forbidImage?: boolean;
	/** 是否禁用媒体（true: 禁用, false: 启用） */
	forbidMedia?: boolean;

	// ===== 窗口尺寸 =====
	/** 打开窗口宽度 */
	openWidth?: number;
	/** 打开窗口高度 */
	openHeight?: number;

	// ===== 书签和位置 =====
	/** 打开书签 */
	openBookmarks?: boolean;
	/** 位置开关 */
	positionSwitch?: string;
	/** 窗口比例位置 */
	windowRatioPosition?: string;

	// ===== 显示和同步 =====
	/** 是否显示名称 */
	isDisplayName?: boolean;
	/** 同步书签 */
	syncBookmarks?: boolean;
	/** 同步历史记录 */
	syncHistory?: boolean;
	/** 同步扩展 */
	syncExtensions?: boolean;
	/** 同步自动填充 */
	syncAutofill?: boolean;
	/** 同步密码 */
	syncPasswords?: boolean;

	// ===== 清除选项 =====
	/** 清除缓存文件 */
	clearCacheFilesBeforeLaunch?: boolean;
	/** 清除 Cookie */
	clearCookiesBeforeLaunch?: boolean;
	/** 清除历史记录 */
	clearHistoryBeforeLaunch?: boolean;

	// ===== 指纹和密码 =====
	/** 随机指纹 */
	randomFingerprint?: boolean;
	/** 禁止保存密码 */
	forbidSavePassword?: boolean;

	// ===== 启动和工作台 =====
	/** 停止打开标签页 */
	stopOpenTabs?: boolean;
	/** 停止打开URL */
	stopOpenUrls?: boolean;
	/** 打开工作台 */
	openWorkbench?: boolean;

	// ===== 分辨率配置 =====
	/** 分辨率类型 */
	resolutionType?: string;
	/** 分辨率宽度 */
	resolutionWidth?: number;
	/** 分辨率高度 */
	resolutionHeight?: number;

	// ===== 字体配置 =====
	/** 字体类型 */
	fontType?: string;

	// ===== WebRTC 配置 =====
	/** WebRTC 设置 */
	webRTC?: string;

	// ===== WebGL 配置 =====
	/** WebGL 供应商 */
	webGL?: string;
	/** WebGL 信息 */
	webGLInfo?: string;
	/** WebGL 供应商（备选字段） */
	webGLVendor?: string;
	/** WebGL 渲染器 */
	webGLRenderer?: string;

	// ===== WebGPU 配置 =====
	/** WebGPU 设置 */
	webGpu?: string;

	// ===== Canvas 配置 =====
	/** Canvas 指纹 */
	canvas?: string;

	// ===== AudioContext 配置 =====
	/** AudioContext 指纹 */
	audioContext?: string;

	// ===== 语音合成 =====
	/** 语音列表 */
	speechVoices?: string;

	// ===== 隐私配置 =====
	/** Do Not Track 设置 */
	doNotTrack?: string;

	// ===== ClientRects 配置 =====
	/** ClientRects 噪声 */
	clientRects?: string;

	// ===== 设备信息 =====
	/** 设备信息 */
	deviceInfo?: string;
	/** 设备名称开关 */
	deviceNameSwitch?: boolean;

	// ===== 硬件信息 =====
	/** MAC 地址信息 */
	macInfo?: string;
	/** 硬件并发数 */
	hardwareConcurrent?: number;
	/** 设备内存（GB） */
	deviceMemory?: number;

	// ===== SSL 配置 =====
	/** 禁用 SSL */
	disableSsl?: boolean;
	/** 禁用 SSL 列表 */
	disableSslList?: string[];

	// ===== 端口扫描保护 =====
	/** 端口扫描保护 */
	portScanProtect?: boolean;
	/** 端口扫描列表 */
	portScanList?: string[];

	// ===== GPU 和沙箱 =====
	/** 使用 GPU */
	useGpu?: boolean;
	/** 沙箱权限 */
	sandboxPermission?: boolean;

	// ===== 启动参数 =====
	/** 启动参数 */
	startupParam?: string;
}
