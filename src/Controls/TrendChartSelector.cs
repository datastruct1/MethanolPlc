using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace Methanol_PLC.Controls;

public class TrendChartSelector : StackPanel
{
    public ObservableCollection<string> AvailableSignals { get; } = new();
    public ObservableCollection<string> SelectedSignals { get; } = new();

    public TrendChartSelector()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
        Spacing = 10;
        Margin = new Thickness(10);
    }

    public void AddSignal(string signalName)
    {
        if (!AvailableSignals.Contains(signalName))
        {
            AvailableSignals.Add(signalName);
        }
    }

    public void RemoveSignal(string signalName)
    {
        AvailableSignals.Remove(signalName);
        SelectedSignals.Remove(signalName);
    }
}
