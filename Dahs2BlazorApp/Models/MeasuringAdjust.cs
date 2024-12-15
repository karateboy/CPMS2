using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;
using Serilog;

// 1: 6minutes, 15minutes
// 2: 1hour
namespace Dahs2BlazorApp.Models;

public class MeasuringAdjust
{
    private readonly MonitorTypeIo _monitorTypeIo;
    private readonly RecordIo _recordIo;
    private readonly PipeIo _pipeIo;
    private readonly ILogger<MeasuringAdjust> _logger;
    private readonly AlarmIo _alarmIo;

    public MeasuringAdjust(MonitorTypeIo monitorTypeIo,
        RecordIo recordIo,
        PipeIo pipeIo,
        ILogger<MeasuringAdjust> logger,
        AlarmIo alarmIo)
    {
        _monitorTypeIo = monitorTypeIo;
        _recordIo = recordIo;
        _pipeIo = pipeIo;
        _logger = logger;
        _alarmIo = alarmIo;
    }

    private static decimal? GetAvg(IEnumerable<Record> values)
    {
        var nonemptyValues = values.Where(record => record.Value.HasValue).ToList();
        if (nonemptyValues.Count == 0)
            return null;
        else
            return Math.Round(nonemptyValues.Select(record => record.Value.GetValueOrDefault(0)).Average(), 2,
                MidpointRounding.AwayFromZero);
    }

    private static decimal? GetSum(IEnumerable<Record> values)
    {
        var nonemptyValues = values.Where(record => record.Value.HasValue).ToList();
        if (nonemptyValues.Count == 0)
            return null;
        else
            return Math.Round(nonemptyValues.Select(record => record.Value.GetValueOrDefault(0)).Sum(), 2,
                MidpointRounding.AwayFromZero);
    }

    private static string GetMaxCountKey(IReadOnlyDictionary<string, int> dataStateCountMap, string[] order)
    {
        return dataStateCountMap.OrderByDescending(kv => kv.Value).ThenBy(kv =>
        {
            int idx = Array.IndexOf(order, kv.Key);
            return idx >= 0 ? idx : order.Length;
        }).Select(kv => kv.Key).First();
    }

    private string[] accumulativeMonitorTypes = { "WaterQuantity", "BFWeightMod" };

    private Dictionary<string, Record> Calculate5MinData(int pipeId,
        IReadOnlyDictionary<DateTime, Dictionary<string, Record>> timeRecordMap)
    {
        Dictionary<string, Record> ret = new Dictionary<string, Record>();

        foreach (var sid in _monitorTypeIo.GetMonitorTypeSids(pipeId))
        {
            var records = timeRecordMap.Values.Select(map => map[sid]).ToList();

            if (records.Count == 0)
            {
                var record = new Record
                {
                    Value = null,
                    Status = "32"
                };
                ret.Add(sid, record);
                continue;
            }


            var status = records.GroupBy(record => record.Status)
                .OrderByDescending(pair => pair.Count()).First().Key;

            ret.Add(sid, new Record
            {
                Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(records) : GetAvg(records),
                Status = status
            });
        }

        return ret;
    }


    public async Task<Dictionary<DateTime, Dictionary<string, Record>>> Get5MinData(int pipeId, DateTime start, DateTime end)
    {
        var timeRecordMap = await _recordIo.GetData(TableType.AdjustedData, pipeId,
            _monitorTypeIo.GetMonitorTypeSids(pipeId), start, end);

        var result = new Dictionary<DateTime, Dictionary<string, Record>>();
        foreach (var dt in Helper.GetTimeSeries(start, end, TimeSpan.FromMinutes(5)))
        {
            var oneMinDataMap = timeRecordMap.Where(kv => kv.Key >= dt && kv.Key < dt.AddMinutes(5))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            result.Add(dt, Calculate5MinData(pipeId, oneMinDataMap));
        }

        return result;
    }

    private Dictionary<string, Record> CalculateHourData(int pipeId,
        IReadOnlyDictionary<DateTime, Dictionary<string, Record>> timeRecordMap)
    {
        Dictionary<string, Record> ret = new Dictionary<string, Record>();
        foreach (var sid in _monitorTypeIo.GetMonitorTypeSids(pipeId))
        {
            var records = timeRecordMap.Values.Select(map => map[sid]).ToList();
            // determine the status of the record
            if (records.Count < 12 || records.Exists(record => record.Status.EndsWith("32")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(records) : GetAvg(records),
                    Status = "32"
                });
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("20")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(records) : GetAvg(records),
                    Status = "20"
                });
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("31")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(records) : GetAvg(records),
                    Status = "31"
                });
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("30")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(records) : GetAvg(records),
                    Status = "30"
                });
                continue;
            }

            var normalRecords = records.Where(record => !record.Status.EndsWith("00"));
            if (records.Exists(record => record.Status.EndsWith("01")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(normalRecords) : GetAvg(normalRecords),
                    Status = "01"
                });
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("02")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(normalRecords) : GetAvg(normalRecords),
                    Status = "02"
                });
                continue;
            }

            if (records.All(record => record.Status.EndsWith("00")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(records) : GetAvg(records),
                    Status = "00"
                });
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("10")))
            {
                ret.Add(sid, new Record
                {
                    Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(normalRecords) : GetAvg(normalRecords),
                    Status = "10"
                });
                continue;
            }


            ret.Add(sid, new Record
            {
                Value = accumulativeMonitorTypes.Contains(sid) ? GetSum(normalRecords) : GetAvg(normalRecords),
                Status = "11"
            });
        }

        return ret;
    }


    private async Task<Dictionary<DateTime, Dictionary<string, Record>>> GetRaw1MinData(int pipeId, List<string> mtList,
        DateTime start, DateTime end)
    {
        try
        {
            var result = await _recordIo.GetData(TableType.RawData, pipeId, mtList, start, end);

            foreach (var dt in Helper.GetTimeSeries(start, end, TimeSpan.FromMinutes(1)))
            {
                if (!result.TryGetValue(dt, out var recordMap))
                {
                    recordMap = new Dictionary<string, Record>();
                    result.TryAdd(dt, recordMap);
                }

                if (!mtList.All(sid => result[dt].ContainsKey(sid)))
                {
                    foreach (string sid in mtList.Where(sid => !recordMap.ContainsKey(sid)))
                    {
                        const string status = "32";

                        recordMap.Add(sid, new Record
                        {
                            Value = null,
                            Status = status,
                            Baf = 1.0m
                        });
                    }
                }

                if (recordMap.ContainsKey("Water")) continue;

                recordMap.Add("Water", new Record
                {
                    Value = null,
                    Status = "10",
                    Baf = 1.0m
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRaw1MinData {PipeId} {Start} {End}", pipeId, start, end);
            throw;
        }
    }

    public Dictionary<string, Record>
        Get1MinAdjustedData(int pipeId, DateTime start,
            List<string> mtList,
            Dictionary<DateTime, Dictionary<string, Record>> rawTimeDataMap, bool updatePipe = true)
    {
        if (!rawTimeDataMap.ContainsKey(start))
        {
            Log.Error("Get1MinAdjustedData {PipeId} {Start} not found", pipeId, start);
        }

        var rawDataMap = rawTimeDataMap[start];
        decimal? dWater = null;
        if (rawDataMap.TryGetValue("Water", out var waterRecord))
        {
            dWater = waterRecord.Value;
            rawDataMap.Remove("Water");
        }
        else
            Log.Error("Calculate1MinAdjustedData {PipeId} {Start} Water not found", pipeId, start);

        var result = new Dictionary<string, Record>();

        decimal? ozoneFactor;

        var pipe = _pipeIo.PipeMap[pipeId];
        // O2 first calculator
        if (rawDataMap.TryGetValue("E36", out var e36Record))
        {
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["E36"];

            result.Add("E36", e36Record);
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["E36"];


            pipe.LastNormalOzone = e36Record.Value.GetValueOrDefault(0);
            pipe.NormalOzoneTime = start;

            decimal rawO2 = e36Record.Value.GetValueOrDefault(0);
            decimal dO2 = typeDef.CheckRange(rawO2);
            (e36Record.Value, ozoneFactor, _) =
                Helper.GetFixOzone(
                    typeDef.AdjustFactor.Water, dO2,
                    dWater.GetValueOrDefault(0),
                    pipe.BaseO2,
                    100,
                    e36Record.Baf.GetValueOrDefault(1m)); // 氧氣-修水

            // status - 監測數據紀錄值是否超過排放標準
            item.CheckOverStatus(e36Record);
            if (updatePipe)
            {
                pipe.LastNormalOzone = e36Record.Value.GetValueOrDefault(0);
                pipe.NormalOzoneTime = start;
            }
        }
        else
        {
            (_, ozoneFactor, _) =
                Helper.GetFixOzone(
                    true, pipe.LastNormalOzone,
                    dWater.GetValueOrDefault(0),
                    pipe.BaseO2,
                    100,
                    1); // 氧氣-修水
        }

        if (rawDataMap.TryGetValue("T59", out var t59Record)) // 預算 -溫度
        {
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["T59"];
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["T59"];
            result.Add("T59", t59Record);

            // 判斷數位 / 類比，如為數位，不需要限制最大範圍
            t59Record.Value = typeDef.CheckRange(t59Record.Value.GetValueOrDefault(0));
            if (updatePipe)
            {
                pipe.LastNormalTemp = t59Record.Value.GetValueOrDefault(100);
                pipe.NormalTempTime = start;
            }

            item.CheckOverStatus(t59Record);
        }

        if (rawDataMap.ContainsKey("F48") && rawDataMap.ContainsKey("T59")) // 流速-修水、修氧
        {
            var record = rawDataMap["F48"];
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["F48"];
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["F48"];
            result.Add("F48", record);

            decimal dF48 = typeDef.CheckRange(record.Value.GetValueOrDefault(0));

            record.Value = dF48 * pipe.Area * 60;
            item.CheckOverStatus(record);
        }


        foreach (var sid in mtList
                     .Where(sid => sid != "E36" && sid != "F48" && sid != "T59"))
        {
            var record = rawDataMap[sid];
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId][sid];
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId][sid];
            switch (sid)
            {
                // accumulative MonitorType
                case "WaterQuantity":
                case "BFWeightMod":
                    if (rawTimeDataMap.TryGetValue(start.AddMinutes(-1), out var rawLast1MinDataMap) &&
                        rawLast1MinDataMap.TryGetValue(sid, out var prevRecord))
                    {
                        if (prevRecord.Value.HasValue)
                        {
                            switch (sid)
                            {
                                case "WaterQuantity":
                                    record.Value -= prevRecord.Value;
                                    break;
                                case "BFWeightMod":
                                    record.Value = prevRecord.Value - record.Value;
                                    break;
                            }

                            if (record.Value < 0)
                                record.Value = 0;
                        }
                        else
                        {
                            record.Value = null;
                        }
                    }
                    else
                    {
                        record.Value = 0m;
                    }

                    break;

                default:
                    if (record.Value.HasValue)
                    {
                        record.Value = typeDef.CheckRange(record.Value.GetValueOrDefault());
                        record.Value = Helper.GetOtherFixValue(sid,
                            typeDef.AdjustFactor.O2,
                            typeDef.AdjustFactor.Water,
                            record.Value.GetValueOrDefault(),
                            dWater.GetValueOrDefault(),
                            record.Baf.GetValueOrDefault(1),
                            ozoneFactor.Value);
                        item.CheckOverStatus(record);
                    }
                    else
                    {
                        record.Value = null;
                    }

                    break;
            }


            //Take care accumulative MonitorType

            result.Add(sid, record);
        }

        if (updatePipe)
            _ = _pipeIo.UpdatePipe(pipe);

        return result;
    }


    public Dictionary<string, Record>
        Get1MinAdjustedDataFixWaterOnly(int pipeId, DateTime start,
            List<string> mtList,
            Dictionary<DateTime, Dictionary<string, Record>> rawTimeDataMap, bool updatePipe = true)
    {
        var rawDataMap = rawTimeDataMap[start];
        decimal? dWater = null;
        if (rawDataMap.TryGetValue("Water", out var waterRecord))
        {
            dWater = waterRecord.Value;
            rawDataMap.Remove("Water");
        }
        else
            Log.Error("Get1MinAdjustedDataFixWaterOnly {PipeId} {Start} Water not found", pipeId, start);

        var result = new Dictionary<string, Record>();

        decimal? ozoneFactor;
        decimal? flowOzoneFactor;
        var pipe = _pipeIo.PipeMap[pipeId];
        // O2 first calculator
        if (rawDataMap.TryGetValue("E36", out var e36Record))
        {
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["E36"];

            result.Add("E36", e36Record);
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["E36"];
            if (e36Record.Status.EndsWith("10") || e36Record.Status.EndsWith("11"))
            {
                pipe.LastNormalOzone = e36Record.Value.GetValueOrDefault(0);
                pipe.NormalOzoneTime = start;
                // 判斷數位 / 類比，如為數位，不需要限制最大範圍
                decimal rawO2 = e36Record.Value.GetValueOrDefault(0);
                decimal dO2 = typeDef.CheckRange(rawO2);
                (e36Record.Value, ozoneFactor, flowOzoneFactor) =
                    Helper.GetFixOzone(
                        true, dO2,
                        dWater.GetValueOrDefault(0),
                        pipe.BaseO2,
                        100,
                        e36Record.Baf.GetValueOrDefault(1m)); // 氧氣-修水

                // status - 監測數據紀錄值是否超過排放標準
                item.CheckOverStatus(e36Record);
                if (updatePipe)
                {
                    pipe.LastNormalOzone = e36Record.Value.GetValueOrDefault(0);
                    pipe.NormalOzoneTime = start;
                }
            }
            else
            {
                if (e36Record.Value.HasValue)
                {
                    decimal rawO2 = e36Record.Value.GetValueOrDefault(0);
                    decimal dO2 = typeDef.CheckRange(rawO2);
                    e36Record.Value = Math.Round(dO2 * e36Record.Baf.GetValueOrDefault(1), 2,
                        MidpointRounding.AwayFromZero);

                    (e36Record.Value, _, _) =
                        Helper.GetFixOzone(
                            true, e36Record.Value.Value,
                            dWater.GetValueOrDefault(0),
                            pipe.BaseO2,
                            100,
                            e36Record.Baf.GetValueOrDefault(1m)); // 氧氣-修水
                }

                (_, ozoneFactor, flowOzoneFactor) =
                    Helper.GetFixOzone(
                        true, pipe.LastNormalOzone,
                        dWater.GetValueOrDefault(0),
                        pipe.BaseO2,
                        100,
                        e36Record.Baf.GetValueOrDefault(1m)); // 氧氣-修水
            }
        }
        else
        {
            (_, ozoneFactor, flowOzoneFactor) =
                Helper.GetFixOzone(
                    true, pipe.LastNormalOzone,
                    dWater.GetValueOrDefault(0),
                    pipe.BaseO2,
                    100,
                    1); // 氧氣-修水
        }

        if (rawDataMap.TryGetValue("T59", out var t59Record)) // 預算 -溫度
        {
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["T59"];
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["T59"];
            result.Add("T59", t59Record);
            if (t59Record.Status.EndsWith("10") || t59Record.Status.EndsWith("11"))
            {
                // 判斷數位 / 類比，如為數位，不需要限制最大範圍
                t59Record.Value = typeDef.CheckRange(t59Record.Value.GetValueOrDefault(0));
                if (updatePipe)
                {
                    pipe.LastNormalTemp = t59Record.Value.GetValueOrDefault(100);
                    pipe.NormalTempTime = start;
                }

                item.CheckOverStatus(t59Record);
            }
        }

        if (rawDataMap.ContainsKey("F48") && rawDataMap.ContainsKey("T59")) // 流速-修水、修氧
        {
            var record = rawDataMap["F48"];
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["F48"];
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["F48"];
            result.Add("F48", record);

            if (record.Status.EndsWith("10") || record.Status.EndsWith("11"))
            {
                decimal dF48 = typeDef.CheckRange(record.Value.GetValueOrDefault(0));

                bool stop = record.Status[..1] != "A" && record.Status[..1] != "N"; // 非N、非A == 其餘固定污染源運轉狀態期間

                if (stop && pipe.LastNormalOzone > 8)
                    record.Value = Helper.GetFlowFixValue(false,
                        true,
                        dF48,
                        dWater.GetValueOrDefault(0),
                        pipe.LastNormalTemp,
                        pipe.Area,
                        record.Baf.GetValueOrDefault(1),
                        flowOzoneFactor.Value);
                else
                    record.Value = Helper.GetFlowFixValue(false,
                        true,
                        dF48,
                        dWater.GetValueOrDefault(0),
                        pipe.LastNormalTemp,
                        pipe.Area,
                        record.Baf.GetValueOrDefault(1),
                        flowOzoneFactor.Value);
                item.CheckOverStatus(record);
            }
            else
            {
                // 20221026 若校正數據 20 不修氧、不修水、不修溫度 start humboldt
                var tempValue = record.Status.EndsWith("20") ? 0 : pipe.LastNormalTemp;
                // 20221026 end humboldt
                if (record.Value.HasValue)
                {
                    record.Value = typeDef.CheckRange(record.Value.GetValueOrDefault()); // 20220906 增加查詢上限 humboldt
                    record.Value = Helper.GetFlowFixValue(false,
                        true,
                        record.Value.Value,
                        dWater.GetValueOrDefault(),
                        tempValue,
                        pipe.Area,
                        record.Baf.GetValueOrDefault(1),
                        flowOzoneFactor.Value);
                }
            }
        }

        foreach (var sid in mtList
                     .Where(sid => sid != "E36" && sid != "F48" && sid != "T59" && sid != "G11"))
        {
            var record = rawDataMap[sid];
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId][sid];
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId][sid];
            if (record.Status.EndsWith("10") || record.Status.EndsWith("11"))
            {
                record.Value = typeDef.CheckRange(record.Value.GetValueOrDefault());
                bool stop = record.Status[..1] != "A" && record.Status[..1] != "N"; // 非N、非A == 其餘固定污染源運轉狀態期間
                if (stop && pipe.LastNormalOzone > 8)
                    record.Value = Helper.GetOtherFixValue(sid,
                        false,
                        true,
                        record.Value.GetValueOrDefault(),
                        dWater.GetValueOrDefault(),
                        record.Baf.GetValueOrDefault(1m),
                        ozoneFactor.Value);
                else
                    record.Value = Helper.GetOtherFixValue(sid,
                        false,
                        true,
                        record.Value.GetValueOrDefault(),
                        dWater.GetValueOrDefault(),
                        record.Baf.GetValueOrDefault(1),
                        ozoneFactor.Value);
                item.CheckOverStatus(record);
            }
            else
            {
                // 20221026 end humboldt
                if (record.Value.HasValue)
                {
                    record.Value =
                        typeDef.CheckRange(record.Value.GetValueOrDefault()); // 20220906 增加查詢上限 humboldt

                    record.Value = Helper.GetOtherFixValue(sid,
                        false,
                        true,
                        record.Value.GetValueOrDefault(),
                        dWater.GetValueOrDefault(),
                        record.Baf.GetValueOrDefault(1),
                        ozoneFactor.Value);
                }
            }

            result.Add(sid, record);
        }

        if (updatePipe)
            _ = _pipeIo.UpdatePipe(pipe);

        return result;
    }


    public async Task<Dictionary<DateTime, Dictionary<string, Record>>> CalculateFix1Min(int pipeId,
        DateTime createDate, bool updateDb = true)
    {
        if (createDate >= DateTime.Now)
        {
            _logger.LogWarning("CalculateFix1Min {Current} into future", createDate);
            return new Dictionary<DateTime, Dictionary<string, Record>>();
        }

        try
        {
            List<string> mtList = _monitorTypeIo.PipeMonitorTypeMap[pipeId].Values.Select(mt => mt.Sid)
                .ToList();

            var rawTimeDataMap =
                await GetRaw1MinData(pipeId, mtList, createDate.AddMinutes(-1), createDate.AddMinutes(1));
            var recordMap = Get1MinAdjustedData(pipeId, createDate, mtList, rawTimeDataMap);

            if (updateDb)
            {
                await _recordIo.UpsertData(TableType.AdjustedData, pipeId, createDate, recordMap);
            }

            return new Dictionary<DateTime, Dictionary<string, Record>>
            {
                { createDate, recordMap }
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed");
            throw;
        }
    }

    public async Task<Dictionary<string, Record>> UpsertAdjustHour(int pipeId, DateTime target)
    {
        if (target >= DateTime.Now)
        {
            _logger.LogWarning("UpsertAdjustHour {Target} into future", target);
            return new Dictionary<string, Record>();
        }

        try
        {
            if (target.Minute != 0)
                return new Dictionary<string, Record>();

            var hourRecordMap = CalculateHourData(pipeId,
                await Get5MinData(pipeId, target.AddHours(-1), target.AddMinutes(1)));
            await _recordIo.UpsertData(TableType.AdjustedData60, pipeId, target, hourRecordMap);
            return hourRecordMap;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UpsertAdjustHour failed {Target}", target);
            throw;
        }
    }

    public Record GetAdjustRawOpRecord(int pipeId, Record record)
    {
        var pipe = _pipeIo.PipeMap[pipeId];
        var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["G11"];
        var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["G11"];
        if (record.Status.EndsWith("10") || record.Status.EndsWith("11"))
        {
            // 判斷數位 / 類比，如為數位，不需要限制最大範圍
            decimal dValue = typeDef.CheckRange(record.Value.GetValueOrDefault());
            decimal value = (decimal)(1 - Math.Pow(10, Math.Log10(1 - (double)dValue * 0.01) *
                                                       (double)(pipe.EmissionDiameter / pipe.LightDiameter))) * 100;
            var adjustedRecord = new Record
            {
                Status = record.Status,
                Value = Math.Round(value, 2, MidpointRounding.AwayFromZero)
            };

            item.CheckOverStatus(record);

            return adjustedRecord;
        }

        return record;
    }
}