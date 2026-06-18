using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Methanol_PLC.Converters;

public class BoolToAlarmBgConverter : IValueConverter
{
    public static readonly BoolToAlarmBgConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.Parse("#8B0000")) : new SolidColorBrush(Color.Parse("#2d2d44"));
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToAlarmFlashConverter : IValueConverter
{
    public static readonly BoolToAlarmFlashConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.Parse("#FF0000")) : new SolidColorBrush(Color.Parse("#8B0000"));
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToAlarmItemBgConverter : IValueConverter
{
    public static readonly BoolToAlarmItemBgConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.Parse("#3a3a3a")) : new SolidColorBrush(Color.Parse("#3a1a1a"));
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToAlarmStatusConverter : IValueConverter
{
    public static readonly BoolToAlarmStatusConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.Parse("#4caf50")) : new SolidColorBrush(Color.Parse("#f44336"));
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToConfirmTextConverter : IValueConverter
{
    public static readonly BoolToConfirmTextConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "已确认" : "确认";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToConfirmBgConverter : IValueConverter
{
    public static readonly BoolToConfirmBgConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "#666666" : "#4caf50";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToInvertedVisConverter : IValueConverter
{
    public static readonly BoolToInvertedVisConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? false : true;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? false : true;
}
