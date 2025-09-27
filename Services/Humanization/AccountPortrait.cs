using System.Collections.Generic;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

public sealed record AccountPortrait(
    string Id,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Metadata);
