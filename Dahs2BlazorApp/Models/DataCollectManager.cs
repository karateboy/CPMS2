using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;
using System.Collections.Concurrent;
using System.Diagnostics;
using HslCommunication;
using HslCommunication.Profinet.Melsec;
using Record = Dahs2BlazorApp.Db.Record;

namespace Dahs2BlazorApp.Models;

public partial class DataCollectManager : IHostedService, IDisposable
{
    private readonly ILogger<DataCollectManager> _logger;
    private readonly RecordIo _recordIo;
    private readonly MonitorTypeIo _monitorTypeIo;
    private readonly DeviceIo _deviceIo;
    private readonly MeasuringAdjust _measuringAdjust;
    private readonly AlarmIo _alarmIo;
    private readonly PipeIo _pipeIo;
    private readonly DeviceOutputIo _deviceOutputIo;
    private readonly ILineNotify _lineNotify;

    public DataCollectManager(ILogger<DataCollectManager> logger,
        RecordIo recordIo,
        MonitorTypeIo monitorTypeIo,
        DeviceIo deviceIo,
        MeasuringAdjust measuringAdjust,
        AlarmIo alarmIo,
        PipeIo pipeIo,
        DeviceOutputIo deviceOutputIo,
        SiteInfoIo siteInfoIo,
        ILineNotify lineNotify
    )
    {
        _logger = logger;
        _recordIo = recordIo;
        _monitorTypeIo = monitorTypeIo;
        _deviceIo = deviceIo;
        _measuringAdjust = measuringAdjust;
        _alarmIo = alarmIo;
        _pipeIo = pipeIo;
        _deviceOutputIo = deviceOutputIo;
        _lineNotify = lineNotify;
    }

    private static string UpdateInvalidReason(int pipeId, string mt, string code, string reason)
    {
        UpdatePipeMonitorTypeInvalidReason(pipeId, mt, new InvalidReason(code, reason));
        return code;
    }

    private string GetRecordStatus(int pipeId, string mt, int deviceId, Record record)
    {
        return record.Status == "20"
            ? UpdateInvalidReason(pipeId, mt, "20", "儀器校正")
            : GetRecordStatusWithoutRecord(pipeId, mt, deviceId, record.Value);
    }

    private string GetRecordStatusWithoutRecord(int pipeId, string mt, int? deviceId, decimal? value)
    {
        var monitorTypeDef = SiteConfig.PipeMonitorTypeMap[pipeId][mt];
        var monitorType = _monitorTypeIo.PipeMonitorTypeMap[pipeId][mt];


        if (value is null)
            return UpdateInvalidReason(pipeId, mt, "32", "儀器斷線");

        // 是否手動切換狀態
        if (string.IsNullOrEmpty(monitorType.OverrideState) == false && monitorType.OverrideState != "10")
            return UpdateInvalidReason(pipeId, mt, monitorType.OverrideState, "手動切換");

        if (monitorType.Standard.HasValue && value.Value > monitorType.Standard.Value)
            return UpdateInvalidReason(pipeId, mt, "11", "測值超過法定標準");

        return "10";
    }

    private Dictionary<string, Record> GenerateRecordMap(int pipeId, DateTime createDate, List<string> monitorTypes,
        Dictionary<string, (int, Record)> deviceRecordMap)
    {
        Dictionary<string, Record> ret = new();

        // Handle raw data first
        foreach (var monitorType in monitorTypes)
        {
            if (!deviceRecordMap.TryGetValue(monitorType, out var deviceRecord)) continue;

            var (deviceId, rawRecord) = deviceRecord;
            string status = GetRecordStatus(pipeId, monitorType, deviceId, rawRecord);
            var mtDef = SiteConfig.PipeMonitorTypes[pipeId]
                .FirstOrDefault(x => x.Sid.ToString() == monitorType);

            var v = rawRecord.Value;
            if (v > mtDef!.RangeMax)
                v = mtDef.RangeMax;
            if (v < mtDef.RangeMin)
                v = mtDef.RangeMin;

            ret[monitorType] = new Record
            {
                Value = v,
                Status = $"{status}",
                Baf = 1.0m
            };
        }

        // Handle calculated data
        foreach (var monitorType in monitorTypes)
        {
            if (ret.ContainsKey(monitorType))
                continue;

            var srcState = _monitorTypeIo.PipeMonitorTypeMap[pipeId][monitorType].SrcState;

            void GenerateEmptyRecord()
            {
                string status = "32";
                ret[monitorType] = new Record
                {
                    Value = null,
                    Status = $"{status}",
                    Baf = 1.0m
                };
            }

            // Check if it is calculated MonitorType
            var mtDef = SiteConfig.PipeMonitorTypes[pipeId]
                .FirstOrDefault(x => x.Sid.ToString() == monitorType);

            if (mtDef?.Calculation != null)
            {
                var calculated = mtDef.Calculation;
                if (calculated.InputSids.Any(sid => ret.ContainsKey(sid)) == false)
                {
                    _logger.LogDebug("GenerateRecordMap calculated {MonitorType} without input", monitorType);
                    GenerateEmptyRecord();
                }
                else
                {
                    var values = calculated.InputSids.Select(sid => ret[sid].Value).ToList();
                    var status = calculated.InputSids.Select(sid => ret[sid].Status).Max()!;
                    var value = calculated.CalculateFunc(values);
                    ret[monitorType] =
                        new Record
                        {
                            Value = value,
                            Status = status,
                            Baf = 1m
                        };
                }
            }
            else
            {
                _logger.LogDebug("GenerateRecordMap {MonitorType} not found", monitorType);
                GenerateEmptyRecord();
            }
        }

        return ret;
    }

    static List<string> GetMonitorTypes(int pipeId) =>
        SiteConfig.PipeMonitorTypes[pipeId]
            .Select(x => x.Sid.ToString()).ToList();

    private async Task ReadPipeMonitorTypes(int pipeId, DateTime createDate, TimeSpan interval, TimeSpan timeout)
    {
        try
        {
            List<string> monitorTypes = GetMonitorTypes(pipeId);
            await DeviceManager.ReadPipeDevices(pipeId, timeout);

            _logger.LogDebug("ReadPipeMonitorTypes prepare adjust data");
            // Flush data to database
            Dictionary<string, (int, Record)> deviceRecordMap =
                _pipeDeviceRecordMap[pipeId].ToDictionary(x => x.Key, y => (y.Value.DeviceId, y.Value.RecordValue));


            var rawRecordMap = GenerateRecordMap(pipeId, createDate, monitorTypes, deviceRecordMap);

            var water = 10m;
            if (createDate.Second == 0)
            {
                await _recordIo.InsertRawData(pipeId, createDate, water, rawRecordMap);
                rawRecordMap.Add("Water",
                    new Record
                    {
                        Value = water,
                        Status = "10",
                        Baf = 1
                    });
                var adjustRecordMap = _measuringAdjust.Get1MinAdjustedData(pipeId, createDate,
                    monitorTypes, new() { { createDate, rawRecordMap } }, false);
                OutputAdjustValues(pipeId, adjustRecordMap);
            }

            var adjustCreateDate = createDate.AddMinutes(-1);
            GenerateAlarms(pipeId, adjustCreateDate, rawRecordMap);
            
            if (adjustCreateDate.Second == 0)
            {
                await _measuringAdjust.CalculateFix1Min(pipeId, adjustCreateDate);
            }

            if (adjustCreateDate is { Minute: 0, Second: 0 })
                await _measuringAdjust.UpsertAdjustHour(pipeId, adjustCreateDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadPipeMonitorTypes failed");
        }
    }

    public Dictionary<string, Record> GetCurrentRawRecordMap(int pipeId)
    {
        List<string> monitorTypes = SiteConfig.PipeMonitorTypes[pipeId]
            .Select(x => x.Sid.ToString()).ToList();
        Dictionary<string, (int, Record)> deviceRecordMap =
            _pipeDeviceRecordMap[pipeId]
                .ToDictionary(x => x.Key,
                    y => (y.Value.DeviceId, y.Value.RecordValue));

        //FIXME: Hardcoded water value
        var water = 10m;

        var recordMap = GenerateRecordMap(pipeId, DateTime.Now, monitorTypes, deviceRecordMap);

        // Add Water record
        recordMap.Add("Water", new Record
        {
            Value = water,
            Status = "10",
            Baf = 1
        });
        return recordMap;
    }

    public Dictionary<int, bool> GetCurrentPipeSignalMap(int pipeId)
    {
        if (PipeSignalMap.TryGetValue(pipeId, out var signalMap))
            return signalMap.ToDictionary(x => x.Key, y => y.Value.Value);
        return new Dictionary<int, bool>();
    }

    private void ReadPipesAction(bool onlyOp, TimeSpan interval, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var createDate = Helper.GetRoundedNow(interval);

            foreach (var pipeId in SiteConfig.PipeMonitorTypes.Keys)
            {
                _ = ReadPipeMonitorTypes(pipeId, createDate, interval, timeout);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadAllPipes {OnlyOp} failed", onlyOp);
        }
    }

    record UploadMonitorType(string MonitorType, string fmt, string equip);
    
    public static decimal GetConvertedLoad(decimal load) => load / 2.5m * 15000;

    public async Task Recalculate(DateTime start, DateTime end, bool update, bool upload, bool reUpload,
        IProgress<(int, DateTime)>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "RecalculateAndUpload {Start}=>{End} update={Update} Upload={Upload} ReUpload={Reupload}",
                start, end, update, upload,
                reUpload);

            var pipeIds = SiteConfig.PipeMonitorTypes.Keys.ToList();
            foreach (var current in Helper.GetTimeSeries(start, end, TimeSpan.FromMinutes(1)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var percent = current == end
                    ? 100
                    : (int)((current - start).TotalMinutes * 100 / (end.AddMinutes(-1) - start).TotalMinutes);
                progress?.Report((percent, current));

                if (update)
                    foreach (var pipeId in pipeIds)
                    {
                        if (!update) continue;

                        await _measuringAdjust.CalculateFix1Min(pipeId, current);

                        if (current.Minute == 0)
                            await _measuringAdjust.UpsertAdjustHour(pipeId, current);
                    }
                
            }

            _logger.LogInformation("RecalculateAndUpload complete successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RecalculateAndUpload is cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecalculateAndUpload failed");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Init PipeRecordMap & SignalMap
            foreach (var pipeId in SiteConfig.PipeMonitorTypes.Keys)
            {
                _pipeDeviceRecordMap =
                    _pipeDeviceRecordMap.Add(pipeId, new ConcurrentDictionary<string, DeviceRecord>());

                _pipeMonitorTypeInvalidReasonMap =
                    _pipeMonitorTypeInvalidReasonMap.Add(pipeId, new ConcurrentDictionary<string, InvalidReason>());

                PipeSignalMap.TryAdd(pipeId, new ConcurrentDictionary<int, SignalRecord>());
            }

            // Helper function
            Task PeriodicAction(Action<bool, TimeSpan, TimeSpan, CancellationToken> action,
                bool param, TimeSpan interval, TimeSpan timeout, string actionName)
            {
                var primaryTask = Task.Run(async () =>
                {
                    try
                    {
                        Debug.Assert(interval >= timeout);
                        var alignedNextTime = Helper.GetAlignedNextTime(interval);
                        await Task.Delay(alignedNextTime - DateTime.Now, cancellationToken);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            using PeriodicTimer periodicTimer = new(interval);
                            while (await periodicTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                            {
                                action(param, interval, timeout, cancellationToken);
                            }

                            _logger.LogInformation("{ActionName} is stopped", actionName);
                        }
                        else
                        {
                            _logger.LogInformation("DataCollectManager is cancelled before started");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{ActionName} is failed", actionName);
                    }
                }, cancellationToken);
                return primaryTask.ContinueWith(task =>
                {
                    Helper.CheckTask(task, _logger, $"PeriodicAction {actionName} failed", actionName);
                    if (cancellationToken.IsCancellationRequested) return;
                    _logger.LogInformation("Try to restart {ActionName}", actionName);
                    PeriodicAction(action, param, interval, timeout, actionName);
                }, cancellationToken);
            }

            Task SimplePeriodicAction(Action<bool, int> action, bool param, int interval, string actionName)
            {
                var primaryTask = Task.Run(async () =>
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            action(param, interval);
                            using PeriodicTimer periodicTimer = new(TimeSpan.FromSeconds(interval));
                            while (await periodicTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                            {
                                action(param, interval);
                            }

                            _logger.LogInformation("{ActionName} is stopped", actionName);
                        }
                        else
                        {
                            _logger.LogInformation("DataCollectManager is cancelled before started");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{ActionName} is failed", actionName);
                    }
                }, cancellationToken);
                return primaryTask.ContinueWith(task =>
                {
                    Helper.CheckTask(task, _logger, $"SimplePeriodicAction {actionName} failed", actionName);
                    if (cancellationToken.IsCancellationRequested) return;
                    _logger.LogInformation("Try to restart {ActionName}", actionName);
                    SimplePeriodicAction(action, param, interval, actionName);
                }, cancellationToken);
            }

            // Read Device Data
            _ = PeriodicAction(ReadPipesAction, false, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(2500),
                "ReadPipesAction");
            
            _logger.LogInformation("DataCollectManager is started");
            _ = _alarmIo.AddAlarm(AlarmIo.AlarmLevel.Info, "DAHS2 啟動");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "DataCollectManager StartAsync error");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = _alarmIo.AddAlarm(AlarmIo.AlarmLevel.Warning, "DAHS2 停止");
        return Task.Delay(1000, cancellationToken);
    }

    public void Dispose()
    {
    }
}