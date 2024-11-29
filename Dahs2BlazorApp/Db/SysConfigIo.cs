using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public class SysConfigIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<SysConfigIo> _logger;

    public SysConfigIo(ISqlServer sqlServer, ILogger<SysConfigIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
    }

    public const string UploadPathKey = "UploadPath";
    private const string TestUploadKey = "TestUpload";
    private const string LawUploadTimeKey = "LawUploadTime";
    
    public interface ISysConfig
    {
        string ConfigKey { get; init; }
        string Value { get; init; }
    }

    public record SysConfig(string ConfigKey, string Value) : ISysConfig;

    public async Task UpsertSysConfig(ISysConfig sysConfig)
    {
        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            await connection.ExecuteAsync(@"
                MERGE into [dbo].[SysConfig] AS target 
                USING (SELECT @ConfigKey AS ConfigKey) AS source
                ON (target.ConfigKey = source.ConfigKey)
                WHEN MATCHED THEN
                    UPDATE SET                        
                        Value = @value
                WHEN NOT MATCHED THEN
                    INSERT (ConfigKey, Value)
                    VALUES (@ConfigKey, @Value);                                
                ", sysConfig);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "UpsertSysConfig");
            throw;
        }
    }

    public async Task<string> GetSysConfig(string configKey)
    {
        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            var ret = connection.QueryFirstOrDefault<string>("SELECT Value  FROM [dbo].[SysConfig] WHERE ConfigKey = @ConfigKey",
                new { ConfigKey = configKey });
            return ret ?? "";
        }catch(Exception e)
        {
            _logger.LogError(e, "GetSysConfig {ConfigKey}", configKey);
            throw;
        }
    }

    private async Task<bool> GetSysConfigBool(string configKey)
    {
        var ret = await GetSysConfig(configKey);
        return string.IsNullOrEmpty(ret) || bool.Parse(ret);
    }
    
    public async Task<string> GetUploadPath()
    {
        return await GetSysConfig(UploadPathKey);
    }
    
    public Task SetUploadPath(string path)
    {
        return UpsertSysConfig(new SysConfig(UploadPathKey, path));
    }
    
    public Task<bool> GetTestUpload()
    {
        return GetSysConfigBool(TestUploadKey);
    }
    
    public Task SetTestUpload(bool testUpload)
    {
        return UpsertSysConfig(new SysConfig(TestUploadKey, testUpload.ToString()));
    }
    
    public Task SetLawUploadTime(TimeOnly lawUploadTime)
    {
        return UpsertSysConfig(new SysConfig(LawUploadTimeKey, lawUploadTime.ToString("hh\\:mm\\:ss")));
    }
    
    public Task<TimeOnly> GetLawUploadTime()
    {
        return GetSysConfig(LawUploadTimeKey).ContinueWith(t =>
        {
            try
            {
                return string.IsNullOrEmpty(t.Result)
                    ? new TimeOnly(9, 0, 0)
                    : TimeOnly.ParseExact(t.Result, "hh\\:mm\\:ss", CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetLawUploadTime");
                return new TimeOnly(9, 0, 0);
            }
        });
    }
    
    private const string LoadDataPathKey = "LoadDataPath";
    public Task SetLoadDataPath(string path)
    {
        return UpsertSysConfig(new SysConfig(LoadDataPathKey, path));
    }
    
    public Task<string> GetLoadDataPath()
    {
        return GetSysConfig(LoadDataPathKey);
    }
    
    private const string ModbusDataPathKey = "ModbusDataPath";
    public Task SetModbusDataPath(string path)
    {
        return UpsertSysConfig(new SysConfig(ModbusDataPathKey, path));
    }
    public Task<string> GetModbusDataPath()
    {
        return GetSysConfig(ModbusDataPathKey);
    }
    
    private const string LineTokenKey = "LineToken";
    public Task SetLineToken(string token)
    {
        return UpsertSysConfig(new SysConfig(LineTokenKey, token));
    }
    public Task<string> GetLineToken()
    {
        return GetSysConfig(LineTokenKey);
    }
}