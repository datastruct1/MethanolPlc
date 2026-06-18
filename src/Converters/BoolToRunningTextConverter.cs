using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Methanol_PLC.Converters;

public class BoolToRunningTextConverter : IValueConverter
{
    public static readonly BoolToRunningTextConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "运行中" : "停止";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
