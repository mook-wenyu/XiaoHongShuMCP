using System;
using System.IO;
using System.Security.Cryptography;
using HushOps.Core.Persistence;
using HushOps.Core.Vision;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Tests.Core.Vision;

[TestFixture]
public sealed class VisionModelRegistryTests
{
    private string rootDir = null!;
    private string templatePath = null!;

    [SetUp]
    public void SetUp()
    {
        rootDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "vision-registry", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(Path.Combine(rootDir, "templates"));
        Directory.CreateDirectory(Path.Combine(rootDir, "profiles"));

        var repoRoot = LocateRepositoryRoot();
        var baselineTemplate = Path.Combine(repoRoot, "profiles", "vision", "templates", "search_button_default.png");
        templatePath = Path.Combine(rootDir, "templates", "search_button_default.png");
        File.Copy(baselineTemplate, templatePath, overwrite: true);

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(templatePath))).ToLowerInvariant();

        File.WriteAllText(Path.Combine(rootDir, "profiles", "search_button.json"),
            "{\n  \"locatorId\": \"search_button\",\n  \"description\": \"测试模板\",\n  \"version\": 1,\n  \"strategy\": {\n    \"type\": \"Template\",\n    \"templateId\": \"search_button_default\",\n    \"matchThreshold\": 0.9\n  },\n  \"roi\": { \"x\": 0, \"y\": 0, \"width\": 0, \"height\": 0 },\n  \"postProcessing\": { \"maxCandidates\": 3, \"scoreSuppression\": 0.1 },\n  \"fallback\": null,\n  \"metadata\": { \"createdAt\": \"2025-09-17T00:00:00Z\", \"updatedAt\": \"2025-09-17T00:00:00Z\", \"source\": \"test\" }\n}\n");

        File.WriteAllText(Path.Combine(rootDir, "index.json"),
            $"{{\n  \"templates\": [{{\n    \"id\": \"search_button_default\",\n    \"file\": \"templates/search_button_default.png\",\n    \"hashSha256\": \"{hash}\",\n    \"width\": 1,\n    \"height\": 1,\n    \"format\": \"png\",\n    \"source\": \"unit-test\"\n  }}],\n  \"models\": [],\n  \"profiles\": [{{\n    \"id\": \"search_button\",\n    \"file\": \"profiles/search_button.json\",\n    \"strategy\": \"Template\"\n  }}]\n}}\n");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(rootDir))
        {
            Directory.Delete(rootDir, true);
        }
    }

    [Test]
    public void Reload_LoadsTemplatesProfilesAndHashes()
    {
        using var registry = new VisionModelRegistry(rootDir, NullLogger<VisionModelRegistry>.Instance);

        Assert.That(registry.TryGetProfile("search_button", out var profile), Is.True);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile!.LocatorId, Is.EqualTo("search_button"));

        var template = registry.GetTemplate("search_button_default");
        Assert.That(File.Exists(template.FilePath), Is.True);
        Assert.That(template.HashSha256.Length, Is.EqualTo(64));

        var all = registry.ListProfiles();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public void Reload_WithTamperedTemplate_Throws()
    {
        using var registry = new VisionModelRegistry(rootDir, NullLogger<VisionModelRegistry>.Instance);

        // 篡改模板文件，触发哈希校验失败
        using (var stream = new FileStream(templatePath, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            stream.WriteByte(0x01);
        }

        Assert.Throws<InvalidDataException>(() => registry.Reload());
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HushOps.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("未能定位仓库根目录 (缺少 HushOps.sln)");
        }

        return directory.FullName;
    }
}
