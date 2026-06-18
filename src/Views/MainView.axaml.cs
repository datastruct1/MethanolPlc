using Avalonia.Controls;
using Avalonia.Threading;
using Methanol_PLC.Controls;
using Methanol_PLC.ViewModels;
using System;
using System.Linq;

namespace Methanol_PLC.Views;

public partial class MainView : Window
{
    private MainViewModel? _viewModel;
    private DispatcherTimer? _trendTimer;

    public MainView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                _viewModel = vm;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        };

        _trendTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _trendTimer.Tick += OnTrendTimerTick;
        _trendTimer.Start();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTrendSignal))
        {
            var chart = this.FindControl<LiveTrendChart>("MainTrendChart");
            chart?.Clear();
        }
    }

    private void OnTrendTimerTick(object? sender, EventArgs e)
    {
        var chart = this.FindControl<LiveTrendChart>("MainTrendChart");
        if (chart == null || _viewModel == null) return;

        var data = _viewModel.GetTrendData();
        if (data.HasValue)
        {
            chart.Clear();
            for (int i = 0; i < data.Value.Dates.Length; i++)
            {
                chart.AddPoint(data.Value.Dates[i], data.Value.Values[i]);
            }

            // 标题通过图表上方的 TextBlock 显示，不设置 chart.Title
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _trendTimer?.Stop();
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.OnClosing(e);
    }
}
