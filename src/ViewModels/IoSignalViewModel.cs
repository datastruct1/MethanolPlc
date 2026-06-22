using CommunityToolkit.Mvvm.ComponentModel;
using Methanol_PLC.Models;
using Methanol_PLC.Services;

namespace Methanol_PLC.ViewModels;

public partial class IoSignalViewModel : ObservableObject
{
    private readonly IoSignal _signal;
    private readonly PlcEngine _engine;
    private readonly MainViewModel _parent;

    public IoSignalViewModel(IoSignal signal, PlcEngine engine, MainViewModel parent)
    {
        _signal = signal;
        _engine = engine;
        _parent = parent;
    }

    public string Tag => _signal.Tag;
    public string Name => _signal.Name;
    public SignalType Type => _signal.Type;
    public string Unit => _signal.Unit;
    public string DisplayValue => FormatValue(_signal.Value);
    public bool IsManualOverride => _engine.IsManualOverride(_signal.Tag);

    public string SignalValue
    {
        get => _signal.Value is float f ? f.ToString("F2") : (_signal.Value?.ToString() ?? "0");
        set
        {
            if (float.TryParse(value, out float floatVal) && _signal.Value != (object?)floatVal)
            {
                _signal.Value = floatVal;
                _engine.Scan();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayValue));
            }
        }
    }

    public string AlarmThresholds =>
        $"{_signal.AlarmHighHigh?.ToString() ?? "-"}/{_signal.AlarmHigh?.ToString() ?? "-"}/{_signal.AlarmLow?.ToString() ?? "-"}/{_signal.AlarmLowLow?.ToString() ?? "-"}";

    public void Refresh()
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(SignalValue));
    }

    private static string FormatValue(object? value) => value switch
    {
        float f => f.ToString("F2"),
        bool b => b ? "ON" : "OFF",
        _ => value?.ToString() ?? "-"
    };
}
