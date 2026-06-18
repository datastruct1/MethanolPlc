using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Methanol_PLC.Converters;

public class BoolToOnOffConverter : IValueConverter
{
    public static readonly BoolToOnOffConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "ON" : "OFF";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
