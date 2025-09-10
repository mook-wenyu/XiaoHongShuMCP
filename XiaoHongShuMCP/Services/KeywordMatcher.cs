using System.Globalization;
using System.Text;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 关键词匹配工具（增强版）。
/// 特性：
/// - 大小写/全半角/变音符号标准化
/// - 去除标点与空白（可配置）
/// - 子串匹配 + 近似匹配（Levenshtein 距离，按长度自适应阈值）
/// - 多词覆盖度判断（用于“iPhone 15 Pro”这类短语）
/// 设计目标：在不引入外部依赖的前提下，提升匹配鲁棒性且保持性能可控。
/// </summary>
public static class KeywordMatcher
{
    /// <summary>
    /// 匹配入口（破坏性变更）：基于“单一关键词”命中即返回 true。
    /// 用途：例如 文本=“我要去旅游了”，关键词=“旅游”→ 返回 true（子串命中，不要求全文等价）。
    /// 兼顾大小写/全半角/去标点/模糊/短语覆盖等鲁棒性处理。
    /// </summary>
    public static bool Matches(string text, string keyword, KeywordMatchOptions? options = null)
    {
        // 空保护
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            return false;

        options ??= KeywordMatchOptions.Default;

        // 预处理文本（两份：保留空格 vs 去空格）
        var normTextKeepSpace = Normalize(text, removeSpaces: false);
        var normText = options.IgnoreSpaces ? Normalize(text, removeSpaces: true) : normTextKeepSpace;

        // 预处理关键词（两份：保留空格 vs 去空格）
        var kwKeepSpace = Normalize(keyword, removeSpaces: false);
        var kw = options.IgnoreSpaces ? Normalize(keyword, removeSpaces: true) : kwKeepSpace;
        if (kw.Length == 0) return false;

        // 1) 直接子串命中（核心场景）
        if (normText.Contains(kw, StringComparison.Ordinal))
            return true;

        // 2) 短语覆盖度（以保留空格的标准化版本判断）
        if (LooksLikePhrase(kwKeepSpace) && PhraseCovered(normTextKeepSpace, kwKeepSpace, options.TokenCoverageThreshold))
            return true;

        // 3) 近似匹配（长度足够时采用，避免短关键字误报）
        if (options.UseFuzzy && AllowFuzzyForLength(kw.Length))
        {
            var maxDist = AllowedDistance(kw.Length, options.MaxDistanceCap);
            if (FuzzyContains(normText, kw, maxDist))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 文本标准化：
    /// - 转半角
    /// - ToLowerInvariant
    /// - Unicode 规范化（去除变音符号）
    /// - 仅保留字母/数字/中日韩文字；标点转为空格；可选移除空格
    /// </summary>
    internal static string Normalize(string input, bool removeSpaces)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);

        // 转半角
        var half = ToHalfWidth(input);

        // 小写 + 分解去除变音符
        var lowered = half.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        foreach (var ch in lowered)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue; // 去除变音符

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (!removeSpaces) sb.Append(' ');
            }
            else
            {
                // 标点一律按空格处理（保留空格版本用于短语覆盖度判断）
                if (!removeSpaces) sb.Append(' ');
            }
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC);
        if (!removeSpaces)
        {
            // 压缩多余空格
            result = CompressSpaces(result);
        }
        else
        {
            // 移除所有空白
            result = result.Replace(" ", string.Empty);
        }
        return result;
    }

    /// <summary>
    /// 将全角字符转换为半角（基本 ASCII 范围），保留其他字符。
    /// </summary>
    internal static string ToHalfWidth(string input)
    {
        var arr = input.ToCharArray();
        for (int i = 0; i < arr.Length; i++)
        {
            // 全角空格 U+3000 -> 空格
            if (arr[i] == '\u3000') { arr[i] = ' '; continue; }

            // 全角 ASCII：U+FF01 - U+FF5E 对应 0x21 - 0x7E
            if (arr[i] >= '\uFF01' && arr[i] <= '\uFF5E')
            {
                arr[i] = (char)(arr[i] - 0xFEE0);
            }
        }
        return new string(arr);
    }

    /// <summary>
    /// 压缩连续空格为单个空格，并修剪首尾。
    /// </summary>
    internal static string CompressSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool prevSpace = false;
        foreach (var ch in s)
        {
            if (ch == ' ')
            {
                if (!prevSpace) sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 判断是否为短语（含至少一个空格）。
    /// </summary>
    internal static bool LooksLikePhrase(string normalizedKeepSpace) => normalizedKeepSpace.Contains(' ');

    /// <summary>
    /// 短语覆盖度：将关键字拆分为 token（按空格），统计有多少 token 在文本中出现。
    /// </summary>
    internal static bool PhraseCovered(string normTextKeepSpace, string kwKeepSpace, double coverageThreshold)
    {
        var tokens = kwKeepSpace.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        int matched = 0;
        foreach (var t in tokens)
        {
            if (t.Length <= 1) continue; // 忽略过短 token
            if (normTextKeepSpace.Contains(t, StringComparison.Ordinal)) matched++;
        }

        var coverage = (double)matched / tokens.Length;
        return coverage >= coverageThreshold;
    }

    /// <summary>
    /// 是否允许对该长度进行模糊匹配。
    /// 避免对过短关键字（<=3）进行模糊，降低误报。
    /// </summary>
    internal static bool AllowFuzzyForLength(int len) => len >= 4;

    /// <summary>
    /// 依据长度给出允许的最大编辑距离（上限受 MaxDistanceCap 约束）。
    /// </summary>
    internal static int AllowedDistance(int len, int maxCap)
    {
        int dist = len switch
        {
            <= 3 => 0,
            <= 5 => 1,
            <= 9 => 2,
            <= 14 => 3,
            _ => 4
        };
        return Math.Min(dist, Math.Max(0, maxCap));
    }

    /// <summary>
    /// 在文本中以滑动窗口进行模糊查找（编辑距离 <= maxDistance）。
    /// 窗口长度围绕关键字长度 ±1（长词 ±2），在保证性能的同时提升容错。
    /// </summary>
    internal static bool FuzzyContains(string normText, string normKeyword, int maxDistance)
    {
        if (maxDistance <= 0) return false;
        if (normKeyword.Length == 0 || normText.Length == 0) return false;

        int k = normKeyword.Length;
        int delta = k >= 8 ? 2 : 1;
        int minLen = Math.Max(1, k - delta);
        int maxLen = Math.Min(normText.Length, k + delta);

        // 先快速剪枝：若文本与关键字字符集完全不相交，直接失败
        var setKw = new HashSet<char>(normKeyword);
        bool intersect = false;
        foreach (var ch in normText)
        {
            if (setKw.Contains(ch)) { intersect = true; break; }
        }
        if (!intersect) return false;

        // 滑动窗口
        for (int win = minLen; win <= maxLen; win++)
        {
            if (win > normText.Length) break;
            for (int i = 0; i + win <= normText.Length; i++)
            {
                var span = normText.AsSpan(i, win);
                int dist = LevenshteinDistance(span, normKeyword.AsSpan());
                if (dist <= maxDistance) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 经典 Levenshtein 距离（O(mn)），对短文本足够。
    /// 使用 Span 降低分配，滚动数组节省空间。
    /// </summary>
    internal static int LevenshteinDistance(ReadOnlySpan<char> s, ReadOnlySpan<char> t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        // 保证 m <= n，减少空间
        if (m > n) { var tmpS = s; s = t; t = tmpS; (n, m) = (m, n); }

        Span<int> prev = stackalloc int[m + 1];
        Span<int> curr = stackalloc int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;

        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                int del = prev[j] + 1;
                int ins = curr[j - 1] + 1;
                int sub = prev[j - 1] + cost;
                int val = Math.Min(Math.Min(del, ins), sub);
                curr[j] = val;
            }
            // 交换 prev 与 curr
            for (int j = 0; j <= m; j++) prev[j] = curr[j];
        }
        return prev[m];
    }
}

/// <summary>
/// 匹配可选项。
/// </summary>
public sealed class KeywordMatchOptions
{
    /// <summary>是否启用模糊匹配（默认 true）。</summary>
    public bool UseFuzzy { get; init; } = true;

    /// <summary>允许的最大编辑距离上限（默认 3）。</summary>
    public int MaxDistanceCap { get; init; } = 3;

    /// <summary>短语覆盖度阈值（默认 0.7，即覆盖≥70% token 即视为匹配）。</summary>
    public double TokenCoverageThreshold { get; init; } = 0.7;

    /// <summary>是否忽略空格（默认 true，将空格移除后参与子串与模糊匹配）。</summary>
    public bool IgnoreSpaces { get; init; } = true;

    public static KeywordMatchOptions Default { get; } = new();
}
