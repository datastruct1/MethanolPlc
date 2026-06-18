using System;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Methanol_PLC.Controls;

public class LiveTrendChart : CartesianChart
{
    private readonly ObservableCollection<double> _values = new();
    private readonly ObservableCollection<DateTime> _dates = new();
    private const int MaxPoints = 100;

    public LiveTrendChart()
    {
        Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = _values,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.5,
                Stroke = new SolidColorPaint(SKColors.LimeGreen, 2)
            }
        };

        XAxes = new Axis[]
        {
            new DateTimeAxis(TimeSpan.FromSeconds(1), date => date.ToString("HH:mm:ss"))
            {
                Name = "时间",
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray)
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray)
            }
        };

        TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Hidden;
    }

    public void AddPoint(DateTime date, double value)
    {
        _dates.Add(date);
        _values.Add(value);

        while (_values.Count > MaxPoints)
        {
            _dates.RemoveAt(0);
            _values.RemoveAt(0);
        }

        // 自动调整 Y 轴范围
        if (_values.Count > 0)
        {
            var min = _values.Min();
            var max = _values.Max();
            var padding = (max - min) * 0.1;
            if (padding < 0.5) padding = 0.5;

            YAxes.First().MinLimit = min - padding;
            YAxes.First().MaxLimit = max + padding;
        }
    }

    public void Clear()
    {
        _values.Clear();
        _dates.Clear();
    }
}
