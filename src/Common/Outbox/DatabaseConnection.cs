using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Common.Outbox;

public sealed class DatabaseConnection : IDatabaseConnection
{
    private readonly ILogger<DatabaseConnection> _logger;
    private readonly SenderSettings _senderSettings;

    public DatabaseConnection(IOptions<SenderSettings> senderSettings, ILogger<DatabaseConnection> logger)
    {
        _logger = logger;
        _senderSettings = senderSettings.Value;
    }

    public async Task<T?> WithConnection<T>(Func<IDbConnection, Task<T?>> func)
    {
        // Connections are internally pooled by Npgsql
        // https://www.npgsql.org/doc/basic-usage.html#pooling
        T? result = default;
        try
        {
            await using var connection = new NpgsqlConnection(_senderSettings.DatabaseConnectionString);
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
            result = await func(connection);
        }
        catch(Exception e)
        {
            _logger.LogCritical(e, "Error on database communication");
        }

        return result;
    }
}

public interface IDatabaseConnection
{
    Task<T?> WithConnection<T>(Func<IDbConnection, Task<T?>> func);
}