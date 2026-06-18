using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Methanol_PLC.Converters;

public class BoolToModeConverter : IValueConverter
{
    public static readonly BoolToModeConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string mode && parameter is string targetMode)
            return mode == targetMode;
        return false;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string targetMode)
            return targetMode;
        return null;
    }
}
