using System.Collections.Concurrent;
using Dahs2BlazorApp.Db;
using Dahs2BlazorApp.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db
{
    public interface IDevice
    {
        int Id { get; set; }
        string Name { get; set; }
        int PipeId { get; set; }
        string ModbusAddress { get; set; }
        int Port { get; set; }
        int SlaveId { get; set; }
        bool Spare { get; set; }
        bool Authenticated { get; set; }
        bool BigEndian { get; set; }
        public bool Output { get; set; }
    }

    public class Device : IDevice
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int PipeId { get; set; }
        public required string ModbusAddress { get; set; }
        public int Port { get; set; }
        public int SlaveId { get; set; }
        public bool Spare { get; set; }
        public bool Authenticated { get; set; }
        public bool BigEndian { get; set; }
        public bool Output { get; set; }
    }
}

public class DeviceIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<DeviceIo> _logger;
    private readonly DeviceMeasuringIo _measuringIo;
    public DeviceIo(ISqlServer sqlServer, ILogger<DeviceIo> logger, DeviceMeasuringIo deviceMeasuringIo)
    {
        _sqlServer = sqlServer;
        _logger = logger;
        _measuringIo = deviceMeasuringIo;
    }

    public ConcurrentDictionary<int, IDevice> DeviceMap {get; init;}= new();
    public async Task Init()
    {
        var devices = await GetDevicesAsync();
        foreach (var device in devices)
        {
            DeviceMap.TryAdd(device.Id, device);
        }
    }

    public List<IDevice> GetDevicesByPipeId(int pipeId) => DeviceMap.Values.Where(d => d.PipeId == pipeId).OrderBy(device=>device.Id).ToList();

    private async Task<IEnumerable<Device>> GetDevicesAsync()
    {
        var sql = "SELECT * FROM Device";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.QueryAsync<Device>(sql);
    }

    public async Task<int> UpsertDevice(IDevice device)
    {
        var sql = @"
                MERGE INTO Device As target
                USING (SELECT @Id AS Id) AS source
                ON target.Id = source.Id
                WHEN MATCHED THEN
                    UPDATE SET
                        Name = @Name,
                        PipeId = @PipeId,
                        ModbusAddress = @ModbusAddress,
                        Port = @Port,
                        SlaveId = @SlaveId,
                        Spare = @Spare,
                        Authenticated = @Authenticated,
                        BigEndian = @BigEndian,
                        Output = @Output
                WHEN NOT MATCHED THEN                                        
                    INSERT (Name, PipeId, ModbusAddress, Port, SlaveId, Spare, Authenticated, BigEndian, Output)                                        
                    VALUES (@Name, @PipeId, @ModbusAddress, @Port, @SlaveId, @Spare, @Authenticated, @BigEndian, @Output)
                OUTPUT Inserted.Id;
                    ";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        int id = await connection.QuerySingleAsync<int>(sql, device);
        
        // It is new device
        
        if (device.Id == 0)
        {
            device.Id = id;
            DeviceMap[device.Id] = device;
        }
        else
        {
            DeviceMap[device.Id] = device;
            DeviceManager.StopDevice(deviceId: id);
        }
        
        return id;
    }

    public async Task DeleteDevice(int id)
    {
        await _measuringIo.DeleteDeviceMeasuringByDeviceIdAsync(id);
        DeviceMap.TryRemove(id, out _);
        DeviceManager.StopDevice(deviceId: id);
        var sql = "DELETE FROM Device WHERE Id = @Id";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}

