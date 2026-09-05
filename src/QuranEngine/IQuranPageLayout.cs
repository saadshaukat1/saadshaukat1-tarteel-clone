namespace TarteelClone.QuranEngine;

public sealed record VerseSpan(int SurahNum, int AyahNum, string Word);

public sealed record PageLine(string Text, string VerseKey, int LineIndex);

public interface IQuranPageLayout
{
    IReadOnlyList<PageLine> LayoutPage(
        IReadOnlyList<(int SurahNum, int AyahNum, string ArabicText)> verses,
        int page = 0);
}

public sealed class Mushaf16LinerLayout : IQuranPageLayout
{
    private const int RowsPerPage = 16;
    private readonly ILineLevelPageSource? _lineSource;

    public Mushaf16LinerLayout()
    {
    }

    public Mushaf16LinerLayout(ILineLevelPageSource lineSource)
    {
        _lineSource = lineSource;
    }

    public IReadOnlyList<PageLine> LayoutPage(
        IReadOnlyList<(int SurahNum, int AyahNum, string ArabicText)> verses,
        int page = 0)
    {
        // Verified line-level data wins when it exists.
        var sourceLines = page > 0 ? _lineSource?.TryGetLines(page) : null;
        if (sourceLines is { Count: > 0 })
        {
            return LayoutFromVerifiedLines(sourceLines, verses);
        }

        if (verses.Count == 0)
        {
            return FillEmptyLines(0);
        }

        // A surah starts on this page when the first verse is ayah 1 — except
        // Surah Al-Fatiha, whose ayah 1 IS the Bismillah text itself. In that
        // case the verse is rendered as content and no separate Bismillah line
        // is added (otherwise it would appear twice).
        var isSurahStart = verses[0].AyahNum == 1 && verses[0].SurahNum != 1;
        var bismillahLines = isSurahStart ? 1 : 0;
        var availableLines = RowsPerPage - bismillahLines;

        var allWords = new List<(string Word, string VerseKey, bool IsEndMarker, bool IsVerseSeparator)>();
        for (var vi = 0; vi < verses.Count; vi++)
        {
            var (surah, ayah, text) = verses[vi];
            var key = $"{surah}:{ayah}";
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var wi = 0; wi < words.Length; wi++)
            {
                var isEnd = wi == words.Length - 1;
                allWords.Add((words[wi], key, isEnd, false));
            }

            if (vi < verses.Count - 1)
            {
                allWords.Add(("\u06DE", key, false, true));
            }
        }

        if (allWords.Count == 0)
        {
            return FillEmptyLines(bismillahLines);
        }

        if (allWords.Count <= availableLines)
        {
            return LayoutSparsePage(allWords, isSurahStart, verses[0].SurahNum, availableLines);
        }

        return LayoutDensePage(allWords, isSurahStart, verses[0].SurahNum, availableLines);
    }

    /// <summary>
    /// Renders verified line strings verbatim, tagging each line with the verse
    /// key of its first word so highlight-by-verse keeps working.
    /// </summary>
    private static List<PageLine> LayoutFromVerifiedLines(
        IReadOnlyList<string> sourceLines,
        IReadOnlyList<(int SurahNum, int AyahNum, string ArabicText)> verses)
    {
        // Build a flat word → key map so a line's first word can be attributed.
        var wordKeys = new List<(string Word, string Key)>();
        foreach (var (surah, ayah, text) in verses)
        {
            var key = $"{surah}:{ayah}";
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                wordKeys.Add((word, key));
            }
        }

        var result = new List<PageLine>(RowsPerPage);
        for (var lineIdx = 0; lineIdx < RowsPerPage; lineIdx++)
        {
            if (lineIdx >= sourceLines.Count)
            {
                result.Add(new PageLine(" ", string.Empty, lineIdx));
                continue;
            }

            var lineText = sourceLines[lineIdx].Trim();
            var lineKey = string.Empty;
            if (lineText.Length > 0)
            {
                var firstWord = lineText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (firstWord is not null)
                {
                    lineKey = wordKeys.FirstOrDefault(item => item.Word == firstWord).Key ?? string.Empty;
                }
            }

            result.Add(new PageLine(lineText, lineKey, lineIdx));
        }

        return result;
    }

    private static List<PageLine> LayoutSparsePage(
        List<(string Word, string VerseKey, bool IsEndMarker, bool IsVerseSeparator)> allWords,
        bool isSurahStart,
        int surahNum,
        int availableLines)
    {
        var result = new List<PageLine>(RowsPerPage);
        var lineIdx = 0;

        if (isSurahStart)
        {
            result.Add(new PageLine("\u06E2\u0628\u0650\u0633\u0652\u0645\u0650 \u0627\u0644\u0644\u064e\u0651\u0647\u0650 \u0627\u0644\u0631\u064e\u0651\u062D\u0652\u0645\u064E\u0670\u0646\u0650 \u0627\u0644\u0631\u064e\u0651\u062D\u0650\u064A\u0645\u0650", $"{surahNum}:0", lineIdx));
            lineIdx++;
        }

        for (var i = 0; i < availableLines; i++)
        {
            if (i < allWords.Count)
            {
                var (word, key, isEnd, isSep) = allWords[i];
                // A separator must never open a line; attach it to the previous
                // line's trailing word instead.
                if (isSep && result.Count > 0)
                {
                    var previous = result[^1];
                    result[^1] = previous with { Text = previous.Text.TrimEnd() + " \u06DE" };
                    continue;
                }

                var text = word;
                if (isEnd)
                {
                    text = $"{word} {GetAyahEnding(key)}";
                }
                result.Add(new PageLine(text, key, lineIdx));
            }
            else
            {
                result.Add(new PageLine(" ", string.Empty, lineIdx));
            }
            lineIdx++;
        }

        return result;
    }

    private static List<PageLine> LayoutDensePage(
        List<(string Word, string VerseKey, bool IsEndMarker, bool IsVerseSeparator)> allWords,
        bool isSurahStart,
        int surahNum,
        int availableLines)
    {
        var totalContentWords = allWords.Count;
        var wordsPerLine = totalContentWords / availableLines;
        var remainder = totalContentWords % availableLines;

        var lines = new List<(string Text, string VerseKey)>();
        if (isSurahStart)
        {
            lines.Add(("\u06E2\u0628\u0650\u0633\u0652\u0645\u0650 \u0627\u0644\u0644\u064e\u0651\u0647\u0650 \u0627\u0644\u0631\u064e\u0651\u062D\u0652\u0645\u064E\u0670\u0646\u0650 \u0627\u0644\u0631\u064e\u0651\u062D\u0650\u064A\u0645\u0650", $"{surahNum}:0"));
        }

        var wordIndex = 0;
        var remainderAccum = 0;
        for (var li = 0; li < availableLines; li++)
        {
            var wordsOnThisLine = wordsPerLine;
            remainderAccum += remainder;
            if (remainderAccum >= availableLines)
            {
                wordsOnThisLine++;
                remainderAccum -= availableLines;
            }

            var lineText = new System.Text.StringBuilder();
            var lineKey = string.Empty;
            var firstTokenOnLine = true;

            for (var w = 0; w < wordsOnThisLine && wordIndex < allWords.Count; w++)
            {
                var (word, key, isEnd, isSep) = allWords[wordIndex];

                if (isSep)
                {
                    // Attach separators to the previous word on the line (or to
                    // the previous line when this line would otherwise open with
                    // one) so they never stand alone at a line start.
                    if (!firstTokenOnLine)
                    {
                        lineText.Append(" \u06DE");
                    }
                    else if (lines.Count > 0)
                    {
                        lines[^1] = (lines[^1].Text.TrimEnd() + " \u06DE", lines[^1].VerseKey);
                    }
                    wordIndex++;
                    continue;
                }

                if (!firstTokenOnLine)
                {
                    lineText.Append(' ');
                }
                lineText.Append(word);
                lineKey = key;
                firstTokenOnLine = false;

                if (isEnd)
                {
                    lineText.Append($" {GetAyahEnding(key)}");
                }

                wordIndex++;
            }

            lines.Add((lineText.ToString(), lineKey));
        }

        var result = new List<PageLine>(RowsPerPage);
        for (var i = 0; i < RowsPerPage; i++)
        {
            if (i < lines.Count)
            {
                result.Add(new PageLine(lines[i].Text, lines[i].VerseKey, i));
            }
            else
            {
                result.Add(new PageLine(" ", string.Empty, i));
            }
        }

        return result;
    }

    private static List<PageLine> FillEmptyLines(int startOffset)
    {
        var result = new List<PageLine>(RowsPerPage);
        for (var i = 0; i < RowsPerPage; i++)
        {
            result.Add(new PageLine(" ", string.Empty, i));
        }
        return result;
    }

    private static string GetAyahEnding(string verseKey)
    {
        // 15 Standard Sajdah Ayahs
        var sajdahAyahs = new HashSet<string>
        {
            "7:206", "13:15", "16:50", "17:109", "19:58", "22:18", "22:77",
            "25:60", "27:26", "32:15", "38:24", "41:38", "53:62", "84:21", "96:19"
        };

        if (sajdahAyahs.Contains(verseKey))
        {
            return "\u06E9"; // Sajdah marker
        }

        // Normal Ayah Ending with Arabic Numerals inside U+06DD (End of Ayah)
        var parts = verseKey.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int ayahNum))
        {
            return $"\u06DD{ToArabicNumerals(ayahNum)}";
        }

        return "\u06DD";
    }

    private static string ToArabicNumerals(int number)
    {
        return string.Concat(number.ToString().Select(c => (char)('\u0660' + (c - '0'))));
    }
}
