using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using HushOps.Servers.XiaoHongShu.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class HumanizedActionToolTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnPlanMetadataAndActions()
    {
        var plan = HumanizedActionPlan.Create(
            HumanizedActionKind.KeywordBrowse,
            new HumanizedActionRequest("闇茶惀", null, null, true, "user", "req-1", "default"),
            "闇茶惀",
            new BrowserOpenResult(BrowserProfileKind.User, "user", "/tmp/user", false, false, null, true, true, null),
            new HumanBehaviorProfileOptions(),
            new HumanizedActionScript(new List<HumanizedAction>
            {
                HumanizedAction.Create(HumanizedActionType.InputText, ActionLocator.Empty, HumanizedActionTiming.Default, HumanizedActionParameters.Empty, "default", "input")
            }),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["script.actionCount"] = "1",
                ["script.actions"] = "InputText",
                ["script.actions.0"] = "InputText",
                ["humanized.plan.count"] = "1",
                ["humanized.plan.actions"] = "InputText",
                ["humanized.plan.actions.0"] = "InputText"
            });

        var service = new StubHumanizedActionService(plan);
        var tool = new HumanizedActionTool(service, NullLogger<HumanizedActionTool>.Instance);

        var result = await tool.KeywordBrowseAsync(new HumanizedActionToolRequest("闇茶惀", null, null, true, "user", "req-1", "default"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("闇茶惀", result.Data!.ResolvedKeyword);
        Assert.Contains("InputText", result.Data.Actions);
        Assert.Equal("default", result.Data.BehaviorProfile);
        Assert.Equal("1", result.Metadata!["script.actionCount"]);
        Assert.Equal(1, result.Data.Planned.Count);
        Assert.Equal(1, result.Data.Executed.Count);
        Assert.Equal("InputText", result.Data.Planned.Actions[0]);
        Assert.Single(result.Data.Warnings);
        Assert.Equal("mock warning", result.Data.Warnings[0]);
        Assert.True(service.PrepareCalled);
        Assert.True(service.ExecutePlanCalled);
    }

    private sealed class StubHumanizedActionService : IHumanizedActionService
    {
        private readonly HumanizedActionPlan _plan;

        public StubHumanizedActionService(HumanizedActionPlan plan)
        {
            _plan = plan;
        }

        public bool PrepareCalled { get; private set; }
        public bool ExecutePlanCalled { get; private set; }

        public Task<HumanizedActionPlan> PrepareAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
        {
            PrepareCalled = true;
            return Task.FromResult(_plan);
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionPlan plan, CancellationToken cancellationToken)
        {
            ExecutePlanCalled = true;
            var metadata = new Dictionary<string, string>(plan.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["execution.status"] = "ok",
                ["execution.actionCount"] = plan.Script.Actions.Count.ToString(),
                ["execution.actions"] = string.Join(",", plan.Script.Actions.Select(a => a.Type.ToString())),
                ["humanized.execute.status"] = "success",
                ["humanized.execute.count"] = plan.Script.Actions.Count.ToString(),
                ["humanized.execute.actions"] = string.Join(",", plan.Script.Actions.Select(a => a.Type.ToString()))
            };

            for (var i = 0; i < plan.Script.Actions.Count; i++)
            {
                metadata[$"execution.actions.{i}"] = plan.Script.Actions[i].Type.ToString();
                metadata[$"humanized.execute.actions.{i}"] = plan.Script.Actions[i].Type.ToString();
            }

            metadata["consistency.warning.0"] = "mock warning";

            return Task.FromResult(HumanizedActionOutcome.Ok(metadata));
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
