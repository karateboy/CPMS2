using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dahs2BlazorApp.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public interface IDeviceMeasuring
{
    int DeviceId { get; set; }
    int PipeId { get; set; }
    string Sid { get; set; }
    bool InputReg { get; set; }
    int Address { get; set; }
    int DataType { get; set; }
    bool MidEndian { get; set; }
}

public class DeviceMeasuring : IDeviceMeasuring
{
    public int DeviceId { get; set; }
    public int PipeId { get; set; }
    public required string Sid { get; set; }
    public bool InputReg { get; set; }
    public int Address { get; set; }
    public int DataType { get; set; }
    
    public bool MidEndian { get; set; }
}

    
public class DeviceMeasuringIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<DeviceMeasuringIo> _logger;

    public enum ModbusDataType
    {
        Int16 = 0,
        Int32 = 1,
        Float = 2,
        Double = 3,
        Uint16 = 4,
        Uint32 = 5,
    }

    public record DataTypeDef(ModbusDataType Type, string Name, ushort Count)
    {
        public int TypeInt => (int) Type;
    }
        
    public static readonly List<DataTypeDef> DataTypeDefList= new ()
    {
        new DataTypeDef(ModbusDataType.Int16, "Int16", 1),
        new DataTypeDef(ModbusDataType.Int32, "Int32", 2),
        new DataTypeDef(ModbusDataType.Float, "Float", 2),
        new DataTypeDef(ModbusDataType.Double, "Double", 4),
        new DataTypeDef(ModbusDataType.Uint16, "UINT16", 1),
        new DataTypeDef(ModbusDataType.Uint32, "UINT32", 2),
    };
        
    public static readonly Dictionary<ModbusDataType, DataTypeDef> DataTypeDefMap = 
        DataTypeDefList.ToDictionary(def => def.Type, def => def);
        
    public readonly ImmutableDictionary<ModbusDataType, string> DataTypeNameMap;
    public DeviceMeasuringIo(ISqlServer sqlServer, ILogger<DeviceMeasuringIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
        var map = ImmutableDictionary<ModbusDataType, string>.Empty;
        foreach (var t in Helper.GetEnumValues<ModbusDataType>())
        {
            map = map.Add(t, t.ToString());
        }
        DataTypeNameMap = map;
    }

    private readonly ConcurrentDictionary<int, ImmutableList<IDeviceMeasuring>> _deviceMeasuringMap = new ();

    public async Task Init()
    {
        var measures = await GetDeviceMeasuringListAsync();
        foreach (var measuring in measures)
        {
            if(_deviceMeasuringMap.TryGetValue(measuring.DeviceId, out var measuringList))
                _deviceMeasuringMap[measuring.DeviceId] = measuringList.Add(measuring);
            else
                _deviceMeasuringMap[measuring.DeviceId] = ImmutableList.Create<IDeviceMeasuring>(measuring);
        }
    }

    public ImmutableList<IDeviceMeasuring> GetDeviceMeasuringList(int deviceId)
    {
        if (_deviceMeasuringMap.TryGetValue(deviceId, out var measuringList))
            return measuringList;
        
        return ImmutableList<IDeviceMeasuring>.Empty;
    }
    
    public int? GetDeviceIdByPipeAndSid(int pipeId, string sid)
    {
        return _deviceMeasuringMap.Values
            .SelectMany(list => list).Where(measuring => measuring.PipeId == pipeId && measuring.Sid == sid)
            .Select(measuring=> (int?)measuring.DeviceId).FirstOrDefault();
    }

    public async Task<IEnumerable<DeviceMeasuring>> GetDeviceMeasuringListAsync()
    {
        var sql = "SELECT * FROM DeviceMeasuring";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.QueryAsync<DeviceMeasuring>(sql);
    }

    public async Task UpsertDeviceMeasuring(IDeviceMeasuring deviceMeasuring)
    {
        if (_deviceMeasuringMap.TryGetValue(deviceMeasuring.DeviceId, out var measuringList))
        {
            _deviceMeasuringMap[deviceMeasuring.DeviceId] = 
                measuringList.RemoveAll(m => m.PipeId == deviceMeasuring.PipeId && m.Sid == deviceMeasuring.Sid).Add(deviceMeasuring);
        }
        else
        {
            _deviceMeasuringMap[deviceMeasuring.DeviceId] = ImmutableList.Create(deviceMeasuring);
        }


        var sql = @"
                MERGE INTO DeviceMeasuring As target
                USING (SELECT @PipeId AS PipeId, @Sid AS Sid) AS source
                ON target.PipeId = source.PipeId AND target.Sid = source.Sid
                WHEN MATCHED THEN
                    UPDATE SET
                        InputReg = @InputReg,
                        Address = @Address,
                        DataType = @DataType,
                        MidEndian = @MidEndian
                WHEN NOT MATCHED THEN
                    INSERT (DeviceId, PipeId, Sid, InputReg, Address, DataType, MidEndian)
                    VALUES (@DeviceId, @PipeId, @Sid, @InputReg, @Address, @DataType, @MidEndian);";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        await connection.ExecuteAsync(sql, deviceMeasuring);
    }

    public async Task<int> DeleteDeviceMeasuringAsync(IDeviceMeasuring deviceMeasuring)
    {
        if (_deviceMeasuringMap.TryGetValue(deviceMeasuring.DeviceId, out var measuringList))
        {
            _deviceMeasuringMap[deviceMeasuring.DeviceId] = 
                measuringList.RemoveAll(m => m.PipeId == deviceMeasuring.PipeId && m.Sid == deviceMeasuring.Sid);
        }

        var sql = @"
                DELETE FROM DeviceMeasuring
                WHERE DeviceId = @DeviceId AND PipeId = @PipeId AND Sid = @Sid;";

        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(sql, deviceMeasuring);
    }

    public async Task<int> DeleteDeviceMeasuringByDeviceIdAsync(int deviceId)
    {
        _deviceMeasuringMap.TryRemove(deviceId, out _);

        var sql = @"
                DELETE FROM DeviceMeasuring
                WHERE DeviceId = @DeviceId;";
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(sql, new { DeviceId = deviceId });
    }
}