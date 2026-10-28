using TarteelMobile.Models;

namespace TarteelMobile.Services;

public static class MushafLayoutService
{
    // Canonical 15-line Madinah Mushaf Page 562 text structure (Surah Al-Mulk 67:1 - 67:12)
    private static readonly string[] Page562Lines =
    [
        "تَبَـٰرَكَ ٱلَّذِى بِيَدِهِ ٱلْمُلْكُ وَهُوَ عَلَىٰ كُلِّ شَىْءٍ",
        "قَدِيرٌ [1] ٱلَّذِى خَلَقَ ٱلْمَوْتَ وَٱلْحَيَوٰةَ لِيَبْلُوَكُمْ أَيُّكُمْ أَحْسَنُ",
        "عَمَلًا وَهُوَ ٱلْعَزِيزُ ٱلْغَفُورُ [2] ٱلَّذِى خَلَقَ سَبْعَ سَمَـٰوَٰتٍ طِبَاقًا",
        "مَّا تَرَىٰ فِى خَلْقِ ٱلرَّحْمَـٰنِ مِن تَفَـٰوُتٍ فَٱرْجِعِ ٱلْبَصَرَ هَلْ تَرَىٰ",
        "مِن فُطُورٍ [3] ثُمَّ ٱرْجِعِ ٱلْبَصَرَ كَرَّتَيْنِ يَنقَلِبْ إِلَيْكَ ٱلْبَصَرُ خَاسِئًا",
        "وَهُوَ حَسِيرٌ [4] وَلَقَدْ زَيَّنَّا ٱلسَّمَآءَ ٱلدُّنْيَا بِمَصَـٰبِيحَ",
        "وَجَعَلْنَـٰهَا رُجُومًا لِّلشَّيَـٰطِينِ وَأَعْتَدْنَا لَهُمْ عَذَابَ ٱلسَّعِيرِ [5]",
        "وَلِلَّذِينَ كَفَرُوا۟ بِرَبِّهِمْ عَذَابُ جَهَنَّمَ وَبِئْسَ ٱلْمَصِيرُ [6]",
        "إِذَآ أُلْقُوا۟ فِيهَا سَمِعُوا۟ لَهَا شَهِيقًا وَهِىَ تَفُورُ [7]",
        "تَكَادُ تَمَيَّزُ مِنَ ٱلْغَيْظِ كُلَّمَآ أُلْقِىَ فِيهَا فَوْجٌ سَأَلَهُمْ",
        "خَزَنَتُهَآ أَلَمْ يَأْتِكُمْ نَذِيرٌ [8] قَالُوا۟ بَلَىٰ قَدْ جَآءَنَا",
        "نَذِيرٌ فَكَذَّبْنَا وَقُلْنَا مَا نَزَّلَ ٱللَّهُ مِن شَىْءٍ إِنْ أَنتُمْ",
        "إِلَّا فِى ضَلَـٰلٍ كَبِيرٍ [9] وَقَالُوا۟ لَوْ كُنَّا نَسْمَعُ أَوْ",
        "نَعْقِلُ مَا كُنَّا فِىٓ أَصْحَـٰبِ ٱلسَّعِيرِ [10] فَٱعْتَرَفُوا۟",
        "بِذَنۢبِهِمْ فَسُحْقًا لِّأَصْحَـٰبِ ٱلسَّعِيرِ [11] إِنَّ ٱلَّذِينَ يَخْشَوْنَ"
    ];

    public const int DefaultLineCount = 16;

    public static (List<QuranWordModel> Words, List<AyahMarkerModel> Markers) BuildPage562Layout(float canvasWidth, float canvasHeight)
    {
        return BuildLinesLayout(Page562Lines, 67, 562, canvasWidth, canvasHeight);
    }

    public static (List<QuranWordModel> Words, List<AyahMarkerModel> Markers) Build16LineLayout(
        IReadOnlyList<Verse> verses,
        int surahNumber,
        int pageNumber,
        float canvasWidth,
        float canvasHeight)
    {
        return BuildLayoutFromVerses(verses, surahNumber, pageNumber, canvasWidth, canvasHeight, DefaultLineCount);
    }

    public static (List<QuranWordModel> Words, List<AyahMarkerModel> Markers) BuildLayoutFromVerses(
        IReadOnlyList<Verse> verses,
        int surahNumber,
        int pageNumber,
        float canvasWidth,
        float canvasHeight,
        int targetLineCount = DefaultLineCount)
    {
        if (verses is null || verses.Count == 0)
        {
            if (surahNumber == 67 && (pageNumber == 562 || pageNumber <= 0))
            {
                return BuildPage562Layout(canvasWidth, canvasHeight);
            }
            return ([], []);
        }

        var allTokens = new List<string>();
        foreach (var v in verses)
        {
            var verseWords = v.ArabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            allTokens.AddRange(verseWords);
            allTokens.Add($"[{v.AyahNum}]");
        }

        var wordsPerLine = (int)Math.Ceiling((double)allTokens.Count / targetLineCount);
        if (wordsPerLine < 3) wordsPerLine = 3;

        var lines = new List<string>();
        for (int i = 0; i < allTokens.Count; i += wordsPerLine)
        {
            var chunk = allTokens.Skip(i).Take(wordsPerLine);
            lines.Add(string.Join(" ", chunk));
        }

        return BuildLinesLayout(lines, surahNumber, pageNumber, canvasWidth, canvasHeight);
    }

    public static (List<QuranWordModel> Words, List<AyahMarkerModel> Markers) BuildLinesLayout(
        IReadOnlyList<string> lines,
        int surahNumber,
        int pageNumber,
        float canvasWidth,
        float canvasHeight)
    {
        var words = new List<QuranWordModel>();
        var markers = new List<AyahMarkerModel>();

        if (canvasWidth <= 0) canvasWidth = 380f;
        if (canvasHeight <= 0) canvasHeight = 560f;

        float horizontalPadding = 16f;
        float topPadding = 10f;
        float bottomPadding = 10f;
        float usableWidth = Math.Max(200f, canvasWidth - (horizontalPadding * 2));
        float usableHeight = Math.Max(300f, canvasHeight - topPadding - bottomPadding);

        int lineCount = Math.Max(1, lines.Count);
        float lineHeight = usableHeight / lineCount;

        int currentAyah = 1;
        int wordCounter = 0;

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var lineText = lines[lineIndex];
            var tokens = lineText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0) continue;

            float y = topPadding + (lineIndex * lineHeight) + (lineHeight * 0.15f);

            // Compute relative widths for each token on this line
            var tokenItems = new List<(string Text, bool IsMarker, int AyahNum, float RelativeWeight)>();
            float totalWeight = 0;

            foreach (var token in tokens)
            {
                if (token.StartsWith('[') && token.EndsWith(']') && int.TryParse(token[1..^1], out int ayahNum))
                {
                    // Verse marker circle
                    tokenItems.Add((token, true, ayahNum, 1.8f));
                    totalWeight += 1.8f;
                }
                else
                {
                    // Word
                    float weight = Math.Max(1.0f, token.Length * 0.7f);
                    tokenItems.Add((token, false, 0, weight));
                    totalWeight += weight;
                }
            }

            // Distribute positions Right-To-Left across usableWidth
            float currentRight = canvasWidth - horizontalPadding;

            foreach (var item in tokenItems)
            {
                float itemWidth = (item.RelativeWeight / totalWeight) * usableWidth;
                float x = currentRight - itemWidth;

                if (item.IsMarker)
                {
                    currentAyah = item.AyahNum;
                    markers.Add(new AyahMarkerModel
                    {
                        AyahNumber = item.AyahNum,
                        LineNumber = lineIndex + 1,
                        X = x + (itemWidth / 2f),
                        Y = y + (lineHeight * 0.35f),
                        Radius = Math.Clamp(lineHeight * 0.32f, 10f, 15f)
                    });
                }
                else
                {
                    wordCounter++;
                    var wordModel = new QuranWordModel
                    {
                        Id = $"{surahNumber}:{currentAyah}:{wordCounter}",
                        SurahNumber = surahNumber,
                        AyahNumber = currentAyah,
                        WordIndex = wordCounter,
                        UthmaniText = item.Text,
                        NormalizedText = ArabicTextNormalizer.Normalize(item.Text),
                        LineNumber = lineIndex + 1,
                        X = x,
                        Y = y,
                        Width = itemWidth,
                        Height = lineHeight * 0.75f,
                        Status = WordRecitationStatus.Hidden
                    };
                    words.Add(wordModel);
                }

                currentRight -= itemWidth;
            }
        }

        return (words, markers);
    }
}
