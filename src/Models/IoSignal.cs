namespace Methanol_PLC.Models;

public class IoSignal
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SignalType Type { get; set; }
    public object? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public float? AlarmHigh { get; set; }
    public float? AlarmHighHigh { get; set; }
    public float? AlarmLow { get; set; }
    public float? AlarmLowLow { get; set; }
}


public enum SignalType
{
    DI,
    DO,
    AI,
    AO
}