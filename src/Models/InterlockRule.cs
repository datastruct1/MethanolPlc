using System;

namespace Methanol_PLC.Models;

public class InterlockRule
{
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Triggered { get; set; }
    public string LastTriggered { get; set; } = string.Empty;
}