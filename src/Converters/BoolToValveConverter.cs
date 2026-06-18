using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Methanol_PLC.Converters;

public class BoolToValveConverter : IValueConverter
{
    private static readonly SolidColorBrush OpenBrush = new SolidColorBrush(Color.Parse("#4caf50"));
    private static readonly SolidColorBrush ClosedBrush = new SolidColorBrush(Color.Parse("#f44336"));

    public static readonly BoolToValveConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? OpenBrush : ClosedBrush;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
