using Dahs2BlazorApp.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;

namespace Dahs2BlazorApp.Db;

public record Record
{
    public decimal? Value;
    public string Status = "";
    public decimal? Baf;
}

public class RecordIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<RecordIo> _logger;

    public RecordIo(ISqlServer sqlServer, ILogger<RecordIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
    }

    public Dictionary<TableType, string> TableTypeNameMap = new()
    {
        { TableType.RawData, "原始資料" },
        { TableType.AdjustedData, "一分鐘修正值" },
        { TableType.AdjustedData60, "小時修正值" },
    };

    public List<TableType> TableTypes => TableTypeNameMap.Keys.OrderBy(k => k).ToList();

    private readonly List<TableType> _gasTables = new()
        { TableType.RawData, TableType.AdjustedData, TableType.AdjustedData60 };

    public async Task Init()
    {
        HashSet<string> monitorTypeSet = new();
        foreach ((_, List<TypeDefinition> typeDefinitions) in SiteConfig.PipeMonitorTypes)
        {
            typeDefinitions.ForEach(td => monitorTypeSet.Add(td.Sid.ToString()));
        }

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);

        foreach (var tableType in _gasTables)
        {
            var columns = await _sqlServer.GetTableColumns(tableType.ToString());
            foreach (var mt in monitorTypeSet)
            {
                if (!columns.Contains(mt))
                {
                    if (tableType == TableType.RawData)
                    {
                        await _sqlServer.AddRawColumn(connection, tableType.ToString(), mt);
                    }
                    else
                    {
                        await _sqlServer.AddColumn(connection, tableType.ToString(), mt);
                    }
                }
            }
        }
    }

    public async Task<int> InsertRawData(int pipeId, DateTime createDate, decimal water,
        Dictionary<string, Record> recordMap)
    {
        var mtList = recordMap.Keys.ToList();
        var colNames = string.Join(",", mtList.Select(mt => $"[{mt}], [{mt}_st], [{mt}_baf]"));
        var valueParams = string.Join(",", mtList.Select(mt => $"@{mt}, @{mt}_st, @{mt}_baf"));
        var upsertStr =
            $"INSERT INTO [RawData] ([PipeId], [CreateDate], [UpdateDate], [Water], {colNames}) VALUES (@pipeId, @createDate, @updateDate, @water, {valueParams})";

        var parameters = new Dictionary<string, object>
        {
            { "PipeId", pipeId },
            { "CreateDate", createDate },
            { "UpdateDate", DateTime.Now },
            { "water", water },
        };
        foreach (var mt in mtList)
        {
            if (!recordMap.TryGetValue(mt, out var record)) continue;

            parameters.Add(mt, record.Value!);
            parameters.Add($"{mt}_st", record.Status);
            parameters.Add($"{mt}_baf", record.Baf!);
        }

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(upsertStr, parameters);
    }
    
    public async Task<Dictionary<DateTime, Dictionary<string, Record>>> GetData(TableType tabType, int pipeId,
        List<string> mtList, DateTime start, DateTime end)
    {
        var targetTab = tabType;

        var sql =
            $"SELECT * FROM {targetTab.ToString()} Where [PipeId] = @pipeId and [CreateDate] >= @start and [CreateDate] < @end";
        if (tabType == TableType.AdjustedData60)
            sql =
                $"SELECT * FROM {targetTab.ToString()} Where [PipeId] = @pipeId and [CreateDate] >= @start and [CreateDate] < @end AND DATEPART(MINUTE,[CreateDate]) = 0";

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        var reader = await connection.ExecuteReaderAsync(
            sql,
            new
            {
                pipeId,
                start,
                end
            });

        var result = new Dictionary<DateTime, Dictionary<string, Record>>();
        while (await reader.ReadAsync())
        {
            var dt = (DateTime)reader["CreateDate"];
            var recordMap = new Dictionary<string, Record>();
            result.TryAdd(dt, recordMap);
            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            foreach (var mt in mtList)
            {
                var mtStatus = $"{mt}_st";
                if (columns.Contains(mtStatus))
                {
                    if (reader[mtStatus] != DBNull.Value)
                    {
                        var record = new Record
                        {
                            Value = GetValue($"{mt}"),
                            Status = ((string)reader[$"{mt}_st"]).Trim()
                        };
                        if (tabType == TableType.RawData)
                            record.Baf = GetValue($"{mt}_baf");

                        recordMap.Add(mt, record);    
                    }
                    else
                    {
                        var record = new Record
                        {
                            Value = GetValue($"{mt}"),
                            Status = "32"
                        };
                        if (tabType == TableType.RawData)
                            record.Baf = GetValue($"{mt}_baf");

                        recordMap.Add(mt, record);
                    }
                }
            }

            if (tabType == TableType.RawData)
            {
                recordMap.Add("Water", new Record { Value = GetValue("Water") });
            }
        }

        await reader.CloseAsync();
        return result;

        decimal? GetValue(string colName)
        {
            try
            {
                if (reader[colName] == DBNull.Value)
                    return null;

                return (decimal)reader[colName];
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetValue failed {ColName} {Unknown}", colName, reader[colName]);
                return null;
            }
        }
    }

    public async Task<int> UpsertData(TableType tabType, int pipeId, DateTime createDate,
        Dictionary<string, Record> recordMap)
    {
        var mtList = recordMap.Keys.ToList();
        TableType targetTab = tabType;

        string colNames = string.Join(",", mtList.Select(mt => $"[{mt}], [{mt}_st]"));
        string valueParams = string.Join(",", mtList.Select(mt => $"@{mt}, @{mt}_st"));
        string setParams = string.Join(",", mtList.Select(mt => $"{mt}=@{mt}, {mt}_st=@{mt}_st"));

        string upsertStr = $@"MERGE INTO {targetTab} AS target
                              USING (SELECT @PipeId AS PipeId, @CreateDate AS CreateDate) AS source
                              ON (target.PipeId = source.PipeId AND target.CreateDate = source.CreateDate) 
                              WHEN MATCHED THEN
                                UPDATE SET
                                [UpdateDate] = @updateDate, 
                                {setParams}
                              WHEN NOT MATCHED THEN
                                  INSERT ([PipeId], [CreateDate], [UpdateDate], {colNames})
                                  VALUES (@pipeId, @createDate, @updateDate, {valueParams});";
        _logger.LogDebug("SQL = {SqlStr}", upsertStr);
        var parameters = new Dictionary<string, object>
        {
            { "PipeId", pipeId },
            { "CreateDate", createDate },
            { "UpdateDate", DateTime.Now },
        };

        foreach (var mt in mtList)
        {
            if (!recordMap.TryGetValue(mt, out var record)) continue;

            parameters.Add(mt, record.Value!);
            parameters.Add($"{mt}_st", record.Status);
        }

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(upsertStr, parameters);
    }

    async Task<int> GetCountMonthlyRawAsync(int pipeId, DateTime start, string sid, decimal rangeMax, bool over)
    {
        string queryStr = @$"SELECT Count(*)
                                FROM [dbo].[RawData]
                                Where PipeId = @pipeId and CreateDate >= @start and CreateDate < @end AND {sid} <= @range ";
        if (over)
            queryStr = @$"SELECT Count(*)
                                FROM [dbo].[RawData]
                                Where PipeId = @pipeId and CreateDate >= @start and CreateDate < @end AND {sid} > @range ";

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return (int)(await connection.ExecuteScalarAsync(queryStr, new
        {
            pipeId,
            start,
            end = start.AddMonths(1),
            range = rangeMax
        }) ?? 0);
    }

    async Task<int> GetCountMonthlyRawOpAsync(int pipeId, DateTime start, decimal rangeMax, bool over)
    {
        string queryStr = @$"SELECT Count(*)
                                FROM [dbo].[RawOp]
                                Where PipeId = @pipeId and CreateDate >= @start and CreateDate < @end AND G11 <= @range ";
        if (over)
            queryStr = @$"SELECT Count(*)
                                FROM [dbo].[RawOp]
                                Where PipeId = @pipeId and CreateDate >= @start and CreateDate < @end AND G11 > @range ";

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return (int)(await connection.ExecuteScalarAsync(queryStr, new
        {
            pipeId,
            start,
            end = start.AddMonths(1),
            range = rangeMax
        }) ?? 0);
    }

    private async Task<int> GetCountMonthlyRawWithinRangeAsync(int pipeId, DateTime start, string sid, decimal rangeMax)
    {
        if (sid == "G11")
            return await GetCountMonthlyRawOpAsync(pipeId, start, rangeMax, false);

        return await GetCountMonthlyRawAsync(pipeId, start, sid, rangeMax, false);
    }

    private async Task<int> GetCountMonthlyRawOverRangeAsync(int pipeId, DateTime start, string sid, decimal rangeMax)
    {
        if (sid == "G11")
            return await GetCountMonthlyRawOpAsync(pipeId, start, rangeMax, true);

        return await GetCountMonthlyRawAsync(pipeId, start, sid, rangeMax, true);
    }

    public async Task<double> GetMonthlyWithinRangePercentageAsync(int pipeId, DateTime start, string sid,
        decimal rangeMax)
    {
        double withinRangeCount = await GetCountMonthlyRawWithinRangeAsync(pipeId, start, sid, rangeMax);
        double overRangeCount = await GetCountMonthlyRawOverRangeAsync(pipeId, start, sid, rangeMax);
        return withinRangeCount == 0 ? 0 : withinRangeCount / (withinRangeCount + overRangeCount);
    }
}

public enum TableType
{
    RawData,
    AdjustedData,
    AdjustedData60
}