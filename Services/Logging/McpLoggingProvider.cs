using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal sealed class McpLoggingProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ChannelWriter<McpLogEntry> _writer;
    private readonly IMcpLoggingState _state;
    private readonly IMcpLogSanitizer _sanitizer;
    private readonly McpLoggingOptions _options;
    private IExternalScopeProvider? _scopeProvider;

    public McpLoggingProvider(
        ChannelWriter<McpLogEntry> writer,
        IMcpLoggingState state,
        IMcpLogSanitizer sanitizer,
        IOptions<McpLoggingOptions> options)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new ArgumentException("类别不能为空", nameof(categoryName));
        }

        return new McpLogger(this, categoryName);
    }

    public void Dispose()
    {
        _writer.TryComplete();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private sealed class McpLogger : ILogger
    {
        private readonly McpLoggingProvider _provider;
        private readonly string _categoryName;

        public McpLogger(McpLoggingProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _provider._scopeProvider?.Push(state) ?? NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _provider._state.ShouldEmit(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter is null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            if (!_provider._state.ShouldEmit(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            message = _provider._sanitizer.Sanitize(message);

            IReadOnlyList<KeyValuePair<string, string?>>? scopeValues = null;
            if (_provider._options.IncludeScopes && _provider._scopeProvider is not null)
            {
                var collector = new List<KeyValuePair<string, string?>>();
                _provider._scopeProvider.ForEachScope(static (scope, state) =>
                {
                    if (scope is IReadOnlyList<KeyValuePair<string, object?>> keyValues)
                    {
                        foreach (var pair in keyValues)
                        {
                            state.Add(new KeyValuePair<string, string?>(pair.Key, pair.Value?.ToString()));
                        }
                        return;
                    }

                    if (scope is IEnumerable<KeyValuePair<string, object?>> enumerable)
                    {
                        foreach (var pair in enumerable)
                        {
                            state.Add(new KeyValuePair<string, string?>(pair.Key, pair.Value?.ToString()));
                        }
                        return;
                    }

                    if (scope is KeyValuePair<string, object?> kv)
                    {
                        state.Add(new KeyValuePair<string, string?>(kv.Key, kv.Value?.ToString()));
                        return;
                    }

                    if (scope is not null)
                    {
                        state.Add(new KeyValuePair<string, string?>("scope", scope.ToString()));
                    }
                }, collector);

                if (collector.Count > 0)
                {
                    scopeValues = SanitizePairs(_provider, collector);
                }
            }

            IReadOnlyList<KeyValuePair<string, string?>>? stateValues = null;
            if (state is IReadOnlyList<KeyValuePair<string, object?>> structured)
            {
                var buffer = new List<KeyValuePair<string, string?>>(structured.Count);
                foreach (var pair in structured)
                {
                    buffer.Add(new KeyValuePair<string, string?>(pair.Key, pair.Value?.ToString()));
                }

                stateValues = SanitizePairs(_provider, buffer);
            }

            var entry = new McpLogEntry(
                logLevel,
                _categoryName,
                eventId,
                message,
                _provider._options.IncludeExceptionDetails ? exception : null,
                DateTimeOffset.UtcNow,
                scopeValues,
                stateValues);

            if (!_provider._writer.TryWrite(entry))
            {
                _ = _provider._writer.WriteAsync(entry).AsTask();
            }
        }

        private static IReadOnlyList<KeyValuePair<string, string?>>? SanitizePairs(
            McpLoggingProvider provider,
            List<KeyValuePair<string, string?>> pairs)
        {
            if (pairs.Count == 0)
            {
                return null;
            }

            var sanitized = new List<KeyValuePair<string, string?>>(pairs.Count);
            foreach (var pair in pairs)
            {
                var value = pair.Value is null ? null : provider._sanitizer.Sanitize(pair.Value);
                sanitized.Add(new KeyValuePair<string, string?>(pair.Key, value));
            }

            return sanitized;
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
