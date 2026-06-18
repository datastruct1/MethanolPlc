using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Methanol_PLC.Controls;

public class TrendChart : UserControl
{
    private ObservableCollection<double> _values = new();
    private string _title = "趋势图";
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<double> Values
    {
        get => _values;
        set
        {
            if (_values != null)
                _values.CollectionChanged -= OnValuesChanged;

            _values = value;

            if (_values != null)
                _values.CollectionChanged += OnValuesChanged;

            InvalidateVisual();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                InvalidateVisual();
            }
        }
    }

    private IBrush _lineBrush = new SolidColorBrush(Color.Parse("#00ff00"));
    private IBrush _gridBrush = new SolidColorBrush(Color.Parse("#444444"));
    private IBrush _textBrush = new SolidColorBrush(Color.Parse("#aaaaaa"));
    private IBrush _backgroundBrush = new SolidColorBrush(Color.Parse("#1a1a2e"));

    public TrendChart()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => InvalidateVisual();
        _refreshTimer.Start();
    }

    private void OnValuesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        context.DrawRectangle(_backgroundBrush, null, bounds);

        if (!string.IsNullOrEmpty(Title))
        {
            var titleFormatted = new FormattedText(
                Title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                14,
                _textBrush);
            context.DrawText(titleFormatted, new Point(10, 5));
        }

        var chartRect = new Rect(50, 30, Bounds.Width - 60, Bounds.Height - 50);

        if (chartRect.Width <= 0 || chartRect.Height <= 0) return;

        context.DrawRectangle(null, new Pen(_gridBrush, 1), chartRect);

        for (int i = 0; i <= 4; i++)
        {
            var y = chartRect.Top + chartRect.Height * i / 4;
            context.DrawLine(new Pen(_gridBrush, 0.5), new Point(chartRect.Left, y), new Point(chartRect.Right, y));

            var labelFormatted = new FormattedText(
                $"{(4 - i) * 25}%",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                9,
                _textBrush);
            context.DrawText(labelFormatted, new Point(5, y - 6));
        }

        var values = Values;
        if (values.Count > 1)
        {
            double minVal = double.MaxValue, maxVal = double.MinValue;
            foreach (var v in values)
            {
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }

            if (maxVal - minVal < 0.001) { minVal -= 1; maxVal += 1; }

            var points = new System.Collections.Generic.List<Point>();
            for (int i = 0; i < values.Count; i++)
            {
                var x = chartRect.Left + chartRect.Width * i / Math.Max(1, values.Count - 1);
                var y = chartRect.Top + chartRect.Height * (1 - (values[i] - minVal) / (maxVal - minVal));
                points.Add(new Point(x, y));
            }

            for (int i = 1; i < points.Count; i++)
            {
                context.DrawLine(new Pen(_lineBrush, 2), points[i - 1], points[i]);
            }

            if (values.Count > 0)
            {
                var currentLabel = $"当前: {values[^1]:F1}";
                var currentFormatted = new FormattedText(
                    currentLabel,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    11,
                    _lineBrush);
                context.DrawText(currentFormatted, new Point(chartRect.Right - 90, 5));
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _refreshTimer.Stop();
        if (_values != null)
            _values.CollectionChanged -= OnValuesChanged;
        base.OnDetachedFromVisualTree(e);
    }
}
