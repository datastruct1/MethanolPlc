using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentModbus;

namespace Methanol_PLC.Services;

public class ModbusSlave:IDisposable
{
    private readonly ModbusTcpServer _server;
    private readonly IoTable _table;
    private readonly PlcEngine _engine;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    private bool _running;

    //地址映射表
    //40001+ -> Tag
    private readonly Dictionary<ushort, string> _holdingRegisterMap = new();

    //30001+ -> Tag
    private readonly Dictionary<ushort, string> _inputRegisterMap = new();

    //00001+ ->Tag
    private readonly Dictionary<ushort, string> _coilMap = new();

    //10001+ ->Tag
    private readonly Dictionary<ushort, string> _discreteInputMap = new();

    public ModbusSlave(IoTable table, PlcEngine engine)
    {
        _table = table;
        _engine = engine;
        _server = new ModbusTcpServer(isAsynchronous: true);
        BuildAddressMap();
    }

    //地址映射方法
    public void BuildAddressMap()
    {
        ushort addr;
        // Holding Register (功能码 03/06/16 - 读写, 对应 AO 或可写的 AI)
        // 本项目没有 AO，但 Holding Register 可读可写，放一些关键设定值
        addr = 0;
        _holdingRegisterMap[addr++] = "ENG-601_MODE"; // 40001
        _holdingRegisterMap[addr++] = "ENG-701_MODE"; // 40002

        //Input Register (功能码 04 - 只读模拟量, 对应 AI)
        addr = 0;
        foreach (var tag in new[]
                 {
                     "LT-101", "LT-201", "PT-101", "PT-201",
                     "OT-101", "OT-201", "FIT-501", "FD-701",
                     "ENG-601_MODE", "ENG-701_MODE", "AIT-401"
                 })
        {
            _inputRegisterMap[addr++] = tag;
        }

        // Coil (功能码 01/05/15 - 读写开关量, 对应 DO)
        addr = 0;
        foreach (var tag in new[]
                 {
                     "XV-101", "XV-102", "XV-201", "XV-202",
                     "XV-501", "XV-502", "XV-503", "XV-401",
                     "ENG-601", "PUMP-501", "FAN-301", "FAN-302",
                     "IG-401", "ENG-701", "IGN-601", "IGN-701",
                     "XV-601", "XV-602", "XV-603", "XV-604",
                     "XV-701", "XV-702", "XV-703", "XV-704",
                     "XV-705", "XV-706", "PUMP-702"
                 })
        {
            _coilMap[addr++] = tag;
        }

        // Discrete Input (功能码 02 - 只读开关量, 对应 DI)
        addr = 0;
        foreach (var tag in new[]
                 {
                     "LS-101A", "LS-201A", "LS-501",
                     "ESD-001", "ESD-002", "ESD-003", "ESD-501",
                     "FD-702", "FD-703",
                     "ENG-601_FLAME", "ENG-601_OILMIST",
                     "ENG-601_FB", "PUMP-501_FB", "FAN-301_FB",
                     "FAN-302_FB", "IG-401_FB", "ZS-101",
                     "ENG-701_FLAME", "ENG-701_OILMIST", "ENG-701_FB"
                 })
        {
            _discreteInputMap[addr++] = tag;
        }
    }
    private bool _started;
    public void Start(int port = 502)
    {
        if (_started)
        {
            Console.WriteLine("Modbus already started, skip.");
            return;
        }
        _started = true;

        Console.WriteLine($"=== Modbus Start called, port={port}, Thread={Thread.CurrentThread.ManagedThreadId} ===");

        //添加从站单元 id=1
        _server.AddUnit(1);
        //启动服务
        _server.Start(new IPEndPoint(IPAddress.Any, port));
    }

    //在 PLC 扫描后同步 IO 值到 Modbus 缓冲区 
    public void SyncBuffers()
    {
        //只读模拟量  -04
        var inputRegs = _server.GetInputRegisters(1);
        foreach (var (addr, tag) in _inputRegisterMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) &&
                signal.Value is float f)
            {
                inputRegs[addr] = (short)(f * 10);
            }
        }

        //读写 -03/06/16
        var holdingRes = _server.GetHoldingRegisters(1);
        foreach (var (addr, tag) in _holdingRegisterMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) &&
                signal.Value is float f)
            {
                holdingRes[addr] = (short)(f * 10);
            }
        }

        //读写开关量 -  01/05/15
        var coils = _server.GetCoils(1);
        foreach (var (addr, tag) in _coilMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) &&
                signal.Value is bool b)
            {
                coils[addr] = (byte)(b ? 1 : 0);
            }
        }

        // 只读开关量 -  02
        var discreteInputs = _server.GetDiscreteInputs(1);
        foreach (var (addr, tag) in _discreteInputMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) && signal.Value is bool b)
            {
                discreteInputs[addr] = (byte)(b ? 1 : 0);
            }
        }

        //通知客户端缓冲区已更新
        _server.Update();
    }

    public void Stop()
    {
        try
        {
            _server.Stop();
        }
        catch { }
    }

    public void Dispose() => Stop();
}