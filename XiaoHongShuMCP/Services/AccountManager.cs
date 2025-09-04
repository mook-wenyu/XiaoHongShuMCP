using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 简化的账号管理器，连接到当前浏览器会话
/// </summary>
public class AccountManager : IAccountManager
{
    private readonly ILogger<AccountManager> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly ISelectorManager _selectorManager;
    private UserInfo _currentUserInfo;

    public UserInfo CurrentUserInfo => _currentUserInfo;

    public AccountManager(
        ILogger<AccountManager> logger,
        IBrowserManager browserManager,
        ISelectorManager selectorManager)
    {
        _logger = logger;
        _browserManager = browserManager;
        _selectorManager = selectorManager;
    }

    /// <summary>
    /// 连接到浏览器并验证登录状态
    /// </summary>
    public async Task<OperationResult<UserInfo>> ConnectToBrowserAsync()
    {
        _logger.LogInformation("正在连接到浏览器...");

        try
        {
            // 获取浏览器上下文，这将触发与浏览器的连接
            await _browserManager.GetBrowserContextAsync();

            // 检查登录状态
            var isLoggedIn = await _browserManager.IsLoggedInAsync();

            _currentUserInfo = new UserInfo
            {
                IsLoggedIn = isLoggedIn,
                LastActiveTime = DateTime.UtcNow
            };

            _logger.LogInformation("浏览器连接成功，登录状态: {IsLoggedIn}", isLoggedIn);
            return OperationResult<UserInfo>.Ok(_currentUserInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到浏览器失败");
            return OperationResult<UserInfo>.Fail(
                $"连接浏览器失败: {ex.Message}",
                ErrorType.BrowserError,
                "BROWSER_CONNECTION_FAILED");
        }
    }


    /// <summary>
    /// 检查是否已登录
    /// </summary>
    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            return await _browserManager.IsLoggedInAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查登录状态失败");
            return false;
        }
    }

    /// <summary>
    /// 从浏览器提取当前用户信息
    /// </summary>
    private async Task<OperationResult<UserInfo>> ExtractCurrentUserInfoAsync()
    {
        try
        {
            var context = await _browserManager.GetBrowserContextAsync();
            var pages = context.Pages;
            if (!pages.Any())
            {
                return OperationResult<UserInfo>.Fail(
                    "没有可用的页面",
                    ErrorType.BrowserError,
                    "NO_PAGES_AVAILABLE");
            }

            var page = pages.First();

            // 导航到小红书主页以确保侧边栏可见
            await EnsureOnXiaoHongShuPageAsync(page);

            var userInfo = new UserInfo
            {
                IsLoggedIn = false,
                LastActiveTime = DateTime.UtcNow
            };

            // 1. 尝试从侧边栏用户链接提取用户ID
            await ExtractUserIdFromSidebarAsync(page, userInfo);

            // 2. 如果找到了用户ID，尝试获取完整的个人页面数据
            if (!string.IsNullOrEmpty(userInfo.UserId))
            {
                userInfo.IsLoggedIn = true;

                // 尝试获取完整的个人页面数据
                var completeUserInfo = await GetCompleteUserProfileDataAsync(userInfo.UserId);
                if (completeUserInfo is {Success: true, Data: not null})
                {
                    // 合并完整的个人页面数据
                    MergeUserInfo(userInfo, completeUserInfo.Data);
                }

                _logger.LogInformation("成功提取用户信息: UserId={UserId}, Username={Username}, RedId={RedId}",
                    userInfo.UserId, userInfo.Username, userInfo.RedId);
            }
            else
            {
                _logger.LogInformation("未检测到登录用户信息");
            }

            return OperationResult<UserInfo>.Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取用户信息失败");
            return OperationResult<UserInfo>.Fail(
                $"提取用户信息失败: {ex.Message}",
                ErrorType.NetworkError,
                "USER_EXTRACTION_FAILED");
        }
    }

    /// <summary>
    /// 确保页面在小红书主页，以便侧边栏可见
    /// </summary>
    private async Task EnsureOnXiaoHongShuPageAsync(IPage page)
    {
        try
        {
            var currentUrl = page.Url;
            if (!currentUrl.Contains("xiaohongshu.com"))
            {
                _logger.LogInformation("当前不在小红书页面，导航到主页");
                await page.GotoAsync("https://www.xiaohongshu.com", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 15000
                });
            }

            // 等待侧边栏加载
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导航到小红书主页失败");
        }
    }

    /// <summary>
    /// 从侧边栏用户链接提取用户ID - 增强版
    /// 支持多种fallback策略和更详细的日志记录
    /// </summary>
    private async Task ExtractUserIdFromSidebarAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            _logger.LogDebug("开始从侧边栏提取用户ID...");
            var userProfileSelectors = _selectorManager.GetSelectors("SidebarUserProfile");

            foreach (var selector in userProfileSelectors)
            {
                try
                {
                    _logger.LogDebug("尝试使用选择器: {Selector}", selector);
                    var userLinkElement = await page.QuerySelectorAsync(selector);
                    if (userLinkElement != null)
                    {
                        var href = await userLinkElement.GetAttributeAsync("href");
                        _logger.LogDebug("找到用户链接: {Href}", href);

                        if (!string.IsNullOrEmpty(href))
                        {
                            var userId = ExtractUserIdFromUrl(href);
                            if (!string.IsNullOrEmpty(userId))
                            {
                                userInfo.UserId = userId;

                                // 尝试提取用户名（如果链接包含用户名信息）
                                await TryExtractUsernameAsync(userLinkElement, userInfo);

                                _logger.LogInformation("成功从侧边栏提取用户信息: UserId={UserId}", userId);
                                return;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("选择器未找到匹配元素: {Selector}", selector);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("使用选择器 {Selector} 提取用户ID失败: {Error}", selector, ex.Message);
                    continue;
                }
            }

            // 如果所有常规选择器都失败，尝试备用策略
            await TryFallbackUserExtractionAsync(page, userInfo);

            if (string.IsNullOrEmpty(userInfo.UserId))
            {
                _logger.LogDebug("未能从侧边栏提取到用户ID");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取用户ID失败");
        }
    }

    /// <summary>
    /// 尝试提取用户名
    /// </summary>
    private async Task TryExtractUsernameAsync(IElementHandle userLinkElement, UserInfo userInfo)
    {
        try
        {
            // 尝试从链接文本提取用户名
            var linkText = await userLinkElement.InnerTextAsync();
            if (!string.IsNullOrWhiteSpace(linkText) && linkText != "我")
            {
                userInfo.Username = linkText.Trim();
                _logger.LogDebug("提取到用户名: {Username}", userInfo.Username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取用户名失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 备用用户信息提取策略
    /// </summary>
    private async Task TryFallbackUserExtractionAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            _logger.LogDebug("执行备用用户信息提取策略...");

            // 备用策略1：从页面URL中提取（如果当前在用户个人页面）
            var currentUrl = page.Url;
            if (currentUrl.Contains("/user/profile/"))
            {
                var userId = ExtractUserIdFromUrl(currentUrl);
                if (!string.IsNullOrEmpty(userId))
                {
                    userInfo.UserId = userId;
                    _logger.LogDebug("从当前页面URL提取到用户ID: {UserId}", userId);
                    return;
                }
            }

            // 备用策略2：从页面中的其他用户链接提取
            var fallbackSelectors = new[]
            {
                "a[href*='/user/profile/']",         // 任何用户个人页面链接
                ".user-info a",                      // 用户信息区域的链接
                ".profile-link",                     // 个人页面链接
                "[class*='user'] a[href*='profile']" // 包含user类的元素下的个人页面链接
            };

            foreach (var selector in fallbackSelectors)
            {
                try
                {
                    var elements = await page.QuerySelectorAllAsync(selector);
                    foreach (var element in elements.Take(3)) // 只检查前3个
                    {
                        var href = await element.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(href))
                        {
                            var userId = ExtractUserIdFromUrl(href);
                            if (!string.IsNullOrEmpty(userId))
                            {
                                userInfo.UserId = userId;
                                _logger.LogDebug("通过备用策略提取到用户ID: {UserId}", userId);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("备用选择器 {Selector} 失败: {Error}", selector, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("备用提取策略失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 提取用户头像信息
    /// </summary>
    private async Task ExtractUserAvatarAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            // 尝试从用户头像元素提取信息
            var avatarSelectors = new[] {".reds-avatar img", ".reds-image-container img", ".user img"};

            foreach (var selector in avatarSelectors)
            {
                try
                {
                    var avatarElement = await page.QuerySelectorAsync(selector);
                    if (avatarElement != null)
                    {
                        var src = await avatarElement.GetAttributeAsync("src");
                        if (!string.IsNullOrEmpty(src))
                        {
                            _logger.LogDebug("提取到用户头像URL: {AvatarUrl}", src);
                            // 可以在UserInfo中添加AvatarUrl字段来存储这个信息
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("使用选择器 {Selector} 提取头像失败: {Error}", selector, ex.Message);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取用户头像失败");
        }
    }

    /// <summary>
    /// 从URL中提取用户ID - 增强版
    /// 支持多种可能的用户ID格式和URL变体
    /// </summary>
    /// <param name="url">用户个人页面URL，支持多种格式</param>
    /// <returns>用户ID</returns>
    private string? ExtractUserIdFromUrl(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // 支持多种可能的用户ID格式：
            // 1. 标准格式：/user/profile/66482064000000001e00e245
            // 2. 带域名：https://www.xiaohongshu.com/user/profile/66482064000000001e00e245
            // 3. 可能的变体格式

            var patterns = new[]
            {
                @"/user/profile/([a-f0-9]{24,32})",    // 24-32位十六进制（最常见）
                @"/user/profile/([0-9a-f]{24,32})",    // 备用格式
                @"/user/profile/([A-Za-z0-9]{20,40})", // 更宽泛的字符集
                @"user/profile/([a-f0-9]{24,32})",     // 不带开头斜杠
                @"profile/([a-f0-9]{24,32})"           // 简化路径
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
                if (match is {Success: true, Groups.Count: > 1})
                {
                    var userId = match.Groups[1].Value;
                    if (IsValidUserId(userId))
                    {
                        _logger.LogDebug("成功提取用户ID: {UserId} (使用模式: {Pattern})", userId, pattern);
                        return userId;
                    }
                }
            }

            _logger.LogDebug("无法从URL提取有效用户ID: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析用户ID失败: {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// 验证用户ID的有效性
    /// </summary>
    /// <param name="userId">待验证的用户ID</param>
    /// <returns>是否为有效的用户ID</returns>
    private bool IsValidUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        // 小红书用户ID通常特征：
        // 1. 长度在20-40字符之间
        // 2. 包含数字和字母（通常是十六进制格式）
        // 3. 不全是数字，不全是字母

        if (userId.Length is < 20 or > 40)
            return false;

        // 检查是否包含有效字符
        if (!Regex.IsMatch(userId, @"^[a-fA-F0-9]+$", RegexOptions.IgnoreCase))
        {
            // 如果不是纯十六进制，检查是否是字母数字组合
            if (!Regex.IsMatch(userId, @"^[a-zA-Z0-9]+$"))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 获取完整的用户个人页面数据
    /// 访问指定用户的个人页面并提取完整数据
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>完整的用户信息</returns>
    public async Task<OperationResult<UserInfo>> GetCompleteUserProfileDataAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return OperationResult<UserInfo>.Fail(
                    "用户ID不能为空",
                    ErrorType.ValidationError,
                    "INVALID_USER_ID");
            }

            var context = await _browserManager.GetBrowserContextAsync();
            var pages = context.Pages;
            if (!pages.Any())
            {
                return OperationResult<UserInfo>.Fail(
                    "没有可用的页面",
                    ErrorType.BrowserError,
                    "NO_PAGES_AVAILABLE");
            }

            var page = pages.First();

            // 构造个人页面URL
            var profileUrl = $"https://www.xiaohongshu.com/user/profile/{userId}";
            _logger.LogInformation("正在访问用户个人页面: {ProfileUrl}", profileUrl);

            // 导航到个人页面
            await page.GotoAsync(profileUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 15000
            });

            // 等待页面加载完成
            await Task.Delay(2000);

            var userInfo = new UserInfo
            {
                UserId = userId,
                IsLoggedIn = true,
                LastActiveTime = DateTime.UtcNow,
            };

            // 检查是否成功到达个人页面
            var isUserPage = await IsOnUserProfilePageAsync(page);
            if (!isUserPage)
            {
                _logger.LogWarning("未能正常访问个人页面，可能页面不存在或访问受限");
                return OperationResult<UserInfo>.Ok(userInfo);
            }

            // 提取个人页面数据
            await ExtractUserProfileFromPageAsync(page, userInfo);

            _logger.LogInformation(
                "成功提取个人页面数据: UserId={UserId}, Username={Username}, RedId={RedId}",
                userInfo.UserId, userInfo.Username, userInfo.RedId);

            return OperationResult<UserInfo>.Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户个人页面数据失败: UserId={UserId}", userId);
            return OperationResult<UserInfo>.Fail(
                $"获取个人页面数据失败: {ex.Message}",
                ErrorType.NetworkError,
                "PROFILE_EXTRACTION_FAILED");
        }
    }

    /// <summary>
    /// 检查是否在用户个人页面
    /// </summary>
    private async Task<bool> IsOnUserProfilePageAsync(IPage page)
    {
        try
        {
            // 检查个人页面的特征元素
            var userPageSelectors = _selectorManager.GetSelectors("UserPageContainer");

            foreach (var selector in userPageSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("检查个人页面状态失败: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 从个人页面提取用户数据 - 新增核心功能
    /// 基于真实HTML结构提取所有可用的用户信息
    /// </summary>
    private async Task ExtractUserProfileFromPageAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            _logger.LogDebug("开始从个人页面提取数据...");

            // 1. 提取用户名
            await ExtractUsernameFromProfileAsync(page, userInfo);

            // 2. 提取小红书号
            await ExtractRedIdFromProfileAsync(page, userInfo);

            // 3. 提取个人简介
            await ExtractDescriptionFromProfileAsync(page, userInfo);

            // 4. 提取头像信息
            await ExtractAvatarFromProfileAsync(page, userInfo);

            // 5. 提取统计数据
            await ExtractStatisticsFromProfileAsync(page, userInfo);

            // 6. 提取认证信息
            await ExtractVerificationFromProfileAsync(page, userInfo);

            _logger.LogDebug("个人页面数据提取完成");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从个人页面提取数据失败");
        }
    }

    /// <summary>
    /// 从个人页面提取用户名
    /// </summary>
    private async Task ExtractUsernameFromProfileAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var usernameSelectors = _selectorManager.GetSelectors("UserPageName");

            foreach (var selector in usernameSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var username = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        userInfo.Username = username.Trim();
                        _logger.LogDebug("提取到用户名: {Username}", userInfo.Username);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取用户名失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 从个人页面提取小红书号
    /// </summary>
    private async Task ExtractRedIdFromProfileAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var redIdSelectors = _selectorManager.GetSelectors("UserRedId");

            foreach (var selector in redIdSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var redIdText = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(redIdText))
                    {
                        var redId = UserInfo.ExtractRedIdFromText(redIdText);
                        if (!string.IsNullOrEmpty(redId))
                        {
                            userInfo.RedId = redId;
                            _logger.LogDebug("提取到小红书号: {RedId}", userInfo.RedId);
                            return;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取小红书号失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 从个人页面提取个人简介
    /// </summary>
    private async Task ExtractDescriptionFromProfileAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var descriptionSelectors = _selectorManager.GetSelectors("UserDescription");

            foreach (var selector in descriptionSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var description = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        userInfo.Description = description.Trim();
                        _logger.LogDebug("提取到个人简介: {Description}",
                            userInfo.Description.Length > 50 ? userInfo.Description.Substring(0, 50) + "..." : userInfo.Description);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取个人简介失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 从个人页面提取头像信息
    /// </summary>
    private async Task ExtractAvatarFromProfileAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var avatarSelectors = _selectorManager.GetSelectors("UserPageAvatar");

            foreach (var selector in avatarSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var avatarUrl = await element.GetAttributeAsync("src");
                    if (!string.IsNullOrWhiteSpace(avatarUrl))
                    {
                        userInfo.AvatarUrl = avatarUrl.Trim();
                        _logger.LogDebug("提取到头像URL: {AvatarUrl}", userInfo.AvatarUrl);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取头像信息失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 从个人页面提取统计数据
    /// </summary>
    private async Task ExtractStatisticsFromProfileAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            // 提取关注数
            await ExtractFollowingCountAsync(page, userInfo);

            // 提取粉丝数
            await ExtractFollowersCountAsync(page, userInfo);

            // 提取获赞与收藏数
            await ExtractLikesCollectsCountAsync(page, userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取统计数据失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 提取关注数
    /// </summary>
    private async Task ExtractFollowingCountAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var followingSelectors = _selectorManager.GetSelectors("UserFollowingCount");

            foreach (var selector in followingSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var countText = await element.InnerTextAsync();
                    if (int.TryParse(countText?.Trim(), out var count))
                    {
                        userInfo.FollowingCount = count;
                        _logger.LogDebug("提取到关注数: {Count}", count);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取关注数失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 提取粉丝数
    /// </summary>
    private async Task ExtractFollowersCountAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var followersSelectors = _selectorManager.GetSelectors("UserFollowersCount");

            foreach (var selector in followersSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var countText = await element.InnerTextAsync();
                    if (int.TryParse(countText?.Trim(), out var count))
                    {
                        userInfo.FollowersCount = count;
                        _logger.LogDebug("提取到粉丝数: {Count}", count);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取粉丝数失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 提取获赞与收藏数
    /// </summary>
    private async Task ExtractLikesCollectsCountAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var likesCollectsSelectors = _selectorManager.GetSelectors("UserLikesCollectsCount");

            foreach (var selector in likesCollectsSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var countText = await element.InnerTextAsync();
                    if (int.TryParse(countText?.Trim(), out var count))
                    {
                        userInfo.LikesCollectsCount = count;
                        _logger.LogDebug("提取到获赞与收藏数: {Count}", count);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取获赞与收藏数失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 提取认证信息
    /// </summary>
    private async Task ExtractVerificationFromProfileAsync(IPage page, UserInfo userInfo)
    {
        try
        {
            var verifySelectors = _selectorManager.GetSelectors("UserVerifyIcon");

            foreach (var selector in verifySelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var xlinkHref = await element.GetAttributeAsync("xlink:href");
                    if (!string.IsNullOrEmpty(xlinkHref))
                    {
                        userInfo.IsVerified = true;
                        userInfo.VerificationType = xlinkHref.Replace("#", "");
                        _logger.LogDebug("提取到认证信息: Type={VerificationType}", userInfo.VerificationType);
                        return;
                    }
                }
            }

            userInfo.IsVerified = false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取认证信息失败: {Error}", ex.Message);
            userInfo.IsVerified = false;
        }
    }

    /// <summary>
    /// 合并用户信息
    /// </summary>
    private void MergeUserInfo(UserInfo target, UserInfo source)
    {
        if (source == null) return;

        if (!string.IsNullOrEmpty(source.Username)) target.Username = source.Username;
        if (!string.IsNullOrEmpty(source.RedId)) target.RedId = source.RedId;
        if (!string.IsNullOrEmpty(source.Description)) target.Description = source.Description;
        if (!string.IsNullOrEmpty(source.AvatarUrl)) target.AvatarUrl = source.AvatarUrl;
        if (source.FollowingCount.HasValue) target.FollowingCount = source.FollowingCount;
        if (source.FollowersCount.HasValue) target.FollowersCount = source.FollowersCount;
        if (source.LikesCollectsCount.HasValue) target.LikesCollectsCount = source.LikesCollectsCount;
        target.IsVerified = source.IsVerified;
        if (!string.IsNullOrEmpty(source.VerificationType)) target.VerificationType = source.VerificationType;

    }
}
