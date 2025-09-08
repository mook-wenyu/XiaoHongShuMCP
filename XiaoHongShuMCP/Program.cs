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
    .AddInMemoryCollection(defaultSettings)   // 代码内默认
    .AddEnvironmentVariables(prefix: "XHS__") // 环境变量覆盖（XHS__Section__Key）
    .AddCommandLine(args)                     // 命令行覆盖（Section:Key=value）
    .Build();

// 确保日志目录存在
var logDirectory = configuration["Serilog:LogDirectory"] ?? "/logs";
// 使用项目根目录作为基准路径，而不是当前工作目录
var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
var fullLogDirectory = Path.IsPathRooted(logDirectory)
    ? logDirectory
    : Path.Combine(projectRoot, logDirectory);
Directory.CreateDirectory(fullLogDirectory);

var logFileTemplate = configuration["Serilog:FileNameTemplate"] ?? "xiaohongshu-mcp-.txt";
var logPath = Path.Combine(fullLogDirectory, logFileTemplate);

// 配置 Serilog 日志（过滤敏感信息）
// 读取最小日志级别（默认 Information），通过代码（Serilog:MinimumLevel）配置
var minimumLevelString = configuration["Serilog:MinimumLevel"] ?? "Information";
if (!Enum.TryParse<LogEventLevel>(minimumLevelString, true, out var minimumLevel))
{
    minimumLevel = LogEventLevel.Information;
}

// 是否仅对 UniversalApiMonitor 开启详细调试日志
var detailedMonitorLogs = false;
var detailedStr = configuration["UniversalApiMonitor:EnableDetailedLogging"];
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

// 从配置读取“按命名空间覆盖”映射：Logging:Overrides:<Namespace>=<Level>
var overridesSection = configuration.GetSection("Logging:Overrides");
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
        .AddEnvironmentVariables(prefix: "XHS__")
        .AddCommandLine(args);

    // 配置日志（统一使用 Serilog，并输出到标准错误流）
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog();


    // 配置 PageLoadWaitService 配置（来源于内存配置）
    builder.Services.Configure<PageLoadWaitConfig>(
        builder.Configuration.GetSection("PageLoadWaitConfig"));

    // 配置搜索相关超时（来源于内存配置）
    builder.Services.Configure<SearchTimeoutsConfig>(
        builder.Configuration.GetSection("SearchTimeoutsConfig"));

    // 配置详情匹配（权重/阈值/拼音）
    builder.Services.Configure<DetailMatchConfig>(
        builder.Configuration.GetSection("DetailMatchConfig"));

    // MCP 统一等待超时配置
    builder.Services.Configure<McpSettings>(
        builder.Configuration.GetSection("McpSettings"));

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
static Dictionary<string, string?> CreateDefaultSettings()
{
    return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        // Serilog
        ["Serilog:LogDirectory"] = "logs",
        ["Serilog:FileNameTemplate"] = "xiaohongshu-mcp-.txt",
        ["Serilog:MinimumLevel"] = "Information",

        // UniversalApiMonitor
        ["UniversalApiMonitor:EnableDetailedLogging"] = "true",

        // 基础参数
        ["BaseUrl"] = "https://www.xiaohongshu.com/explore",
        ["DefaultTimeout"] = "120000",
        ["MaxRetries"] = "3",

        // 浏览器
        ["BrowserSettings:Headless"] = "false",
        ["BrowserSettings:RemoteDebuggingPort"] = "9222",
        ["BrowserSettings:ConnectionTimeoutSeconds"] = "30",

        // MCP
        ["McpSettings:EnableProgressReporting"] = "true",
        ["McpSettings:MaxBatchSize"] = "10",
        ["McpSettings:DelayBetweenOperations"] = "1000",
        ["McpSettings:WaitTimeoutMs"] = "600000",

        // 页面加载等待配置
        ["PageLoadWaitConfig:DOMContentLoadedTimeout"] = "15000",
        ["PageLoadWaitConfig:LoadTimeout"] = "30000",
        ["PageLoadWaitConfig:NetworkIdleTimeout"] = "600000",
        ["PageLoadWaitConfig:MaxRetries"] = "3",
        ["PageLoadWaitConfig:RetryDelayMs"] = "2000",
        ["PageLoadWaitConfig:EnableDegradation"] = "true",
        ["PageLoadWaitConfig:FastModeTimeout"] = "10000",

        // 搜索超时
        ["SearchTimeoutsConfig:UiWaitMs"] = "12000",
        ["SearchTimeoutsConfig:ApiCollectionMaxWaitMs"] = "60000",

        // 详情匹配（权重/阈值/拼音）
        ["DetailMatchConfig:WeightedThreshold"] = "0.5",
        ["DetailMatchConfig:TitleWeight"] = "4",
        ["DetailMatchConfig:AuthorWeight"] = "3",
        ["DetailMatchConfig:ContentWeight"] = "2",
        ["DetailMatchConfig:HashtagWeight"] = "2",
        ["DetailMatchConfig:ImageAltWeight"] = "1",
        ["DetailMatchConfig:UseFuzzy"] = "true",
        ["DetailMatchConfig:MaxDistanceCap"] = "3",
        ["DetailMatchConfig:TokenCoverageThreshold"] = "0.7",
        ["DetailMatchConfig:IgnoreSpaces"] = "true",
        ["DetailMatchConfig:UsePinyin"] = "true",
        ["DetailMatchConfig:PinyinInitialsOnly"] = "true"
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
            .AddEnvironmentVariables(prefix: "XHS__")
            .AddCommandLine(args);
        builder.Services.Configure<PageLoadWaitConfig>(builder.Configuration.GetSection("PageLoadWaitConfig"));
        builder.Services.Configure<SearchTimeoutsConfig>(builder.Configuration.GetSection("SearchTimeoutsConfig"));
        builder.Services.Configure<McpSettings>(builder.Configuration.GetSection("McpSettings"));
        builder.Services.Configure<DetailMatchConfig>(builder.Configuration.GetSection("DetailMatchConfig"));

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
