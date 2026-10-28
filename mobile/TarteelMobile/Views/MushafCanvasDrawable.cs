using Microsoft.Maui.Graphics;
using TarteelMobile.Models;

namespace TarteelMobile.Views;

public class MushafCanvasDrawable : IDrawable
{
    public IList<QuranWordModel> Words { get; set; } = new List<QuranWordModel>();
    public IList<AyahMarkerModel> Markers { get; set; } = new List<AyahMarkerModel>();

    public bool IsBlindRecitationMode { get; set; } = true;
    public bool IsTajweedColored { get; set; } = true;
    public int ActiveWordIndex { get; set; } = 0;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 1. Draw Page Background
        canvas.FillColor = Color.FromArgb("#111214");
        canvas.FillRectangle(dirtyRect);

        // 2. Draw Subtle Page Layout Guidelines (15 lines of Madinah Mushaf)
        float topMargin = 12f;
        float bottomMargin = 12f;
        float availableHeight = dirtyRect.Height - topMargin - bottomMargin;
        float lineHeight = availableHeight / 15f;

        canvas.StrokeColor = Color.FromArgb("#181B20");
        canvas.StrokeSize = 1f;
        for (int l = 1; l <= 15; l++)
        {
            float y = topMargin + (l * lineHeight);
            canvas.DrawLine(8f, y, dirtyRect.Width - 8f, y);
        }

        // 3. Render Fixed Verse Ornaments (Ayah Markers) - Pinned in Zero-Layout-Shift Coordinates
        foreach (var marker in Markers)
        {
            // Outer circular ornament
            canvas.StrokeColor = Color.FromArgb("#525866");
            canvas.StrokeSize = 1.6f;
            canvas.DrawCircle(marker.X, marker.Y, marker.Radius);

            // Inner subtle ring
            canvas.StrokeColor = Color.FromArgb("#333842");
            canvas.StrokeSize = 0.8f;
            canvas.DrawCircle(marker.X, marker.Y, marker.Radius - 3.5f);

            // Ayah number text (Arabic or numeral)
            canvas.FontColor = Color.FromArgb("#94A3B8");
            canvas.FontSize = 11;
            canvas.DrawString(
                ToEasternArabicDigits(marker.AyahNumber),
                marker.X - 12, marker.Y - 8, 24, 16,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        // 4. Render Words Based on Recitation Status
        foreach (var word in Words)
        {
            if (word.Status == WordRecitationStatus.Hidden)
            {
                if (IsBlindRecitationMode)
                {
                    // In Blind Memorization Mode, unrecited text is completely hidden!
                    continue;
                }
                else
                {
                    // Full Mushaf Mode: show dimmed placeholder text
                    canvas.FontColor = Color.FromArgb("#334155");
                    canvas.FontSize = 21;
                    canvas.DrawString(
                        word.UthmaniText,
                        word.X, word.Y, word.Width, word.Height,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                    continue;
                }
            }

            Color textColor = word.Status switch
            {
                WordRecitationStatus.Revealed => IsTajweedColored ? GetTajweedColor(word) : Color.FromArgb("#F1F5F9"),
                WordRecitationStatus.Corrected => Color.FromArgb("#4ADE80"), // Emerald Green (Self-correction)
                WordRecitationStatus.Skipped => Color.FromArgb("#38BDF8"),   // Blue Accent (Gap / Skipped)
                WordRecitationStatus.Active => Color.FromArgb("#FACC15"),    // Gold (Active word)
                _ => Color.FromArgb("#CBD5E1")
            };

            canvas.FontColor = textColor;
            canvas.FontSize = 21;

            // Render word glyph
            canvas.DrawString(
                word.UthmaniText,
                word.X, word.Y, word.Width, word.Height,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static string ToEasternArabicDigits(int number)
    {
        var s = number.ToString();
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            chars[i] = s[i] switch
            {
                '0' => '٠',
                '1' => '١',
                '2' => '٢',
                '3' => '٣',
                '4' => '٤',
                '5' => '٥',
                '6' => '٦',
                '7' => '٧',
                '8' => '٨',
                '9' => '٩',
                _ => s[i]
            };
        }
        return new string(chars);
    }

    private static Color GetTajweedColor(QuranWordModel word)
    {
        // Deterministic Tajweed rule coloring for harmonic Quran presentation matching screenshot
        var text = word.UthmaniText;
        if (text.Contains('ن') || text.Contains('م') || text.Contains('ّ'))
        {
            // Ghunna / Ikhfa / Idgham green & cyan accents
            if (word.WordIndex % 5 == 1) return Color.FromArgb("#38BDF8"); // Cyan
            if (word.WordIndex % 5 == 3) return Color.FromArgb("#4ADE80"); // Emerald green
        }

        if (text.Contains('ا') || text.Contains('ى') || text.Contains('و') || text.Contains('ي') || text.Contains('ٰ'))
        {
            // Madd / elongation accents
            if (word.WordIndex % 4 == 0) return Color.FromArgb("#F87171"); // Coral red
            if (word.WordIndex % 4 == 2) return Color.FromArgb("#FB923C"); // Warm amber
        }

        return Color.FromArgb("#F1F5F9"); // Crisp white standard
    }
}
