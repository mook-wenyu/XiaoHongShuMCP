using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal sealed record McpLogEntry(
    LogLevel Level,
    string Category,
    EventId EventId,
    string Message,
    Exception? Exception,
    DateTimeOffset Timestamp,
    IReadOnlyList<KeyValuePair<string, string?>>? ScopeValues,
    IReadOnlyList<KeyValuePair<string, string?>>? StateValues);
