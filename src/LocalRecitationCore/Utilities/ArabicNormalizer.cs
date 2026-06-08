using System.Globalization;
using System.Text;

namespace TarteelClone.LocalRecitationCore.Utilities;

public static class ArabicNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mappedCharacter = character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ؤ' => 'و',
                'ئ' or 'ى' => 'ي',
                'ة' => 'ه',
                'ـ' => '\0',
                _ => character
            };

            if (mappedCharacter == '\0')
            {
                continue;
            }

            if (char.IsLetterOrDigit(mappedCharacter) || char.IsWhiteSpace(mappedCharacter))
            {
                builder.Append(mappedCharacter);
            }
        }

        var compact = builder.ToString().Normalize(NormalizationForm.FormC).Trim();
        if (compact.Length == 0)
        {
            return string.Empty;
        }

        var normalized = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', normalized);
    }

    public static string[] TokenizeAndNormalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var normalized = Normalize(part);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                tokens.Add(normalized);
            }
        }

        return tokens.ToArray();
    }
}
