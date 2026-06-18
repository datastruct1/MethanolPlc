using System;
using System.Collections.Generic;
using Methanol_PLC.Models;

namespace Methanol_PLC.Services;

public class IoTable
{
    public Dictionary<string, IoSignal> Signals { get; set; } = new();

    private void AddSignal(string tag, string name,
        SignalType type, string unit = "",
        float? alarmHigh = null, float? alarmHighHigh = null,
        float? alarmLow = null, float? alarmLowLow = null)
    {
        Signals[tag] = new IoSignal()
        {
            Tag = tag,
            Name = name,
            Type = type,
            Unit = unit,
            AlarmHigh = alarmHigh,
            AlarmHighHigh = alarmHighHigh,
            AlarmLow = alarmLow,
            AlarmLowLow = alarmLowLow,
            Value = type == SignalType.DO || type == SignalType.DI ? false : 0f
        };
    }

    public void Initialize()
    {
        // === 燃料舱 1 ===
        AddSignal("LT-101", "燃料舱1液位", SignalType.AI, "%", 85, 98, 10, 5);
        AddSignal("PT-101", "燃料舱1压力", SignalType.AI, "MPa", 0.3f, 0.4f, null, null);
        AddSignal("OT-101", "燃料舱1氧气含量", SignalType.AI, "%", 5, 8, null, null);
        AddSignal("XV-101", "燃料舱1主阀", SignalType.DO);
        AddSignal("XV-102", "燃料舱1加注阀", SignalType.DO);
        AddSignal("LS-101A", "双壁管1泄漏探测", SignalType.DI);
        AddSignal("ZS-101", "燃料舱1主阀限位", SignalType.DI);

        // === 燃料舱 2 ===
        AddSignal("LT-201", "燃料舱2液位", SignalType.AI, "%", 85, 98, 10, 5);
        AddSignal("PT-201", "燃料舱2压力", SignalType.AI, "MPa", 0.3f, 0.4f, null, null);
        AddSignal("OT-201", "燃料舱2氧气含量", SignalType.AI, "%", 5, 8, null, null);
        AddSignal("XV-201", "燃料舱2主阀", SignalType.DO);
        AddSignal("XV-202", "燃料舱2加注阀", SignalType.DO);
        AddSignal("LS-201A", "双壁管2泄漏探测", SignalType.DI);

        // === 通风系统 ===
        AddSignal("FAN-301", "燃料准备间风机", SignalType.DO);
        AddSignal("FAN-302", "备用风机", SignalType.DO);
        AddSignal("FAN-301_FB", "风机1运行反馈", SignalType.DI);
        AddSignal("FAN-302_FB", "风机2运行反馈", SignalType.DI);
        AddSignal("PDT-301", "双壁管差压", SignalType.AI, "Pa", null, null, 50, null);

        // === 惰性气体系统 ===
        AddSignal("IG-401", "惰性气体发生器", SignalType.DO);
        AddSignal("IG-401_FB", "发生器运行反馈", SignalType.DI);
        AddSignal("AIT-401", "惰气含氧量", SignalType.AI, "%", 5, null, null, null);
        AddSignal("XV-401", "惰气供应阀", SignalType.DO);

        // === 加注系统 ===
        AddSignal("XV-501", "加注总阀", SignalType.DO);
        AddSignal("XV-502", "加注遥控阀", SignalType.DO);
        AddSignal("XV-503", "加注透气阀", SignalType.DO);
        AddSignal("PUMP-501", "加注泵", SignalType.DO);
        AddSignal("PUMP-501_FB", "加注泵运行反馈", SignalType.DI);
        AddSignal("FIT-501", "加注流量计", SignalType.AI, "L/min", null, null, null, null);
        AddSignal("LS-501", "加注接口泄漏探测", SignalType.DI);
        AddSignal("ESD-501", "加注ESD", SignalType.DI);

        // === 发动机 1 ===
        AddSignal("ENG-601", "甲醇发动机1", SignalType.DO);
        AddSignal("ENG-601_FB", "发动机1运行反馈", SignalType.DI);
        AddSignal("ENG-601_MODE", "发动机1模式", SignalType.AI);
        AddSignal("ENG-601_FLAME", "发动机1失火探测", SignalType.DI);
        AddSignal("ENG-601_OILMIST", "发动机1油雾探测", SignalType.DI);
        AddSignal("XV-601", "发动机1主甲醇阀", SignalType.DO);
        AddSignal("XV-602", "发动机1进气防爆阀", SignalType.DO);
        AddSignal("XV-603", "发动机1排气防爆阀", SignalType.DO);
        AddSignal("XV-604", "发动机1排气扫除阀", SignalType.DO);
        AddSignal("IGN-601", "发动机1点火器", SignalType.DO);

        // === 发动机 2 ===
        AddSignal("ENG-701", "甲醇发动机2", SignalType.DO);
        AddSignal("ENG-701_FB", "发动机2运行反馈", SignalType.DI);
        AddSignal("ENG-701_MODE", "发动机2模式", SignalType.AI);
        AddSignal("ENG-701_FLAME", "发动机2失火探测", SignalType.DI);
        AddSignal("ENG-701_OILMIST", "发动机2油雾探测", SignalType.DI);
        AddSignal("XV-701", "发动机2主甲醇阀", SignalType.DO);
        AddSignal("XV-702", "发动机2进气防爆阀", SignalType.DO);
        AddSignal("XV-703", "发动机2排气防爆阀", SignalType.DO);
        AddSignal("XV-704", "发动机2排气扫除阀", SignalType.DO);
        AddSignal("IGN-701", "发动机2点火器", SignalType.DO);

        // === 消防系统 ===
        AddSignal("FD-701", "可燃气体探测", SignalType.AI, "%LEL", 25, 50, null, null);
        AddSignal("FD-702", "火焰探测", SignalType.DI);
        AddSignal("FD-703", "烟感探测", SignalType.DI);
        AddSignal("XV-705", "水雾系统阀", SignalType.DO);
        AddSignal("XV-706", "泡沫系统阀", SignalType.DO);
        AddSignal("PUMP-702", "消防水泵", SignalType.DO);

        // === 全局 ESD ===
        AddSignal("ESD-001", "驾驶室ESD", SignalType.DI);
        AddSignal("ESD-002", "集控室ESD", SignalType.DI);
        AddSignal("ESD-003", "机旁ESD", SignalType.DI);
    }

    public IoSignal? GetSignal(string tag) =>
        Signals.TryGetValue(tag, out var signal) ? signal : null;

    public object? GetValue(string tag) =>
        Signals.TryGetValue(tag, out var signal) ? signal.Value : null;

    public void SetValue(string tag, object value)
    {
        if (Signals.TryGetValue(tag, out var signal))
            signal.Value = value;
    }
}