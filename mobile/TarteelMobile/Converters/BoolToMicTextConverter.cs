using System.Globalization;

namespace TarteelMobile.Converters;

public class BoolToMicTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isRecording = value is bool b && b;
        return isRecording ? "Stop" : "Mic";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
