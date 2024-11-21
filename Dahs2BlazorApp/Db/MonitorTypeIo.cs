using System.Collections.Concurrent;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public interface IMonitorType
{
    int PipeId { get; set; }
    string Sid { get; set; }
    string Name { get; set; }
    string Unit { get; set; }
    string Unit2 { get; set; }
    bool Upload { get; set; }
    decimal? Standard { get; set; }
    decimal? Standard4Stop { get; set; }
    decimal? Alarm { get; set; }
    decimal? Warning { get; set; }
    decimal? AlarmLow { get; set; }
    decimal? WarningLow { get; set; }
    decimal? EmissionFactor { get; set; }
    decimal? ControlEfficiency { get; set; }
    string OverrideState { get; set; }
    
    string SrcState { get; set; }
    
    int Order { get; set; }
    
    decimal? MaxValue { get; set; }
    string? MonitorOtherPipe { get; set; }

    void CheckOverStatus(Record record)
    {
        if (!record.Status.EndsWith("10") && !record.Status.EndsWith("11"))
            return;

        record.Status = record.Value > Standard ? "11" : "10";
    }
}

public class MonitorType : IMonitorType
{
    public int PipeId { get; set; }
    public required string Sid { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public required string Unit2 { get; set; }
    public bool Upload { get; set; }
    public decimal? Standard { get; set; }
    public decimal? Standard4Stop { get; set; }
    public decimal? Alarm { get; set; }
    public decimal? Warning { get; set; }
    public decimal? AlarmLow { get; set; }
    public decimal? WarningLow { get; set; }
    public decimal? EmissionFactor { get; set; }
    public decimal? ControlEfficiency { get; set; }
    public required string OverrideState { get; set; }
    public required string SrcState { get; set; }
    public int Order { get; set; }
    
    public decimal? MaxValue { get; set; }
    public string? MonitorOtherPipe { get; set; }
}
public class MonitorTypeIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<MonitorTypeIo> _logger;

    public ConcurrentDictionary<int, ConcurrentDictionary<string, IMonitorType>> PipeMonitorTypeMap { get; } = new();

    public List<IMonitorType> GetMonitorTypes(int pipeId) =>
        PipeMonitorTypeMap[pipeId].Values.OrderBy(mt => mt.Order).ToList();

    public List<string> GetMonitorTypeSids(int pipeId) =>
        GetMonitorTypes(pipeId).Select(mt=>mt.Sid).ToList();

    public List<IMonitorType> GetCemsMonitorTypes(int pipeId) =>
        GetMonitorTypes(pipeId)
            .Where(mt => SiteConfig.CemsMonitorTypeCodes.Exists(mtc=>mtc.ToString() == mt.Sid)).ToList();

    public List<IMonitorType> GetPemsMonitorTypes(int pipeId) =>
        GetMonitorTypes(pipeId)
            .Where(mt => !SiteConfig.CemsMonitorTypeCodes.Exists(mtc=>mtc.ToString() == mt.Sid)).ToList();

    public List<string> GetCemsMonitorTypeSids(int pipeId) =>
        GetMonitorTypeSids(pipeId).Where(sid=>
            SiteConfig.CemsMonitorTypeCodes.Exists(mtc=>mtc.ToString() == sid)).ToList();
    
    public List<string> GetNonCemsMonitorTypeSids(int pipeId) =>
        GetMonitorTypeSids(pipeId).Where(sid=>
            !SiteConfig.CemsMonitorTypeCodes.Exists(mtc=>mtc.ToString() == sid)).ToList();
    
    public List<IMonitorType> GetNonG11MonitorTypes(int pipeId) =>
        GetMonitorTypes(pipeId).Where(mt => mt.Sid != "G11").ToList();
    
    public List<string> GetNonG11MonitorTypeSids(int pipeId) =>
        GetNonG11MonitorTypes(pipeId).Select(mt=>mt.Sid).ToList();
    
    public bool CanUpload(int pipeId, string sid) =>
        PipeMonitorTypeMap[pipeId].TryGetValue(sid, out var monitorType) && monitorType.Upload;
    
    public MonitorTypeIo(ISqlServer sqlServer, ILogger<MonitorTypeIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
    }

    record PipeMonitorType(int PipeId, string Sid);

    private void UpdatePipeMonitorTypeMap(IMonitorType mt)
    {
        var mtMap = PipeMonitorTypeMap.GetOrAdd(mt.PipeId, static (pipeId) => new ConcurrentDictionary<string, IMonitorType>());
        mtMap[mt.Sid] = mt;
    }

    public async Task Init()
    {
        HashSet<PipeMonitorType> pipeMonitorTypeSet = new ();
        IEnumerable<MonitorType> pipeMonitorTypes = await GetMonitorTypeAsync();
        foreach(var pipeMt in pipeMonitorTypes)
        {
            pipeMonitorTypeSet.Add(new PipeMonitorType(pipeMt.PipeId, pipeMt.Sid));
            UpdatePipeMonitorTypeMap (pipeMt);
        }

        foreach (var pipeMtPair in SiteConfig.PipeMonitorTypes)
        {
            var pipeId = pipeMtPair.Key;
            var sequence = 1;
            foreach (var mt in pipeMtPair.Value
                         .Where(mt => !pipeMonitorTypeSet.Contains(new PipeMonitorType(pipeId, mt.Sid.ToString()))))
            {
                var typeInfo = SiteConfig.TypeCodeNameMap[mt.Sid];
                var mtItem = new MonitorType
                {
                    PipeId = pipeMtPair.Key,
                    Sid = mt.Sid.ToString(),                    
                    Name = typeInfo.Name,
                    Unit = typeInfo.Unit,
                    Unit2 = typeInfo.Unit1,
                    OverrideState = "",
                    SrcState = "N",
                    Order = sequence++
                };
                UpdatePipeMonitorTypeMap(mtItem);
                await UpsertMonitorType(mtItem);
            }
        }
    }


    private async Task<IEnumerable<MonitorType>> GetMonitorTypeAsync()
    {
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.QueryAsync<MonitorType>("SELECT *  FROM [dbo].[MonitorType]");
    }

    public async Task UpsertMonitorType(IMonitorType monitorType)
    {
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        await connection.ExecuteAsync(@"
                MERGE INTO MonitorType AS target
                USING (SELECT @PipeId AS PipeId, @Sid AS Sid) AS source
                ON (target.PipeId = source.PipeId AND target.Sid = source.Sid)
                WHEN MATCHED THEN
                    UPDATE SET
                        Name = @Name,
                        Unit = @Unit,
                        Unit2 = @Unit2,
                        Upload = @Upload,
                        Standard = @Standard,
                        Standard4Stop = @Standard4Stop,
                        Alarm = @Alarm,
                        Warning = @Warning,
                        AlarmLow = @AlarmLow,
                        WarningLow = @WarningLow,
                        EmissionFactor = @EmissionFactor,
                        ControlEfficiency = @ControlEfficiency,
                        OverrideState = @OverrideState,
                        SrcState = @SrcState,
                        [Order] = @Order,
                        MaxValue = @MaxValue,
                        MonitorOtherPipe = @MonitorOtherPipe
                WHEN NOT MATCHED THEN
                    INSERT (PipeId, Sid, Name, Unit, Unit2, Upload, Standard, Standard4Stop, 
                        Alarm, Warning, AlarmLow, WarningLow, EmissionFactor, ControlEfficiency, 
                        OverrideState, [Order], MaxValue, MonitorOtherPipe)
                    VALUES (@PipeId, @Sid, @Name, @Unit, @Unit2, @Upload, @Standard, @Standard4Stop, 
                        @Alarm, @Warning, @AlarmLow, @WarningLow, @EmissionFactor, @ControlEfficiency, 
                        @OverrideState, @Order, @MaxValue, @MonitorOtherPipe);
                ", monitorType);

        UpdatePipeMonitorTypeMap(monitorType);
    }
        
}