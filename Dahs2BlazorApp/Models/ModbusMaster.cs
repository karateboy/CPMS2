using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;
using FluentModbus;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Dahs2BlazorApp.Models;

public class ModbusMaster : IDisposable
{
    private int DeviceId { get; }
    private readonly DeviceIo _deviceIo;
    private readonly DeviceMeasuringIo _measuringIo;
    private readonly DeviceSignalIo _signalIo;
    private readonly ILogger _logger;

    private record ModbusTarget(string Address, int Port);

    private static readonly ConcurrentDictionary<ModbusTarget, SemaphoreSlim> TargetLockMap = new();
    private DateTime? _lastConnectTime;
    private readonly SemaphoreSlim _targetLock;
    private TimeSpan Timeout { get; }

    public ModbusMaster(int deviceId,
        DeviceIo deviceIo,
        DeviceMeasuringIo measuringIo,
        DeviceSignalIo signalIo,
        TimeSpan timeout)
    {
        DeviceId = deviceId;
        _deviceIo = deviceIo;
        _measuringIo = measuringIo;
        _signalIo = signalIo;
        _logger = Log.ForContext<ModbusMaster>();
        Timeout = timeout;

        var device = _deviceIo.DeviceMap[DeviceId];
        var modbusTarget = GetModbusTarget(device);
        TargetLockMap.TryGetValue(modbusTarget, out var targetLock);
        _targetLock = targetLock ?? TargetLockMap.GetOrAdd(modbusTarget, new SemaphoreSlim(1, 1));
    }

    private ModbusClient? _client;

    private ModbusClient EnsureClientConnected()
    {
        var modbusTarget = GetModbusTarget(_deviceIo.DeviceMap[DeviceId]);
        if (_client is not null)
        {
            if (_client.IsConnected)
                return _client;

            DisposeClient();
        }

        if (_lastConnectTime.HasValue && DateTime.Now - _lastConnectTime.Value < TimeSpan.FromSeconds(5))
        {
            throw new Exception("Connect too frequently");
        }

        _lastConnectTime = DateTime.Now;

        var device = _deviceIo.DeviceMap[DeviceId];

        
        if (device.ModbusAddress.Contains("COM"))
        {
            _logger.Information("Modbus RTU Master Connect to {Port}", device.ModbusAddress);
            //COM1,19200,8,E,1
            var parts = device.ModbusAddress.Split(',');
            var rtuClient = new ModbusRtuClient
            {
                BaudRate = int.Parse(parts[1]),
                Parity = parts[3] switch
                {
                    "N" => Parity.None,
                    "E" => Parity.Even,
                    "O" => Parity.Odd,
                    _ => throw new Exception("Invalid parity")
                },
                StopBits = parts[4] switch
                {
                    "1" => StopBits.One,
                    "2" => StopBits.Two,
                    _ => throw new Exception("Invalid stop bits")
                },
            };
            _client = rtuClient;
            rtuClient.Connect(parts[0]);
        }
        else
        {
            var tcpClient = new ModbusTcpClient();
            _client = tcpClient;
            tcpClient.Connect(new IPEndPoint(IPAddress.Parse(device.ModbusAddress), device.Port),
                device.BigEndian ? ModbusEndianness.BigEndian : ModbusEndianness.LittleEndian);
        }

        return _client;
    }

    private void DisposeClient()
    {
        try
        {
            switch (_client)
            {
                case ModbusTcpClient tcpClient:
                    tcpClient.Dispose();
                    break;
                case ModbusRtuClient rtuClient:
                    rtuClient.Dispose();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Device {DeviceName} dispose client error",
                _deviceIo.DeviceMap[DeviceId].Name);
        }
        finally
        {
            _client = null;
        }
    }

    public async Task WriteSignalAsync(DeviceSignalIo.IDeviceSignal deviceSignal, bool value,
        CancellationToken cancellationToken)
    {
        try
        {
            await _targetLock.WaitAsync(cancellationToken).ConfigureAwait(true);
            var client = EnsureClientConnected();

            var device = _deviceIo.DeviceMap[DeviceId];
            if (!deviceSignal.Coil)
            {
                _logger.Warning("Write to discrete register is invalid. {SlaveId} {Address} {Value}", device.SlaveId,
                    deviceSignal.Address, value);
                return;
            }


            Log.Debug("Write coil {SlaveId} {Address} {Value}", device.SlaveId, deviceSignal.Address, value);

            await client.WriteSingleCoilAsync((byte)device.SlaveId,
                (ushort)deviceSignal.Address + deviceSignal.Offset, value, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Device {DeviceName} write signal error", _deviceIo.DeviceMap[DeviceId].Name);
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }
    }

    public void WriteSignal(DeviceSignalIo.IDeviceSignal deviceSignal, bool value, CancellationToken cancellationToken)
    {
        try
        {
            _targetLock.Wait(cancellationToken);
            var client = EnsureClientConnected();

            var device = _deviceIo.DeviceMap[DeviceId];
            if (!deviceSignal.Coil)
            {
                _logger.Warning("Write to discrete register is invalid. {SlaveId} {Address} {Value}", device.SlaveId,
                    deviceSignal.Address, value);
                return;
            }


            Log.Debug("Write coil {SlaveId} {Address} {Value}", device.SlaveId, deviceSignal.Address, value);

            client.WriteSingleCoil((byte)device.SlaveId,
                (ushort)deviceSignal.Address + deviceSignal.Offset, value);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Device {DeviceName} write signal error", _deviceIo.DeviceMap[DeviceId].Name);
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }
    }

    private ModbusTarget GetModbusTarget(IDevice device) =>
        new(device.ModbusAddress, device.Port);

    public List<(DeviceSignalIo.IDeviceSignal, bool)> ReadSignal(CancellationToken cancellationToken)
    {
        var recordList = new List<(DeviceSignalIo.IDeviceSignal, bool)>();

        try
        {
            _targetLock.Wait(cancellationToken);
            var client = EnsureClientConnected();
            var device = _deviceIo.DeviceMap[DeviceId];

            BitArray ReadCoils(ushort address, ushort count)
            {
                var coilMemory = client.ReadCoils((byte)device.SlaveId, address, count);
                return new BitArray(coilMemory.ToArray());
            }

            BitArray ReadDiscreteInputs(ushort address, ushort count)
            {
                var discrete = client.ReadDiscreteInputs((byte)device.SlaveId, address, count);
                return new BitArray(discrete.ToArray());
            }

            var coils = _signalIo.GetDeviceSignalListByDeviceId(DeviceId).Where(s => s.Coil).ToList();
            if (coils.Count != 0)
            {
                var addresses = coils.Select(s => (ushort)(s.Address + s.Offset)).ToArray();
                var start = addresses.Min();
                var count = (ushort)(addresses.Max() - start + 1);
                var coilBits = ReadCoils(start, count);
                recordList.AddRange(from coil in coils
                    let value = coilBits[coil.Address + coil.Offset - start]
                    select (coil, value));
            }

            var discreteInputs = _signalIo.GetDeviceSignalListByDeviceId(DeviceId).Where(s => !s.Coil).ToList();
            if (discreteInputs.Count != 0)
            {
                var addresses = discreteInputs.Select(s => (ushort)(s.Address + s.Offset)).ToArray();
                var start = addresses.Min();
                var count = (ushort)(addresses.Max() - start + 1);
                var discreteBits = ReadDiscreteInputs(start, count);
                recordList.AddRange(from discrete in discreteInputs
                    let value = discreteBits[discrete.Address + discrete.Offset - start]
                    select (discrete, value));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Device {DeviceName} read signal error {ExceptionMessage}",
                _deviceIo.DeviceMap[DeviceId].Name, ex.Message);
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }


        return recordList;
    }

    public async Task<List<(DeviceSignalIo.IDeviceSignal, bool)>> ReadSignalAsync(CancellationToken cancellationToken)
    {
        var recordList = new List<(DeviceSignalIo.IDeviceSignal, bool)>();

        try
        {
            await _targetLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            var client = EnsureClientConnected();
            cancellationToken.ThrowIfCancellationRequested();


            foreach (var signal in _signalIo.GetDeviceSignalListByDeviceId(DeviceId))
            {
                var device = _deviceIo.DeviceMap[DeviceId];

                if (signal.Coil)
                {
                    _logger.Debug("Read coil {SlaveId} {Address}", device.SlaveId, signal.Address);
                    var coilMemory =
                        await client.ReadCoilsAsync((byte)device.SlaveId, (ushort)signal.Address + signal.Offset, 1,
                            cancellationToken);
                    var ret = new BitArray(coilMemory.ToArray());
                    recordList.Add((signal, ret[0]));
                }
                else
                {
                    _logger.Debug("Read discrete {SlaveId} {Address}", device.SlaveId, signal.Address);
                    var discrete =
                        await client.ReadDiscreteInputsAsync((byte)device.SlaveId,
                            (ushort)signal.Address + signal.Offset, 1, cancellationToken);
                    var ret = new BitArray(discrete.ToArray());
                    recordList.Add((signal, ret[0]));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Device {DeviceName} read signal error: {ErrorMsg}",
                _deviceIo.DeviceMap[DeviceId].Name, ex.Message);
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }

        return recordList;
    }

    private decimal ReadInputReg(IDevice device, IDeviceMeasuring measuring)
    {
        var dataTypeDef =
            DeviceMeasuringIo.DataTypeDefMap[(DeviceMeasuringIo.ModbusDataType)measuring.DataType];

        if (_client is null)
            throw new Exception("Client is null");

        switch (dataTypeDef.Type)
        {
            case DeviceMeasuringIo.ModbusDataType.Double:
                if (measuring.MidEndian == false)
                {
                    var doubleMemory = _client.ReadInputRegisters<double>(
                        (byte)device.SlaveId,
                        (ushort)measuring.Address, 1);
                    return
                        new decimal(Math.Round(doubleMemory[0], 2,
                            MidpointRounding.AwayFromZero));
                }

                var shortDoubleMemory = _client.ReadInputRegisters<short>(
                    (byte)device.SlaveId,
                    (ushort)measuring.Address, 4);
                return
                    new decimal(Math.Round(shortDoubleMemory.GetMidLittleEndian<double>(0),
                        2, MidpointRounding.AwayFromZero));

            case DeviceMeasuringIo.ModbusDataType.Float:
                if (measuring.MidEndian == false)
                {
                    var floatMemory = _client.ReadInputRegisters<float>((byte)device.SlaveId,
                        (ushort)measuring.Address, 1);
                    return
                        new decimal(Math.Round(floatMemory[0], 2, MidpointRounding.AwayFromZero));
                }

                var shortFloatMemory = _client.ReadInputRegisters<short>(
                    (byte)device.SlaveId,
                    (ushort)measuring.Address, 2);

                return
                    new decimal(Math.Round(shortFloatMemory.GetMidLittleEndian<float>(0),
                        2, MidpointRounding.AwayFromZero));

            case DeviceMeasuringIo.ModbusDataType.Int16:
                var int16Memory = _client.ReadInputRegisters<short>((byte)device.SlaveId,
                    (ushort)measuring.Address, 1);

                return measuring.MidEndian
                    ? new decimal(BinaryPrimitives.ReverseEndianness(int16Memory[0]))
                    : new decimal(int16Memory[0]);

            case DeviceMeasuringIo.ModbusDataType.Uint16:
                var uint16Memory = _client.ReadInputRegisters<ushort>((byte)device.SlaveId,
                    (ushort)measuring.Address, 1);

                return measuring.MidEndian
                    ? new decimal(BinaryPrimitives.ReverseEndianness(uint16Memory[0]))
                    : new decimal(uint16Memory[0]);

            case DeviceMeasuringIo.ModbusDataType.Int32:
                if (measuring.MidEndian == false)
                {
                    var int32Memory = _client.ReadInputRegisters<int>((byte)device.SlaveId,
                        (ushort)measuring.Address, 1);
                    return new decimal(int32Memory[0]);
                }

                var shortIntMemory = _client.ReadInputRegisters<short>(
                    (byte)device.SlaveId,
                    (ushort)measuring.Address, 2);
                return
                    new decimal(shortIntMemory.GetMidLittleEndian<int>(0));

            default:
                throw new ArgumentOutOfRangeException(nameof(measuring), $@"Invalid data type {dataTypeDef.Type}");
        }
    }

    private decimal ReadHoldingReg(IDevice device, IDeviceMeasuring measuring)
    {
        var dataTypeDef =
            DeviceMeasuringIo.DataTypeDefMap[(DeviceMeasuringIo.ModbusDataType)measuring.DataType];

        if (_client is null)
            throw new Exception("Client is null");

        switch (dataTypeDef.Type)
        {
            case DeviceMeasuringIo.ModbusDataType.Double:
                if (measuring.MidEndian == false)
                {
                    var doubleMemory = _client.ReadHoldingRegisters<double>(
                        (byte)device.SlaveId,
                        (ushort)measuring.Address, 1);
                    return
                        new decimal(Math.Round(doubleMemory[0], 2,
                            MidpointRounding.AwayFromZero));
                }

                var shortDoubleMemory = _client.ReadHoldingRegisters<short>(
                    (byte)device.SlaveId,
                    (ushort)measuring.Address, 4);
                return
                    new decimal(Math.Round(shortDoubleMemory.GetMidLittleEndian<double>(0),
                        2, MidpointRounding.AwayFromZero));

            case DeviceMeasuringIo.ModbusDataType.Float:
                if (measuring.MidEndian == false)
                {
                    var floatMemory = _client.ReadHoldingRegisters<float>(
                        (byte)device.SlaveId,
                        (ushort)measuring.Address, 1);
                    return
                        new decimal(Math.Round(floatMemory[0], 2,
                            MidpointRounding.AwayFromZero));
                }

                var shortFloatMemory = _client.ReadHoldingRegisters<short>(
                    (byte)device.SlaveId,
                    (ushort)measuring.Address, 2);
                return
                    new decimal(Math.Round(shortFloatMemory.GetMidLittleEndian<float>(0),
                        2, MidpointRounding.AwayFromZero));


            case DeviceMeasuringIo.ModbusDataType.Int16:
                var int16Memory = _client.ReadHoldingRegisters<short>((byte)device.SlaveId,
                    (ushort)measuring.Address, 1);
                return measuring.MidEndian
                    ? new decimal(BinaryPrimitives.ReverseEndianness(int16Memory[0]))
                    : new decimal(int16Memory[0]);

            case DeviceMeasuringIo.ModbusDataType.Uint16:
                var uint16Memory = _client.ReadHoldingRegisters<ushort>((byte)device.SlaveId,
                    (ushort)measuring.Address, 1);
                return measuring.MidEndian
                    ? new decimal(BinaryPrimitives.ReverseEndianness(uint16Memory[0]))
                    : new decimal(uint16Memory[0]);

            case DeviceMeasuringIo.ModbusDataType.Int32:
                if (measuring.MidEndian == false)
                {
                    var int32Memory = _client.ReadHoldingRegisters<int>((byte)device.SlaveId,
                        (ushort)measuring.Address, 1);
                    return new decimal(int32Memory[0]);
                }

                var shortIntMemory = _client.ReadHoldingRegisters<short>(
                    (byte)device.SlaveId,
                    (ushort)measuring.Address, 2);
                return
                    new decimal(shortIntMemory.GetMidLittleEndian<int>(0));

            default:
                throw new ArgumentOutOfRangeException(nameof(measuring), $@"Invalid data type {dataTypeDef.Type}");
        }
    }

    public (List<(IDeviceMeasuring, decimal?)>, List<(DeviceSignalIo.IDeviceSignal, bool)>)
        ReadDataAndSignal(bool? onlyOpFilter, bool includeSignal, CancellationToken cancellationToken)
    {
        var recordMap = new Dictionary<IDeviceMeasuring, decimal?>();
        var signalList = new List<(DeviceSignalIo.IDeviceSignal, bool)>();
        // Init recordMap with null value
        foreach (var measuring in _measuringIo.GetDeviceMeasuringList(deviceId: DeviceId))
        {
            if (onlyOpFilter.HasValue)
            {
                if (onlyOpFilter.Value && measuring.Sid != MonitorTypeCode.G11.ToString())
                    continue;

                if (!onlyOpFilter.Value && measuring.Sid == MonitorTypeCode.G11.ToString())
                    continue;
            }

            recordMap[measuring] = null;
        }

        try
        {
            _targetLock.Wait(cancellationToken);
            var client = EnsureClientConnected();
            var device = _deviceIo.DeviceMap[DeviceId];
            foreach (var measuring in _measuringIo.GetDeviceMeasuringList(deviceId: DeviceId))
            {
                if (onlyOpFilter.HasValue)
                {
                    if (onlyOpFilter.Value && measuring.Sid != MonitorTypeCode.G11.ToString())
                        continue;

                    if (!onlyOpFilter.Value && measuring.Sid == MonitorTypeCode.G11.ToString())
                        continue;
                }


                try
                {
                    if (measuring.InputReg)
                    {
                        recordMap[measuring] = ReadInputReg(device, measuring);
                    }
                    else
                    {
                        recordMap[measuring] = ReadHoldingReg(device, measuring);
                    }
                }
                catch (Exception)
                {
                    _logger.Debug("Device {DeviceId} read error SlaveId={SlaveId} Addr={Address}", DeviceId,
                        device.SlaveId, (ushort)measuring.Address);
                    throw;
                }
            }

            BitArray ReadCoils(ushort address, ushort count)
            {
                var coilMemory = client.ReadCoils((byte)device.SlaveId, address, count);
                return new BitArray(coilMemory.ToArray());
            }

            BitArray ReadDiscreteInputs(ushort address, ushort count)
            {
                var discrete = client.ReadDiscreteInputs((byte)device.SlaveId, address, count);
                return new BitArray(discrete.ToArray());
            }

            if (includeSignal)
            {
                var coils = _signalIo.GetDeviceSignalListByDeviceId(DeviceId).Where(s => s.Coil).ToList();
                if (coils.Count != 0)
                {
                    var addresses = coils.Select(s => (ushort)(s.Address + s.Offset)).ToArray();
                    var start = addresses.Min();
                    var count = (ushort)(addresses.Max() - start + 1);
                    var coilBits = ReadCoils(start, count);
                    signalList.AddRange(from coil in coils
                        let value = coilBits[coil.Address + coil.Offset - start]
                        select (coil, value));
                }

                var discreteInputs = _signalIo.GetDeviceSignalListByDeviceId(DeviceId).Where(s => !s.Coil).ToList();
                if (discreteInputs.Count != 0)
                {
                    var addresses = discreteInputs.Select(s => (ushort)(s.Address + s.Offset)).ToArray();
                    var start = addresses.Min();
                    var count = (ushort)(addresses.Max() - start + 1);
                    var discreteBits = ReadDiscreteInputs(start, count);
                    signalList.AddRange(from discrete in discreteInputs
                        let value = discreteBits[discrete.Address + discrete.Offset - start]
                        select (discrete, value));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("ReadDataAndSignal: Pipe {PipeId} Device {DeviceName} error: {Exception} {ExceptionType}",
                _deviceIo.DeviceMap[DeviceId].PipeId,
                _deviceIo.DeviceMap[DeviceId].Name,
                ex.Message,
                ex.GetType());
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }

        // If the operation is cancelled, dispose the client
        if (cancellationToken.IsCancellationRequested)
            DisposeClient();

        var list = recordMap.Select(pair => (pair.Key, pair.Value))
            .Where(pair => pair.Item2.HasValue)
            .ToList();

        return (list, signalList);
    }

    public async Task<List<(IDeviceMeasuring, decimal)>> ReadDataAsync(bool? onlyOpFilter,
        CancellationToken cancellationToken)
    {
        var recordList = new List<(IDeviceMeasuring, decimal)>();

        try
        {
            await _targetLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            var client = EnsureClientConnected();

            foreach (var measuring in _measuringIo.GetDeviceMeasuringList(deviceId: DeviceId))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var device = _deviceIo.DeviceMap[DeviceId];
                var dataTypeDef =
                    DeviceMeasuringIo.DataTypeDefMap[(DeviceMeasuringIo.ModbusDataType)measuring.DataType];

                if (onlyOpFilter.HasValue)
                {
                    if (onlyOpFilter.Value && measuring.Sid != MonitorTypeCode.G11.ToString())
                        continue;

                    if (!onlyOpFilter.Value && measuring.Sid == MonitorTypeCode.G11.ToString())
                        continue;
                }


                try
                {
                    if (measuring.InputReg)
                    {
                        switch (dataTypeDef.Type)
                        {
                            case DeviceMeasuringIo.ModbusDataType.Double:
                                if (measuring.MidEndian == false)
                                {
                                    var doubleMemory = await client.ReadInputRegistersAsync<double>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 1, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(doubleMemory.Span[0], 2,
                                            MidpointRounding.AwayFromZero))));
                                }
                                else
                                {
                                    var shortDoubleMemory = await client.ReadInputRegistersAsync<short>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 4, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(shortDoubleMemory.Span.GetMidLittleEndian<double>(0),
                                            2, MidpointRounding.AwayFromZero))));
                                }

                                break;
                            case DeviceMeasuringIo.ModbusDataType.Float:
                                if (measuring.MidEndian == false)
                                {
                                    var floatMemory = await client.ReadInputRegistersAsync<float>((byte)device.SlaveId,
                                        (ushort)measuring.Address, 1, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(floatMemory.Span[0], 2,
                                            MidpointRounding.AwayFromZero))));
                                }
                                else
                                {
                                    var shortFloatMemory = await client.ReadInputRegistersAsync<short>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 2, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(shortFloatMemory.Span.GetMidLittleEndian<float>(0),
                                            2, MidpointRounding.AwayFromZero))));
                                }

                                //var shortSpan = MemoryMarshal.Cast<byte, short>(byteSpan);
                                //float midLittleEndian = shortSpan.GetMidLittleEndian<float>(0);
                                //Logger.Debug("{Sid} {ByteHexStr} {FloatValue}", measuring.Sid, 
                                //    BitConverter.ToString(byteSpan.ToArray()), midLittleEndian);
                                //

                                break;
                            case DeviceMeasuringIo.ModbusDataType.Int16:
                                var int16Memory = await client.ReadInputRegistersAsync<short>((byte)device.SlaveId,
                                    (ushort)measuring.Address, 1, cancellationToken);
                                recordList.Add((measuring, new decimal(int16Memory.Span[0])));
                                break;
                            case DeviceMeasuringIo.ModbusDataType.Uint16:
                                var uint16Memory = await client.ReadInputRegistersAsync<ushort>((byte)device.SlaveId,
                                    (ushort)measuring.Address, 1, cancellationToken);
                                recordList.Add((measuring, new decimal(uint16Memory.Span[0])));
                                break;
                            case DeviceMeasuringIo.ModbusDataType.Int32:
                                if (measuring.MidEndian == false)
                                {
                                    var int32Memory = await client.ReadInputRegistersAsync<int>((byte)device.SlaveId,
                                        (ushort)measuring.Address, 1, cancellationToken);
                                    recordList.Add((measuring, new decimal(int32Memory.Span[0])));
                                }
                                else
                                {
                                    var shortIntMemory = await client.ReadInputRegistersAsync<short>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 2, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(shortIntMemory.Span.GetMidLittleEndian<int>(0))));
                                }

                                break;
                        }
                    }
                    else
                    {
                        switch (dataTypeDef.Type)
                        {
                            case DeviceMeasuringIo.ModbusDataType.Double:
                                if (measuring.MidEndian == false)
                                {
                                    var doubleMemory = await client.ReadHoldingRegistersAsync<double>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 1, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(doubleMemory.Span[0], 2,
                                            MidpointRounding.AwayFromZero))));
                                }
                                else
                                {
                                    var shortDoubleMemory = await client.ReadHoldingRegistersAsync<short>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 4, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(shortDoubleMemory.Span.GetMidLittleEndian<double>(0),
                                            2, MidpointRounding.AwayFromZero))));
                                }

                                break;
                            case DeviceMeasuringIo.ModbusDataType.Float:
                                if (measuring.MidEndian == false)
                                {
                                    var floatMemory = await client.ReadHoldingRegistersAsync<float>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 1, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(floatMemory.Span[0], 2,
                                            MidpointRounding.AwayFromZero))));
                                }
                                else
                                {
                                    var shortFloatMemory = await client.ReadHoldingRegistersAsync<short>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 2, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(Math.Round(shortFloatMemory.Span.GetMidLittleEndian<float>(0),
                                            2, MidpointRounding.AwayFromZero))));
                                }

                                break;
                            case DeviceMeasuringIo.ModbusDataType.Int16:
                                var int16Memory = await client.ReadHoldingRegistersAsync<short>((byte)device.SlaveId,
                                    (ushort)measuring.Address, 1, cancellationToken);
                                recordList.Add((measuring, new decimal(int16Memory.Span[0])));
                                break;
                            case DeviceMeasuringIo.ModbusDataType.Uint16:
                                var uint16Memory = await client.ReadHoldingRegistersAsync<ushort>((byte)device.SlaveId,
                                    (ushort)measuring.Address, 1, cancellationToken);
                                recordList.Add((measuring, new decimal(uint16Memory.Span[0])));
                                break;
                            case DeviceMeasuringIo.ModbusDataType.Int32:
                                if (measuring.MidEndian == false)
                                {
                                    var int32Memory = await client.ReadHoldingRegistersAsync<int>((byte)device.SlaveId,
                                        (ushort)measuring.Address, 1, cancellationToken);
                                    recordList.Add((measuring, new decimal(int32Memory.Span[0])));
                                }
                                else
                                {
                                    var shortIntMemory = await client.ReadHoldingRegistersAsync<short>(
                                        (byte)device.SlaveId,
                                        (ushort)measuring.Address, 2, cancellationToken);
                                    recordList.Add((measuring,
                                        new decimal(shortIntMemory.Span.GetMidLittleEndian<int>(0))));
                                }

                                break;
                        }
                    }
                }
                catch (Exception)
                {
                    _logger.Debug("Device {DeviceId} read error SlaveId={SlaveId} Addr={Address}", DeviceId,
                        device.SlaveId, (ushort)measuring.Address);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("ReadData: Device {DeviceName} error: {Exception} {ExceptionType}",
                _deviceIo.DeviceMap[DeviceId].Name, ex.Message,
                ex.GetType());
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }

        return recordList;
    }

    public void WriteData(IDeviceOutput deviceOutput, decimal value, CancellationToken cancellationToken)
    {
        try
        {
            _targetLock.Wait(cancellationToken);
            var client = EnsureClientConnected();

            var device = _deviceIo.DeviceMap[DeviceId];

            var dataTypeDef =
                DeviceMeasuringIo.DataTypeDefMap[(DeviceMeasuringIo.ModbusDataType)deviceOutput.DataType];

            try
            {
                switch (dataTypeDef.Type)
                {
                    case DeviceMeasuringIo.ModbusDataType.Double:
                        client.WriteSingleRegister((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, BitConverter.GetBytes(decimal.ToDouble(value)));
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Float:
                        client.WriteSingleRegister((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, BitConverter.GetBytes(decimal.ToSingle(value)));
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Int16:
                        client.WriteSingleRegister((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, decimal.ToInt16(value));
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Uint16:
                        client.WriteSingleRegister((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, decimal.ToUInt16(value));
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Int32:
                        client.WriteSingleRegister((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, BitConverter.GetBytes(decimal.ToInt32(value)));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown data type {dataTypeDef.Type}");
                }
            }
            catch (Exception)
            {
                _logger.Debug("Device {DeviceName} write error DeviceId={SlaveId} Addr={Address}",
                    _deviceIo.DeviceMap[DeviceId].Name,
                    device.SlaveId, (ushort)deviceOutput.Address);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("WriteData: Device {DeviceName} error: {Exception} {ExceptionType}",
                _deviceIo.DeviceMap[DeviceId].Name, ex.Message,
                ex.GetType());
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }
    }

    public async Task WriteDataAsync(IDeviceOutput deviceOutput, decimal value, CancellationToken cancellationToken)
    {
        try
        {
            await _targetLock.WaitAsync(cancellationToken).ConfigureAwait(true);
            var client = EnsureClientConnected();

            var device = _deviceIo.DeviceMap[DeviceId];

            var dataTypeDef =
                DeviceMeasuringIo.DataTypeDefMap[(DeviceMeasuringIo.ModbusDataType)deviceOutput.DataType];

            try
            {
                switch (dataTypeDef.Type)
                {
                    case DeviceMeasuringIo.ModbusDataType.Double:
                        await client.WriteSingleRegisterAsync((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, BitConverter.GetBytes(decimal.ToDouble(value)),
                            cancellationToken);
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Float:
                        await client.WriteSingleRegisterAsync((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, BitConverter.GetBytes(decimal.ToSingle(value)),
                            cancellationToken);
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Int16:
                        await client.WriteSingleRegisterAsync((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, decimal.ToInt16(value),
                            cancellationToken);
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Uint16:
                        await client.WriteSingleRegisterAsync((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, decimal.ToUInt16(value),
                            cancellationToken);
                        break;
                    case DeviceMeasuringIo.ModbusDataType.Int32:
                        await client.WriteSingleRegisterAsync((byte)device.SlaveId,
                            (ushort)deviceOutput.Address, BitConverter.GetBytes(decimal.ToInt32(value)),
                            cancellationToken);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown data type {dataTypeDef.Type}");
                }
            }
            catch (Exception)
            {
                _logger.Debug("Device {DeviceId} write error DeviceId={SlaveId} Addr={Address}", DeviceId,
                    device.SlaveId, (ushort)deviceOutput.Address);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("WriteDataAsync: Device {DeviceName} error: {Exception} {ExceptionType}",
                _deviceIo.DeviceMap[DeviceId].Name, ex.Message,
                ex.GetType());
            DisposeClient();
        }
        finally
        {
            _targetLock.Release();
        }
    }

    public void Dispose()
    {
        DisposeClient();
    }
}