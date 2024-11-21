using System.Collections.Concurrent;
using Dahs2BlazorApp.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public interface IPipe
{
    int Id { get; init; }
    string Name { get; init; }
    string EpaCode { get; set; }
    decimal Area { get; set; }
    decimal BaseO2 { get; set; }
    decimal LightDiameter { get; set; }
    decimal EmissionDiameter { get; set; }
    decimal LastNormalOzone { get; set; }
    DateTime NormalOzoneTime { get; set; }
    decimal LastNormalTemp { get; set; }
    DateTime NormalTempTime { get; set; }

    string UpperSource { get; set; }

    bool AutoLoadState { get; set; }

    decimal? StopThreshold { get; set; }

    public decimal? CurrentLoad { get; set; }
}

public class Pipe : IPipe
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string EpaCode { get; set; }
    public decimal Area { get; set; }
    public decimal BaseO2 { get; set; }
    public decimal LightDiameter { get; set; }
    public decimal EmissionDiameter { get; set; }
    public decimal LastNormalOzone { get; set; }
    public DateTime NormalOzoneTime { get; set; }
    public decimal LastNormalTemp { get; set; }
    public DateTime NormalTempTime { get; set; }

    public required string UpperSource { get; set; }

    public bool AutoLoadState { get; set; }

    public decimal? StopThreshold { get; set; }

    public decimal? CurrentLoad { get; set; }
    
}

public class PipeIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<PipeIo> _logger;
    public ConcurrentDictionary<int, IPipe> PipeMap { get; }

    public PipeIo(ISqlServer sqlServer, ILogger<PipeIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
        PipeMap = new ConcurrentDictionary<int, IPipe>();
    }

    public List<Pipe> Pipes { get; } = new();

    public async Task Init()
    {
        foreach (var pipe in await GetPipeAsync())
        {
            PipeMap.TryAdd(pipe.Id, pipe);
            Pipes.Add(pipe);
        }

        foreach (var pipe in SiteConfig.DefaultPipes)
        {
            if (!PipeMap.TryAdd(pipe.Id, pipe)) continue;
            await InsertPipe(pipe);
        }
    }

    private async Task<IEnumerable<Pipe>> GetPipeAsync()
    {
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.QueryAsync<Pipe>("SELECT *  FROM [dbo].[Pipe]");
    }

    private async Task InsertPipe(IPipe pipe)
    {
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        await connection.ExecuteAsync(@"
                INSERT INTO Pipe (Id, Name, EpaCode, Area, BaseO2, LightDiameter, EmissionDiameter, LastNormalOzone, NormalOzoneTime, LastNormalTemp, NormalTempTime)
                VALUES (@Id, @Name, @EpaCode, @Area, @BaseO2, @LightDiameter, @EmissionDiameter, @LastNormalOzone, @NormalOzoneTime, @LastNormalTemp, @NormalTempTime)
                ", pipe);
    }

    public async Task UpdatePipe(IPipe pipe)
    {
        PipeMap[pipe.Id] = pipe;
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        await connection.ExecuteAsync(@"
                UPDATE Pipe 
                SET Name = @Name, EpaCode = @EpaCode, Area = @Area, BaseO2 = @BaseO2, LightDiameter = @LightDiameter, 
                            EmissionDiameter = @EmissionDiameter, 
                            LastNormalOzone = @LastNormalOzone, NormalOzoneTime = @NormalOzoneTime,
                            LastNormalTemp = @LastNormalTemp, NormalTempTime = @NormalTempTime, 
                            UpperSource = @UpperSource, AutoLoadState = @AutoLoadState, StopThreshold = @StopThreshold
                WHERE Id = @Id                
                ", pipe);
    }
}