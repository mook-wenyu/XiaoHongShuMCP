using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using HushOps.Core.Observability;
using HushOps.Observability; // AddObservability 扩展方法所在命名空间

namespace Tests.Observability
{
    /// <summary>
    /// 中文单测：验证 AddObservability 装配注册行为与禁用回退路径。
    /// - 默认启用：应注册为 InProcessMetrics（移除外部 Otel 依赖后仍保持可观测数据聚合）。
    /// - 显式禁用：应注册为 NoopMetrics（避免业务层判空）。
    /// </summary>
    [TestFixture]
    public class ServiceCollectionExtensionsTests
    {
        [Test]
        public void AddObservability_DefaultEnabled_Should_Register_InProcessMetrics()
        {
            // 配置：默认 Enabled 未显式禁用；Exporter=console
            var inMemory = new[]
            {
                new KeyValuePair<string,string?>("XHS:Metrics:Exporter", "console"),
            };
            IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

            var services = new ServiceCollection();
            services.AddObservability(cfg);
            var sp = services.BuildServiceProvider();

            var metrics = sp.GetService<IMetrics>();
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics, Is.TypeOf<InProcessMetrics>(), "默认应注册为 InProcessMetrics 实现，确保本地聚合可用。");
        }

        [Test]
        public void AddObservability_Disabled_Should_Register_NoopMetrics()
        {
            // 配置：显式禁用 Metrics
            var inMemory = new[]
            {
                new KeyValuePair<string,string?>("XHS:Metrics:Enabled", "false"),
            };
            IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

            var services = new ServiceCollection();
            services.AddObservability(cfg);
            var sp = services.BuildServiceProvider();

            var metrics = sp.GetService<IMetrics>();
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics, Is.InstanceOf<NoopMetrics>(), "禁用时应注册 NoopMetrics（避免业务层判空）。");
        }
    }
}
