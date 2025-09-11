using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using XiaoHongShuMCP.Services;
using System.Text.Json;
using System.Reflection;
using XiaoHongShuMCP.Tools;
using ModelContextProtocol.Server;
using System.Text.RegularExpressions;
using System.ComponentModel;

// 使用代码定义所有配置（不再依赖 appsettings.json）
var defaultSettings = CreateDefaultSettings();

// 先创建基础配置以获取 Serilog 设置
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(defaultSettings)   // 代码内默认（已统一到根节 XHS）
    .AddEnvironmentVariables()                // 移除前缀过滤：统一在节 XHS 下读取
    .AddCommandLine(args)                     // 命令行覆盖（Section:Key=value）
    .Build();

// 确保日志目录存在
// 从统一根节 XHS 读取日志配置
var logDirectory = configuration["XHS:Serilog:LogDirectory"] ?? "/logs";
// 使用项目根目录作为基准路径，而不是当前工作目录
var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
var fullLogDirectory = Path.IsPathRooted(logDirectory)
    ? logDirectory
    : Path.Combine(projectRoot, logDirectory);
Directory.CreateDirectory(fullLogDirectory);

var logFileTemplate = configuration["XHS:Serilog:FileNameTemplate"] ?? "xiaohongshu-mcp-.txt";
var logPath = Path.Combine(fullLogDirectory, logFileTemplate);

// 配置 Serilog 日志（过滤敏感信息）
// 读取最小日志级别（默认 Information），通过代码（Serilog:MinimumLevel）配置
var minimumLevelString = configuration["XHS:Serilog:MinimumLevel"] ?? "Information";
if (!Enum.TryParse<LogEventLevel>(minimumLevelString, true, out var minimumLevel))
{
    minimumLevel = LogEventLevel.Information;
}

// 是否仅对 UniversalApiMonitor 开启详细调试日志
var detailedMonitorLogs = false;
var detailedStr = configuration["XHS:UniversalApiMonitor:EnableDetailedLogging"];
if (!string.IsNullOrWhiteSpace(detailedStr))
{
    bool.TryParse(detailedStr, out detailedMonitorLogs);
}

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(minimumLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Filter.ByExcluding(ContainsSensitive)
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day);

if (detailedMonitorLogs)
{
    loggerConfig = loggerConfig
        .MinimumLevel.Override(typeof(UniversalApiMonitor).FullName!, LogEventLevel.Debug);
}

// 从配置读取“按命名空间覆盖”映射：XHS:Logging:Overrides:<Namespace>=<Level>
var overridesSection = configuration.GetSection("XHS:Logging:Overrides");
if (overridesSection.Exists())
{
    foreach (var child in overridesSection.GetChildren())
    {
        var ns = child.Key;
        var levelStr = child.Value;
        if (!string.IsNullOrWhiteSpace(ns) && !string.IsNullOrWhiteSpace(levelStr)
            && Enum.TryParse<LogEventLevel>(levelStr, true, out var nsLevel))
        {
            loggerConfig = loggerConfig.MinimumLevel.Override(ns, nsLevel);
        }
    }
}

Log.Logger = loggerConfig.CreateLogger();

Log.Information("UniversalApiMonitor detailed logging: {Enabled}", detailedMonitorLogs);

try
{
    // 提前处理 CLI 自测试：callTool 模式
    if (await TryHandleDocsVerify(args))
    {
        return;
    }
    if (await TryHandleToolsList(args))
    {
        return;
    }
    if (await TryHandleCallTool(args, configuration))
    {
        return; // 已处理并输出结果
    }

    Log.Information("启动小红书 MCP 服务器...");

    var builder = Host.CreateApplicationBuilder(args);

    // 覆盖 Host 配置为代码内存配置 + 环境变量/命令行覆盖（代码优先，可被外部覆盖）
    builder.Configuration
        .AddInMemoryCollection(defaultSettings)
        .AddEnvironmentVariables() // 不再使用前缀过滤；统一从根节 XHS 读取
        .AddCommandLine(args);
    // 支持统一 Configs:* 键的兼容映射
    // 已移除兼容映射：请直接使用新节名（如 BrowserSettings:*, InteractionCache:*）

    // 配置日志（统一使用 Serilog，并输出到标准错误流）
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog();


    // 仅注册一个配置类：XhsSettings（破坏性变更）
    builder.Services.Configure<XhsSettings>(
        builder.Configuration.GetSection("XHS"));

    // 配置服务依赖注入
    builder.Services
        .AddSingleton<IBrowserManager, PlaywrightBrowserManager>()
        .AddSingleton<PlaywrightBrowserManager>(provider => (PlaywrightBrowserManager)provider.GetRequiredService<IBrowserManager>())
        .AddSingleton<IAccountManager, AccountManager>()
        .AddSingleton<IDomElementManager, DomElementManager>()
        .AddSingleton<IDelayManager, DelayManager>()
        .AddSingleton<IElementFinder, ElementFinder>()
        .AddSingleton<ITextInputStrategy, RegularInputStrategy>()
        .AddSingleton<ITextInputStrategy, ContentEditableInputStrategy>()
        .AddSingleton<IHumanizedInteractionService, HumanizedInteractionService>()
        .AddSingleton<IUniversalApiMonitor, UniversalApiMonitor>()
        .AddSingleton<IPageStateGuard, PageStateGuard>()
        .AddSingleton<ISmartCollectionController, SmartCollectionController>()
        .AddSingleton<IXiaoHongShuService, XiaoHongShuService>()
        .AddSingleton<IPageLoadWaitService, PageLoadWaitService>();

    // 注册浏览器自动连接后台服务
    builder.Services.AddHostedService<BrowserConnectionHostedService>();

    // 配置 MCP 服务器
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var host = builder.Build();

    Log.Information("小红书 MCP 服务器已启动，等待客户端连接...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "服务器启动失败");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static bool ContainsSensitive(LogEvent le)
{
    try
    {
        var text = le.MessageTemplate.Text;
        var keyHit = le.Properties.Keys.Any(k =>
            k.Contains("Authorization", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Cookie", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("X-Api-Key", StringComparison.OrdinalIgnoreCase));
        return MsgHas("authorization") || MsgHas("cookie") || keyHit;
        bool MsgHas(string s) => text.Contains(s, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

/// <summary>
/// 使用代码定义的默认配置键值
/// </summary>
/// <summary>
/// 统一根节：XHS
/// - 所有默认键值均置于 XHS 下，实现“一致的命名空间”。
/// - 环境变量需使用双下划线映射冒号（示例：XHS__Serilog__MinimumLevel）。
/// </summary>
static Dictionary<string, string?> CreateDefaultSettings()
{
    return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        // Serilog（统一到 XHS 根节）
        ["XHS:Serilog:LogDirectory"] = "logs",
        ["XHS:Serilog:FileNameTemplate"] = "xiaohongshu-mcp-.txt",
        ["XHS:Serilog:MinimumLevel"] = "Information",

        // UniversalApiMonitor
        ["XHS:UniversalApiMonitor:EnableDetailedLogging"] = "true",

        // 基础参数（如需全局使用，统一置于 XHS 根）
        ["XHS:BaseUrl"] = "https://www.xiaohongshu.com/explore",
        ["XHS:DefaultTimeout"] = "120000",
        ["XHS:MaxRetries"] = "3",

        // 浏览器设置
        ["XHS:BrowserSettings:Headless"] = "false",
        ["XHS:BrowserSettings:RemoteDebuggingPort"] = "9222",
        ["XHS:BrowserSettings:ConnectionTimeoutSeconds"] = "30",

        // MCP 统一设置
        ["XHS:McpSettings:EnableProgressReporting"] = "true",
        ["XHS:McpSettings:MaxBatchSize"] = "10",
        ["XHS:McpSettings:DelayBetweenOperations"] = "1000",
        ["XHS:McpSettings:WaitTimeoutMs"] = "600000",

        // 页面加载等待配置
        ["XHS:PageLoadWaitConfig:DOMContentLoadedTimeout"] = "15000",
        ["XHS:PageLoadWaitConfig:LoadTimeout"] = "30000",
        ["XHS:PageLoadWaitConfig:NetworkIdleTimeout"] = "600000",
        ["XHS:PageLoadWaitConfig:MaxRetries"] = "3",
        ["XHS:PageLoadWaitConfig:RetryDelayMs"] = "2000",
        ["XHS:PageLoadWaitConfig:EnableDegradation"] = "true",
        ["XHS:PageLoadWaitConfig:FastModeTimeout"] = "10000",

        // 搜索相关超时
        ["XHS:SearchTimeoutsConfig:UiWaitMs"] = "12000",
        ["XHS:SearchTimeoutsConfig:ApiCollectionMaxWaitMs"] = "60000",

        // 端点等待重试（默认：2分钟 + 最多3次重试）
        ["XHS:EndpointRetry:AttemptTimeoutMs"] = "120000",
        ["XHS:EndpointRetry:MaxRetries"] = "3",

        // 详情匹配（权重/阈值/拼音）
        ["XHS:DetailMatchConfig:WeightedThreshold"] = "0.5",
        ["XHS:DetailMatchConfig:TitleWeight"] = "4",
        ["XHS:DetailMatchConfig:AuthorWeight"] = "3",
        ["XHS:DetailMatchConfig:ContentWeight"] = "2",
        ["XHS:DetailMatchConfig:HashtagWeight"] = "2",
        ["XHS:DetailMatchConfig:ImageAltWeight"] = "1",
        ["XHS:DetailMatchConfig:UseFuzzy"] = "true",
        ["XHS:DetailMatchConfig:MaxDistanceCap"] = "3",
        ["XHS:DetailMatchConfig:TokenCoverageThreshold"] = "0.7",
        ["XHS:DetailMatchConfig:IgnoreSpaces"] = "true",
        ["XHS:DetailMatchConfig:UsePinyin"] = "true",
        ["XHS:DetailMatchConfig:PinyinInitialsOnly"] = "true"
        ,
        // 交互缓存（单位：分钟）
        ["XHS:InteractionCache:TtlMinutes"] = "3"
    };
}

/// <summary>
/// 处理 CLI 自测试（callTool）
/// 调用形如：dotnet run --project XiaoHongShuMCP -- callTool ToolName --json '{...}'
/// 或：dotnet run --project XiaoHongShuMCP -- callTool ToolName --file args.json
/// </summary>
static async Task<bool> TryHandleCallTool(string[] args, IConfiguration configuration)
{
    try
    {
        if (args.Length == 0) return false;
        var idx = Array.FindIndex(args, a => string.Equals(a, "callTool", StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        if (idx + 1 >= args.Length)
        {
            Console.Error.WriteLine("[callTool] 缺少工具名参数");
            return true;
        }

        var toolName = args[idx + 1];
        string jsonArgs = "{}";
        var jsonIdx = Array.FindIndex(args, i => i.Equals("--json", StringComparison.OrdinalIgnoreCase));
        var fileIdx = Array.FindIndex(args, i => i.Equals("--file", StringComparison.OrdinalIgnoreCase));
        if (jsonIdx >= 0 && jsonIdx + 1 < args.Length)
        {
            jsonArgs = args[jsonIdx + 1];
        }
        else if (fileIdx >= 0 && fileIdx + 1 < args.Length)
        {
            var path = args[fileIdx + 1];
            jsonArgs = await File.ReadAllTextAsync(path);
        }

        // 构建最小宿主用于依赖注入（不启动）
        var builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog();
        builder.Configuration
            .AddInMemoryCollection(CreateDefaultSettings())
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        // 仅注册一个配置类：XhsSettings
        builder.Services.Configure<XhsSettings>(builder.Configuration.GetSection("XHS"));

        builder.Services
            .AddSingleton<IBrowserManager, PlaywrightBrowserManager>()
            .AddSingleton<PlaywrightBrowserManager>(p => (PlaywrightBrowserManager)p.GetRequiredService<IBrowserManager>())
            .AddSingleton<IAccountManager, AccountManager>()
            .AddSingleton<IDomElementManager, DomElementManager>()
            .AddSingleton<IDelayManager, DelayManager>()
            .AddSingleton<IElementFinder, ElementFinder>()
            .AddSingleton<ITextInputStrategy, RegularInputStrategy>()
            .AddSingleton<ITextInputStrategy, ContentEditableInputStrategy>()
            .AddSingleton<IHumanizedInteractionService, HumanizedInteractionService>()
            .AddSingleton<IUniversalApiMonitor, UniversalApiMonitor>()
            .AddSingleton<ISmartCollectionController, SmartCollectionController>()
            .AddSingleton<IXiaoHongShuService, XiaoHongShuService>()
            .AddSingleton<IPageLoadWaitService, PageLoadWaitService>();

        using var host = builder.Build();
        var serviceProvider = host.Services;

        // 反射查找工具方法
        var toolsType = typeof(XiaoHongShuTools);
        var method = toolsType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>() != null && m.Name.Equals(toolName, StringComparison.Ordinal));

        if (method == null)
        {
            Console.Error.WriteLine($"[callTool] 未找到工具: {toolName}");
            return true;
        }

        // 解析参数 JSON
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(jsonArgs) ? "{}" : jsonArgs);
        var root = doc.RootElement;

        var paramInfos = method.GetParameters();
        var finalArgs = new object?[paramInfos.Length];

        for (int i = 0; i < paramInfos.Length; i++)
        {
            var p = paramInfos[i];
            if (p.ParameterType == typeof(IServiceProvider))
            {
                finalArgs[i] = serviceProvider;
                continue;
            }

            // 从 JSON 中按名称（不区分大小写）取值
            object? value = null;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty(p.Name!, out var exact))
                {
                    value = ConvertJsonToType(exact, p.ParameterType);
                }
                else
                {
                    // 大小写不敏感匹配
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, p.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            value = ConvertJsonToType(prop.Value, p.ParameterType);
                            break;
                        }
                    }
                }
            }

            if (value == null)
            {
                value = p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType);
            }

            finalArgs[i] = value;
        }

        var invokeResult = method.Invoke(null, finalArgs);
        if (invokeResult is Task task)
        {
            await task;
            var retType = method.ReturnType;
            if (retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultProp = retType.GetProperty("Result");
                var value = resultProp?.GetValue(task);
                var json = JsonSerializer.Serialize(value, new JsonSerializerOptions {WriteIndented = true});
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine("null");
            }
        }
        else
        {
            var json = JsonSerializer.Serialize(invokeResult, new JsonSerializerOptions {WriteIndented = true});
            Console.WriteLine(json);
        }

        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[callTool] 执行异常: {ex.Message}\n{ex}");
        return true;
    }
}

/// <summary>
/// 文档核对：扫描 README 中的 callTool 用例，与代码中的 MCP 工具进行比对
/// 用法：dotnet run --project XiaoHongShuMCP -- docs-verify
/// 输出：JSON 差异报告
/// </summary>
static async Task<bool> TryHandleDocsVerify(string[] args)
{
    try
    {
        if (args.Length == 0) return false;
        if (!args.Any(a => string.Equals(a, "docs-verify", StringComparison.OrdinalIgnoreCase)))
            return false;

        var readmePath = Path.Combine(Directory.GetCurrentDirectory(), "README.md");
        if (!File.Exists(readmePath))
        {
            Console.Error.WriteLine("{ \"status\": \"error\", \"message\": \"README.md 不存在\" }");
            return true;
        }

        var readme = await File.ReadAllTextAsync(readmePath);
        var toolCalls = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(readme, "callTool\\(\\\"([^\\\"]+)\\\""))
        {
            var name = m.Groups[1].Value;
            toolCalls.Add(name);
        }

        var toolsType = typeof(XiaoHongShuTools);
        var codeTools = toolsType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute(typeof(McpServerToolAttribute)) != null)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var onlyInDocs = toolCalls.Except(codeTools).OrderBy(s => s).ToList();
        var onlyInCode = codeTools.Except(toolCalls).OrderBy(s => s).ToList();

        var report = new
        {
            status = (onlyInDocs.Count == 0 && onlyInCode.Count == 0) ? "ok" : "mismatch",
            toolsInDocs = toolCalls.OrderBy(s => s).ToList(),
            toolsInCode = codeTools.OrderBy(s => s).ToList(),
            onlyInDocs,
            onlyInCode
        };

        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions {WriteIndented = true}));
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[docs-verify] 执行异常: {ex.Message}\\n{ex}");
        return true;
    }
}

/// <summary>
/// 输出代码中的工具清单与签名（参数名/类型/默认值），用于人工核对
/// 用法：dotnet run --project XiaoHongShuMCP -- tools-list
/// </summary>
static async Task<bool> TryHandleToolsList(string[] args)
{
    try
    {
        if (args.Length == 0) return false;
        if (!args.Any(a => string.Equals(a, "tools-list", StringComparison.OrdinalIgnoreCase)))
            return false;

        var toolsType = typeof(XiaoHongShuTools);
        var items = new List<object>();
        foreach (var m in toolsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.GetCustomAttribute(typeof(McpServerToolAttribute)) == null) continue;
            var desc = m.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var ps = m.GetParameters().Select(p => new
            {
                name = p.Name,
                type = p.ParameterType.FullName,
                hasDefault = p.HasDefaultValue,
                defaultValue = p.HasDefaultValue ? p.DefaultValue : null
            });
            items.Add(new {name = m.Name, description = desc, parameters = ps});
        }

        Console.WriteLine(JsonSerializer.Serialize(new {tools = items}, new JsonSerializerOptions {WriteIndented = true}));
        await Task.CompletedTask;
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[tools-list] 执行异常: {ex.Message}\\n{ex}");
        return true;
    }
}
static object? ConvertJsonToType(JsonElement el, Type targetType)
{
    try
    {
        if (targetType == typeof(string))
        {
            return el.ValueKind == JsonValueKind.Null ? null : el.GetString();
        }
        if (targetType == typeof(int))
        {
            return el.ValueKind == JsonValueKind.Number ? el.GetInt32() : int.TryParse(el.GetString(), out var n) ? n : 0;
        }
        if (targetType == typeof(bool))
        {
            return el.ValueKind == JsonValueKind.True || (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b) && b);
        }
        if (targetType == typeof(List<string>))
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                return el.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
            }
            var s = el.GetString();
            return string.IsNullOrEmpty(s) ? new List<string>() : new List<string> {s};
        }
        if (targetType.IsEnum)
        {
            var s = el.GetString();
            return string.IsNullOrEmpty(s) ? GetDefault(targetType) : Enum.Parse(targetType, s, true);
        }

        // 默认走反序列化
        var json = el.GetRawText();
        return JsonSerializer.Deserialize(json, targetType);
    }
    catch
    {
        return GetDefault(targetType);
    }
}

static object? GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

/// <summary>
/// 兼容映射：将统一的 Configs:* 键映射为原有的节键，便于逐步迁移。
/// 例如：Configs:Headless -> BrowserSettings:Headless；Configs:TtlMinutes -> InteractionCache:TtlMinutes。
/// </summary>
// 兼容配置映射函数已删除：不再支持 Configs:* 到旧节名的自动转发。
