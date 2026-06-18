using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Methanol_PLC.Converters;

public class BoolToStatusConverter : IValueConverter
{
    private static readonly SolidColorBrush RunningBrush = new SolidColorBrush(Color.Parse("#4caf50"));
    private static readonly SolidColorBrush StoppedBrush = new SolidColorBrush(Color.Parse("#f44336"));

    public static readonly BoolToStatusConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? RunningBrush : StoppedBrush;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
