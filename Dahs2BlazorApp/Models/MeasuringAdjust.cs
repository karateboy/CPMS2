using System.Collections.Concurrent;
using System.Diagnostics;
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

    private static string GetMaxCountKey(IReadOnlyDictionary<string, int> dataStateCountMap, string[] order)
    {
        return dataStateCountMap.OrderByDescending(kv => kv.Value).ThenBy(kv =>
        {
            int idx = Array.IndexOf(order, kv.Key);
            return idx >= 0 ? idx : order.Length;
        }).Select(kv => kv.Key).First();
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
                var record = new Record
                {
                    Value = GetAvg(records),
                    Status = "32"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("20")))
            {
                var record = new Record
                {
                    Value = GetAvg(records),
                    Status = "20"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("31")))
            {
                var record = new Record
                {
                    Value = GetAvg(records),
                    Status = "31"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("30")))
            {
                var record = new Record
                {
                    Value = GetAvg(records),
                    Status = "30"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("01")))
            {
                var record = new Record
                {
                    Value = GetAvg(records.Where(record => !record.Status.EndsWith("00"))),
                    Status = "01"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("02")))
            {
                var record = new Record
                {
                    Value = GetAvg(records.Where(record => !record.Status.EndsWith("00"))),
                    Status = "02"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.All(record => record.Status.EndsWith("00")))
            {
                var record = new Record
                {
                    Value = GetAvg(records),
                    Status = "00"
                };
                ret.Add(sid, record);
                continue;
            }

            if (records.Exists(record => record.Status.EndsWith("10")))
            {
                var record = new Record
                {
                    Value = GetAvg(records.Where(record => !record.Status.EndsWith("00"))),
                    Status = "10"
                };
                ret.Add(sid, record);
                continue;
            }

            {
                var record = new Record
                {
                    Value = GetAvg(records.Where(record => !record.Status.EndsWith("00"))),
                    Status = "11"
                };
                ret.Add(sid, record);
            }
        }

        return ret;
    }

    private Record CalculateOp6Data(int pipeId, IDictionary<DateTime, Record> timeRecordMap)
    {
        Dictionary<char, int> opStateCountMap = new Dictionary<char, int>();
        Dictionary<char, int> equipStateCountMap = new Dictionary<char, int>();
        Dictionary<string, int> dataStateCountMap = new Dictionary<string, int>();
        List<Record> recordList = timeRecordMap.Values.ToList();
        foreach (var record in recordList)
        {
            UpdateMapCount(opStateCountMap, record.Status[0]);
            UpdateMapCount(equipStateCountMap, record.Status[1]);

            // 20220622 狀態碼記數若有尾數11替換10，給後續計算使用。 humboldt start
            UpdateMapCount(dataStateCountMap, record.Status.Substring(2, 2).Replace("11", "10"));
            continue;
            // 20220622 end

            void UpdateMapCount<T>(Dictionary<T, int> map, T key) where T : notnull
            {
                if (map.ContainsKey(key))
                    map[key] += 1;
                else
                    map[key] = 1;
            }
        }

        var opState = opStateCountMap.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).First();
        var equipState = equipStateCountMap.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).First();

        (string dataState, decimal? avg) = GetDataStateAvg();
        return new Record
        {
            Value = avg,
            Status = $"{opState}{equipState}{dataState}"
        };

        (string, decimal?) GetDataStateAvg()
        {
            dataStateCountMap.TryGetValue("30", out int count30);
            dataStateCountMap.TryGetValue("40", out int count40);
            if (count30 + count40 >= 10)
            {
                return count30 >= count40 ? ("30", GetAvg(recordList)) : ("40", GetAvg(recordList));
            }

            string maxCountKey = GetMaxCountKey(dataStateCountMap,
                new[] { "21", "20", "31", "32", "01", "02", "03", "30", "40", "11", "10" });

            if (maxCountKey != "10" && maxCountKey != "11")
            {
                return maxCountKey == "40"
                    ? (maxCountKey, null)
                    : (maxCountKey, GetAvg(recordList.Where(record => record.Status.EndsWith(maxCountKey)).ToList()));
            }
            else
            {
                decimal? valueAvg = GetAvg(recordList.Where(record =>
                    record.Status.EndsWith("10") || record.Status.EndsWith("11")).ToList());
                var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["G11"];
                if (item.Standard is not null && valueAvg > item.Standard)
                    return ("11", valueAvg);

                return ("10", valueAvg);
            }
        }
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
                        string status = "32";
                        var record = new Record
                        {
                            Value = null,
                            Status = status,
                            Baf = 1.0m
                        };
                        recordMap.Add(sid, record);
                    }
                }

                if (!recordMap.ContainsKey("Water"))
                {
                    var record = new Record
                    {
                        Value = null,
                        Status = "NA10",
                        Baf = 1.0m
                    };
                    recordMap.Add("Water", record);
                }
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
        decimal? flowOzoneFactor;
        var pipe = _pipeIo.PipeMap[pipeId];
        // O2 first calculator
        if (rawDataMap.TryGetValue("E36", out var e36Record))
        {
            var item = _monitorTypeIo.PipeMonitorTypeMap[pipeId]["E36"];

            result.Add("E36", e36Record);
            var typeDef = SiteConfig.PipeMonitorTypeMap[pipeId]["E36"];


            pipe.LastNormalOzone = e36Record.Value.GetValueOrDefault(0);
            pipe.NormalOzoneTime = start;
            // 判斷數位 / 類比，如為數位，不需要限制最大範圍
            decimal rawO2 = e36Record.Value.GetValueOrDefault(0);
            decimal dO2 = typeDef.CheckRange(rawO2);
            (e36Record.Value, ozoneFactor, flowOzoneFactor) =
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

            var rawTimeDataMap = await GetRaw1MinData(pipeId, mtList, createDate, createDate.AddMinutes(1));
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

            var mtList = _monitorTypeIo.GetMonitorTypeSids(pipeId);

            var hourRecordMap = CalculateHourData(pipeId,
                await _recordIo.GetData(TableType.AdjustedData, pipeId, mtList, target.AddHours(-1).AddMinutes(1),
                    target.AddMinutes(1)));
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