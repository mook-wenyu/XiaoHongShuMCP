namespace XiaoHongShuMCP.Services;

/// <summary>
/// 智能文本分割器：按“语义单位”拆分文本以配合拟人化输入节奏。
/// - 规则概览：
///   1) 先按中英文标点切大块（标点尽量与前文绑定）；
///   2) 再在块内按中文(2–4字)、英文单词、数字等进行细分；
///   3) 保留合理空白并移除无意义的空串。
/// - 适用：拟人化输入策略在语义单位之间插入“思考/检查”停顿，产生更贴近真人的节奏感。
/// - 限制：该拆分仅做启发式处理，对特定专业文本（代码、公式、混合语言）可能不够理想。
/// </summary>
public static class SmartTextSplitter
{
    // 中文标点符号
    private static readonly HashSet<char> _chinesePunctuation =
    [
        '\u3001', '\u3002', '\uFF01', '\uFF1F', '\uFF1B', '\uFF1A', // 、。！？；：
        '\u2026',                                                   // …
        '\u201C', '\u201D', '\u2018', '\u2019',                     // ""''
        '\uFF0C', '\u3010', '\u3011'
    ];

    // 英文标点符号
    private static readonly HashSet<char> _englishPunctuation = [',', '.', '!', '?', ';', ':', '"', '\'', '-', '(', ')', '[', ']'];

    /// <summary>
    /// 按语义单位分割文本
    /// </summary>
    public static List<string> SplitBySemanticUnits(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // 1. 首先按标点符号分割大的语义块
        var largeChunks = SplitByPunctuation(text);
        var semanticUnits = new List<string>();

        // 2. 对每个大语义块进行智能细分
        foreach (var chunk in largeChunks)
        {
            var subUnits = SplitChunkIntelligently(chunk.Trim());
            semanticUnits.AddRange(subUnits);
        }

        return semanticUnits.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    /// <summary>
    /// 按标点符号分割文本，保持标点与前面内容组合
    /// </summary>
    private static List<string> SplitByPunctuation(string text)
    {
        var chunks = new List<string>();
        var currentChunk = "";

        for (int i = 0; i < text.Length; i++)
        {
            var currentChar = text[i];
            currentChunk += currentChar;

            // 如果遇到标点符号，结束当前chunk
            if (_chinesePunctuation.Contains(currentChar) || _englishPunctuation.Contains(currentChar))
            {
                // 检查后面是否还有相关的标点或空格
                if (i + 1 < text.Length && (char.IsWhiteSpace(text[i + 1]) ||
                                            _chinesePunctuation.Contains(text[i + 1]) || _englishPunctuation.Contains(text[i + 1])))
                {
                    continue; // 继续添加到当前chunk
                }

                chunks.Add(currentChunk);
                currentChunk = "";
            }
        }

        // 添加剩余内容
        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(currentChunk);
        }

        return chunks;
    }

    /// <summary>
    /// 智能分割语义块
    /// </summary>
    private static List<string> SplitChunkIntelligently(string chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk))
            return [];

        var units = new List<string>();
        var currentUnit = "";
        var i = 0;

        while (i < chunk.Length)
        {
            var currentChar = chunk[i];

            // 跳过空格
            if (char.IsWhiteSpace(currentChar))
            {
                if (!string.IsNullOrWhiteSpace(currentUnit))
                {
                    units.Add(currentUnit.Trim());
                    currentUnit = "";
                }
                i++;
                continue;
            }

            // 处理英文单词
            if (char.IsLetter(currentChar) && currentChar <= 127) // ASCII字符
            {
                var word = ExtractEnglishWord(chunk, ref i);
                if (!string.IsNullOrWhiteSpace(currentUnit))
                {
                    units.Add(currentUnit.Trim());
                    currentUnit = "";
                }
                units.Add(word);
                continue;
            }

            // 处理数字
            if (char.IsDigit(currentChar))
            {
                var number = ExtractNumber(chunk, ref i);
                currentUnit += number;
                continue;
            }

            // 处理中文字符
            if (IsChinese(currentChar))
            {
                currentUnit += currentChar;

                // 中文按2-4个字符分组
                if (currentUnit.Length >= GetChineseUnitLength())
                {
                    units.Add(currentUnit);
                    currentUnit = "";
                }
                i++;
                continue;
            }

            // 其他字符直接添加
            currentUnit += currentChar;
            i++;
        }

        // 添加剩余内容
        if (!string.IsNullOrWhiteSpace(currentUnit))
        {
            units.Add(currentUnit.Trim());
        }

        return units;
    }

    /// <summary>
    /// 提取英文单词
    /// </summary>
    private static string ExtractEnglishWord(string text, ref int index)
    {
        var word = "";
        while (index < text.Length && (char.IsLetter(text[index]) || text[index] == '\'' || text[index] == '-'))
        {
            word += text[index];
            index++;
        }
        return word;
    }

    /// <summary>
    /// 提取数字（包括小数点）
    /// </summary>
    private static string ExtractNumber(string text, ref int index)
    {
        var number = "";
        while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.' || text[index] == ','))
        {
            number += text[index];
            index++;
        }
        return number;
    }

    /// <summary>
    /// 判断是否为中文字符
    /// </summary>
    private static bool IsChinese(char character)
    {
        return character >= 0x4E00 && character <= 0x9FFF;
    }

    /// <summary>
    /// 获取中文语义单位的长度（2-4个字符）
    /// </summary>
    private static int GetChineseUnitLength()
    {
        return Random.Shared.Next(2, 5); // 2-4个字符
    }

}
