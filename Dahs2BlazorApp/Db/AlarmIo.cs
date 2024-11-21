using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public class AlarmIo
{
    private readonly ISqlServer _sqlServer;
    private readonly ILogger<AlarmIo> _logger;

    public AlarmIo(ISqlServer sqlServer, ILogger<AlarmIo> logger)
    {
        _sqlServer = sqlServer;
        _logger = logger;
    }

    private interface IAlarm
    {
        long Id { get; set; }
        DateTime CreateDate { get; set; }
        int Level { get; set; }
        string Message { get; set; }
    }
    
    public enum AlarmLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
    
    public record AlarmLevelDisplay(AlarmLevel Level, string Display);
    
    public static IEnumerable<AlarmLevelDisplay> AlarmLevelDisplayList => new[]
    {
        new AlarmLevelDisplay(AlarmLevel.Info, "資訊"),
        new AlarmLevelDisplay(AlarmLevel.Warning, "警告"),
        new AlarmLevelDisplay(AlarmLevel.Error, "錯誤")
    };
    
    public class Alarm : IAlarm
    {
        public long Id { get; set; }
        public DateTime CreateDate { get; set; }
        public int Level { get; set; }
        
        public string LevelDisplay => Level switch
        {
            0 => "資訊",
            1 => "警告",
            2 => "錯誤",
            _ => throw new Exception($"Unhandled AlarmLevel {Level}")
        };
        public required string Message { get; set; }
    }
    
    public async Task<int> AddAlarm(AlarmLevel level, string message)
    {
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.ExecuteAsync(
            "INSERT INTO Alarm (CreateDate, Level, Message) " +
            "VALUES (@CreateDate, @Level, @Message)",
            new { CreateDate = DateTime.Now, Level = (int)level, Message = message });
    }
    
    public async Task<IEnumerable<Alarm>> GetAlarm(AlarmLevel level, DateTime start, DateTime end)
    {
        await using var connection = new SqlConnection(_sqlServer.ConnectionString);
        return await connection.QueryAsync<Alarm>(
            "SELECT * FROM Alarm WHERE CreateDate >= @start AND CreateDate < @end AND Level = @level ORDER BY CreateDate DESC",
            new { start, end, level= (int)level });
    }
}