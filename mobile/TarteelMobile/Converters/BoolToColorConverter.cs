using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TarteelMobile.Converters;

/// <summary>
/// Resolves two resource-key names separated by '|' and returns the
/// corresponding Color (isTrue ? first : second). Passes theme-aware
/// AppThemeBinding resources through unchanged.
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b)
            return Colors.Transparent;

        var parameterValue = parameter as string;
        if (string.IsNullOrWhiteSpace(parameterValue))
            return Colors.Transparent;

        var keys = parameterValue.Split('|');
        if (keys.Length != 2)
            return Colors.Transparent;

        var key = b ? keys[0] : keys[1];

        if (Application.Current?.Resources[key] is Color color)
            return color;

        // Fallback: try parsing as a raw hex ARGB string if the key isn't found
        try
        {
            return Color.FromArgb(key);
        }
        catch (ArgumentException)
        {
            return Colors.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
