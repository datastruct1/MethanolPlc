using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using Methanol_PLC.Models;

namespace Methanol_PLC.Services;

public class PlcEngine
{
    private readonly Timer _scanTimer;
    private readonly IoTable _ioTable;
    private readonly List<InterlockRule> _rules;
    private readonly HashSet<string> _manualOverrides = new();
    private readonly Random _rng = new();

    public ObservableCollection<AlarmEvent> Alarms { get; } = new();
    public List<InterlockRule> Rules => _rules;
    public bool EsdActive { get; private set; }
    public int ScanCount { get; private set; }

    public PlcEngine(IoTable ioTable)
    {
        _ioTable = ioTable;
        _rules = CreateInterLockRules();
        _scanTimer = new Timer(500);
        _scanTimer.Elapsed += (_, _) => Scan();
    }

    private List<InterlockRule>? CreateInterLockRules() => new()
    {
        new() { Name = "ESD紧急切断",       Condition = "任一ESD按钮按下",                  Action = "关闭所有燃料阀",                            Priority = 1 },
        new() { Name = "消防灭火联锁",       Condition = "火焰/烟感/可燃气>=50%LEL",         Action = "水雾+泡沫+消防泵+关所有燃料阀",              Priority = 2 },
        new() { Name = "高高液位联锁",       Condition = "液位>=98%",                       Action = "关闭对应舱主阀+加注阀",                    Priority = 3 },
        new() { Name = "双壁管泄漏联锁",     Condition = "泄漏探测触发",                     Action = "关闭对应管路主阀",                          Priority = 4 },
        new() { Name = "可燃气体高联锁",     Condition = "可燃气>=50%LEL",                   Action = "关闭所有燃料阀",                            Priority = 5 },
        new() { Name = "氧气含量高联锁",     Condition = "舱内O2>=8%",                      Action = "启动惰气系统",                              Priority = 6 },
        new() { Name = "惰气含氧高联锁",     Condition = "惰气含氧>5%",                      Action = "开释放阀+报警",                            Priority = 7 },
        new() { Name = "通风失效联锁",       Condition = "风机全部停运",                     Action = "报警",                                      Priority = 8 },
        new() { Name = "发动机1失火联锁",    Condition = "失火探测触发",                     Action = "切断燃料+关进排气阀",                       Priority = 9 },
        new() { Name = "发动机2失火联锁",    Condition = "失火探测触发",                     Action = "切断燃料+关进排气阀",                       Priority = 9 },
        new() { Name = "高液位报警",         Condition = "液位>=85%",                       Action = "声光报警",                                  Priority = 10 },
        new() { Name = "可燃气体预警",       Condition = "可燃气>=25%LEL",                  Action = "声光报警",                                  Priority = 11 },
        new() { Name = "曲轴箱油雾联锁",     Condition = "油雾探测触发",                     Action = "报警+建议停机",                             Priority = 12 },
        new() { Name = "低液位联锁",         Condition = "液位<=5%",                        Action = "停供料泵",                                  Priority = 13 },
        new() { Name = "双壁管差压低联锁",   Condition = "差压<50Pa",                       Action = "报警",                                      Priority = 14 },
        new() { Name = "加注泄漏联锁",       Condition = "加注接口泄漏探测触发",             Action = "停加注泵+关加注阀",                         Priority = 4 },
        new() { Name = "加注ESD联锁",        Condition = "加注ESD按下",                      Action = "停加注泵+关加注阀",                         Priority = 1 },
    };

    public void Start() => _scanTimer.Start();
    public void Stop() => _scanTimer.Stop();

    public void Scan()
    {
        ScanCount++;

        // === 仿真：根据阀门和发动机状态变化 AI 值 ===
        SimulateProcess();

        // === 报警检测（不清空已有报警，只添加新的） ===
        CheckAlarms();

        // === 连锁执行 ===
        ExecuteInterlocks();

        // === 更新连锁规则状态 ===
        UpdateRuleStates();
    }

    // ==================== 过程仿真 ====================
    private void SimulateProcess()
    {
        // 获取当前 DO 状态
        bool eng601 = GetBool("ENG-601");
        bool eng701 = GetBool("ENG-701");
        bool xv101 = GetBool("XV-101");
        bool xv102 = GetBool("XV-102");
        bool xv201 = GetBool("XV-201");
        bool xv202 = GetBool("XV-202");
        bool xv501 = GetBool("XV-501");
        bool xv502 = GetBool("XV-502");
        bool xv601 = GetBool("XV-601");
        bool xv701 = GetBool("XV-701");
        bool pump501 = GetBool("PUMP-501");

        // 获取当前 AI 值
        float lt101 = GetFloat("LT-101");
        float lt201 = GetFloat("LT-201");
        float fit501 = GetFloat("FIT-501");

        // --- 加注流程：PUMP-501 + XV-501/502 打开 → 液位上升 ---
        if (pump501 && xv501 && xv502)
        {
            float fillRate = 0.8f + (float)(_rng.NextDouble() * 0.4f); // 0.8~1.2 每周期
            lt101 = Math.Min(100, lt101 + fillRate * 0.5f);
            lt201 = Math.Min(100, lt201 + fillRate * 0.5f);
            fit501 = 10f + (float)(_rng.NextDouble() * 5f);
        }
        else
        {
            // 流量自然衰减
            fit501 = Math.Max(0, fit501 - 2f);
        }

        // --- 供应流程：发动机运行 + 主阀打开 → 液位下降 ---
        if (eng601 && xv101 && xv601)
        {
            float burnRate = 0.5f + (float)(_rng.NextDouble() * 0.3f);
            lt101 = Math.Max(0, lt101 - burnRate);
        }
        if (eng701 && xv201 && xv701)
        {
            float burnRate = 0.5f + (float)(_rng.NextDouble() * 0.3f);
            lt201 = Math.Max(0, lt201 - burnRate);
        }

        // --- 压力随液位变化 ---
        float pt101 = lt101 * 0.004f;  // 液位越高压力越大
        float pt201 = lt201 * 0.004f;

        // --- 氧气含量缓慢变化 ---
        float ot101 = GetFloat("OT-101");
        float ot201 = GetFloat("OT-201");
        if (lt101 > 0)
            ot101 = Math.Max(0, ot101 + (float)(_rng.NextDouble() * 0.2f - 0.1f));
        if (lt201 > 0)
            ot201 = Math.Max(0, ot201 + (float)(_rng.NextDouble() * 0.2f - 0.1f));

        // 写入仿真结果（不覆盖手动覆盖的值）
        SetFloat("LT-101", lt101);
        SetFloat("LT-201", lt201);
        SetFloat("PT-101", pt101);
        SetFloat("PT-201", pt201);
        SetFloat("OT-101", ot101);
        SetFloat("OT-201", ot201);
        SetFloat("FIT-501", fit501);
    }

    private void SetFloat(string tag, float value)
    {
        if (_ioTable.Signals.TryGetValue(tag, out var signal)
            && !_manualOverrides.Contains(tag))
        {
            signal.Value = value;
        }
    }

    // ==================== 报警检测 ====================
    private void CheckAlarms()
    {
        // 检查所有报警是否仍然触发
        var alarmsToRemove = new List<AlarmEvent>();
        
        foreach (var alarm in Alarms)
        {
            bool stillTriggered = IsAlarmStillTriggered(alarm);
            
            if (alarm.Acknowledged && stillTriggered)
            {
                // 已确认但仍在触发：保持状态
                alarm.IsActive = true;
            }
            else if (alarm.Acknowledged && !stillTriggered)
            {
                // 已确认且条件已消除：标记为非活跃，可从列表移除
                alarm.IsActive = false;
                alarmsToRemove.Add(alarm);  // 条件消除，可以移除
            }
            else if (!alarm.Acknowledged && !stillTriggered)
            {
                // 未确认且条件已消除：移除报警
                alarmsToRemove.Add(alarm);
            }
        }
        
        // 移除条件已消除的报警
        foreach (var alarm in alarmsToRemove)
        {
            Alarms.Remove(alarm);
        }

        // 添加新的报警
        foreach (var signal in _ioTable.Signals.Values.Where(s => s.Type == SignalType.AI))
        {
            if (signal.Value is float val)
            {
                if (signal.AlarmHighHigh.HasValue)
                    CheckAndAddAlarm(signal.Tag, signal.Name, "HIGH_HIGH", 
                        signal.AlarmHighHigh, val >= signal.AlarmHighHigh.Value,
                        $"{signal.Name}={val:F1}>={signal.AlarmHighHigh}");
                    
                if (signal.AlarmHigh.HasValue)
                    CheckAndAddAlarm(signal.Tag, signal.Name, "HIGH", 
                        signal.AlarmHigh, val >= signal.AlarmHigh.Value,
                        $"{signal.Name}={val:F1}>={signal.AlarmHigh}");
                    
                if (signal.AlarmLowLow.HasValue)
                    CheckAndAddAlarm(signal.Tag, signal.Name, "LOW_LOW", 
                        signal.AlarmLowLow, val <= signal.AlarmLowLow.Value,
                        $"{signal.Name}={val:F1}<={signal.AlarmLowLow}");
                    
                if (signal.AlarmLow.HasValue)
                    CheckAndAddAlarm(signal.Tag, signal.Name, "LOW", 
                        signal.AlarmLow, val <= signal.AlarmLow.Value,
                        $"{signal.Name}={val:F1}<={signal.AlarmLow}");
            }
        }
    }

    private void CheckAndAddAlarm(string tag, string name, string level, float? threshold, bool condition, string message)
    {
        if (!threshold.HasValue) return;
        
        float thresholdValue = threshold.Value;
        
        // 检查是否已存在相同 Tag + Level 的报警（无论是否确认）
        var existingAlarm = Alarms.FirstOrDefault(a => a.Tag == tag && a.Level == level);
        
        if (condition)
        {
            // 条件满足
            if (existingAlarm == null)
            {
                // 新报警：添加并触发声光
                AddAlarm(tag, name, level, message);
            }
            else if (existingAlarm.Acknowledged && !existingAlarm.IsActive)
            {
                // 已确认且之前已消除，现在重新触发：更新状态但不触发声光
                existingAlarm.IsActive = true;
                existingAlarm.Timestamp = DateTime.Now;
                existingAlarm.Message = message;
                // 不设置为未确认，所以不会触发声光
            }
        }
        else
        {
            // 条件不满足
            if (existingAlarm != null && !existingAlarm.Acknowledged)
            {
                // 未确认且条件消除：移除
                Alarms.Remove(existingAlarm);
            }
            else if (existingAlarm != null && existingAlarm.Acknowledged)
            {
                // 已确认且条件消除：标记为非活跃
                existingAlarm.IsActive = false;
            }
        }
    }

    private bool IsAlarmStillTriggered(AlarmEvent alarm)
    {
        // 检查 AI 阈值报警
        if (_ioTable.Signals.TryGetValue(alarm.Tag, out var signal))
        {
            if (signal.Value is float val)
            {
                return alarm.Level switch
                {
                    "HIGH_HIGH" => signal.AlarmHighHigh.HasValue && val >= signal.AlarmHighHigh.Value,
                    "HIGH" => signal.AlarmHigh.HasValue && val >= signal.AlarmHigh.Value,
                    "LOW_LOW" => signal.AlarmLowLow.HasValue && val <= signal.AlarmLowLow.Value,
                    "LOW" => signal.AlarmLow.HasValue && val <= signal.AlarmLow.Value,
                    _ => false
                };
            }
            // 检查 DI 信号报警
            if (signal.Value is bool boolVal)
            {
                return boolVal;
            }
        }

        // 检查系统级报警（如 ESD、ENG-601 等）
        return alarm.Tag switch
        {
            "ESD" => GetBool("ESD-001") || GetBool("ESD-002") || GetBool("ESD-003"),
            "FD" => GetBool("FD-702") || GetBool("FD-703") || GetFloat("FD-701") >= 50,
            "ENG-601" => GetBool("ENG-601_FLAME"),
            "ENG-701" => GetBool("ENG-701_FLAME"),
            "ENG" => GetBool("ENG-601_OILMIST") || GetBool("ENG-701_OILMIST"),
            "LS-101A" => GetBool("LS-101A"),
            "LS-201A" => GetBool("LS-201A"),
            "LS-501" => GetBool("LS-501"),
            "ESD-501" => GetBool("ESD-501"),
            "ESD-502" => GetBool("ESD-502"),
            _ => false
        };
    }

    private void AddAlarm(string tag, string name, string level, string message)
    {
        // 防重复：同一 Tag + 同一 Level + 未确认，不重复插入
        if (Alarms.Any(a => a.Tag == tag && a.Level == level && !a.Acknowledged))
            return;
        Alarms.Add(new AlarmEvent
        {
            Timestamp = DateTime.Now,
            Tag = tag,
            Name = name,
            Level = level,
            Message = message,
            Acknowledged = false
        });
        // 最多保留 100 条
        while (Alarms.Count > 100)
            Alarms.RemoveAt(0);
    }

    // ==================== 连锁执行 ====================
    private void ExecuteInterlocks()
    {
        // Priority 1: ESD
        if (EvalEsd()) { /* already triggered */ }

        // Priority 1: 加注 ESD
        EvalFillingEsd();

        // Priority 2: 消防
        EvalFire();

        // Priority 3: 高高液位
        EvalHighHighLevel();

        // Priority 4: 双壁管泄漏 + 加注泄漏
        EvalPipeLeak();
        EvalFillingLeak();

        // Priority 5: 可燃气体高
        EvalGasHigh();

        // Priority 6: 氧气含量高
        EvalO2High();

        // Priority 7: 惰气含氧高
        EvalIgO2High();

        // Priority 8: 通风失效
        EvalFanFail();

        // Priority 9: 发动机失火
        EvalEngineFlame("601");
        EvalEngineFlame("701");

        // Priority 12: 油雾
        EvalOilMist();

        // Priority 13: 低液位
        EvalLowLevel();

        // Priority 14: 差压低
        EvalDpLow();
    }

    // ==================== 更新连锁规则状态 ====================
    private void UpdateRuleStates()
    {
        foreach (var rule in _rules)
        {
            bool wasTriggered = rule.Triggered;
            rule.Triggered = rule.Name switch
            {
                "ESD紧急切断" => EvalEsdActive(),
                "消防灭火联锁" => EvalFireActive(),
                "高高液位联锁" => EvalHighHighLevelActive(),
                "双壁管泄漏联锁" => EvalPipeLeakActive(),
                "可燃气体高联锁" => EvalGasHighActive(),
                "氧气含量高联锁" => EvalO2HighActive(),
                "惰气含氧高联锁" => EvalIgO2HighActive(),
                "通风失效联锁" => EvalFanFailActive(),
                "发动机1失火联锁" => EvalEngineFlameActive("601"),
                "发动机2失火联锁" => EvalEngineFlameActive("701"),
                "高液位报警" => EvalHighLevelActive(),
                "可燃气体预警" => EvalGasWarningActive(),
                "曲轴箱油雾联锁" => EvalOilMistActive(),
                "低液位联锁" => EvalLowLevelActive(),
                "双壁管差压低联锁" => EvalDpLowActive(),
                "加注泄漏联锁" => EvalFillingLeakActive(),
                "加注ESD联锁" => EvalFillingEsdActive(),
                _ => false
            };
            if (rule.Triggered && !wasTriggered)
                rule.LastTriggered = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    // ========== 检测方法（带副作用） ==========

    private bool EvalEsd()
    {
        var triggered = GetBool("ESD-001") || GetBool("ESD-002") || GetBool("ESD-003");
        if (triggered)
        {
            CloseAllFuelValves();
            AddAlarm("ESD", "ESD紧急切断", "HIGH_HIGH", "ESD按钮按下，关闭所有燃料阀");
            EsdActive = true;
        }
        else
        {
            EsdActive = false;
        }
        return triggered;
    }
    private bool EvalEsdActive() => GetBool("ESD-001") || GetBool("ESD-002") || GetBool("ESD-003");

    private bool EvalFire()
    {
        var triggered = GetBool("FD-702") || GetBool("FD-703") || GetFloat("FD-701") >= 50;
        if (triggered)
        {
            SetBool("XV-705", true);
            SetBool("XV-706", true);
            SetBool("PUMP-702", true);
            CloseAllFuelValves();
            AddAlarm("FD", "消防灭火", "HIGH_HIGH", "火情检测，水雾+泡沫+消防泵启动");
        }
        return triggered;
    }
    private bool EvalFireActive() => GetBool("FD-702") || GetBool("FD-703") || GetFloat("FD-701") >= 50;

    private bool EvalHighHighLevel()
    {
        var triggered = false;
        if (GetFloat("LT-101") >= 98) { SetBool("XV-101", false); SetBool("XV-102", false); triggered = true; }
        if (GetFloat("LT-201") >= 98) { SetBool("XV-201", false); SetBool("XV-202", false); triggered = true; }
        return triggered;
    }
    private bool EvalHighHighLevelActive() => GetFloat("LT-101") >= 98 || GetFloat("LT-201") >= 98;

    private bool EvalPipeLeak()
    {
        var triggered = false;
        if (GetBool("LS-101A")) { SetBool("XV-101", false); triggered = true; }
        if (GetBool("LS-201A")) { SetBool("XV-201", false); triggered = true; }
        return triggered;
    }
    private bool EvalPipeLeakActive() => GetBool("LS-101A") || GetBool("LS-201A");

    private bool EvalGasHigh()
    {
        var triggered = GetFloat("FD-701") >= 50;
        if (triggered) { CloseAllFuelValves(); AddAlarm("FD-701", "可燃气体高", "HIGH", "可燃气>=50%LEL"); }
        return triggered;
    }
    private bool EvalGasHighActive() => GetFloat("FD-701") >= 50;

    private bool EvalO2High()
    {
        var triggered = false;
        if (GetFloat("OT-101") >= 8) { SetBool("IG-401", true); SetBool("XV-401", true); triggered = true; }
        if (GetFloat("OT-201") >= 8) { SetBool("IG-401", true); SetBool("XV-401", true); triggered = true; }
        return triggered;
    }
    private bool EvalO2HighActive() => GetFloat("OT-101") >= 8 || GetFloat("OT-201") >= 8;

    private bool EvalIgO2High()
    {
        var triggered = GetFloat("AIT-401") > 5;
        if (triggered) AddAlarm("AIT-401", "惰气含氧高", "HIGH", "惰气含氧>5%");
        return triggered;
    }
    private bool EvalIgO2HighActive() => GetFloat("AIT-401") > 5;

    private bool EvalFanFail()
    {
        var triggered = !GetBool("FAN-301_FB") && !GetBool("FAN-302_FB");
        if (triggered) AddAlarm("FAN", "通风失效", "HIGH", "双风机全部停转");
        return triggered;
    }
    private bool EvalFanFailActive() => !GetBool("FAN-301_FB") && !GetBool("FAN-302_FB");

    private bool EvalEngineFlame(string engId)
    {
        var triggered = GetBool($"ENG-{engId}_FLAME");
        if (triggered)
        {
            SetBool($"XV-{engId}01", false);
            SetBool($"XV-{engId}02", false);
            SetBool($"XV-{engId}03", false);
            AddAlarm($"ENG-{engId}", "发动机失火", "HIGH_HIGH", $"发动机{engId}失火");
        }
        return triggered;
    }
    private bool EvalEngineFlameActive(string engId) => GetBool($"ENG-{engId}_FLAME");

    private bool EvalOilMist()
    {
        var triggered = GetBool("ENG-601_OILMIST") || GetBool("ENG-701_OILMIST");
        if (triggered) AddAlarm("ENG", "油雾检测", "HIGH", "油雾探测触发");
        return triggered;
    }
    private bool EvalOilMistActive() => GetBool("ENG-601_OILMIST") || GetBool("ENG-701_OILMIST");

    private bool EvalLowLevel()
    {
        var triggered = false;
        if (GetFloat("LT-101") <= 5) { AddAlarm("LT-101", "低液位", "LOW_LOW", "舱1液位<=5%"); triggered = true; }
        if (GetFloat("LT-201") <= 5) { AddAlarm("LT-201", "低液位", "LOW_LOW", "舱2液位<=5%"); triggered = true; }
        return triggered;
    }
    private bool EvalLowLevelActive() => GetFloat("LT-101") <= 5 || GetFloat("LT-201") <= 5;

    private bool EvalDpLow()
    {
        var dp = GetFloat("PDT-301");
        var triggered = dp > 0 && dp < 50;
        if (triggered) AddAlarm("PDT-301", "差压低", "LOW", "双壁管差压<50Pa");
        return triggered;
    }
    private bool EvalDpLowActive() { var dp = GetFloat("PDT-301"); return dp > 0 && dp < 50; }

    private bool EvalFillingLeak()
    {
        var triggered = GetBool("LS-501");
        if (triggered) { SetBool("PUMP-501", false); SetBool("XV-501", false); SetBool("XV-502", false); AddAlarm("LS-501", "加注泄漏", "HIGH_HIGH", "加注接口泄漏"); }
        return triggered;
    }
    private bool EvalFillingLeakActive() => GetBool("LS-501");

    private bool EvalFillingEsd()
    {
        var triggered = GetBool("ESD-501");
        if (triggered) { SetBool("PUMP-501", false); SetBool("XV-501", false); SetBool("XV-502", false); AddAlarm("ESD-501", "加注ESD", "HIGH_HIGH", "加注ESD按下"); }
        return triggered;
    }
    private bool EvalFillingEsdActive() => GetBool("ESD-501");

    private bool EvalHighLevelActive() => GetFloat("LT-101") >= 85 || GetFloat("LT-201") >= 85;
    private bool EvalGasWarningActive() => GetFloat("FD-701") >= 25;

    // ==================== 辅助方法 ====================

    private void CloseAllFuelValves()
    {
        SetBool("XV-101", false); SetBool("XV-102", false);
        SetBool("XV-201", false); SetBool("XV-202", false);
        SetBool("XV-501", false); SetBool("XV-502", false); SetBool("XV-503", false);
        SetBool("XV-601", false); SetBool("XV-602", false); SetBool("XV-603", false); SetBool("XV-604", false);
        SetBool("XV-701", false); SetBool("XV-702", false); SetBool("XV-703", false); SetBool("XV-704", false);
    }

    private void SetBool(string tag, bool value)
    {
        if (_ioTable.Signals.TryGetValue(tag, out var signal)
            && !_manualOverrides.Contains(tag))
        {
            signal.Value = value;
        }
    }

    private bool GetBool(string tag) =>
        _ioTable.Signals.TryGetValue(tag, out var signal) && signal.Value is bool b && b;

    private float GetFloat(string tag) =>
        _ioTable.Signals.TryGetValue(tag, out var signal) && signal.Value is float f ? f : 0f;

    public void AcknowledgeAlarm(int index)
    {
        if (index >= 0 && index < Alarms.Count)
            Alarms[index].Acknowledged = true;
    }
    public void SetManualOverride(string tag) => _manualOverrides.Add(tag);
    public void ClearManualOverride() => _manualOverrides.Clear();
    public bool IsManualOverride(string tag) => _manualOverrides.Contains(tag);
    public void Reset()
    {
        EsdActive = false;
        Alarms.Clear();
        ScanCount = 0;
        foreach (var signal in _ioTable.Signals.Values)
        {
            if (signal.Type == SignalType.DO || signal.Type == SignalType.DI)
                signal.Value = false;
            else
                signal.Value = 0f;
        }
    }
}
