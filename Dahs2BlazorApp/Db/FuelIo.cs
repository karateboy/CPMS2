using System.Collections;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public class FuelIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<FuelIo> _logger;

    public FuelIo(ISqlServer sqlServer, ILogger<FuelIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
    }

    public interface IFuelRecord
    {
        string FuelName { get; set; }
        double Usage { get; set; }
        string Unit { get; set; }
        double SulfurPercent { get; set; }
    }

    public interface IFuelTypeInfo
    {
        string FuelName { get; set; }
        decimal A23EmissionFactor { get; set; }
        decimal? SulfurPercent { get; set; }
        decimal A22EmissionFactor { get; set; }
    }

    public class FuelTypeInfo : IFuelTypeInfo
    {
        public required string FuelName { get; set; }
        public decimal A23EmissionFactor { get; set; }
        public decimal? SulfurPercent { get; set; }
        public decimal A22EmissionFactor { get; set; }
    }

    public class FuelRecord : IFuelRecord
    {
        public required string FuelName { get; set; }
        public double Usage { get; set; }
        public required string Unit { get; set; }
        public required double SulfurPercent { get; set; }
    }

    public class FuelUsage
    {
        public required List<FuelRecord> FuelList { get; set; }
    }

    public async Task<IEnumerable<FuelTypeInfo>> GetFuelTypeInfoLists()
    {
        const string sql = @"SELECT [FuelName]
                        ,[A22EmissionFactor]
                        ,[SulfurPercent]
                        ,[A23EmissionFactor]                                                        
                        FROM [dbo].[FuelTypeInfo]";

        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            return await connection.QueryAsync<FuelTypeInfo>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFuelTypeInfo error");
            throw;
        }
    }
    
    public async Task<Dictionary<string, IFuelTypeInfo>> GetFuelTypeInfo()
    {
        const string sql = @"SELECT [FuelName]
                        ,[A22EmissionFactor]
                        ,[SulfurPercent]
                        ,[A23EmissionFactor]                                                        
                        FROM [dbo].[FuelTypeInfo]";

        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            var rets = await connection.QueryAsync<FuelTypeInfo>(sql);
            var map = new Dictionary<string, IFuelTypeInfo>();
            foreach (var fuelType in rets)
            {
                map.Add(fuelType.FuelName, fuelType);
            }

            return map;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFuelTypeInfo error");
            throw;
        }
    }

    public async Task<int> UpsertFuelTypeInfoAsync(IFuelTypeInfo fuelTypeInfo)
    {
        string sql = @"UPDATE [dbo].[FuelTypeInfo]
                        SET [A22EmissionFactor] = @A22EmissionFactor
                                                ,[SulfurPercent] = @SulfurPercent
                                                ,[A23EmissionFactor] = @A23EmissionFactor
                        WHERE [FuelName] = @FuelName
                    IF(@@ROWCOUNT = 0)
                    BEGIN
                        INSERT INTO [dbo].[FuelTypeInfo]
                            ([FuelName],[A22EmissionFactor],[SulfurPercent],[A23EmissionFactor])
                        VALUES
                        (@FuelName, @A22EmissionFactor, @SulfurPercent, @A23EmissionFactor)
                    END";
        try
        {
            await using var connection = new SqlConnection(_sqlServer.ConnectionString);
            return await connection.ExecuteAsync(sql, fuelTypeInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertFuelTypeInfoAsync error");
            throw;
        }
    }
}