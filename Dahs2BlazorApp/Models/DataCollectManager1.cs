using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Models;

public partial class DataCollectManager
{
    public record DeviceRecord(int DeviceId, DateTime RecordTime, Record RecordValue);

    private record SignalRecord(int SignalId, DateTime RecordTime, bool Value);

    private static ImmutableDictionary<int,
        ConcurrentDictionary<string, DeviceRecord>> _pipeDeviceRecordMap =
        ImmutableDictionary.Create<int, ConcurrentDictionary<string, DeviceRecord>>();

    public record InvalidReason(string Code, string Reason);

    private static ImmutableDictionary<int,
        ConcurrentDictionary<string, InvalidReason>> _pipeMonitorTypeInvalidReasonMap =
        ImmutableDictionary.Create<int, ConcurrentDictionary<string, InvalidReason>>();

    public static void UpdatePipeMonitorTypeMap(int pipeId, int deviceId, string monitorType, Record record)
    {
        _pipeDeviceRecordMap[pipeId][monitorType] = new DeviceRecord(deviceId, DateTime.Now, record);
    }

    private static void UpdatePipeMonitorTypeInvalidReason(int pipeId, string monitorType, InvalidReason reason)
    {
        _pipeMonitorTypeInvalidReasonMap[pipeId][monitorType] = reason;
    }

    public static InvalidReason? GetPipeMonitorTypeInvalidReason(int pipeId, string monitorType)
    {
        _pipeMonitorTypeInvalidReasonMap[pipeId].TryGetValue(monitorType, out var invalidReason);
        return invalidReason;
    }

    private void RemoveOutdatedData(int pipeId, DateTime updateTime, TimeSpan timeSpan,
        ISet<string> checkingMonitorTypes)
    {
        var recordMap = _pipeDeviceRecordMap[pipeId];
        var limit = updateTime - timeSpan;

        //Remove data that is before time span
        foreach (var mt in checkingMonitorTypes.Intersect(recordMap.Keys))
        {
            if (recordMap[mt].RecordTime < limit)
            {
                recordMap.TryRemove(mt, out _);
            }
        }
    }

    private static void AddCalculatedMeasuring(int pipeId)
    {
        var recordMap = _pipeDeviceRecordMap[pipeId];
        foreach (var mt in SiteConfig.PipeMonitorTypes[pipeId])
        {
            if (mt.Calculation is null)
                continue;

            var calculated = mt.Calculation;
            if (calculated.InputSids.Any(sid => recordMap.ContainsKey(sid)) == false)
                continue;

            var values = calculated.InputSids.Select(sid => recordMap[sid].RecordValue.Value).ToList();
            var status = calculated.InputSids.Select(sid => recordMap[sid].RecordValue.Status).Max();
            var value = calculated.CalculateFunc(values);
            recordMap[mt.Sid.ToString()] = new DeviceRecord(0, DateTime.Now,
                new Record
                {
                    Value = value,
                    Status = status ?? "10",
                    Baf = 1m
                });
        }
    }

    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, SignalRecord>> PipeSignalMap = new();

    public static void UpdateSignalMap(int pipeId, int signalId, bool value)
    {
        var signal = new SignalRecord(signalId, DateTime.Now, value);
        PipeSignalMap[pipeId].AddOrUpdate(signalId, signal, (_, _) => signal);
    }

    public static void RemoveOutdatedSignal(int pipeId, TimeSpan ts)
    {
        if (PipeSignalMap.TryGetValue(pipeId, out var signalMap))
        {
            HashSet<int> outdatedSignalIds = new();
            foreach (var pair in signalMap)
            {
                if (pair.Value.RecordTime < DateTime.Now - ts)
                    outdatedSignalIds.Add(pair.Key);
            }

            foreach (var signalId in outdatedSignalIds)
            {
                signalMap.TryRemove(signalId, out _);
            }
        }
    }
}