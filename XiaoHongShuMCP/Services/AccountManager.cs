using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 简化的账号管理器，连接到当前浏览器会话
/// </summary>
public class AccountManager : IAccountManager
{
    private readonly ILogger<AccountManager> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly IDomElementManager _domElementManager;
    private readonly IPageLoadWaitService _pageLoadWaitService;
    // === 全局用户信息管理功能（合并自 GlobalUserInfo） ===
    private readonly object _lock = new object();
    private UserInfo? _currentUser;
    private DateTime? _lastUpdated;

    /// <summary>
    /// 全局当前用户信息
    /// </summary>
    public UserInfo? CurrentUser
    {
        get
        {
            lock (_lock)
            {
                return _currentUser;
            }
        }
        private set
        {
            lock (_lock)
            {
                _currentUser = value;
                _lastUpdated = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? LastUpdated
    {
        get
        {
            lock (_lock)
            {
                return _lastUpdated;
            }
        }
    }

    /// <summary>
    /// 是否有有效的全局用户信息
    /// </summary>
    public bool HasValidUserInfo
    {
        get
        {
            lock (_lock)
            {
                return _currentUser is {IsLoggedIn: true};
            }
        }
    }

    /// <summary>
    /// 更新全局用户信息
    /// </summary>
    /// <param name="userInfo">新的用户信息</param>
    public void UpdateUserInfo(UserInfo? userInfo)
    {
        CurrentUser = userInfo;
    }

    /// <summary>
    /// 从API响应JSON更新全局用户信息
    /// </summary>
    /// <param name="responseJson">API响应的JSON字符串</param>
    /// <returns>是否成功更新</returns>
    public bool UpdateFromApiResponse(string responseJson)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            // 尝试解析小红书用户API响应格式
            var apiResponse = JsonSerializer.Deserialize<XiaoHongShuUserApiResponse>(responseJson, options);
            
            if (apiResponse is {Success: true, Data: not null})
            {
                var userData = apiResponse.Data;
                var userInfo = new UserInfo
                {
                    UserId = userData.UserId,
                    Nickname = userData.Nickname,
                    IsLoggedIn = true,
                    RedId = userData.RedId,
                    AvatarUrl = userData.Avatar,
                    Avatar = userData.Avatar,
                    FollowersCount = userData.FansCount ?? userData.FollowersCount,
                    FollowingCount = userData.FollowCount ?? userData.FollowingCount,
                    LikesCollectsCount = userData.NoteCount ?? userData.NotesCount,
                    Description = userData.Desc ?? userData.Description
                };

                UpdateUserInfo(userInfo);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析用户API响应失败");
            return false;
        }
    }

    /// <summary>
    /// 清除全局用户信息
    /// </summary>
    public void ClearUserInfo()
    {
        CurrentUser = null;
    }

    /// <summary>
    /// 获取全局用户信息的简要描述
    /// </summary>
    /// <returns>用户信息描述</returns>
    public string GetUserInfoSummary()
    {
        lock (_lock)
        {
            if (_currentUser == null)
                return "无用户信息";

            var summary = $"用户: {_currentUser.Nickname}";
            if (!string.IsNullOrEmpty(_currentUser.RedId))
                summary += $" (小红书号: {_currentUser.RedId})";
            
            if (_lastUpdated.HasValue)
                summary += $" [更新于: {_lastUpdated.Value:HH:mm:ss}]";

            return summary;
        }
    }

    public AccountManager(
        ILogger<AccountManager> logger,
        IBrowserManager browserManager,
        IDomElementManager domElementManager,
        IPageLoadWaitService pageLoadWaitService)
    {
        _logger = logger;
        _browserManager = browserManager;
        _domElementManager = domElementManager;
        _pageLoadWaitService = pageLoadWaitService;
    }

    /// <summary>
    /// 连接到浏览器并验证登录状态
    /// </summary>
    public async Task<OperationResult<bool>> ConnectToBrowserAsync()
    {
        _logger.LogInformation("正在连接到浏览器...");

        try
        {
            // 获取浏览器上下文，这将触发与浏览器的连接
            await _browserManager.GetBrowserContextAsync();

            // 检查登录状态
            var isLoggedIn = await _browserManager.IsLoggedInAsync();
            
            if (!isLoggedIn)
            {
                ClearUserInfo();
                _logger.LogWarning("浏览器未登录，请先登录小红书账号");
                
                return OperationResult<bool>.Fail(
                    "浏览器未登录，请先登录小红书账号",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }
            
            _logger.LogInformation("浏览器连接成功，登录状态: {IsLoggedIn}", isLoggedIn);
            return OperationResult<bool>.Ok(isLoggedIn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到浏览器失败");
            return OperationResult<bool>.Fail(
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
    /// 获取完整的用户个人页面数据
    /// 访问指定用户的个人页面并提取完整数据
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>完整的用户信息</returns>
    public async Task<OperationResult<UserInfo>> GetUserProfileDataAsync(string userId)
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

            var page = await _browserManager.GetPageAsync();

            // 构造个人页面URL
            var profileUrl = $"https://www.xiaohongshu.com/user/profile/{userId}";
            _logger.LogInformation("正在访问用户个人页面: {ProfileUrl}", profileUrl);

            // 导航到个人页面（带忙碌租约 + 自愈重试）
            _browserManager.BeginOperation();
            try
            {
                try
                {
                    await page.GotoAsync(profileUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });
                }
                catch (Microsoft.Playwright.PlaywrightException)
                {
                    _logger.LogWarning("页面在导航用户页时关闭，尝试重新获取页面并重试");
                    page = await _browserManager.GetPageAsync();
                    await page.GotoAsync(profileUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });
                }
                // 等待页面加载完成（统一等待服务）
                await _pageLoadWaitService.WaitForPageLoadAsync(page);
            }
            finally
            {
                _browserManager.EndOperation();
            }

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
                "成功提取个人页面数据: UserId={UserId}, Nickname={Nickname}, RedId={RedId}",
                userInfo.UserId, userInfo.Nickname, userInfo.RedId);

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
            var userPageSelectors = _domElementManager.GetSelectors("UserPageContainer");

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
            var usernameSelectors = _domElementManager.GetSelectors("UserPageName");

            foreach (var selector in usernameSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var username = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        userInfo.Nickname = username.Trim();
                        _logger.LogDebug("提取到用户名: {Nickname}", userInfo.Nickname);
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
            var redIdSelectors = _domElementManager.GetSelectors("UserRedId");

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
            var descriptionSelectors = _domElementManager.GetSelectors("UserDescription");

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
            var avatarSelectors = _domElementManager.GetSelectors("UserPageAvatar");

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
            var followingSelectors = _domElementManager.GetSelectors("UserFollowingCount");

            foreach (var selector in followingSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var countText = await element.InnerTextAsync();
                    if (int.TryParse(countText.Trim(), out var count))
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
            var followersSelectors = _domElementManager.GetSelectors("UserFollowersCount");

            foreach (var selector in followersSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var countText = await element.InnerTextAsync();
                    if (int.TryParse(countText.Trim(), out var count))
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
            var likesCollectsSelectors = _domElementManager.GetSelectors("UserLikesCollectsCount");

            foreach (var selector in likesCollectsSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var countText = await element.InnerTextAsync();
                    if (int.TryParse(countText.Trim(), out var count))
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
            var verifySelectors = _domElementManager.GetSelectors("UserVerifyIcon");

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

}

/// <summary>
/// 小红书用户API响应数据结构
/// </summary>
public class XiaoHongShuUserApiResponse
{
    public bool Success { get; set; }
    public UserInfo? Data { get; set; }
    public int Code { get; set; }
    public string? Msg { get; set; }
}
