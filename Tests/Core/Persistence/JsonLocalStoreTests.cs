using System;
using System.IO;
using HushOps.Core.Persistence;

namespace Tests.Core.Persistence;

/// <summary>
/// 覆盖 JsonLocalStore 的基础行为（写入、读取、列举与删除）。
/// </summary>
[TestFixture]
public class JsonLocalStoreTests
{
    [Test]
    public async Task SaveLoadDelete_ShouldPersistRoundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "json_store_" + Guid.NewGuid().ToString("N"));
        var store = new JsonLocalStore(new JsonLocalStoreOptions(root));

        var payload = new DummyPayload { Message = "测试", Count = 42 };
        var entry = await store.SaveAsync("cases/payload.json", payload);

        Assert.That(entry.FullPath, Does.Contain("cases"));
        Assert.That(entry.ContentLengthBytes, Is.GreaterThan(0));
        Assert.That(entry.ChecksumSha256, Has.Length.GreaterThan(0));

        var loaded = await store.LoadAsync<DummyPayload>("cases/payload.json");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Message, Is.EqualTo("测试"));
        Assert.That(loaded.Count, Is.EqualTo(42));

        var exists = await store.ExistsAsync("cases/payload.json");
        Assert.That(exists, Is.True);

        var list = await store.ListAsync("cases");
        Assert.That(list.Count, Is.EqualTo(1));

        var deleted = await store.DeleteAsync("cases/payload.json");
        Assert.That(deleted, Is.True);
        Assert.That(await store.ExistsAsync("cases/payload.json"), Is.False);
    }

    private sealed class DummyPayload
    {
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
