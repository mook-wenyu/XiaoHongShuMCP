using System;
using System.Text.Json;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using XiaoHongShuMCP.Internal;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Tooling;
using XiaoHongShuMCP.Tools;
using HushOps.Core.Automation.Abstractions;

namespace XiaoHongShuMCP.Tests.Tools;

/// <summary>
/// ToolBroker 行为验证：确保动态工具清单与调用逻辑符合预期。
/// </summary>
[TestFixture]
public class ToolBrokerTests
{
    private ServiceProvider _serviceProvider = null!;
    private Mock<IAccountManager> _accountManager = null!;
    private Mock<IXiaoHongShuService> _xhsService = null!;
    private Mock<IBrowserManager> _browserManager = null!;
    private Mock<IHumanizedInteractionService> _humanized = null!;
    private Mock<IAutoPage> _autoPage = null!;

    [SetUp]
    public void SetUp()
    {
        _accountManager = new Mock<IAccountManager>();
        _accountManager.Setup(x => x.ConnectToBrowserAsync())
            .ReturnsAsync(OperationResult<bool>.Ok(true));

        _xhsService = new Mock<IXiaoHongShuService>();
        _browserManager = new Mock<IBrowserManager>();
        _autoPage = new Mock<IAutoPage>();
        _browserManager.Setup(x => x.GetAutoPageAsync())
            .ReturnsAsync(_autoPage.Object);

        _humanized = new Mock<IHumanizedInteractionService>();
        _humanized.Setup(x => x.HumanScrollAsync(It.IsAny<IAutoPage>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(_accountManager.Object);
        services.AddSingleton(_xhsService.Object);
        services.AddSingleton(_browserManager.Object);
        services.AddSingleton(_humanized.Object);
        services.AddSingleton<IMcpElicitationClient>(Mock.Of<IMcpElicitationClient>());
        services.AddSingleton<XiaoHongShuTools>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task ListToolsAsync_ShouldRespectEnabledFilter()
    {
        var settings = new XhsSettings();
        settings.McpSettings.EnabledToolNames.Add(nameof(XiaoHongShuMCP.Tools.XiaoHongShuTools.LikeNote));

        var broker = new ToolBroker(_serviceProvider, Options.Create(settings), NullLogger<ToolBroker>.Instance);
        var context = CreateListContext();

        var result = await broker.ListToolsAsync(context, CancellationToken.None);

        Assert.That(result.Tools, Is.Not.Null);
        Assert.That(result.Tools.Count, Is.EqualTo(1), "仅应暴露白名单内的工具。");
        var onlyTool = result.Tools[0];
        Assert.That(onlyTool.Title, Does.Contain("点赞"));
    }

    [Test]
    public async Task InvokeAsync_WhenToolDisabled_ShouldReturnError()
    {
        var settings = new XhsSettings();
        settings.McpSettings.DisabledToolNames.Add(nameof(XiaoHongShuMCP.Tools.XiaoHongShuTools.InteractNote));

        var broker = new ToolBroker(_serviceProvider, Options.Create(settings), NullLogger<ToolBroker>.Instance);
        const string disabledToolName = "interact_note";

        var callContext = CreateCallContext(disabledToolName);
        var result = await broker.InvokeAsync(callContext, CancellationToken.None);

        Assert.That(result.IsError, Is.True, "禁用工具调用应返回错误结果。");
        Assert.That(result.Content, Is.Not.Empty);
        Assert.That(result.Content[0], Is.InstanceOf<TextContentBlock>());
        var textBlock = (TextContentBlock)result.Content[0];
        Assert.That(textBlock.Text, Does.Contain("禁用"));
    }

    [Test]
    public async Task InvokeAsync_ShouldExecuteUnderlyingTool()
    {
        var settings = new XhsSettings();
        var broker = new ToolBroker(_serviceProvider, Options.Create(settings), NullLogger<ToolBroker>.Instance);

        var list = await broker.ListToolsAsync(CreateListContext(), CancellationToken.None);
        Assert.That(list.Tools, Is.Not.Empty);
        var tools = list.Tools ?? throw new InvalidOperationException("工具集合不应为 null");
        Tool? browserTool = null;
        foreach (var tool in tools)
        {
            if (tool.Title != null && tool.Title.Contains("浏览器", StringComparison.Ordinal))
            {
                browserTool = tool;
                break;
            }
        }
        if (browserTool is null) throw new InvalidOperationException("未在工具列表中找到浏览器工具");

        var callContext = CreateCallContext(browserTool.Name);
        var result = await broker.InvokeAsync(callContext, CancellationToken.None);

        Assert.That(result.IsError, Is.Not.True, "成功调用不应标记为错误。");
        Assert.That(result.Content, Is.Not.Null);
        Assert.That(result.Content, Is.Not.Empty);
        var payload = result.Content.OfType<TextContentBlock>().First();
        using var doc = JsonDocument.Parse(payload.Text);
        Assert.That(doc.RootElement.GetProperty("isConnected").GetBoolean(), Is.True);
    }

    private RequestContext<ListToolsRequestParams> CreateListContext()
    {
#pragma warning disable SYSLIB0050
        var context = (RequestContext<ListToolsRequestParams>)FormatterServices.GetUninitializedObject(typeof(RequestContext<ListToolsRequestParams>));
#pragma warning restore SYSLIB0050
        context.Services = _serviceProvider;
        context.Params = new ListToolsRequestParams();
        return context;
    }

    private RequestContext<CallToolRequestParams> CreateCallContext(string toolName)
    {
#pragma warning disable SYSLIB0050
        var context = (RequestContext<CallToolRequestParams>)FormatterServices.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
#pragma warning restore SYSLIB0050
        context.Services = _serviceProvider;
        context.Params = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = null
        };
        return context;
    }
}
