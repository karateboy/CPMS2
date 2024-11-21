using System.Collections.Concurrent;
using System.Diagnostics;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;
using IoTClient.Clients.PLC;
using IoTClient.Enums;
using Serilog;

namespace Dahs2BlazorApp.Models;

public static class DeviceManager
{
    enum DeviceState
    {
        Normal,
        Calibrating,
    }

    record DeviceControl(ModbusClient ModbusClient, DeviceState State) : IDisposable
    {
        public void Dispose()
        {
            ModbusClient.Dispose();
        }
    }

    private static readonly ConcurrentDictionary<int, DeviceControl> DeviceControlMap = new();
    private static DeviceIo? _deviceIo;
    private static DeviceMeasuringIo? _deviceMeasuringIo;
    private static DeviceSignalIo? _deviceSignalIo;

    public static void Init(DeviceIo deviceIo, DeviceMeasuringIo deviceMeasuringIo, DeviceSignalIo deviceSignalIo)
    {
        _deviceIo = deviceIo;
        _deviceMeasuringIo = deviceMeasuringIo;
        _deviceSignalIo = deviceSignalIo;
    }

    private static void StartDevice(int deviceId, TimeSpan timeout)
    {
        if (_deviceIo == null) return;
        if (_deviceMeasuringIo == null) return;
        if (_deviceSignalIo == null) return;
        var modbusReader = new ModbusClient(deviceId, _deviceIo, _deviceMeasuringIo, _deviceSignalIo, timeout);
        DeviceControlMap[deviceId] = new DeviceControl(modbusReader, DeviceState.Normal);
    }

    public static void StopDevice(int deviceId)
    {
        if (DeviceControlMap.TryRemove(deviceId, out var deviceCtl))
        {
        }
    }

    private static Task ReadDeviceData(int deviceId, CancellationToken cancellationToken)
    {
        if (_deviceIo == null) return Task.CompletedTask;
        return Task.Run(() =>
        {
            if (!DeviceControlMap.TryGetValue(deviceId, out var deviceCtl)) return;
            
            var (data, signalData) =
                deviceCtl.ModbusClient.ReadDataAndSignal(null, includeSignal: true, cancellationToken);
            
            HandleDeviceData(_deviceIo.DeviceMap[deviceId], data);
            
            foreach (var (signal, value) in signalData)
            {
                DataCollectManager.UpdateSignalMap(signal.PipeId, signal.Id, value);
            }
        }, cancellationToken);
    }

    public static List<(IDeviceMeasuring, decimal?)> ReadDeviceDataWithoutSignal(int deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceControlMap.TryGetValue(deviceId, out var deviceCtl))
            return new List<(IDeviceMeasuring, decimal?)>();

        var (dataList, _) = deviceCtl.ModbusClient.ReadDataAndSignal(null, includeSignal: false, cancellationToken);
        return dataList;
    }

    private static Task ReadMitsubishiPLC(int pipeid, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            //1、Instantiate the client-enter the correct IP and port
            MitsubishiClient client = new MitsubishiClient(MitsubishiVersion.A_1E, "192.168.2.10", 2000);
            
            client.Open();
            //3、Read operation
            var result = client.ReadUInt32("W0");
            
            var record = new Record
            {
                Value = result.IsSucceed ? Convert.ToDecimal(result.Value) : null,
                Status = result.IsSucceed ? "10" : "32"
            };

            DataCollectManager.UpdatePipeMonitorTypeMap(pipeid, 100, MonitorTypeCode.SecondTemp.ToString(), record);
            
            client.Close();
        }, cancellationToken);
    }

    public static Task ReadPipeDevices(int pipeId, TimeSpan timeout)
    {
        if (_deviceIo == null) return Task.CompletedTask;
        if (_deviceMeasuringIo == null) return Task.CompletedTask;

        DataCollectManager.RemoveOutdatedSignal(pipeId, timeout * 6);
        var devices = _deviceIo.GetDevicesByPipeId(pipeId);
        CancellationTokenSource cts = new();
        List<Task> tasks = new();
        foreach (var device in devices)
        {

            if (!DeviceControlMap.ContainsKey(device.Id))
                StartDevice(device.Id, timeout);

            tasks.Add(ReadDeviceData(device.Id, cts.Token));
        }

        //tasks.Add(ReadMitsubishiPLC(pipeId, cts.Token));

        Log.Debug("Pipe {PipeId} ReadOpDevice #task={TasksCount}", pipeId, tasks.Count);
        var readTask = Task.WhenAll(tasks).ContinueWith(_ =>
        {
            Log.Debug("ReadPipeDevices> Pipe {PipeId} Completed", pipeId);
            cts.Cancel();
            cts.Dispose();
        }, cts.Token);
        var timeoutTask = Task.Delay(timeout, cts.Token).ContinueWith(_ =>
        {
            Log.Error("ReadPipeDevices> Pipe {PipeId} Timeout", pipeId);
            cts.Cancel();
            cts.Dispose();
        }, cts.Token);

        return Task.WhenAny(readTask, timeoutTask);
    }

    public static void ResumeDevice(int deviceId) => ChangeDeviceState(deviceId, DeviceState.Normal);
    public static void CalibrateDevice(int deviceId) => ChangeDeviceState(deviceId, DeviceState.Calibrating);

    private static void ChangeDeviceState(int deviceId, DeviceState state)
    {
        if (DeviceControlMap.TryGetValue(deviceId, out var deviceCtl))
        {
            var devCtrl = deviceCtl with { State = state };
            DeviceControlMap.TryUpdate(deviceId, devCtrl, deviceCtl);
        }
        else
            Log.Error("ChangeDeviceState {DeviceId} not found", deviceId);
    }

    public static void WriteSignal(DeviceSignalIo.IDeviceSignal deviceSignal, bool value,
        CancellationToken cancellationToken = default)
    {
        if (DeviceControlMap.TryGetValue(deviceSignal.DeviceId, out var deviceCtl))
            deviceCtl.ModbusClient.WriteSignal(deviceSignal, value, cancellationToken);
    }

    public static void WriteValue(IDeviceOutput deviceOutput, decimal value,
        CancellationToken cancellationToken = default)
    {
        if (DeviceControlMap.TryGetValue(deviceOutput.DeviceId, out var deviceCtl))
            deviceCtl.ModbusClient.WriteData(deviceOutput, value, cancellationToken);
    }

    public static decimal? ConvertMeasuring(int pipeId, IDeviceMeasuring measuring, decimal data)
    {
        var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId][measuring.Sid];
        if (typeDef.InterpolationFactor is { } factor)
        {
            // ReadMin : 4ma, ReadMax : 20ma, disconnect : 2ma
            var disconnectValue = factor.ReadMin - (factor.ReadMax - factor.ReadMin) / (20 - 4) * 2;
            if (data <= disconnectValue)
                return null;

            var adjustValue = typeDef.RangeMin + (typeDef.RangeMax - typeDef.RangeMin) /
                (factor.ReadMax - factor.ReadMin) *
                (data - factor.ReadMin);

            return adjustValue;
        }

        return (data - typeDef.Offset) * typeDef.Multiplier;
    }

    private static void HandleDeviceData(IDevice device, List<(IDeviceMeasuring, decimal?)> measuringDataList)
    {
        foreach (var (measuring, dataOpt) in measuringDataList)
        {
            var status = DeviceControlMap[device.Id].State switch
            {
                DeviceState.Normal => "10",
                DeviceState.Calibrating => "20",
                _ => "30"
            };

            var value = dataOpt.HasValue ? ConvertMeasuring(device.PipeId, measuring, dataOpt.Value) : null;

            var record = new Record
            {
                Value = value,
                Status = value is null ? "32" : status
            };

            DataCollectManager.UpdatePipeMonitorTypeMap(device.PipeId, device.Id, measuring.Sid, record);
        }
    }
}