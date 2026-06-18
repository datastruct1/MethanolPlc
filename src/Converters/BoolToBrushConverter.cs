using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Methanol_PLC.Converters;

public class BoolToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush OpenBrush = new SolidColorBrush(Color.Parse("rgb(35,134,54)"));
    private static readonly SolidColorBrush ClosedBrush = new SolidColorBrush(Color.Parse("rgb(218,54,51)"));

    public static readonly BoolToBrushConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? OpenBrush : ClosedBrush;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
