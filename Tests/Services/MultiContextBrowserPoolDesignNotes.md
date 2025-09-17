该文件说明 MultiContextBrowserPool 的测试策略：

1) 由于 Playwright 的持久化上下文需要真实浏览器进程，本项目的单元测试不直接实例化 MultiContextBrowserPool；
2) 通过 BrowserContextPoolTests 验证池化租约协议与页面复用行为；
3) 多上下文行为在真实环境集成测试（[Explicit][Category("RealEnv")]）中覆盖，确保 UserDataDir 隔离、池容量与健康清理。

