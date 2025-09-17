using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Tools;

namespace XiaoHongShuMCP.Tooling;

/// <summary>
/// 工具经纪实现：集中管理所有拟人化自动化工具，提供动态列表与调用分发能力。
/// </summary>
public sealed class ToolBroker : IToolBroker
{
    private static readonly Type[] DefaultToolTypes =
    {
        typeof(XiaoHongShuTools)
    };

    private static readonly IReadOnlyDictionary<string, ToolSemanticHint> DefaultSemanticHints =
        new Dictionary<string, ToolSemanticHint>(StringComparer.Ordinal)
        {
            [nameof(XiaoHongShuTools.ConnectToBrowser)] = new("浏览器连接与登录检测", "browser", ReadOnly: true, Destructive: false, Humanized: true, SortOrder: 0),
            [nameof(XiaoHongShuTools.GetNoteDetail)] = new("关键词笔记详情采集", "read", ReadOnly: true, Destructive: false, Humanized: true, SortOrder: 10),
            [nameof(XiaoHongShuTools.ScrollCurrentPage)] = new("拟人化页面滚动", "humanized_navigation", ReadOnly: true, Destructive: false, Humanized: true, SortOrder: 20),
            [nameof(XiaoHongShuTools.LikeNote)] = new("点赞笔记", "interaction", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 30),
            [nameof(XiaoHongShuTools.UnlikeNote)] = new("取消点赞", "interaction", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 31),
            [nameof(XiaoHongShuTools.FavoriteNote)] = new("收藏笔记", "interaction", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 40),
            [nameof(XiaoHongShuTools.UncollectNote)] = new("取消收藏", "interaction", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 41),
            [nameof(XiaoHongShuTools.InteractNote)] = new("组合交互（点赞/收藏）", "interaction_bundle", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 50),
            [nameof(XiaoHongShuTools.PostComment)] = new("发布评论", "interaction", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 60),
            [nameof(XiaoHongShuTools.TemporarySaveAndLeave)] = new("暂存草稿并退出", "draft", ReadOnly: false, Destructive: true, Humanized: true, SortOrder: 70),
        };

    private readonly ILogger<ToolBroker> _logger;
    private readonly IReadOnlyDictionary<string, ToolBinding> _toolBindings;
    private readonly HashSet<string> _enabledTokens;
    private readonly HashSet<string> _disabledTokens;
    private readonly IReadOnlyDictionary<string, string> _titleOverrides;
    private readonly IReadOnlyDictionary<string, string> _descriptionOverrides;

    /// <summary>
    /// 初始化工具经纪实例。
    /// </summary>
    public ToolBroker(
        IServiceProvider services,
        IOptions<XhsSettings> settings,
        ILogger<ToolBroker> logger)
    {
        _logger = logger;
        var xhs = settings.Value ?? new XhsSettings();
        var mcp = xhs.McpSettings ?? new XhsSettings.McpSettingsSection();

        _enabledTokens = BuildTokenSet(mcp.EnabledToolNames);
        _disabledTokens = BuildTokenSet(mcp.DisabledToolNames);
        _titleOverrides = (mcp.ToolTitleOverrides ?? new Dictionary<string, string>()).ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        _descriptionOverrides = (mcp.ToolDescriptionOverrides ?? new Dictionary<string, string>()).ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

        _toolBindings = BuildBindings(services);
    }

    /// <inheritdoc />
    public ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        var result = new ListToolsResult();
        foreach (var binding in EnumerateActiveBindings())
        {
            var manifest = CreateManifest(binding);
            result.Tools.Add(manifest);
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var toolName = request.Params?.Name;
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return CreateErrorResult("TOOL_NAME_MISSING", "未提供工具名称，无法执行。");
        }

        if (!_toolBindings.TryGetValue(toolName, out var binding))
        {
            _logger.LogWarning("[MCP] 未找到工具: {ToolName}", toolName);
            return CreateErrorResult("TOOL_NOT_FOUND", $"工具 {toolName} 不在经纪列表中。");
        }

        if (!IsToolEnabled(binding))
        {
            _logger.LogInformation("[MCP] 工具被配置禁用: {Tool}", toolName);
            return CreateErrorResult("TOOL_DISABLED", $"工具 {toolName} 已被禁用，请检查配置。");
        }

        _logger.LogInformation("[MCP] 调用工具: {Tool}", toolName);
        return await binding.Tool.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyDictionary<string, ToolBinding> BuildBindings(IServiceProvider services)
    {
        var map = new Dictionary<string, ToolBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var toolType in DefaultToolTypes)
        {
            foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is null)
                {
                    continue;
                }

                var tool = method.IsStatic
                    ? McpServerTool.Create(method, options: new McpServerToolCreateOptions { Services = services })
                    : McpServerTool.Create(method, request => CreateInstance(request, toolType, services), new McpServerToolCreateOptions { Services = services });

                var methodName = method.Name;
                var hint = ResolveHint(methodName);
                var binding = new ToolBinding(tool.ProtocolTool.Name, methodName, tool, hint);
                map[binding.Name] = binding;
            }
        }

        return map;
    }

    private static object CreateInstance(RequestContext<CallToolRequestParams> request, Type toolType, IServiceProvider rootServices)
    {
        var provider = request.Services ?? rootServices;
        return provider.GetRequiredService(toolType);
    }

    private ToolSemanticHint ResolveHint(string methodName)
    {
        if (DefaultSemanticHints.TryGetValue(methodName, out var hint))
        {
            return hint;
        }

        _logger.LogDebug("[MCP] 工具缺少预设语义提示，按默认策略降级：{Method}", methodName);
        return new ToolSemanticHint($"{methodName}（自动生成）", "generic", ReadOnly: true, Destructive: false, Humanized: false, SortOrder: 100);
    }

    private IEnumerable<ToolBinding> EnumerateActiveBindings()
    {
        var sequence = _toolBindings.Values
            .Where(IsToolEnabled)
            .OrderBy(b => b.Hint.SortOrder)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase);
        return sequence;
    }

    private bool IsToolEnabled(ToolBinding binding)
    {
        if (_disabledTokens.Contains(binding.Name) || _disabledTokens.Contains(binding.MethodName))
        {
            return false;
        }

        if (_enabledTokens.Count == 0)
        {
            return true;
        }

        return _enabledTokens.Contains(binding.Name) || _enabledTokens.Contains(binding.MethodName);
    }

    private static HashSet<string> BuildTokenSet(IReadOnlyCollection<string>? tokens)
    {
        if (tokens is null || tokens.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(tokens.Where(t => !string.IsNullOrWhiteSpace(t)), StringComparer.OrdinalIgnoreCase);
    }

    private Tool CreateManifest(ToolBinding binding)
    {
        var protocolTool = binding.Tool.ProtocolTool;
        var title = ResolveTitle(binding);
        var description = ResolveDescription(binding, protocolTool.Description);

        return new Tool
        {
            Name = protocolTool.Name,
            Title = title,
            Description = description,
            InputSchema = protocolTool.InputSchema,
            OutputSchema = protocolTool.OutputSchema,
            Annotations = BuildAnnotations(binding, title)
        };
    }

    private string ResolveTitle(ToolBinding binding)
    {
        if (_titleOverrides.TryGetValue(binding.Name, out var title))
        {
            return title;
        }

        if (_titleOverrides.TryGetValue(binding.MethodName, out title))
        {
            return title;
        }

        return binding.Hint.Title;
    }

    private string? ResolveDescription(ToolBinding binding, string? baseDescription)
    {
        if (_descriptionOverrides.TryGetValue(binding.Name, out var desc))
        {
            return desc;
        }

        if (_descriptionOverrides.TryGetValue(binding.MethodName, out desc))
        {
            return desc;
        }

        var categoryInfo = $"分类: {binding.Hint.Category}; 拟人化: {(binding.Hint.Humanized ? "是" : "否")}";
        if (string.IsNullOrWhiteSpace(baseDescription))
        {
            return categoryInfo;
        }

        return $"{baseDescription}\n{categoryInfo}";
    }

    private static ToolAnnotations BuildAnnotations(ToolBinding binding, string title)
    {
        return new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = binding.Hint.ReadOnly,
            DestructiveHint = binding.Hint.Destructive,
            IdempotentHint = binding.Hint.ReadOnly,
            OpenWorldHint = binding.Hint.Category is "browser" or "humanized_navigation"
        };
    }

    private static CallToolResult CreateErrorResult(string code, string message)
    {
        return new CallToolResult
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = $"[{code}] {message}"
                }
            ]
        };
    }

    private sealed record ToolBinding(string Name, string MethodName, McpServerTool Tool, ToolSemanticHint Hint);

    private sealed record ToolSemanticHint(string Title, string Category, bool ReadOnly, bool Destructive, bool Humanized, int SortOrder);
}
