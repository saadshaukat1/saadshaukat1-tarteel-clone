using TarteelClone.QuranEngine;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class QuranLayoutTests
{
    private static readonly (int SurahNum, int AyahNum, string ArabicText)[] DensePageVerses =
    [
        (2, 1, "\u0627\u0644\u0645"),
        (2, 2, "\u0630\u0644\u0643 \u0627\u0644\u0643\u062A\u0628 \u0644\u0627 \u0631\u064A\u0628 \u0641\u064A\u0647 \u0647\u062F\u0649 \u0644\u0644\u0645\u062A\u0642\u064A\u0646"),
        (2, 3, "\u0627\u0644\u0630\u064A\u0646 \u064A\u0624\u0645\u0646\u0648\u0646 \u0628\u0627\u0644\u063A\u064A\u0628 \u0648\u064A\u0642\u064A\u0645\u0648\u0646 \u0627\u0644\u0635\u0644\u0627\u0629"),
        (2, 4, "\u0648\u0627\u0644\u0630\u064A\u0646 \u064A\u0624\u0645\u0646\u0648\u0646 \u0628\u0645\u0627 \u0623\u0646\u0632\u0644 \u0625\u0644\u064A\u0643 \u0648\u0645\u0627 \u0623\u0646\u0632\u0644 \u0645\u0646 \u0642\u0628\u0644\u0643"),
        (2, 5, "\u0623\u0648\u0644\u0626\u0643 \u0639\u0644\u0649 \u0647\u062F\u0649 \u0645\u0646 \u0631\u0628\u0647\u0645 \u0648\u0623\u0648\u0644\u0626\u0643 \u0647\u0645 \u0627\u0644\u0645\u0641\u0644\u062D\u0648\u0646")
    ];

    private static readonly (int SurahNum, int AyahNum, string ArabicText)[] FatihaVerses =
    [
        (1, 1, "\u0628\u0650\u0633\u0652\u0645\u0650 \u0627\u0644\u0644\u064e\u0651\u0647\u0650 \u0627\u0644\u0631\u064e\u0651\u062D\u0652\u0645\u064E\u0670\u0646\u0650 \u0627\u0644\u0631\u064e\u0651\u062D\u0650\u064A\u0645\u0650"),
        (1, 2, "\u0627\u0644\u0652\u062D\u064E\u0645\u0652\u062F\u064F \u0644\u0650\u0644\u064e\u0651\u0647\u0650 \u0631\u064E\u0628\u0650\u0651 \u0627\u0644\u0652\u0639\u064E\u0627\u0644\u064E\u0645\u0650\u064A\u0646\u064E"),
        (1, 3, "\u0627\u0644\u0631\u064E\u0651\u062D\u0652\u0645\u064E\u0670\u0646\u0650 \u0627\u0644\u0631\u064E\u0651\u062D\u0650\u064A\u0645\u0650"),
        (1, 4, "\u0645\u064E\u0644\u0650\u0643\u0650 \u064A\u064E\u0648\u0652\u0645\u0650 \u0627\u0644\u062F\u0650\u0651\u064A\u0646\u0650"),
        (1, 5, "\u0625\u0650\u064A\u064E\u0651\u0627\u0643\u064E \u0646\u064E\u0639\u0652\u0628\u064F\u062F\u064F \u0648\u064E\u0625\u0650\u064A\u064E\u0651\u0627\u0643\u064E \u0646\u064E\u0633\u0652\u062A\u064E\u0639\u0650\u064A\u0646\u064F"),
        (1, 6, "\u0627\u0647\u0652\u062F\u0650\u0646\u064E\u0627 \u0627\u0644\u0635\u0650\u0651\u0631\u064E\u0627\u0637\u064E \u0627\u0644\u0652\u0645\u064F\u0633\u0652\u062A\u064E\u0642\u0650\u064A\u0645\u064E"),
        (1, 7, "\u0635\u0650\u0631\u064E\u0627\u0637\u064E \u0627\u0644\u0651\u0630\u0650\u064A\u0646\u064E \u0623\u064E\u0646\u0652\u0639\u064E\u0645\u0652\u062A\u064E \u0639\u064E\u0644\u064E\u064A\u0652\u0647\u0650\u0645\u0652 \u063A\u064E\u064A\u0652\u0631\u0650 \u0627\u0644\u0652\u0645\u064E\u063A\u0652\u0636\u064F\u0648\u0628\u0650 \u0639\u064E\u0644\u064E\u064A\u0652\u0647\u0650\u0645\u0652 \u0648\u064E\u0644\u064E\u0627 \u0627\u0644\u0636\u064E\u0651\u0627\u0644\u0650\u0651\u064A\u0646\u064E")
    ];

    [Fact]
    public void DensePage_AlwaysReturnsExactly16Lines()
    {
        var layout = new Mushaf16LinerLayout();
        var result = layout.LayoutPage(DensePageVerses);
        Assert.Equal(16, result.Count);
        Assert.All(result, line => Assert.NotNull(line.Text));
    }

    [Fact]
    public void FatihaPage_NoDuplicateBismillah()
    {
        // Surah 1's ayah 1 IS the Bismillah — the layout must render it as
        // content and never emit a separate Bismillah line (would duplicate).
        var layout = new Mushaf16LinerLayout();
        var result = layout.LayoutPage(FatihaVerses, page: 1);

        Assert.Equal(16, result.Count);
        // No synthetic "surah:0" bismillah line.
        Assert.DoesNotContain(result, line => line.VerseKey == "1:0");
        // The Bismillah's words are rendered as 1:1 content — not repeated on
        // a dedicated line. (Words like الرَّحْمَٰنِ also appear in 1:3 in the
        // real text, so only verify the 1:0 line absence + 1:1 key presence.)
        Assert.Contains(result, line => line.VerseKey == "1:1");
        var lineZeroText = result[0].Text.Trim();
        Assert.StartsWith(FatihaVerses[0].ArabicText.Split(' ')[0], lineZeroText, StringComparison.Ordinal);
    }

    [Fact]
    public void SparsePage_NoSeparatorAtLineStart()
    {
        var layout = new Mushaf16LinerLayout();
        var result = layout.LayoutPage(FatihaVerses, page: 1);

        foreach (var line in result)
        {
            var trimmed = line.Text.TrimStart();
            Assert.False(trimmed.StartsWith("\u06DE", StringComparison.Ordinal), "Separator must not open a line");
        }
    }

    [Fact]
    public void DensePage_NoSeparatorAtLineStart()
    {
        var layout = new Mushaf16LinerLayout();
        var result = layout.LayoutPage(DensePageVerses);

        foreach (var line in result)
        {
            var trimmed = line.Text.TrimStart();
            Assert.False(trimmed.StartsWith("\u06DE", StringComparison.Ordinal), "Separator must not open a line");
        }
    }

    [Fact]
    public void LineSource_OverridesAlgorithmicLayout()
    {
        const string json = """
            [
              { "page": 2, "lines": ["one", "two", "three"] }
            ]
            """;
        var source = new JsonLinePageSource(json);
        var layout = new Mushaf16LinerLayout(source);

        var result = layout.LayoutPage(DensePageVerses, page: 2);

        Assert.Equal(16, result.Count);
        Assert.Equal("one", result[0].Text);
        Assert.Equal("two", result[1].Text);
        Assert.Equal("three", result[2].Text);
        Assert.All(result.Skip(3), line => Assert.Equal(" ", line.Text.Trim().Length == 0 ? " " : line.Text));
    }

    [Fact]
    public void EmptyLineSource_FallsBackToAlgorithmicLayout()
    {
        var layout = new Mushaf16LinerLayout(new JsonLinePageSource("[]"));
        var result = layout.LayoutPage(DensePageVerses, page: 3);
        Assert.Equal(16, result.Count);
        Assert.DoesNotContain(result, line => string.IsNullOrEmpty(line.Text) && line.LineIndex < 16);
    }
}
