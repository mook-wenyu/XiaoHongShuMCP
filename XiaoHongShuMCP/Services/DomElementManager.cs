using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services
{
    /// <summary>
    /// DOM 元素管理服务（实现 IDomElementManager）。
    /// - 职责：维护“选择器别名 → 候选选择器列表”的映射，提供基于页面状态（<see cref="PageState"/>）的
    ///         精细化候选集；用于屏蔽页面结构调整带来的不稳定，提升选择器健壮性。
    /// - 选择器策略：
    ///   1) 同一别名维护 3–5 个候选（从高到低按成功概率排序）；
    ///   2) 可维护页面状态特定的候选集，使用时会“状态候选优先 + 通用候选补充（去重）”；
    ///   3) 支持回退：如找不到别名，则直接返回别名本身以兼容“直接传 CSS/XPath”。
    /// - 页面状态检测：先 URL 关键词判断，后 DOM 特征回退，避免纯 URL 方案对 SPA 场景不敏感。
    /// - 线程安全：该管理器通常在单线程 UI 自动化流程中使用（读多写少）。默认字典非并发容器，
    ///             如需动态更新映射请在上层提供同步保障。
    /// 维护建议：新增或调整选择器时，保持“状态优先 + 通用兜底 + 顺序代表优先级”的约定。
    /// </summary>
    public class DomElementManager : IDomElementManager
    {
        private readonly Dictionary<string, List<string>> _selectors;
        private Dictionary<PageState, Dictionary<string, List<string>>> _pageStateSelectors;

        public DomElementManager()
        {
            _pageStateSelectors = new Dictionary<PageState, Dictionary<string, List<string>>>();
            // 基于真实小红书HTML结构的CSS选择器管理
            // 为每个关键功能提供3-5个真实的备用选择器，按成功概率排序
            _selectors = new Dictionary<string, List<string>>
            {
                // ===== 登录认证相关 =====

                // 登录按钮 - 优先级从高到低（基于真实HTML结构优化）
                {
                    "LoginButton", [
                        "#login-btn",                                     // 真实登录按钮ID，最准确
                        ".side-bar-component.login-btn button#login-btn", // 侧边栏登录按钮完整路径
                        ".reds-button-new.login-btn",                     // 真实的按钮类组合
                        "button.login-btn[id='login-btn']",               // 带ID的登录按钮
                        ".login-btn",
                        "button:has-text('登录')",
                        "[data-testid='login-button']",
                        "button[type='button']:has-text('登录')",
                        ".auth-btn.primary"
                    ]
                },

                // 用户头像 - 基于真实CSS类名
                {
                    "AvatarIcon", [
                        ".reds-avatar",
                        ".reds-avatar-img-box",
                        ".user-avatar",
                        ".avatar-wrapper img",
                        "[data-testid='user-avatar']"
                    ]
                },

                // 用户信息区域
                {
                    "UserInfo", [
                        ".user-name",
                        ".author",
                        ".user-info",
                        ".user-profile-info",
                        "[data-testid='user-info']"
                    ]
                },

                // ===== 个人页面数据提取选择器 - 基于真实HTML结构 =====

                // 个人页面容器 - 识别是否在个人页面
                {
                    "UserPageContainer", [
                        "#userPageContainer.user-page",     // 真实个人页面容器（最精确）
                        ".user-page[data-csr-exp='false']", // 带CSR属性的用户页面
                        "div[id='userPageContainer']",      // 通用个人页面容器
                        ".user-page",                       // 通用用户页面类
                        "[data-testid='user-page']"
                    ]
                },

                // 用户头像区域 - 个人页面专用
                {
                    "UserPageAvatar", [
                        ".user .user-info .avatar img.user-image",                 // 完整的头像路径（最精确）
                        ".avatar-wrapper img.user-image[crossorigin='anonymous']", // 跨域头像图片
                        "img[src*='sns-avatar-qc.xhscdn.com']",                    // 小红书头像CDN
                        ".user-image[data-xhs-img]",                               // 带小红书图片标记的头像
                        ".user-info .avatar img",                                  // 用户信息区域头像
                        "[data-testid='user-page-avatar']"
                    ]
                },

                // 用户名 - 个人页面专用
                {
                    "UserPageName", [
                        ".user-basic .user-nickname .user-name", // 完整的用户名路径（最精确）
                        ".basic-info .user-basic .user-name",    // 基础信息区域用户名
                        ".user-nickname .user-name",             // 用户昵称容器内的名称
                        ".user-name:not(.verify-icon)",          // 排除认证图标的用户名
                        ".user-name",                            // 通用用户名
                        "[data-testid='user-page-name']"
                    ]
                },

                // 小红书号 - 个人页面专用
                {
                    "UserRedId", [
                        ".user-content .user-redId", // 真实小红书号路径（最精确）
                        "span.user-redId",           // 小红书号span元素
                        "span:has-text('小红书号：')",    // 包含小红书号文本的span
                        "[data-testid='user-red-id']"
                    ]
                },

                // 个人简介 - 个人页面专用
                {
                    "UserDescription", [
                        ".user-desc",                     // 真实用户描述类（最精确）
                        "div.user-desc[data-v-4947d265]", // 带Vue组件ID的用户描述
                        ".info-part .user-desc",          // 信息区域的用户描述
                        "[data-testid='user-description']"
                    ]
                },

                // 用户统计数据容器
                {
                    "UserStatistics", [
                        ".data-info .user-interactions",          // 真实统计数据路径（最精确）
                        "div.user-interactions[data-v-18b45ae8]", // 带Vue组件ID的用户交互统计
                        ".user-interactions",                     // 通用用户交互统计
                        "[data-testid='user-statistics']"
                    ]
                },

                // 关注数 - 个人页面统计
                {
                    "UserFollowingCount", [
                        ".user-interactions div:nth-child(1) .count",  // 第一个统计项的计数（关注）
                        "span.count:has(+ span.shows:has-text('关注'))", // 关注数的计数span
                        ".user-interactions .count:first-child",       // 第一个计数项
                        "[data-testid='following-count']"
                    ]
                },

                // 粉丝数 - 个人页面统计
                {
                    "UserFollowersCount", [
                        ".user-interactions div:nth-child(2) .count",  // 第二个统计项的计数（粉丝）
                        "span.count:has(+ span.shows:has-text('粉丝'))", // 粉丝数的计数span
                        "[data-testid='followers-count']"
                    ]
                },

                // 获赞与收藏数 - 个人页面统计
                {
                    "UserLikesCollectsCount", [
                        ".user-interactions div:nth-child(3) .count",     // 第三个统计项的计数（获赞与收藏）
                        "span.count:has(+ span.shows:has-text('获赞与收藏'))", // 获赞与收藏数的计数span
                        "[data-testid='likes-collects-count']"
                    ]
                },

                // 认证图标 - 识别认证用户
                {
                    "UserVerifyIcon", [
                        ".user-name .verify-icon svg",             // 用户名旁的认证图标SVG
                        "span.verify-icon[data-v-1d90bc98] svg",   // 带Vue组件ID的认证图标
                        ".verify-icon svg[xlink:href='#company']", // 企业认证图标
                        "svg use[xlink:href='#company']",          // 企业认证SVG使用
                        "[data-testid='user-verify-icon']"
                    ]
                },

                // 关注按钮 - 个人页面操作
                {
                    "UserFollowButton", [
                        ".info-right-area .follow-button",        // 真实关注按钮路径（最精确）
                        "button.reds-button-new.follow-button",   // 完整的关注按钮类
                        "button:has-text('关注')",                  // 包含关注文本的按钮
                        ".reds-button-new.primary.follow-button", // 主要样式的关注按钮
                        "[data-testid='follow-button']"
                    ]
                },

                // 更多操作按钮 - 个人页面
                {
                    "UserMoreActions", [
                        ".info-right-area-more-container svg", // 更多操作图标（最精确）
                        "svg use[xlink:href='#more']",         // 更多操作SVG
                        ".report-entrance-wrapper svg",        // 举报入口包装器图标
                        "[data-testid='user-more-actions']"
                    ]
                },

                // 个人页面标签切换
                {
                    "UserPageTabs", [
                        ".reds-tabs-list .reds-tab-item",                 // 标签列表项（最精确）
                        ".tertiary.center.reds-tabs-list .reds-tab-item", // 完整的标签列表
                        ".reds-tab-item.active",                          // 激活状态的标签
                        ".sub-tab-list span:has-text('笔记')",              // 笔记标签
                        ".sub-tab-list span:has-text('收藏')",              // 收藏标签
                        "[data-testid='user-page-tabs']"
                    ]
                },

                // 空内容提示 - 个人页面
                {
                    "UserPageEmptyContent", [
                        ".empty-container .empty",               // 空内容容器（最精确）
                        "svg[xlink:href='#user_empty_collect']", // 空收藏图标
                        ".empty-text:has-text('还没有发布任何内容')",     // 空发布内容提示
                        ".empty-text:has-text('还没有收藏任何内容')",     // 空收藏内容提示
                        "[data-testid='empty-content']"
                    ]
                },

                // ===== 侧边栏导航相关 =====

                // 侧边栏发布按钮 - 基于真实HTML结构
                {
                    "SidebarPublishButton", [
                        "a[href*='creator.xiaohongshu.com/publish/publish?source=official']", // 真实链接最准确
                        "a[target='_blank'][href*='publish']",                                // 带target属性的发布链接
                        ".link-wrapper[href*='creator.xiaohongshu.com']",                     // 创作平台链接
                        "a:has-text('发布')",                                                   // 包含"发布"文本的链接
                        "[data-testid='sidebar-publish']"
                    ]
                },

                // 侧边栏用户信息链接 - 基于真实HTML结构
                {
                    "SidebarUserProfile", [
                        "a[href*='/user/profile/']",                // 真实用户个人页面链接
                        ".user.side-bar-component a.link-wrapper",  // 用户组件下的链接
                        "a:has(.reds-avatar)",                      // 包含用户头像的链接
                        ".link-wrapper:has(.reds-image-container)", // 包含头像容器的链接
                        "[data-testid='sidebar-user-profile']"
                    ]
                },

                // 侧边栏导航项通用选择器
                {
                    "SidebarNavItem", [
                        ".channel-list-content li", // 导航列表项
                        ".side-bar-component",      // 侧边栏组件
                        ".link-wrapper",            // 通用链接包装器
                        ".bottom-channel",          // 底部导航项
                        "[data-testid='sidebar-nav-item']"
                    ]
                },

                // 侧边栏发现页面链接 - 增强版多级容错选择器
                {
                    "SidebarDiscoverLink", [
                        "a[href*='/explore?channel_id=homefeed_recommend']", // 真实发现页面链接（最高优先级）
                        "a[href*='/explore'][href*='channel_id']",           // 带channel_id的探索链接
                        "a[href='/explore?channel_id=homefeed_recommend']",  // 完整匹配的发现链接
                        "nav a[href*='/explore']",                           // 导航区域的探索链接
                        ".sidebar a[href*='/explore']",                      // 侧边栏探索链接
                        "a:has-text('发现')",                                  // 包含"发现"文本的链接
                        "a:has-text('探索')",                                  // 包含"探索"文本的链接（备选）
                        "a:has-text('Recommend')",                           // 英文版发现链接
                        "[data-v-*] a[href*='explore']",                     // Vue组件内的探索链接
                        ".nav-item a[href*='explore']",                      // 导航项中的探索链接
                        ".menu-item a[href*='explore']",                     // 菜单项中的探索链接
                        "#explore-guide-refresh a",                          // 发现页面刷新链接
                        "[aria-label*='发现']",                                // 带发现标签的元素
                        "[aria-label*='探索']",                                // 带探索标签的元素
                        "[title*='发现']",                                     // 带发现标题的元素
                        "[title*='探索']",                                     // 带探索标题的元素
                        "[data-testid='sidebar-discover']",                  // 测试ID备用
                        "[data-testid='explore-link']",                      // 探索链接测试ID
                        ".discovery-link",                                   // 发现链接类名
                        ".explore-link"
                    ]
                },

                // ===== 搜索功能相关 =====

                // 搜索输入框 - 基于真实HTML结构
                {
                    "SearchInput", [
                        "#search-input",              // 真实ID，最准确
                        ".search-input",              // 真实class名
                        "input[placeholder='搜索小红书']", // 真实placeholder文本
                        ".input-box input",           // 父容器下的input
                        "input[placeholder*='搜索']",   // 通用搜索placeholder
                        "[data-testid='search-input']"
                    ]
                },

                // 搜索按钮 - 基于用户提供的HTML结构分析
                {
                    "SearchButton", [
                        ".input-button .search-icon", // 用户提供的真实结构
                        ".search-icon",               // 搜索图标
                        ".input-button button",       // 输入按钮容器中的按钮
                        ".input-button",              // 容器本身可点击（兜底）
                        "button.search-btn",          // 搜索按钮类名
                        ".search-btn",                // 搜索按钮
                        "[data-testid='search-button']"
                    ]
                },

                // 筛选按钮 - 基于真实HTML结构
                {
                    "FilterButton", [
                        ".filter",                        // 真实class名，最准确
                        "div[data-v-eb91fffe=''].filter", // 带Vue组件ID的筛选按钮
                        "span:has-text('筛选')",            // 包含"筛选"文本的元素
                        ".filter:has(.filter-icon)",      // 包含筛选图标的按钮
                        "[data-testid='filter-button']"
                    ]
                },

                // 筛选面板 - 基于真实HTML结构
                {
                    "FilterPanel", [
                        ".filter-panel",                        // 真实class名，最准确
                        "div[data-v-eb91fffe=''].filter-panel", // 带Vue组件ID的筛选面板
                        ".filter-container",                    // 筛选容器
                        ".filters-wrapper",                     // 筛选包装器
                        "[data-testid='filter-panel']"
                    ]
                },

                // 筛选选项容器
                {
                    "FilterOptions", [
                        ".filters",                  // 筛选组
                        ".filters-wrapper .filters", // 筛选包装器下的筛选组
                        ".tag-container",            // 标签容器
                        "[data-testid='filter-options']"
                    ]
                },

                // 排序选项标签 - 基于真实HTML结构
                {
                    "SortTags", [
                        ".tag-container .tags", // 真实结构，最准确
                        ".tags",                // 标签元素
                        ".tags.active",         // 激活状态的标签
                        ".tags:has(span)",      // 包含span的标签
                        "[data-testid='sort-tags']"
                    ]
                },

                // 具体排序选项选择器
                {
                    "SortOptionComprehensive", [
                        ".tags:has(span:has-text('综合'))", // 综合排序
                        "span:has-text('综合')",
                        "[data-testid='sort-comprehensive']"
                    ]
                },

                {
                    "SortOptionLatest", [
                        ".tags:has(span:has-text('最新'))", // 最新排序
                        "span:has-text('最新')",
                        "[data-testid='sort-latest']"
                    ]
                },

                {
                    "SortOptionMostLiked", [
                        ".tags:has(span:has-text('最多点赞'))", // 最多点赞排序
                        "span:has-text('最多点赞')",
                        "[data-testid='sort-most-liked']"
                    ]
                },

                {
                    "SortOptionMostCommented", [
                        ".tags:has(span:has-text('最多评论'))", // 最多评论排序
                        "span:has-text('最多评论')",
                        "[data-testid='sort-most-commented']"
                    ]
                },

                {
                    "SortOptionMostFavorited", [
                        ".tags:has(span:has-text('最多收藏'))", // 最多收藏排序
                        "span:has-text('最多收藏')",
                        "[data-testid='sort-most-favorited']"
                    ]
                },

                // 笔记类型筛选选项
                {
                    "NoteTypeVideo", [
                        ".tags:has(span:has-text('视频'))", // 视频类型
                        "span:has-text('视频')",
                        "[data-testid='note-type-video']"
                    ]
                },

                {
                    "NoteTypeImage", [
                        ".tags:has(span:has-text('图文'))", // 图文类型
                        "span:has-text('图文')",
                        "[data-testid='note-type-image']"
                    ]
                },

                // 笔记项容器 - 基于真实HTML结构（支持explore和search_result两种页面）
                {
                    "NoteItem", [
                        "section.note-item",                   // 最精确的标签+类名组合
                        "[data-v-a264b01a].note-item",         // 探索页面Vue组件
                        "[data-v-330d9cca].note-item",         // 搜索页面Vue组件
                        ".note-item[data-width][data-height]", // 带数据属性的笔记项
                        ".note-item",                          // 通用备选
                        ".query-note-item",
                        ".feed-item",
                        "[data-testid='note-item']"
                    ]
                },

                // 笔记标题 - 基于真实HTML结构优化
                {
                    "NoteTitle", [
                        ".title span",                   // 真实结构：标题文本在span内
                        ".footer .title",                // 更精确的上下文定位
                        "a.title span[data-v-51ec0135]", // 完整的Vue组件选择器
                        "a.title",                       // 标题链接
                        ".title",                        // 通用备选
                        ".hotspot-title",
                        "a[title]",
                        ".note-title",
                        "[data-testid='note-title']"
                    ]
                },

                // 笔记作者信息 - 支持explore和search_result两种页面格式
                {
                    "NoteAuthor", [
                        ".card-bottom-wrapper .author .name",   // 搜索页面作者名称
                        ".author-wrapper .author .name",        // 探索页面作者名称（如果存在）
                        ".card-bottom-wrapper .name span.name", // 搜索页面完整路径
                        ".author .name",                        // 通用作者名称
                        ".author span.name",                    // 作者名称在span内
                        ".name",                                // 简化选择器
                        ".user-name",
                        ".author",
                        ".note-author-name",
                        ".creator-name",
                        "[data-testid='note-author']"
                    ]
                },

                // ===== 笔记链接相关 - 基于真实HTML结构 =====

                // 笔记隐藏链接 - 优先提取（更稳定）
                {
                    "NoteHiddenLink", [
                        "a[style*='display: none']",               // 隐藏的explore链接
                        "a[href*='/explore/'][style*='none']",     // 隐藏的探索链接
                        "section a:first-child[style*='display']", // 笔记项的首个隐藏链接
                        "[data-testid='hidden-link']"
                    ]
                },

                // 笔记显示链接 - 备选方案
                {
                    "NoteVisibleLink", [
                        "a.cover.mask.ld",            // 封面链接（真实类名）
                        "a.cover[target='_self']",    // 带target属性的封面链接
                        "a[href*='/search_result/']", // 搜索结果链接
                        "a[href*='/explore/']",       // 探索链接
                        ".note-item a[href]",         // 笔记项内的任意链接
                        "[data-testid='visible-link']"
                    ]
                },

                // ===== 时间信息相关 - 基于真实HTML结构 =====

                // 笔记时间信息
                {
                    "NoteTime", [
                        ".time span.time",              // 搜索页面时间格式
                        ".card-bottom-wrapper .time",   // 搜索页面上下文
                        "[data-v-11fd8d4e] .time span", // Vue组件内的时间
                        ".author-wrapper .time",        // 探索页面可能格式
                        ".time",                        // 通用备选
                        ".publish-time",
                        "[data-time]",
                        ".note-time",
                        ".date",
                        ".timestamp",
                        "[data-testid='note-time']"
                    ]
                },

                // ===== 封面图片相关 - 基于真实HTML结构 =====

                // 笔记封面图片
                {
                    "NoteCoverImage", [
                        ".cover img[data-xhs-img]",                 // 真实结构：带小红书图片标记
                        "a.cover.mask.ld img",                      // 封面链接内的图片
                        ".cover img[elementtiming='card-exposed']", // 带性能监控属性的图片
                        "img[src*='sns-webpic-qc.xhscdn.com']",     // 小红书CDN图片
                        ".cover img",                               // 通用封面图片
                        "a.cover img",                              // 更精确的上下文
                        "[data-testid='cover-image']"
                    ]
                },


                // ===== 评论交互相关 =====

                // 评论按钮
                {
                    "CommentButton", [
                        ".comment-wrapper",
                        "#comment",
                        ".comment-icon",
                        "[data-testid='comment-button']",
                        ".comment-btn"
                    ]
                },

                // 评论输入框
                {
                    "CommentInput", [
                        ".comment-input",
                        "textarea[placeholder*='评论']",
                        ".comment-box textarea",
                        "[data-testid='comment-input']",
                        ".reply-input"
                    ]
                },

                // 评论列表容器
                {
                    "CommentList", [
                        ".comment-list",
                        ".comments-container",
                        "#comment-list",
                        "[data-testid='comment-list']",
                        ".comment-section"
                    ]
                },

                // 单个评论项
                {
                    "CommentItem", [
                        ".comment-item",
                        ".comment-card",
                        ".comment",
                        "[data-testid='comment-item']",
                        ".comment-wrapper"
                    ]
                },

                // ===== 笔记详情页相关 =====

                // 笔记正文内容
                {
                    "NoteContent", [
                        ".note-content",
                        ".desc",
                        "#note-content",
                        "[data-testid='note-content']",
                        ".note-text"
                    ]
                },

                // 提交/发布按钮
                {
                    "SubmitButton", [
                        ".submit-btn",
                        "button[type='submit']",
                        ".publish-btn",
                        "button:has-text('发布')",
                        "[data-testid='submit-button']"
                    ]
                },

                // ===== 发布/创作页相关 =====

                // 发布页容器 - Vue.js创作平台特化
                {
                    "PublishContainer", [
                        ".publish-container",
                        ".create-note",
                        ".publisher-wrapper",
                        "#publish-page",
                        "[data-testid='publish-container']",
                        ".note-editor",
                        "[data-v-*]", // Vue.js组件
                        ".vue-publish-container"
                    ]
                },

                // ===== 图片上传相关 - 核心功能 =====

                // 图片上传区域 - 必须首先处理（基于真实HTML结构）
                {
                    "ImageUploadArea", [
                        ".upload-area",
                        ".image-uploader",
                        ".file-drop-zone",
                        ".drop-zone",
                        ".upload-zone",
                        ".file-upload-area",
                        "[data-testid='upload-area']",
                        ".drag-drop-area",
                        ".image-drop-area"
                    ]
                },

                // 图片上传外层容器 - 基于真实HTML结构
                {
                    "ImageUploadWrapper", [
                        ".upload-wrapper",                  // 真实class名
                        "[data-v-8c223b18].upload-wrapper", // 带Vue组件ID的选择器
                        "[data-v-*].upload-wrapper",        // 通用Vue组件选择器
                        "div.upload-wrapper",
                        "[data-testid='upload-wrapper']"
                    ]
                },

                // 拖拽上传区域 - 基于真实HTML结构
                {
                    "DragUploadArea", [
                        ".drag-over",                  // 真实class名
                        "[data-v-7cbccdb2].drag-over", // 带Vue组件ID的选择器
                        "[data-v-*].drag-over",        // 通用Vue组件选择器
                        "div.drag-over",
                        ".upload-drop-zone",
                        "[data-testid='drag-area']"
                    ]
                },

                // 上传按钮 - 基于真实HTML结构
                {
                    "UploadButton", [
                        ".el-button.upload-button",          // 真实class组合，最准确
                        ".upload-button",                    // 真实class名
                        "button:has-text('上传图片')",           // 真实按钮文本
                        ".el-button[aria-disabled='false']", // Element UI按钮
                        "button.upload-button",
                        "[data-testid='upload-button']"
                    ]
                },

                // 拖拽提示文本 - 基于真实HTML结构
                {
                    "DragText", [
                        ".drag-text",                // 真实class名
                        "p:has-text('拖拽图片到此或点击上传')", // 真实提示文本
                        "p.drag-text",
                        ".upload-hint",
                        ".drag-hint-text",
                        "[data-testid='drag-text']"
                    ]
                },

                // 上传限制信息文本
                {
                    "UploadInfo", [
                        ".info",                   // 真实class名（上下文：upload-wrapper内的.info）
                        "p:has-text('最多支持上传18张')", // 真实信息文本
                        "p.info",
                        ".upload-limit-info",
                        ".upload-tip",
                        "[data-testid='upload-info']"
                    ]
                },

                // 图片选择按钮（已整合到UploadButton，保留兼容性）
                {
                    "ImageSelectButton", [
                        ".el-button.upload-button", // 使用真实的上传按钮
                        ".upload-button",
                        "button:has-text('上传图片')", // 真实按钮文本
                        ".upload-btn",
                        ".select-files",
                        ".choose-image",
                        "button:has-text('选择图片')",
                        "button:has-text('添加图片')",
                        "[data-testid='image-select']",
                        ".file-selector-btn"
                    ]
                },

                // 文件输入框 - 基于真实HTML结构
                {
                    "FileInput", [
                        ".upload-input",                             // 真实class名
                        "input.upload-input[type='file'][multiple]", // 完整选择器
                        "input[accept='.jpg,.jpeg,.png,.webp']",     // 真实accept属性
                        "input[type='file'][multiple]",              // 通用文件输入
                        "input[accept*='image']",
                        "input[accept*='video']",
                        "[data-testid='file-input']"
                    ]
                },

                // 图片预览区域
                {
                    "ImagePreview", [
                        ".image-preview",
                        ".preview-container",
                        ".image-list",
                        ".uploaded-images",
                        ".preview-wrapper",
                        "[data-testid='image-preview']",
                        ".image-thumbnails"
                    ]
                },

                // 上传进度指示器
                {
                    "UploadProgress", [
                        ".upload-progress",
                        ".progress-bar",
                        ".uploading",
                        ".upload-status",
                        "[data-testid='upload-progress']",
                        ".progress-indicator"
                    ]
                },

                // ===== Vue.js富文本编辑器相关 =====

                // 编辑器容器 - Vue.js特化
                {
                    "EditorContainer", [
                        ".editor-container",
                        ".rich-editor",
                        ".text-editor",
                        ".vue-editor",
                        ".note-editor-wrapper",
                        "[data-v-*] .editor",
                        "[data-testid='editor-container']",
                        ".content-editor-wrapper"
                    ]
                },

                // 标题输入框 - Vue.js创作平台特化（基于真实HTML结构）
                {
                    "PublishTitleInput", [
                        "input[placeholder*='填写标题会有更多赞哦']", // 真实placeholder文本，最准确
                        ".d-text",                          // 真实class名
                        ".title-input",
                        ".note-title",
                        "input[placeholder*='标题']",
                        "input[placeholder*='请输入标题']",
                        "#note-title-input",
                        "[data-testid='title-input']",
                        ".note-title-editor",
                        "[data-v-*] input[type='text']"
                    ]
                },

                // 内容编辑器 - TipTap富文本编辑器（基于真实HTML结构）
                {
                    "PublishContentInput", [
                        ".tiptap.ProseMirror",                         // 真实的TipTap编辑器class组合，最准确
                        "[contenteditable='true']",                    // contenteditable元素
                        "div[contenteditable='true'][role='textbox']", // 更精确的contenteditable
                        ".tiptap",                                     // TipTap编辑器
                        ".ProseMirror",                                // ProseMirror编辑器
                        ".content-editor",
                        ".rich-text-editor",
                        ".editor-content",
                        "textarea[placeholder*='内容']", // 备用：传统textarea
                        "textarea[placeholder*='分享你的生活']",
                        "[data-testid='content-input']"
                    ]
                },

                // 标签输入框 - Vue.js特化
                {
                    "PublishTagInput", [
                        ".tag-input",
                        ".hashtag-input",
                        ".topic-input",
                        "input[placeholder*='标签']",
                        "input[placeholder*='话题']",
                        "input[placeholder*='添加标签']",
                        "[data-testid='tag-input']",
                        ".tag-selector",
                        ".topic-selector",
                        "[data-v-*] .tag-input"
                    ]
                },

                // ===== 发布操作按钮 =====

                // 暂存离开按钮 - 基于真实HTML结构
                {
                    "TemporarySaveButton", [
                        ".cancelBtn",              // 真实class名，最准确
                        "button:has-text('暂存离开')", // 真实按钮文本
                        "button:has-text('暂存')",
                        ".temp-save-btn",
                        ".leave-btn",
                        "[data-testid='temporary-save']",
                        ".temp-leave-btn",
                        ".draft-leave-btn"
                    ]
                },

                // 发布按钮 - 基于真实HTML结构
                {
                    "PublishButton", [
                        ".publishBtn",           // 真实class名，最准确
                        "button:has-text('发布')", // 真实按钮文本
                        ".publish-btn",
                        ".submit-btn",
                        "button:has-text('立即发布')",
                        "button[type='submit']",
                        "[data-testid='publish-button']",
                        ".btn-publish",
                        ".publish-submit"
                    ]
                },

                // ===== 编辑器等待和状态指示器 =====

                // 编辑器加载指示器
                {
                    "EditorLoading", [
                        ".editor-loading",
                        ".editor-spinner",
                        ".content-loading",
                        "[data-testid='editor-loading']",
                        ".loading-editor"
                    ]
                },

                // 编辑器就绪指示器
                {
                    "EditorReady", [
                        ".editor-ready",
                        ".editor-active",
                        ".editor[data-ready='true']",
                        "[data-testid='editor-ready']",
                        ".content-editor.ready"
                    ]
                },

                // ===== Vue.js动态内容选择器 =====

                // Vue.js数据绑定元素
                {
                    "VueDataElement", [
                        "[v-model]",
                        "[v-bind]",
                        "[data-v-*]",
                        ".v-enter",
                        ".v-leave",
                        "[data-testid*='vue']"
                    ]
                },

                // 动态渲染的表单元素
                {
                    "DynamicFormElement", [
                        "[data-dynamic='true']",
                        ".dynamic-field",
                        ".form-field[data-loaded='true']",
                        "[data-testid*='dynamic']",
                        ".vue-form-item"
                    ]
                },

                // ===== 位置和地点相关 =====

                // 位置选择器
                {
                    "LocationSelector", [
                        ".location-selector",
                        ".place-selector",
                        "input[placeholder*='位置']",
                        "input[placeholder*='地点']",
                        "[data-testid='location-input']",
                        ".location-input"
                    ]
                },

                // ===== 通用UI元素 =====

                // 加载指示器
                {
                    "LoadingIndicator", [
                        ".loading",
                        ".spinner",
                        ".loading-wrapper",
                        "[data-testid='loading']",
                        ".lds-ring"
                    ]
                },

                // 错误提示
                {
                    "ErrorMessage", [
                        ".error-message",
                        ".error-tip",
                        ".toast-error",
                        "[data-testid='error-message']",
                        ".notification.error"
                    ]
                },

                // 成功提示
                {
                    "SuccessMessage", [
                        ".success-message",
                        ".success-tip",
                        ".toast-success",
                        "[data-testid='success-message']",
                        ".notification.success"
                    ]
                },

                // 模态框/弹窗
                {
                    "Modal", [
                        ".modal",
                        ".dialog",
                        ".popup",
                        "[data-testid='modal']",
                        ".overlay-container"
                    ]
                },

                // 关闭按钮
                {
                    "CloseButton", [
                        ".close-btn",
                        ".modal-close",
                        "button[aria-label='关闭']",
                        "[data-testid='close-button']",
                        ".icon-close"
                    ]
                },

                // ===== 页面状态检测选择器 =====

                // 检测是否在上传状态（暂存后回归的状态）
                {
                    "UploadState", [
                        ".upload-wrapper",           // 上传容器存在表示在上传状态
                        ".drag-over",                // 拖拽区域存在
                        ".el-button.upload-button",  // 上传按钮存在
                        "p:has-text('拖拽图片到此或点击上传')", // 特征性文本存在
                        "[data-testid='upload-state']"
                    ]
                },

                // 检测是否在编辑状态
                {
                    "EditState", [
                        ".tiptap.ProseMirror",              // TipTap编辑器存在表示在编辑状态
                        "input[placeholder*='填写标题会有更多赞哦']", // 标题输入存在
                        ".editor-container",                // 编辑器容器存在
                        "[data-testid='edit-state']"
                    ]
                },

                // ===== 笔记详情页面专用选择器 =====

                // 笔记详情页面模态框容器
                {
                    "NoteDetailModal", [
                        ".note-detail-mask[note-id]",          // 带note-id属性的详情页模态框（最准确）
                        ".note-detail-mask",                   // 详情页模态框
                        "[note-id]",                           // 任何带note-id属性的元素
                        "#noteContainer",                      // 笔记容器ID
                        ".note-container[data-type='normal']", // 普通笔记容器
                        "[data-testid='note-detail-modal']"
                    ]
                },

                // 详情页关闭按钮
                {
                    "NoteDetailCloseButton", [
                        ".close-circle .close",   // 主关闭按钮（圆形）
                        ".close-box",             // 方形关闭按钮
                        ".close-circle",          // 关闭按钮容器
                        ".close.close-mask-dark", // 带暗色遮罩的关闭按钮
                        "[data-testid='note-detail-close']"
                    ]
                },

                // 详情页笔记标题（区别于列表页标题）
                {
                    "NoteDetailTitle", [
                        "#detail-desc .note-text span",       // 详情页标题文本（最精确）
                        "#detail-desc .desc .note-text span", // 完整路径
                        ".desc .note-text span",              // 描述区域的文本
                        "#detail-desc",                       // 详情描述容器
                        "[data-testid='note-detail-title']"
                    ]
                },

                // 详情页作者信息（区别于列表页作者）
                {
                    "NoteDetailAuthor", [
                        ".author-wrapper .info .name .username", // 详情页作者名（最精确）
                        ".author .info .name .username",         // 作者信息区域的用户名
                        ".username",                             // 用户名span
                        ".author-wrapper .name",                 // 作者链接
                        ".info .name",                           // 信息区域的名称链接
                        "[data-testid='note-detail-author']"
                    ]
                },

                // 详情页作者头像
                {
                    "NoteDetailAuthorAvatar", [
                        ".author-wrapper .info a img.avatar-item", // 详情页作者头像（最精确）
                        ".author .info img.avatar-item",           // 作者信息区域头像
                        ".avatar-item[crossorigin='anonymous']",   // 跨域头像图片
                        ".info a img",                             // 信息区域链接中的图片
                        "[data-testid='note-detail-avatar']"
                    ]
                },

                // ===== 详情页评论系统专用选择器 =====

                // 评论总数显示
                {
                    "CommentTotal", [
                        ".comments-container .total",       // 评论总数容器（最精确）
                        ".comments-el .total",              // 评论元素总数
                        ".total[selected-disabled-search]", // 带特殊属性的总数
                        ".comment-total",                   // 通用评论总数
                        "[data-testid='comment-total']"
                    ]
                },

                // 单个评论项（详情页专用）
                {
                    "CommentItemDetail", [
                        ".comment-item[id^='comment-']",        // 带comment-ID的评论项（最精确）
                        ".parent-comment .comment-item",        // 父级评论项
                        ".comment-item:not(.comment-item-sub)", // 非子评论项
                        ".comments-container .comment-item",    // 评论容器中的评论项
                        "[data-testid='comment-item-detail']"
                    ]
                },

                // 子评论项
                {
                    "CommentItemSub", [
                        ".comment-item.comment-item-sub[id^='comment-']", // 子评论项（最精确）
                        ".comment-item-sub",                              // 子评论项类
                        ".reply-container .comment-item",                 // 回复容器中的评论项
                        "[data-testid='comment-item-sub']"
                    ]
                },

                // 评论作者名（详情页专用）
                {
                    "CommentAuthorDetail", [
                        ".comment-item .author .name",                   // 评论项中的作者名（最精确）
                        ".comment-item .author-wrapper .name",           // 评论作者包装器中的名称
                        ".comment-inner-container .right .author .name", // 完整路径
                        ".comment-item a.name[target='_blank']",         // 评论作者链接
                        "[data-testid='comment-author-detail']"
                    ]
                },

                // 评论内容（详情页专用）
                {
                    "CommentContentDetail", [
                        ".comment-item .content .note-text span",            // 评论内容文本（最精确）
                        ".comment-inner-container .content .note-text span", // 完整路径
                        ".comment-item .note-text",                          // 评论项中的笔记文本
                        ".content .note-text",                               // 内容区域的笔记文本
                        "[data-testid='comment-content-detail']"
                    ]
                },

                // 评论点赞数（详情页专用）
                {
                    "CommentLikeCountDetail", [
                        ".comment-item .like-wrapper .count",       // 评论点赞数（最精确）
                        ".comment-item .interactions .like .count", // 评论交互区域点赞数
                        ".like-wrapper.like-active .count",         // 激活状态的点赞数
                        ".comment-item .like .count",               // 评论项点赞数
                        "[data-testid='comment-like-count-detail']"
                    ]
                },

                // 评论回复数
                {
                    "CommentReplyCount", [
                        ".comment-item .reply .count",               // 评论回复数（最精确）
                        ".comment-item .interactions .reply .count", // 交互区域回复数
                        ".reply.icon-container .count",              // 回复图标容器数量
                        ".show-more",                                // 展开更多回复
                        "[data-testid='comment-reply-count']"
                    ]
                },

                // 评论时间和地点
                {
                    "CommentDateTime", [
                        ".comment-item .date span[selected-disabled-search]", // 评论日期（最精确）
                        ".comment-item .info .date",                          // 评论信息区域日期
                        ".comment-item .location",                            // 评论地点
                        ".date .location",                                    // 日期区域的地点
                        "[data-testid='comment-datetime']"
                    ]
                },


                // ===== 详情页评论输入区域选择器 =====

                // 详情页评论输入框
                {
                    "DetailPageCommentInput", [
                        "#content-textarea[contenteditable='true']",   // 详情页评论输入框（最精确）
                        ".content-input[data-tribute='true']",         // 带tribute属性的内容输入框
                        ".input-box .content-edit p[contenteditable]", // 输入框可编辑段落
                        ".engage-bar .input-box",                      // 交互栏输入框
                        "[data-testid='detail-page-comment-input']"
                    ]
                },

                // 详情页评论发送按钮
                {
                    "DetailPageCommentSubmit", [
                        ".right-btn-area .btn.submit",  // 详情页发送按钮（最精确）
                        ".bottom .btn.submit",          // 底部发送按钮
                        "button.submit.gray[disabled]", // 禁用状态发送按钮
                        ".engage-bar .submit",          // 交互栏提交按钮
                        "[data-testid='detail-page-comment-submit']"
                    ]
                },

                // 详情页评论取消按钮
                {
                    "DetailPageCommentCancel", [
                        ".right-btn-area .btn.cancel", // 详情页取消按钮（最精确）
                        ".bottom .btn.cancel",         // 底部取消按钮
                        ".engage-bar .cancel",         // 交互栏取消按钮
                        "[data-testid='detail-page-comment-cancel']"
                    ]
                },

                // ===== 动态评论交互状态选择器 =====

                // 激活状态的评论区域 - 检测评论输入是否被激活
                {
                    "EngageBarActive", [
                        ".engage-bar.active",           // 激活状态的交互栏（最精确）
                        ".engage-bar[class*='active']", // 包含active类的交互栏
                        "div.engage-bar.active",        // 更具体的标签+类选择器
                        "[data-testid='engage-bar-active']"
                    ]
                },

                // 评论输入框就绪状态 - contenteditable + tribute支持
                {
                    "CommentInputReady", [
                        "#content-textarea[contenteditable='true'][data-tribute='true']", // 完整属性匹配（最精确）
                        "p.content-input[contenteditable='true']",                        // 段落类型的可编辑输入
                        ".content-edit p[contenteditable][data-tribute]",                 // 支持@提及的可编辑段落
                        ".input-box .content-input[contenteditable]",                     // 输入框容器内的可编辑元素
                        "[data-testid='comment-input-ready']"
                    ]
                },

                // 发送按钮状态检测 - 区分启用和禁用状态
                {
                    "CommentSubmitEnabled", [
                        ".right-btn-area .btn.submit:not([disabled])", // 启用状态的发送按钮
                        ".btn.submit:not(.gray):not([disabled])",      // 非灰色非禁用的发送按钮
                        "button.submit:not([disabled]):not(.gray)",    // 启用状态的提交按钮
                        "[data-testid='comment-submit-enabled']"
                    ]
                },

                {
                    "CommentSubmitDisabled", [
                        ".right-btn-area .btn.submit.gray[disabled]", // 禁用状态的发送按钮（最精确）
                        ".btn.submit[disabled]",                      // 任何禁用的发送按钮
                        "button.submit.gray",                         // 灰色状态的提交按钮
                        ".btn.submit:disabled",                       // CSS禁用选择器
                        "[data-testid='comment-submit-disabled']"
                    ]
                },

                // ===== 表情符号交互系统选择器 =====

                // 最近使用的表情符号区域
                {
                    "RecentEmojiArea", [
                        ".emoji-area.recent-emoji",               // 最近表情区域（最精确）
                        "[class*='emoji-area'][class*='recent']", // 包含emoji-area和recent的元素
                        ".recent-emoji .click-area",              // 最近表情的点击区域
                        "[data-testid='recent-emoji-area']"
                    ]
                },

                // 单个表情符号点击区域
                {
                    "EmojiClickArea", [
                        ".emoji-area .click-area",       // 表情区域内的点击区域（最精确）
                        ".recent-emoji .click-area img", // 最近表情的图片点击区域
                        ".emoji-area img.emoji",         // 表情区域内的表情图片
                        "[data-testid='emoji-click-area']"
                    ]
                },

                // 表情符号触发按钮
                {
                    "EmojiTriggerButton", [
                        "#showEmojiEl",                                   // 显示表情的触发元素（最精确）
                        "svg[id='showEmojiEl']",                          // SVG表情触发按钮
                        ".left-icon-area .icon svg[xlink:href='#emoji']", // 表情图标SVG
                        "[data-testid='emoji-trigger']"
                    ]
                },

                // ===== 底部控制按钮选择器 =====

                // @提及触发按钮
                {
                    "MentionTriggerButton", [
                        "#showMentionEl",                                   // 显示提及的触发元素（最精确）
                        "svg[id='showMentionEl']",                          // SVG提及触发按钮
                        ".left-icon-area .icon svg[xlink:href='#mention']", // 提及图标SVG
                        "[data-testid='mention-trigger']"
                    ]
                },

                // 左侧图标区域（提及+表情）
                {
                    "LeftIconArea", [
                        ".left-icon-area",                   // 左侧图标区域（最精确）
                        ".bottom-inner .left-icon-area",     // 底部内部的左侧图标区域
                        ".engage-bar .left-icon-area .icon", // 交互栏左侧图标
                        "[data-testid='left-icon-area']"
                    ]
                },

                // 右侧按钮区域（发送+取消）
                {
                    "RightButtonArea", [
                        ".right-btn-area",               // 右侧按钮区域（最精确）
                        ".bottom-inner .right-btn-area", // 底部内部的右侧按钮区域
                        ".engage-bar .right-btn-area",   // 交互栏右侧按钮区域
                        "[data-testid='right-button-area']"
                    ]
                },

                // ===== 实时计数更新选择器 =====

                // 动态点赞数 - 支持实时更新
                {
                    "DynamicLikeCount", [
                        ".like-wrapper.like-active .count[selected-disabled-search]", // 激活状态点赞数（最精确）
                        ".engage-bar .like-wrapper .count",                           // 交互栏点赞数
                        ".buttons.engage-bar-style .like-wrapper .count",             // 交互栏样式的点赞数
                        "[data-testid='dynamic-like-count']"
                    ]
                },

                // 动态收藏数 - 支持实时更新
                {
                    "DynamicCollectCount", [
                        "#note-page-collect-board-guide .count", // 收藏引导区域计数（最精确）
                        ".collect-wrapper .count",               // 收藏包装器计数
                        ".engage-bar .collect-wrapper .count",   // 交互栏收藏数
                        "[data-testid='dynamic-collect-count']"
                    ]
                },

                // 动态评论数 - 支持实时更新
                {
                    "DynamicCommentCount", [
                        ".engage-bar .chat-wrapper .count",               // 交互栏聊天数（最精确）
                        ".buttons.engage-bar-style .chat-wrapper .count", // 交互栏样式的评论数
                        ".chat-wrapper .count",                           // 聊天包装器计数
                        "[data-testid='dynamic-comment-count']"
                    ]
                },


                // ===== 点赞功能选择器 - 支持多状态检测 =====

                // 未点赞状态的点赞按钮 - 基于真实HTML结构优化
                {
                    "likeButton", [
                        ".like-wrapper:not(.like-active)",                           // 未点赞状态（最精确）
                        ".like-wrapper svg use[xlink\\:href='#like']",               // 基于SVG图标识别未点赞状态
                        ".like-wrapper:has(use[xlink\\:href='#like'])",              // 包含未点赞图标的wrapper
                        ".like-wrapper:not(:has(.like-active))",                     // 排除已点赞状态
                        ".engage-bar .like-wrapper:not(.like-active)",               // 详情页未激活的点赞按钮
                        ".buttons.engage-bar-style .like-wrapper:not(.like-active)", // 交互栏样式未激活点赞
                        "[data-testid='like-button']:not([aria-pressed='true'])",    // 测试ID未按下状态
                        ".like-icon:not(.liked)"
                    ]
                },

                // 已点赞状态的点赞按钮 - 基于真实HTML结构优化
                {
                    "likeButtonActive", [
                        ".like-wrapper.like-active",                           // 已点赞状态（最精确）
                        ".like-wrapper svg use[xlink\\:href='#liked']",        // 基于SVG图标识别已点赞状态
                        ".like-wrapper:has(use[xlink\\:href='#liked'])",       // 包含已点赞图标的wrapper
                        ".like-wrapper.like-active:has(.count)",               // 激活状态且包含计数的点赞按钮
                        ".engage-bar .like-wrapper.like-active",               // 详情页激活的点赞按钮
                        ".buttons.engage-bar-style .like-wrapper.like-active", // 交互栏样式激活点赞
                        "[data-testid='like-button'][aria-pressed='true']",    // 测试ID按下状态
                        ".like-icon.liked.active"
                    ]
                },

                // 点赞操作加载状态的按钮
                {
                    "likeButtonLoading", [
                        ".like-wrapper.loading",                         // 加载状态的点赞按钮（最精确）
                        ".like-wrapper[data-loading='true']",            // 带加载属性的点赞按钮
                        ".like-button.loading",                          // 加载状态的点赞按钮类
                        ".like-container .loading",                      // 点赞容器内的加载状态
                        ".interaction-buttons .like.loading",            // 交互按钮区域加载状态
                        "[data-testid='like-button'][aria-busy='true']", // 测试ID繁忙状态
                        ".like-icon.loading"
                    ]
                },

                // ===== 收藏功能选择器 - 支持多状态检测 =====

                // 未收藏状态的收藏按钮 - 基于真实HTML结构优化
                {
                    "favoriteButton", [
                        ".collect-wrapper svg use[xlink\\:href='#collect']",               // 基于SVG图标识别未收藏状态（最精确）
                        ".collect-wrapper:has(use[xlink\\:href='#collect'])",              // 包含未收藏图标的wrapper
                        ".collect-wrapper:not(:has(use[xlink\\:href='#collected']))",      // 排除已收藏状态
                        ".collect-wrapper",                                                // 通用收藏按钮
                        ".engage-bar .collect-wrapper:not(.collect-active)",               // 详情页未激活的收藏按钮
                        ".buttons.engage-bar-style .collect-wrapper:not(.collect-active)", // 交互栏样式未激活收藏
                        "[data-testid='favorite-button']:not([aria-pressed='true'])",      // 测试ID未按下状态
                        ".collect-icon:not(.collected)"
                    ]
                },

                // 已收藏状态的收藏按钮 - 基于真实HTML结构优化
                {
                    "favoriteButtonActive", [
                        ".collect-wrapper svg use[xlink\\:href='#collected']",       // 基于SVG图标识别已收藏状态（最精确）
                        ".collect-wrapper:has(use[xlink\\:href='#collected'])",      // 包含已收藏图标的wrapper
                        ".collect-wrapper:has(.count):has(use[href*='collected'])",  // 包含计数和已收藏图标
                        ".collect-wrapper[aria-pressed='true']",                     // 按下状态的收藏按钮
                        ".engage-bar .collect-wrapper.collect-active",               // 详情页激活的收藏按钮
                        ".buttons.engage-bar-style .collect-wrapper.collect-active", // 交互栏样式激活收藏
                        "[data-testid='favorite-button'][aria-pressed='true']",      // 测试ID按下状态
                        ".collect-icon.collected.active"
                    ]
                },

                // 收藏操作加载状态的按钮
                {
                    "favoriteButtonLoading", [
                        ".collect-wrapper.loading",                          // 加载状态的收藏按钮（最精确）
                        ".collect-wrapper[data-loading='true']",             // 带加载属性的收藏按钮
                        ".favorite-button.loading",                          // 加载状态的收藏按钮类
                        ".favorite-container .loading",                      // 收藏容器内的加载状态
                        ".interaction-buttons .collect.loading",             // 交互按钮区域加载状态
                        "[data-testid='favorite-button'][aria-busy='true']", // 测试ID繁忙状态
                        ".collect-icon.loading"
                    ]
                },

                // ===== 互动数据统计选择器 - 支持实时更新 =====

                // 点赞数文本显示
                {
                    "likeCount", [
                        ".engage-bar .like-wrapper .count",                           // 详情页点赞数（最精确）
                        ".like-wrapper.like-active .count[selected-disabled-search]", // 激活状态点赞数
                        ".buttons.engage-bar-style .like-wrapper .count",             // 交互栏样式的点赞数
                        ".like-counter .count-text",                                  // 点赞计数器文本
                        ".interaction-stats .like-count",                             // 交互统计中的点赞数
                        ".engage-stats .like-number",                                 // 参与统计中的点赞数字
                        "[data-testid='like-count-display']",                         // 测试ID点赞数显示
                        ".like-wrapper span.count"
                    ]
                },

                // 收藏数文本显示
                {
                    "favoriteCount", [
                        ".engage-bar .collect-wrapper .count",                              // 详情页收藏数（最精确）
                        ".collect-wrapper.collect-active .count[selected-disabled-search]", // 激活状态收藏数
                        ".buttons.engage-bar-style .collect-wrapper .count",                // 交互栏样式的收藏数
                        ".favorite-counter .count-text",                                    // 收藏计数器文本
                        ".interaction-stats .collect-count",                                // 交互统计中的收藏数
                        ".engage-stats .collect-number",                                    // 参与统计中的收藏数字
                        "[data-testid='favorite-count-display']",                           // 测试ID收藏数显示
                        ".collect-wrapper span.count"
                    ]
                },

                // 评论数文本显示（扩展现有选择器）
                {
                    "commentCount", [
                        ".engage-bar .chat-wrapper .count",               // 详情页评论数（最精确）
                        ".buttons.engage-bar-style .chat-wrapper .count", // 交互栏样式的评论数
                        ".chat-wrapper .count[selected-disabled-search]", // 特殊属性的评论数
                        ".comment-counter .count-text",                   // 评论计数器文本
                        ".interaction-stats .comment-count",              // 交互统计中的评论数
                        ".engage-stats .comment-number",                  // 参与统计中的评论数字
                        ".comments-container .total",                     // 评论容器总数（详情页专用）
                        "[data-testid='comment-count-display']",          // 测试ID评论数显示
                        ".chat-wrapper span.count"
                    ]
                },

                // ===== 交互状态检测选择器 =====

                // 评论区域展开状态检测
                {
                    "CommentAreaExpanded", [
                        ".engage-bar.active .input-box",    // 激活状态下的输入框
                        ".engage-bar.active .content-edit", // 激活状态下的内容编辑区
                        ".engage-bar.active .bottom",       // 激活状态下的底部区域
                        "[data-testid='comment-area-expanded']"
                    ]
                },

                // 按钮hover状态检测
                {
                    "ShareButtonHovered", [
                        ".share-icon-container.hovered", // 悬停状态的分享图标容器（最精确）
                        ".share-wrapper .hovered",       // 分享包装器内的悬停元素
                        ".share-icon-container:hover",   // CSS悬停状态
                        "[data-testid='share-button-hovered']"
                    ]
                }
            };

            // 初始化页面状态特定的选择器配置
            InitializePageStateSelectors();
        }

        /// <summary>
        /// 初始化页面状态特定的选择器配置
        /// </summary>
        private void InitializePageStateSelectors()
        {
            _pageStateSelectors = new Dictionary<PageState, Dictionary<string, List<string>>>
            {
                [PageState.Explore] = new()
                {
                    // 探索页面特定的笔记容器选择器
                    ["NoteItem"] =
                    [
                        "#exploreFeeds .note-item",      // 探索页面特定容器
                        "[data-v-a264b01a].note-item",   // 探索页面Vue组件
                        ".channel-container .note-item", // 频道容器下的笔记
                        ".note-item[data-index]",        // 带索引的笔记项
                        ".note-item"
                    ],

                    // 探索页面状态检测选择器
                    ["PageContainer"] =
                    [
                        "#exploreFeeds",      // 探索页面主容器
                        ".channel-container", // 频道容器
                        "[data-testid='explore-page']"
                    ],

                    // 探索页面搜索框（如果存在）
                    ["SearchInput"] =
                    [
                        ".search-bar .search-input", // 探索页面搜索栏
                        "#search-input",             // 全局搜索输入框
                        ".input-box input"
                    ]
                },

                [PageState.SearchResult] = new()
                {
                    // 搜索结果页面特定的笔记容器选择器
                    ["NoteItem"] =
                    [
                        ".search-layout__main .note-item", // 搜索结果页面特定容器
                        "[data-v-330d9cca].note-item",     // 搜索页面Vue组件
                        ".search-content .note-item",      // 搜索内容容器下的笔记
                        ".result-list .note-item",         // 结果列表中的笔记
                        ".note-item"
                    ],

                    // 搜索结果页面状态检测选择器
                    ["PageContainer"] =
                    [
                        ".search-layout",       // 搜索布局容器
                        ".search-layout__main", // 搜索主容器
                        "[data-testid='search-result-page']"
                    ],

                    // 搜索结果页面的搜索框（原地搜索）
                    ["SearchInput"] =
                    [
                        ".search-layout .search-input", // 搜索布局中的搜索框
                        ".search-header .search-input", // 搜索头部的输入框
                        "#search-input",                // 全局搜索输入框
                        ".input-box input"
                    ]
                }
            };
        }

        /// <summary>
        /// 获取某别名对应的通用候选选择器列表。
        /// - 若未找到别名映射，则返回仅包含该别名本身的列表（允许直接写选择器）。
        /// - 列表顺序代表优先级，建议从最稳定、最具体的选择器开始。
        /// </summary>
        public List<string> GetSelectors(string alias)
        {
            if (_selectors.TryGetValue(alias, out var selectorList))
            {
                return selectorList;
            }
            // 如果别名不存在，返回一个包含别名本身的列表，以支持直接使用选择器
            return [alias];
        }

        /// <summary>
        /// 获取“页面状态 + 别名”的候选选择器列表。
        /// - 当 <paramref name="pageState"/> 为 Auto/Unknown 时，退回通用候选；
        /// - 若存在状态特定候选，则返回“状态候选优先 + 通用候选去重追加”的合并序列。
        /// </summary>
        public List<string> GetSelectors(string alias, PageState pageState)
        {
            // 如果页面状态是自动检测，返回通用选择器
            if (pageState is PageState.Auto or PageState.Unknown)
            {
                return GetSelectors(alias);
            }

            // 首先尝试获取页面状态特定的选择器
            if (_pageStateSelectors.TryGetValue(pageState, out var pageSelectors) &&
                pageSelectors.TryGetValue(alias, out var stateSpecificSelectors))
            {
                // 如果找到页面状态特定的选择器，将其与通用选择器合并
                var generalSelectors = GetSelectors(alias);
                var combinedSelectors = new List<string>();

                // 优先使用状态特定的选择器
                combinedSelectors.AddRange(stateSpecificSelectors);

                // 添加通用选择器作为备选（去重）
                foreach (var selector in generalSelectors)
                {
                    if (!combinedSelectors.Contains(selector))
                    {
                        combinedSelectors.Add(selector);
                    }
                }

                return combinedSelectors;
            }

            // 如果没有找到页面状态特定的选择器，返回通用选择器
            return GetSelectors(alias);
        }

        /// <summary>
        /// 检测当前页面状态。
        /// - 先通过 URL 片段进行快速判断；
        /// - 若无法判定，则按状态对应的页面容器选择器做 DOM 存在性检测；
        /// - 若仍无法判定，返回 Unknown。
        /// </summary>
        public async Task<PageState> DetectPageStateAsync(IPage page)
        {
            try
            {
                // 获取当前页面URL
                var currentUrl = page.Url;

                // 基于URL进行页面状态检测
                if (currentUrl.Contains("/search_result"))
                {
                    return PageState.SearchResult;
                }

                if (currentUrl.Contains("/explore"))
                {
                    return PageState.Explore;
                }

                // 辅助DOM特征检测
                var exploreDetectors = _pageStateSelectors[PageState.Explore]["PageContainer"];
                foreach (var selector in exploreDetectors)
                {
                    try
                    {
                        var element = await page.QuerySelectorAsync(selector);
                        if (element != null)
                        {
                            return PageState.Explore;
                        }
                    }
                    catch
                    {
                        // 忽略单个选择器检测失败
                    }
                }

                var searchDetectors = _pageStateSelectors[PageState.SearchResult]["PageContainer"];
                foreach (var selector in searchDetectors)
                {
                    try
                    {
                        var element = await page.QuerySelectorAsync(selector);
                        if (element != null)
                        {
                            return PageState.SearchResult;
                        }
                    }
                    catch
                    {
                        // 忽略单个选择器检测失败
                    }
                }

                return PageState.Unknown;
            }
            catch
            {
                return PageState.Unknown;
            }
        }

        /// <summary>
        /// 获取通用别名到候选选择器的完整映射（不包含页面状态特定映射）。
        /// </summary>
        public Dictionary<string, List<string>> GetAllSelectors()
        {
            return _selectors;
        }
    }
}
