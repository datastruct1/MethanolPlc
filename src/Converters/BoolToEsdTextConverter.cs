using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Methanol_PLC.Converters;

public class BoolToEsdTextConverter : IValueConverter
{
    public static readonly BoolToEsdTextConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "ESD 已触发" : "正常";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
