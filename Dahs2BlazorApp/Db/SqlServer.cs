using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public interface ISqlServer
{
    string ConnectionString { get; init; }

    Task AddRawColumn(SqlConnection connection, string tableName, string mt);
    Task AddColumn(SqlConnection connection, string tableName, string mt);
    Task<List<string>> GetTableColumns(string tableName);
}

public class SqlServer : ISqlServer
{
    public string ConnectionString { get; init; }
    public SqlServer(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("Dahs2")??"";
        Debug.Assert(ConnectionString is not null, "Connection String cannot be empty!");
    }

    public Task AddRawColumn(SqlConnection connection, string tableName, string mt)
    {
        var sql = $@"
                ALTER TABLE {tableName}
                ADD [{mt}] decimal(18, 2)
                ALTER TABLE {tableName}
                ADD [{mt}_st] nvarchar(8)
                ALTER TABLE {tableName}
                ADD [{mt}_baf] decimal(18, 2)
            ";
        return connection.ExecuteAsync(sql);
    }

    public Task AddColumn(SqlConnection connection, string tableName, string mt)
    {
        var sql = $@"
                ALTER TABLE {tableName}
                ADD [{mt}] decimal(18, 2)
                ALTER TABLE {tableName}
                ADD [{mt}_st] nvarchar(8)
            ";
        return connection.ExecuteAsync(sql);
    }

    public async Task<List<string>> GetTableColumns(string tableName)
    {
        var sql = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName
            ";
        var parameters = new Dictionary<string, object>
        {
            { "@tableName", tableName }
        };
        await using var connection = new SqlConnection(ConnectionString);
        var ret = await connection.QueryAsync<string>(sql, parameters);
        return ret is null ? new List<string>() : ret.ToList();
    }

    public async Task AlterTable(string tableName, string columnName, string columnType)
    {
        var sql = $@"
                ALTER TABLE {tableName}
                ADD {columnName} {columnType}
            ";
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(sql);
    }
}