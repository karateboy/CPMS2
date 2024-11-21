using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dahs2BlazorApp.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace Dahs2BlazorApp.Db;

public interface IDeviceOutput
{
    int DeviceId { get; set; }
    int Address { get; set; }
    int DataType { get; set; }
    int OutputConfigId { get; set; }
}

public class DeviceOutput : IDeviceOutput
{
    public int DeviceId { get; set; }
    public int Address { get; set; }
    public int DataType { get; set; }
    public int OutputConfigId { get; set; }
    
}

    
public class DeviceOutputIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<DeviceOutputIo> _logger;
    
    public DeviceOutputIo(ISqlServer sqlServer, ILogger<DeviceOutputIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
    }

    public readonly ConcurrentDictionary<int, ImmutableList<IDeviceOutput>> DeviceOutputMap = new ();

    public async Task Init()
    {
        var deviceOutputs = await GetDeviceOutputs();
        foreach (var deviceOutput in deviceOutputs)
        {
            var currentDeviceOutputList = DeviceOutputMap.GetOrAdd(deviceOutput.DeviceId, ImmutableList<IDeviceOutput>.Empty);
            DeviceOutputMap.TryUpdate(deviceOutput.DeviceId, currentDeviceOutputList.Add(deviceOutput),
                currentDeviceOutputList);
        }    
    }

    private async Task<IEnumerable<DeviceOutput>> GetDeviceOutputs()
    {
        const string sql = "SELECT * FROM DeviceOutput";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.QueryAsync<DeviceOutput>(sql);
    }

    public async Task UpsertDeviceOutput(IDeviceOutput deviceOutput)
    {
        var currentList = DeviceOutputMap.GetOrAdd(deviceOutput.DeviceId, ImmutableList<IDeviceOutput>.Empty);
        
        var deviceOutputList = currentList.Any(x => x.OutputConfigId == deviceOutput.OutputConfigId) ? 
            currentList.Select(x => x.OutputConfigId == deviceOutput.OutputConfigId ? 
                deviceOutput : x).ToImmutableList() : currentList.Add(deviceOutput);
        
        DeviceOutputMap[deviceOutput.DeviceId] = deviceOutputList;

        var sql = @"
                MERGE INTO DeviceOutput AS Target
                USING (SELECT @DeviceId AS DeviceId, @OutputConfigId AS OutputConfigId) AS Source
                ON Target.DeviceId = Source.DeviceId AND Target.OutputConfigId = Source.OutputConfigId
                WHEN MATCHED THEN
                    UPDATE SET
                        Address = @Address,
                        DataType = @DataType
                WHEN NOT MATCHED THEN
                    INSERT (DeviceId, Address, DataType, OutputConfigId)
                    VALUES (@DeviceId, @Address, @DataType, @OutputConfigId);";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        await connection.ExecuteAsync(sql, deviceOutput);
    }

    public async Task<int> DeleteDeviceOutput(IDeviceOutput deviceOutput)
    {
        var currentList = DeviceOutputMap.GetOrAdd(deviceOutput.DeviceId, ImmutableList<IDeviceOutput>.Empty);
        var deviceOutputList = currentList.Where(x => x.OutputConfigId != deviceOutput.OutputConfigId).ToImmutableList();
        DeviceOutputMap[deviceOutput.DeviceId] = deviceOutputList;

        var sql = @"
                DELETE FROM DeviceOutput
                WHERE DeviceId = @DeviceId;";

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(sql, deviceOutput);
    }

    public async Task<int> DeleteDeviceMeasuringByDeviceIdAsync(int deviceId)
    {
        DeviceOutputMap.TryRemove(deviceId, out _);

        var sql = @"
                DELETE FROM DeviceMeasuring
                WHERE DeviceId = @DeviceId;";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(sql, new { DeviceId = deviceId });
    }
}