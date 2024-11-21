using System.Collections.Concurrent;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Models;

public partial class DataCollectManager
{
    private record MonitorTypeKey(int PipeId, string MonitorType);

    private readonly ConcurrentDictionary<MonitorTypeKey, string> _monitorTypeStatusMap = new();

    private void GenerateAlarms(int pipeId, DateTime createDate, IReadOnlyDictionary<string, Record> recordMap)
    {
        var alarmsStatus = new[] { "11", "30", "40", "10" };
        foreach (var (monitorType, record) in recordMap)
        {
            var mtKey = new MonitorTypeKey(pipeId, monitorType);
            if (_monitorTypeStatusMap.TryGetValue(mtKey, out var status))
            {
                if (status == record.Status[2..])
                    continue;
            }

            if (!alarmsStatus.Contains(record.Status[2..]))
                continue;

            var mt = _monitorTypeIo.PipeMonitorTypeMap[pipeId][monitorType];
            var statusName = MonitorTypeState.StateNameMap.TryGetValue(record.Status[2..], out var stateName)
                ? stateName
                : record.Status;

            if (record.Status[2..] == "10")
            {
                _ = _alarmIo.AddAlarm(
                    AlarmIo.AlarmLevel.Info,
                    $"{_pipeIo.PipeMap[pipeId].Name} {mt.Name} {createDate:g} {record.Value} 恢復 {statusName}");
            }else {
                var level = record.Status[2..] switch
                {
                    "11" => AlarmIo.AlarmLevel.Warning,
                    _ => AlarmIo.AlarmLevel.Error,
                };
                
                _ = _alarmIo.AddAlarm(level,
                    $"{_pipeIo.PipeMap[pipeId].Name} {mt.Name} {createDate:g} {record.Value} 觸發 {statusName}");
            }
            
            _monitorTypeStatusMap[mtKey] = record.Status[2..];
        }
    }

    private void OutputAdjustValues(int pipeId, IReadOnlyDictionary<string, Record> recordMap)
    {
        
        var outputDevices = 
            _deviceIo.DeviceMap.Values.Where(d => d.PipeId == pipeId && d.Output).ToList();
        _logger.LogDebug("OutputAdjustValues {PipeId} {DeviceCount}", pipeId, outputDevices.Count);
        
        foreach (var device in outputDevices)
        {
            if (!_deviceOutputIo.DeviceOutputMap.TryGetValue(device.Id, out var deviceOutputs))
            {
                _logger.LogDebug("Device {DeviceId} is not configured to output", device.Id);
                continue;
            }
                
            
            foreach (var deviceOutput in deviceOutputs)
            {
                if (!SiteConfig.DeviceOutputConfigMap.TryGetValue(deviceOutput.OutputConfigId, out var outputConfig))
                {
                    _logger.LogError("DeviceOutputConfigMap {OutputConfigId} not found", deviceOutput.OutputConfigId);
                    continue;
                }
                
                var outputValue = outputConfig.OutputGenerator(pipeId, recordMap);
                _logger.LogDebug("DeviceOutput pipeId={PipeId} {DeviceId} {OutputValue}", 
                    pipeId, deviceOutput.DeviceId, outputValue?.ToString("F2"));
                
                if(outputValue is not null)
                    DeviceManager.WriteValue(deviceOutput, outputValue.Value);
            }
        }
    }
}