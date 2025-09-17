using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HushOps.Core.Core.Selectors;
using HushOps.Core.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace XiaoHongShuMCP.Tests.Core.Selectors;

/// <summary>
/// 验证选择器注册表的发布、回滚与计划构建能力。
/// </summary>
public sealed class DefaultSelectorRegistryTests : IDisposable
{
    private readonly string tempRoot;
    private readonly JsonLocalStore store;
    private readonly DefaultSelectorRegistry registry;

    public DefaultSelectorRegistryTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "xhs_selector_registry_" + Guid.NewGuid().ToString("N"));
        store = new JsonLocalStore(new JsonLocalStoreOptions(tempRoot, null, false));
        registry = new DefaultSelectorRegistry(
            store,
            Options.Create(new SelectorRegistryOptions()),
            NullLogger<DefaultSelectorRegistry>.Instance);
    }

    [Test]
    public async Task Publish_Should_Write_Snapshot_And_Plan()
    {
        var revision = new SelectorRevision
        {
            Alias = "NoteTitle",
            Workflow = "Discovery",
            Version = "20250917.1",
            PublishedAtUtc = DateTimeOffset.Parse("2025-09-17T12:00:00Z"),
            Author = "auto",
            Source = "telemetry",
            Before = new[] { "#title", ".note-title" },
            After = new[] { ".note-title", "#title" },
            Demoted = new[] { "#title" },
            Tags = new[] { "primary" },
            Notes = "自动降级"
        };

        var item = await registry.PublishAsync(revision);
        Assert.That(item.Version, Is.EqualTo("20250917.1"));
        Assert.That(item.After, Is.EqualTo(new[] { ".note-title", "#title" }));

        var plan = await registry.BuildPlanAsync("Discovery");
        Assert.That(plan.Items.Count(i => i.Alias == "NoteTitle"), Is.EqualTo(1));
        Assert.That(plan.Items.Single(i => i.Alias == "NoteTitle").After, Is.EqualTo(new[] { ".note-title", "#title" }));

    }

    [Test]
    public async Task Rollback_Should_Restore_History_Version()
    {
        var baseRevision = new SelectorRevision
        {
            Alias = "ActionButton",
            Workflow = "Comment",
            Version = "20250917.1",
            PublishedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            Author = "ops",
            Source = "manual",
            Before = new[] { "button.action" },
            After = new[] { "button.action", "button.primary" },
            Demoted = Array.Empty<string>()
        };
        await registry.PublishAsync(baseRevision);

        var hotfixRevision = new SelectorRevision
        {
            Alias = "ActionButton",
            Workflow = "Comment",
            Version = "20250917.2",
            PublishedAtUtc = DateTimeOffset.UtcNow,
            Author = "ops",
            Source = "manual",
            Before = new[] { "button.action", "button.primary" },
            After = new[] { "button.primary" },
            Demoted = new[] { "button.action" }
        };
        await registry.PublishAsync(hotfixRevision);

        var rollbackItem = await registry.RollbackAsync("ActionButton", "20250917.1");
        Assert.That(rollbackItem.Version, Is.EqualTo("20250917.1"));
        Assert.That(rollbackItem.After, Does.Contain("button.primary"));
        Assert.That(rollbackItem.Demoted, Is.Empty);

        var plan = await registry.BuildPlanAsync("Comment");
        Assert.That(plan.Items.Single(i => i.Alias == "ActionButton").After, Does.Contain("button.primary"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
            // 测试环境忽略清理异常。
        }
    }
}
