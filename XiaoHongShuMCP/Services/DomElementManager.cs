using System.Text.RegularExpressions;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Persistence;
using HushOps.Core.Runtime.Playwright;
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
        private readonly Dictionary<string, List<string>> _selectorTemplates = new();
        private Dictionary<PageState, Dictionary<string, List<string>>> _pageStateSelectors;
        private readonly Dictionary<string, Func<IPage, ILocator>> _locatorBuilders = new(StringComparer.Ordinal);
        private readonly Dictionary<(string alias, PageState state), Func<IPage, ILocator>> _stateLocatorBuilders = new();
        private readonly LocatorSelectorsCatalog _catalog;


        public DomElementManager()
            : this(new LocatorSelectorsCatalog())
        {
        }

        internal DomElementManager(LocatorSelectorsCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _selectors = new Dictionary<string, List<string>>();
            _pageStateSelectors = new Dictionary<PageState, Dictionary<string, List<string>>>();
            LoadSelectorsFromCatalog();
            InitializeSelectorTemplates();
            InitializeLocatorBuilders();
        }

        /// <summary>
        /// 试图对指定别名的选择器顺序进行重排（仅在 newOrder 完全覆盖原集合且不含未知条目时生效）。
        /// </summary>
        public bool TryReorderSelectors(string alias, IEnumerable<string> newOrder)
        {
            if (string.IsNullOrWhiteSpace(alias)) return false;
            if (!_selectors.TryGetValue(alias, out var old)) return false;
            var list = newOrder?.ToList() ?? new List<string>();
            if (list.Count != old.Count) return false;
            // newOrder 必须是原集合的一个排列
            var setOld = new HashSet<string>(old);
            if (!list.All(setOld.Contains)) return false;
            _selectors[alias] = list;
            // 同步页面状态选择器中的相同别名（若存在多状态维护，则优先保留状态版本；此处仅更新通用集合）
            return true;
        }

        /// <summary>从 JSON 目录加载别名选择器映射，并构建页面状态专属集合。</summary>
        private void LoadSelectorsFromCatalog()
        {
            foreach (PageState state in Enum.GetValues<PageState>())
            {
                if (state is PageState.Auto or PageState.Unknown)
                {
                    continue;
                }

                if (!_pageStateSelectors.ContainsKey(state))
                {
                    _pageStateSelectors[state] = new Dictionary<string, List<string>>();
                }
            }

            foreach (var (alias, entry) in _catalog.Entries)
            {
                var general = entry.Selectors.Count == 0
                    ? new List<string>()
                    : new List<string>(entry.Selectors.Count);

                if (entry.Selectors.Count > 0)
                {
                    foreach (var selector in entry.Selectors)
                    {
                        general.Add(selector);
                    }
                }

                if (general.Count == 0)
                {
                    general.Add(alias);
                }

                _selectors[alias] = general;

                if (entry.States.Count == 0)
                {
                    continue;
                }

                foreach (var (stateName, selectors) in entry.States)
                {
                    if (!Enum.TryParse<PageState>(stateName, true, out var pageState) ||
                        pageState is PageState.Auto or PageState.Unknown)
                    {
                        continue;
                    }

                    if (!_pageStateSelectors.TryGetValue(pageState, out var aliasMap))
                    {
                        aliasMap = new Dictionary<string, List<string>>();
                        _pageStateSelectors[pageState] = aliasMap;
                    }

                    if (selectors.Count == 0)
                    {
                        continue;
                    }

                    var stateList = new List<string>(selectors.Count);
                    foreach (var selector in selectors)
                    {
                        stateList.Add(selector);
                    }

                    aliasMap[alias] = stateList;
                }
            }
        }

        /// <summary>初始化选择器模板集合，便于生成带参数的候选列表。</summary>
        private void InitializeSelectorTemplates()
        {
            _selectorTemplates["FilterButtonByType"] = new List<string>
            {
                "[data-filter='{type}']",
                ".filter-{typeLower}",
                ".filter-item:has-text('{type}')",
                "button:has-text('{type}')"
            };

            _selectorTemplates["FilterValueByText"] = new List<string>
            {
                "[data-value='{value}']",
                "button:has-text('{value}')"
            };
        }

        /// <summary>初始化常用 Locator 构建器，封装重复的 Playwright 调用。</summary>
        private void InitializeLocatorBuilders()
        {
            _locatorBuilders["SearchInput"] = page => page.GetByPlaceholder("搜索", new PageGetByPlaceholderOptions { Exact = false });
            _locatorBuilders["SearchButton"] = page => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
            {
                NameRegex = new Regex("(?i)^(搜索|Search)$")
            });
            _locatorBuilders["MainScrollContainer"] = page => page.Locator("main[role='main'], .search-content, .note-list, .note-feed");
        }

        private static ILocator? SafeBuildLocator(Func<IPage, ILocator> builder, IPage page)
        {
            try { return builder(page); }
            catch { return null; }
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
        /// 基于模板别名与占位符构建动态选择器列表（支持 {name} 与 {nameLower}）。
        /// </summary>
        public List<string> BuildSelectors(string templateAlias, IDictionary<string, string> tokens)
        {
            if (!_selectorTemplates.TryGetValue(templateAlias, out var templates) || templates.Count == 0)
            {
                return new List<string>();
            }

            string ReplacePlaceholders(string tpl)
            {
                foreach (var kvp in tokens)
                {
                    var key = kvp.Key;
                    var val = kvp.Value ?? string.Empty;
                    tpl = tpl.Replace("{" + key + "}", val);
                    tpl = tpl.Replace("{" + key + "Lower}", val.ToLowerInvariant());
                }
                return tpl;
            }

            var result = new List<string>(templates.Count);
            foreach (var t in templates)
            {
                result.Add(ReplacePlaceholders(t));
            }
            return result;
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

        public ILocator? CreateLocator(IPage page, string alias, PageState pageState = PageState.Auto)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentNullException(nameof(alias));

            if (pageState != PageState.Auto &&
                _stateLocatorBuilders.TryGetValue((alias, pageState), out var stateBuilder))
            {
                var stateLocator = SafeBuildLocator(stateBuilder, page);
                if (stateLocator != null) return stateLocator;
            }

            if (_locatorBuilders.TryGetValue(alias, out var builder))
            {
                var locator = SafeBuildLocator(builder, page);
                if (locator != null) return locator;
            }

            var selectors = pageState == PageState.Auto
                ? GetSelectors(alias)
                : GetSelectors(alias, pageState);

            foreach (var selector in selectors)
            {
                if (string.IsNullOrWhiteSpace(selector)) continue;
                return page.Locator(selector);
            }

            return null;
        }

        /// <summary>
        /// 检测当前页面状态。
        /// - 先通过 URL 片段进行快速判断；
        /// - 若无法判定，则按状态对应的页面容器选择器做 DOM 存在性检测；
        /// - 若仍无法判定，返回 Unknown。
        /// </summary>
        /// <summary>
        /// 基于抽象页面的页面状态检测入口（内部转换为 Playwright 原生实现）。
        /// </summary>
        /// <param name="page">抽象页面实例</param>
        public async Task<PageState> DetectPageStateAsync(IAutoPage page)
        {
            var nativePage = PlaywrightAutoFactory.TryUnwrap(page);
            if (nativePage == null)
            {
                throw new InvalidOperationException("DomElementManager 仅支持 Playwright 实现的 IAutoPage");
            }
            return await DetectPageStateAsync(nativePage);
        }

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
