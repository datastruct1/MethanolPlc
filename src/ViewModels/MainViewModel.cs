using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Methanol_PLC.Models;
using Methanol_PLC.Services;

namespace Methanol_PLC.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IoTable _ioTable;
    private readonly PlcEngine _engine;
    private readonly System.Timers.Timer _uiTimer;
    private readonly ObservableCollection<AlarmEvent> _alarms = new();
    private bool _isUpdatingAlarms = false;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _scanCount;
    [ObservableProperty] private bool _esdActive;
    [ObservableProperty] private int _unacknowledgedCount;
    [ObservableProperty] private bool _hasNewAlarm;
    [ObservableProperty] private bool _alarmFlashState;

    public ObservableCollection<AlarmEvent> Alarms => _alarms;
    public ObservableCollection<InterlockRule> Rules { get; } = new();
    public ObservableCollection<IoSignalViewModel> AiSignals { get; } = new();
    public ObservableCollection<IoSignalViewModel> DiSignals { get; } = new();
    public ObservableCollection<IoSignalViewModel> DoSignals { get; } = new();

    // 趋势图相关
    private readonly Dictionary<string, Queue<(DateTime Date, double Value)>> _trendData = new();
    private const int MaxTrendPoints = 100;

    [ObservableProperty] private string? _selectedTrendSignal;
    [ObservableProperty] private ObservableCollection<string> _trendSignalTags = new();

    [ObservableProperty] private float _lt101;
    [ObservableProperty] private float _pt101;
    [ObservableProperty] private float _ot101;
    [ObservableProperty] private float _lt201;
    [ObservableProperty] private float _pt201;
    [ObservableProperty] private float _ot201;

    [ObservableProperty] private bool _xv101Open;
    [ObservableProperty] private bool _xv102Open;
    [ObservableProperty] private bool _xv201Open;
    [ObservableProperty] private bool _xv202Open;

    [ObservableProperty] private bool _eng601Running;
    [ObservableProperty] private bool _xv601Open;
    [ObservableProperty] private bool _xv602Open;
    [ObservableProperty] private bool _xv603Open;
    [ObservableProperty] private bool _xv604Open;
    [ObservableProperty] private bool _eng601Flame;
    [ObservableProperty] private bool _eng601OilMist;

    [ObservableProperty] private bool _eng701Running;
    [ObservableProperty] private bool _xv701Open;
    [ObservableProperty] private bool _xv702Open;
    [ObservableProperty] private bool _xv703Open;
    [ObservableProperty] private bool _xv704Open;
    [ObservableProperty] private bool _eng701Flame;
    [ObservableProperty] private bool _eng701OilMist;

    [ObservableProperty] private bool _pump501Running;
    [ObservableProperty] private float _fit501Flow;

    [ObservableProperty] private bool _xv501Open;
    [ObservableProperty] private bool _xv502Open;
    [ObservableProperty] private bool _xv503Open;

    [ObservableProperty] private float _eng601Mode;
    [ObservableProperty] private float _eng701Mode;

    [ObservableProperty] private bool _eng601Fb;
    [ObservableProperty] private bool _eng701Fb;
    [ObservableProperty] private bool _fan301Fb;
    [ObservableProperty] private bool _fan302Fb;
    [ObservableProperty] private bool _pump501Fb;
    [ObservableProperty] private bool _ig401Fb;

    [ObservableProperty] private bool _ls101a;
    [ObservableProperty] private bool _ls201a;
    [ObservableProperty] private bool _ls501;
    [ObservableProperty] private bool _esd501;
    [ObservableProperty] private bool _esd001;
    [ObservableProperty] private bool _esd002;
    [ObservableProperty] private bool _esd003;

    [ObservableProperty] private bool _fd702;
    [ObservableProperty] private bool _fd703;
    [ObservableProperty] private bool _xv705Open;
    [ObservableProperty] private bool _xv706Open;
    [ObservableProperty] private bool _pump702Running;

    [ObservableProperty] private bool _fan301Running;
    [ObservableProperty] private bool _fan302Running;
    [ObservableProperty] private bool _ig401Running;
    [ObservableProperty] private bool _xv401Open;

    private readonly System.Timers.Timer _alarmFlashTimer;
    private int _lastAlarmCount;

    public MainViewModel()
    {
        _ioTable = new IoTable();
        _ioTable.Initialize();
        _engine = new PlcEngine(_ioTable);
        _engine.Alarms.CollectionChanged += OnAlarmsCollectionChanged;

        // 初始化趋势图信号标签列表
        _trendSignalTags = new ObservableCollection<string>(
            _ioTable.Signals.Values
                .Where(s => s.Type == SignalType.AI)
                .Select(s => s.Tag)
                .OrderBy(t => t)
        );

        // 初始化趋势数据字典
        foreach (var tag in _trendSignalTags)
        {
            _trendData[tag] = new Queue<(DateTime, double)>();
        }

        // 默认选中第一个信号
        if (_trendSignalTags.Count > 0)
        {
            _selectedTrendSignal = _trendSignalTags[0];
        }

        // UI refresh timer: every 300ms push engine state to UI
        _uiTimer = new System.Timers.Timer(300);
        _uiTimer.Elapsed += (_, _) => RefreshAll();
        _uiTimer.Start();

        // Alarm flash timer: 500ms flash effect
        _alarmFlashTimer = new System.Timers.Timer(500);
        _alarmFlashTimer.Elapsed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AlarmFlashState = !AlarmFlashState;
            });
        };

        LoadRules();
        LoadSignalLists();
        RefreshAll();
    }

    private void OnAlarmsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 只检测是否有新报警，不修改集合
        // 集合同步在 RefreshAll() 中处理
        if (e.NewItems != null)
        {
            bool hasNewAlarm = false;
            foreach (var item in e.NewItems)
            {
                if (item is AlarmEvent alarm && !alarm.Acknowledged)
                {
                    hasNewAlarm = true;
                    break;
                }
            }
            
            if (hasNewAlarm)
            {
                HasNewAlarm = true;
                // 启动闪烁定时器
                _alarmFlashTimer.Start();
            }
        }
        
        UpdateUnacknowledgedCount();
    }

    private void LoadRules()
    {
        Rules.Clear();
        foreach (var rule in _engine.Rules)
            Rules.Add(rule);
    }

    private void LoadSignalLists()
    {
        AiSignals.Clear();
        DiSignals.Clear();
        DoSignals.Clear();
        foreach (var signal in _ioTable.Signals.Values)
        {
            var vm = new IoSignalViewModel(signal, _engine, this);
            switch (signal.Type)
            {
                case SignalType.AI: AiSignals.Add(vm); break;
                case SignalType.DI: DiSignals.Add(vm); break;
                case SignalType.DO: DoSignals.Add(vm); break;
            }
        }
    }

    public void RefreshAll()
    {
        Lt101 = GetFloat("LT-101");
        Pt101 = GetFloat("PT-101");
        Ot101 = GetFloat("OT-101");
        Lt201 = GetFloat("LT-201");
        Pt201 = GetFloat("PT-201");
        Ot201 = GetFloat("OT-201");
        Xv101Open = GetBool("XV-101");
        Xv102Open = GetBool("XV-102");
        Xv201Open = GetBool("XV-201");
        Xv202Open = GetBool("XV-202");
        Eng601Running = GetBool("ENG-601");
        Xv601Open = GetBool("XV-601");
        Xv602Open = GetBool("XV-602");
        Xv603Open = GetBool("XV-603");
        Xv604Open = GetBool("XV-604");
        Eng601Flame = GetBool("ENG-601_FLAME");
        Eng601OilMist = GetBool("ENG-601_OILMIST");
        Eng701Running = GetBool("ENG-701");
        Xv701Open = GetBool("XV-701");
        Xv702Open = GetBool("XV-702");
        Xv703Open = GetBool("XV-703");
        Xv704Open = GetBool("XV-704");
        Eng701Flame = GetBool("ENG-701_FLAME");
        Eng701OilMist = GetBool("ENG-701_OILMIST");
        Pump501Running = GetBool("PUMP-501");
        Fit501Flow = GetFloat("FIT-501");
        Xv501Open = GetBool("XV-501");
        Xv502Open = GetBool("XV-502");
        Xv503Open = GetBool("XV-503");
        Eng601Mode = GetFloat("ENG-601_MODE");
        Eng701Mode = GetFloat("ENG-701_MODE");
        Eng601Fb = GetBool("ENG-601_FB");
        Eng701Fb = GetBool("ENG-701_FB");
        Fan301Fb = GetBool("FAN-301_FB");
        Fan302Fb = GetBool("FAN-302_FB");
        Pump501Fb = GetBool("PUMP-501_FB");
        Ig401Fb = GetBool("IG-401_FB");
        Ls101a = GetBool("LS-101A");
        Ls201a = GetBool("LS-201A");
        Ls501 = GetBool("LS-501");
        Esd501 = GetBool("ESD-501");
        Esd001 = GetBool("ESD-001");
        Esd002 = GetBool("ESD-002");
        Esd003 = GetBool("ESD-003");
        Fd702 = GetBool("FD-702");
        Fd703 = GetBool("FD-703");
        Xv705Open = GetBool("XV-705");
        Xv706Open = GetBool("XV-706");
        Pump702Running = GetBool("PUMP-702");
        Fan301Running = GetBool("FAN-301");
        Fan302Running = GetBool("FAN-302");
        Ig401Running = GetBool("IG-401");
        Xv401Open = GetBool("XV-401");
        ScanCount = _engine.ScanCount;
        EsdActive = _engine.EsdActive;
        foreach (var vm in AiSignals) vm.Refresh();
        foreach (var vm in DiSignals) vm.Refresh();
        foreach (var vm in DoSignals) vm.Refresh();
        
        // 同步报警集合
        SyncAlarms();

        // 更新趋势图数据
        UpdateTrendData();
    }

    public (DateTime[] Dates, double[] Values)? GetTrendData()
    {
        if (string.IsNullOrEmpty(_selectedTrendSignal) || !_trendData.ContainsKey(_selectedTrendSignal))
            return null;

        var queue = _trendData[_selectedTrendSignal];
        return (queue.Select(x => x.Date).ToArray(), queue.Select(x => x.Value).ToArray());
    }

    private void UpdateTrendData()
    {
        if (string.IsNullOrEmpty(_selectedTrendSignal)) return;

        var signal = _ioTable.Signals.Values.FirstOrDefault(s => s.Tag == _selectedTrendSignal);
        if (signal?.Value is float value)
        {
            // 初始化队列（如果不存在）
            if (!_trendData.ContainsKey(_selectedTrendSignal))
            {
                _trendData[_selectedTrendSignal] = new Queue<(DateTime, double)>();
            }

            var queue = _trendData[_selectedTrendSignal];
            queue.Enqueue((DateTime.Now, value));
            while (queue.Count > MaxTrendPoints)
            {
                queue.Dequeue();
            }
        }
    }

    partial void OnSelectedTrendSignalChanged(string? value)
    {
        // 切换信号时通知 UI 更新
        OnPropertyChanged(nameof(GetTrendData));
    }
    
    private void SyncAlarms()
    {
        // 使用 Tag + Level 作为唯一标识
        var engineAlarms = _engine.Alarms.ToList();
        var engineKeys = new HashSet<string>(engineAlarms.Select(a => $"{a.Tag}_{a.Level}"));
        var localKeys = new HashSet<string>(_alarms.Select(a => $"{a.Tag}_{a.Level}"));
        
        // 移除本地集合中不在引擎集合中的报警（条件已消除）
        var toRemove = _alarms.Where(a => !engineKeys.Contains($"{a.Tag}_{a.Level}")).ToList();
        foreach (var alarm in toRemove)
        {
            _alarms.Remove(alarm);
        }
        
        // 添加引擎集合中不在本地集合中的报警（新报警）
        foreach (var engineAlarm in engineAlarms)
        {
            string key = $"{engineAlarm.Tag}_{engineAlarm.Level}";
            if (!localKeys.Contains(key))
            {
                // 新报警，添加到本地集合
                _alarms.Add(engineAlarm);
            }
        }
        
        UpdateUnacknowledgedCount();
    }

    [RelayCommand]
    private void Start()
    {
        _engine.Start();
        IsRunning = true;
    }

    [RelayCommand]
    private void Stop()
    {
        _engine.Stop();
        IsRunning = false;
    }

    [RelayCommand]
    private void Reset()
    {
        _engine.Reset();
        _ioTable.Initialize();
        // 清空本地报警列表
        _alarms.Clear();
        RefreshAll();
    }



    [RelayCommand]
    private void AcknowledgeAlarm(int index)
    {
        _engine.AcknowledgeAlarm(index);
        UpdateUnacknowledgedCount();
    }

    [RelayCommand]
    private void AcknowledgeAllAlarms()
    {
        // 确认所有报警，标记为已确认，停止声光报警
        foreach (var alarm in _engine.Alarms)
        {
            alarm.Acknowledged = true;
        }
        foreach (var alarm in _alarms)
        {
            alarm.Acknowledged = true;
        }
        UpdateUnacknowledgedCount();
        HasNewAlarm = false;
    }

    [RelayCommand]
    private void SilenceAlarms()
    {
        // 只停止声光报警，不确认报警
        HasNewAlarm = false;
        _alarmFlashTimer.Stop();
        AlarmFlashState = false;
    }

    [RelayCommand]
    private void ResetAlarms()
    {
        _engine.Alarms.Clear();
        _alarms.Clear();
        UpdateUnacknowledgedCount();
        HasNewAlarm = false;
    }

    [RelayCommand]
    private void ClearAcknowledgedAlarms()
    {
        // 从引擎中移除已确认的报警
        var acknowledgedAlarms = _engine.Alarms.Where(a => a.Acknowledged).ToList();
        foreach (var alarm in acknowledgedAlarms)
        {
            _engine.Alarms.Remove(alarm);
        }
        // 同步到本地集合
        var localAcknowledged = _alarms.Where(a => a.Acknowledged).ToList();
        foreach (var alarm in localAcknowledged)
        {
            _alarms.Remove(alarm);
        }
        UpdateUnacknowledgedCount();
    }

    [RelayCommand]
    private void AcknowledgeSingleAlarm(AlarmEvent alarm)
    {
        if (alarm != null && !alarm.Acknowledged)
        {
            alarm.Acknowledged = true;
            
            // 同步到引擎中的报警对象
            var engineAlarm = _engine.Alarms.FirstOrDefault(a => a.Tag == alarm.Tag && a.Level == alarm.Level);
            if (engineAlarm != null)
            {
                engineAlarm.Acknowledged = true;
            }
            
            UpdateUnacknowledgedCount();
            
            // 如果所有报警都已确认，停止声光
            if (!_alarms.Any(a => !a.Acknowledged))
            {
                HasNewAlarm = false;
            }
        }
    }

    [RelayCommand]
    private void ModifyAi(string tag)
    {
        if (_ioTable.Signals.TryGetValue(tag, out var signal) && signal.Value is float f)
        {
            float newVal;
            if (f < 50) newVal = 85f; // trigger high alarm
            else if (f < 90) newVal = 98f; // trigger high-high interlock
            else newVal = 30f; // normal
            _engine.SetManualOverride(tag);
            _ioTable.SetValue(tag, newVal);
            _engine.Scan();
        }
    }

    [RelayCommand]
    private void ToggleDi(string tag)
    {
        var val = GetBool(tag);
        _ioTable.SetValue(tag, !val);
        _engine.SetManualOverride(tag);
        _engine.Scan();
        RefreshAll();
    }

    [RelayCommand]
    private void ToggleEngine(string tag)
    {
        var val = GetBool(tag);
        _engine.SetManualOverride(tag);
        _ioTable.SetValue(tag, !val);
        _engine.Scan();
        RefreshAll();
    }

    [RelayCommand]
    private void ToggleDo(string tag)
    {
        var val = GetBool(tag);
        _engine.SetManualOverride(tag);
        _ioTable.SetValue(tag, !val);
        // 立即执行一次扫描，让新状态生效
        _engine.Scan();
        RefreshAll();
    }

    [RelayCommand]
    private void ToggleValve(string tag)
    {
        var val = GetBool(tag);
        _engine.SetManualOverride(tag);
        _ioTable.SetValue(tag, !val);
        _engine.Scan();
        RefreshAll();
    }

    [RelayCommand]
    private void InjectFault(string tag)
    {
        var val = GetBool(tag);
        _engine.SetManualOverride(tag);
        _ioTable.SetValue(tag, !val);
        _engine.Scan();
        RefreshAll();
    }

    [RelayCommand]
    private void ClearAllFaults()
    {
        _engine.ClearManualOverride();
        foreach (var signal in _ioTable.Signals.Values.Where(s => s.Type == SignalType.DI))
            _ioTable.SetValue(signal.Tag, false);
        _engine.Scan();
        RefreshAll();
    }

    private void UpdateUnacknowledgedCount() =>
        UnacknowledgedCount = Alarms.Count(a => !a.Acknowledged);

    private bool GetBool(string tag) =>
        _ioTable.Signals.TryGetValue(tag, out var s) && s.Value is bool b && b;

    private float GetFloat(string tag) =>
        _ioTable.Signals.TryGetValue(tag, out var s) && s.Value is float f ? f : 0f;
}