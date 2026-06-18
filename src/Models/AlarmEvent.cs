using System;

namespace Methanol_PLC.Models;


public class AlarmEvent
{
    public DateTime Timestamp { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;  // HIGH / HIGH_HIGH / LOW / LOW_LOW
    public string Message { get; set; } = string.Empty;
    public bool Acknowledged { get; set; }
    public bool IsActive { get; set; } = true;  // 是否仍在触发状态
}