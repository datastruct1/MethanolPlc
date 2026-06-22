using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentModbus;

namespace Methanol_PLC.Services;

public class ModbusSlave : IDisposable
{
    private readonly IoTable _table;
    private readonly PlcEngine _engine;
    private ModbusTcpServer? _server;

    private readonly Dictionary<ushort, string> _holdingRegisterMap = new();
    private readonly Dictionary<ushort, string> _inputRegisterMap = new();
    private readonly Dictionary<ushort, string> _coilMap = new();
    private readonly Dictionary<ushort, string> _discreteInputMap = new();

    public ModbusSlave(IoTable table, PlcEngine engine)
    {
        _table = table;
        _engine = engine;
        BuildAddressMap();
    }

    public void BuildAddressMap()
    {
        ushort addr;

        addr = 0;
        _holdingRegisterMap[addr++] = "ENG-601_MODE";
        _holdingRegisterMap[addr++] = "ENG-701_MODE";

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

    /// <summary>
    /// 杀掉占用指定端口的旧进程
    /// </summary>
    private static void KillProcessOnPort(int port)
    {
        try
        {
            // 找到占用该端口的进程
            var processes = Process.GetProcessesByName("dotnet");
            foreach (var proc in processes)
            {
                try
                {
                    // 检查进程是否占用了目标端口
                    var netstat = Process.Start(new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    var output = netstat?.StandardOutput.ReadToEnd() ?? "";
                    netstat?.WaitForExit();

                    // 如果该端口的监听进程是当前 dotnet 进程
                    if (output.Contains($":{port}") && output.Contains(proc.Id.ToString()))
                    {
                        Console.WriteLine($"Killing process {proc.Id} on port {port}");
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    public void Start(int port = 502)
    {
        Console.WriteLine($"=== Modbus Start called, port={port} ===");

        // 先杀掉占用端口的旧进程
        KillProcessOnPort(port);

        // 等待端口释放
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                testSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                testSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                testSocket.Close();
                testSocket.Dispose();
                break;
            }
            catch (SocketException)
            {
                Console.WriteLine($"Waiting for port {port}... ({i + 1}/30)");
                Thread.Sleep(500);
            }
        }

        _server = new ModbusTcpServer(isAsynchronous: false);
        _server.AddUnit(1);

        // 尝试启动，带重试
        for (int i = 0; i < 20; i++)
        {
            try
            {
                _server.Start(new IPEndPoint(IPAddress.Any, port));
                Console.WriteLine($"Modbus server listening on port {port}");

                // 同步模式下后台处理请求
                var serverRef = _server;
                _ = Task.Run(() =>
                {
                    while (serverRef == _server && _server != null)
                    {
                        try
                        {
                            lock (_server.Lock)
                            {
                                _server.Update();
                            }
                        }
                        catch { break; }
                        Thread.Sleep(1);
                    }
                });

                return;
            }
            catch (SocketException ex) when (ex.ErrorCode == 10048)
            {
                Console.WriteLine($"Port {port} still in use, retry {i + 1}/20...");
                Thread.Sleep(500);
            }
        }

        throw new InvalidOperationException($"Cannot start Modbus server on port {port}");
    }

    public void SyncBuffers()
    {
        if (_server == null) return;

        // === Holding Register：双向同步（大端序） ===
        var holdingBytes = _server.GetHoldingRegisterBuffer(1);
        foreach (var (addr, tag) in _holdingRegisterMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) && signal.Value is float ioValue)
            {
                short ioValueScaled = (short)(ioValue * 100);
                holdingBytes[addr * 2] = (byte)(ioValueScaled >> 8);   // 高字节
                holdingBytes[addr * 2 + 1] = (byte)(ioValueScaled & 0xFF); // 低字节
            }
        }

        // === Input Register：单向写入（大端序） ===
        var inputBytes = _server.GetInputRegisterBuffer(1);
        foreach (var (addr, tag) in _inputRegisterMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) && signal.Value is float f)
            {
                short scaled = (short)(f * 100);
                inputBytes[addr * 2] = (byte)(scaled >> 8);   // 高字节
                inputBytes[addr * 2 + 1] = (byte)(scaled & 0xFF); // 低字节
            }
        }

        var coils = _server.GetCoilBuffer<byte>(1);
        foreach (var (addr, tag) in _coilMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) && signal.Value is bool b)
            {
                coils[addr] = (byte)(b ? 1 : 0);
            }
        }

        var discreteInputs = _server.GetDiscreteInputBuffer<byte>(1);
        foreach (var (addr, tag) in _discreteInputMap)
        {
            if (_table.Signals.TryGetValue(tag, out var signal) && signal.Value is bool b)
            {
                discreteInputs[addr] = (byte)(b ? 1 : 0);
            }
        }

        lock (_server.Lock)
        {
            _server.Update();
        }
    }

    public void EnableWriteHandling()
    {
        // 写入处理已在 SyncBuffers() 中实现
    }

    public void Stop()
    {
        try
        {
            _server?.Stop();
            _server?.Dispose();
            _server = null;
            Console.WriteLine("Modbus server stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stop error: {ex.Message}");
        }
    }

    public void Dispose() => Stop();

}
