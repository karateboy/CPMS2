using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dahs2BlazorApp.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public class DeviceSignalIo
{
    public interface IDeviceSignal
    {
        int Id { get; set; }
        int DeviceId { get; set; }
        string? Name { get; set; }
        int PipeId { get; set; }
        bool Coil { get; set; }
        int Address { get; set; }
        int Offset { get; set; }
    }

    public class DeviceSignal : IDeviceSignal
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public required string? Name { get; set; }
        public int PipeId { get; set; }
        public bool Coil { get; set; }
        public int Address { get; set; }
        public int Offset { get; set; }
    }

    private readonly ISqlServer _sqlServer;
    private readonly ILogger<DeviceSignalIo> _logger;

    public DeviceSignalIo(ISqlServer sqlServer, ILogger<DeviceSignalIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
        DeviceSignalMap = new();
    }

    private ConcurrentDictionary<int, ImmutableList<IDeviceSignal>> DeviceSignalMap { get; }
    public ConcurrentDictionary<int, IDeviceSignal> SignalMap { get; init; } = new();

    public async Task Init()
    {
        var deviceSignals = await GetDeviceSignalsAsync();
        foreach (var deviceSignal in deviceSignals)
        {
            if (!DeviceSignalMap.TryGetValue(deviceSignal.DeviceId, out var deviceSignalList))
            {
                deviceSignalList = ImmutableList<IDeviceSignal>.Empty;
            }

            SignalMap[deviceSignal.Id] = deviceSignal;
            deviceSignalList = deviceSignalList.Add(deviceSignal);
            DeviceSignalMap[deviceSignal.DeviceId] = deviceSignalList;
        }
    }

     
    public ImmutableList<IDeviceSignal> GetDeviceSignalListByDeviceId(int deviceId) => 
        DeviceSignalMap.TryGetValue(deviceId, out var deviceSignalList) ? deviceSignalList : ImmutableList<IDeviceSignal>.Empty;

    public ImmutableList<IDeviceSignal> GetDeviceSignalListByPipeId(int pipeId) => 
        DeviceSignalMap.Values.SelectMany(list => list).Where(signal => signal.PipeId == pipeId).ToImmutableList();
    
    private async Task<IEnumerable<IDeviceSignal>> GetDeviceSignalsAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            return await connection.QueryAsync<DeviceSignal>("SELECT * FROM DeviceSignal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDeviceSignalsAsync");
            throw;
        }
    }

    public async Task<int> UpsertDeviceSignal(IDeviceSignal deviceSignal)
    {
        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            int id = await connection.QuerySingleAsync<int>(@"
                MERGE INTO DeviceSignal As target
                USING (SELECT @Id as Id) AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET
                        [DeviceId] = @DeviceId,
                        [Name] = @Name,
                        [PipeId] = @PipeId,
                        [Coil] = @Coil,
                        [Address] = @Address,
                        [Offset] = @Offset                        
                WHEN NOT MATCHED THEN
                    INSERT (DeviceId, Name, PipeId, Coil, Address, Offset)
                    VALUES (@DeviceId, @Name, @PipeId, @Coil, @Address, @Offset)
                OUTPUT Inserted.Id;", deviceSignal);
            if(deviceSignal.Id == 0)
                deviceSignal.Id = id;
     
            DeviceSignalMap.AddOrUpdate(deviceSignal.DeviceId, ImmutableList.Create(deviceSignal),
                (key, list) =>
                    list.RemoveAll(signal => signal.Id == deviceSignal.Id).Add(deviceSignal));
            SignalMap[deviceSignal.Id] = deviceSignal;
            
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertDeviceSignal");
            throw;
        }
    }

    public async Task<int> DeleteDeviceSignalAsync(IDeviceSignal deviceSignal)
    {
        try
        {
            DeviceSignalMap.AddOrUpdate(deviceSignal.DeviceId, ImmutableList<IDeviceSignal>.Empty,
                (key, oldValue) =>
                    oldValue.RemoveAll(signal => signal.Id == deviceSignal.Id));
            SignalMap.TryRemove(deviceSignal.Id, out _);
            
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            return await connection.ExecuteAsync("DELETE FROM DeviceSignal WHERE Id = @Id", deviceSignal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteDeviceSignal");
            throw;
        }
    }

    public async Task<int> DeleteDeviceSignalByDeviceIdAsync(int deviceId)
    {
        try
        {
            var deviceSignals = GetDeviceSignalListByDeviceId(deviceId);
            foreach (var signal in deviceSignals)
            {
                SignalMap.TryRemove(signal.Id, out _);
            }
            DeviceSignalMap.TryRemove(deviceId, out _);
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            return await connection.ExecuteAsync("DELETE FROM DeviceSignal WHERE DeviceId = @Id", 
                new { Id = deviceId});
        }catch(Exception ex)
        {
            _logger.LogError(ex, "DeleteDeviceSignalByDeviceId");
            throw;
        }
    }
}