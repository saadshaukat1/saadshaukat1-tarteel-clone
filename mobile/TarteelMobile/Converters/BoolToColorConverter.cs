using System.Globalization;

namespace TarteelMobile.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        var parameterValue = parameter as string;
        if (string.IsNullOrWhiteSpace(parameterValue))
        {
            return Colors.Transparent;
        }

        var colors = parameterValue.Split('|');
        if (colors.Length != 2)
        {
            return Colors.Transparent;
        }

        try
        {
            return Color.FromArgb(isTrue ? colors[0] : colors[1]);
        }
        catch (ArgumentException)
        {
            return Colors.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
