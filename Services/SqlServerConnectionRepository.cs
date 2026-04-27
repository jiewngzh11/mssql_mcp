using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using mssqlMCP.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace mssqlMCP.Services;

/// <summary>
/// SQL Server implementation of the connection repository.
/// </summary>
public class SqlServerConnectionRepository : IConnectionRepository
{
    private const string StoreConnectionStringEnvVar = "MSSQL_MCP_CONNECTION_STORE_CONNECTION_STRING";
    private readonly ILogger<SqlServerConnectionRepository> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly string _storeConnectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqlServerConnectionRepository(
        ILogger<SqlServerConnectionRepository> logger,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;
        _storeConnectionString = Environment.GetEnvironmentVariable(StoreConnectionStringEnvVar) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_storeConnectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable '{StoreConnectionStringEnvVar}' is required when using SQL Server connection store.");
        }
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing SQL Server database for connection storage");
            await using var connection = new SqlConnection(_storeConnectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(@"
IF OBJECT_ID(N'dbo.McpConnections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.McpConnections (
        Name NVARCHAR(200) NOT NULL PRIMARY KEY,
        ConnectionString NVARCHAR(MAX) NOT NULL,
        ServerType NVARCHAR(100) NOT NULL,
        Description NVARCHAR(1000) NULL,
        LastUsed DATETIME2 NULL,
        CreatedOn DATETIME2 NOT NULL
    );
END
");

            _initialized = true;
            _logger.LogInformation("SQL Server connection store initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQL Server connection store");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IEnumerable<ConnectionEntry>> GetAllConnectionsAsync()
    {
        await InitializeAsync();
        await using var connection = new SqlConnection(_storeConnectionString);
        var result = await connection.QueryAsync<ConnectionEntry>(@"
SELECT Name, ConnectionString, ServerType, Description, LastUsed, CreatedOn
FROM dbo.McpConnections
ORDER BY Name");

        foreach (var entry in result)
        {
            entry.ConnectionString = _encryptionService.Decrypt(entry.ConnectionString);
        }

        return result;
    }

    public async Task<IEnumerable<ConnectionEntry>> GetAllConnectionsRawAsync()
    {
        await InitializeAsync();
        await using var connection = new SqlConnection(_storeConnectionString);
        return await connection.QueryAsync<ConnectionEntry>(@"
SELECT Name, ConnectionString, ServerType, Description, LastUsed, CreatedOn
FROM dbo.McpConnections
ORDER BY Name");
    }

    public async Task<ConnectionEntry?> GetConnectionAsync(string name)
    {
        await InitializeAsync();
        await using var connection = new SqlConnection(_storeConnectionString);
        var result = await connection.QueryFirstOrDefaultAsync<ConnectionEntry>(@"
SELECT Name, ConnectionString, ServerType, Description, LastUsed, CreatedOn
FROM dbo.McpConnections
WHERE Name = @Name", new { Name = name });

        if (result != null)
        {
            result.ConnectionString = _encryptionService.Decrypt(result.ConnectionString);
        }

        return result;
    }

    public async Task SaveConnectionAsync(ConnectionEntry connection)
    {
        await InitializeAsync();
        var encryptedConnection = new ConnectionEntry
        {
            Name = connection.Name,
            ConnectionString = _encryptionService.Encrypt(connection.ConnectionString),
            ServerType = connection.ServerType,
            Description = connection.Description,
            LastUsed = connection.LastUsed,
            CreatedOn = connection.CreatedOn
        };

        await SaveConnectionStringDirectlyAsync(encryptedConnection);
    }

    public async Task SaveConnectionStringDirectlyAsync(ConnectionEntry connection)
    {
        await InitializeAsync();
        await using var dbConnection = new SqlConnection(_storeConnectionString);

        if (connection.CreatedOn == default)
        {
            connection.CreatedOn = DateTime.UtcNow;
        }

        await dbConnection.ExecuteAsync(@"
MERGE dbo.McpConnections AS target
USING (SELECT @Name AS Name) AS src
ON target.Name = src.Name
WHEN MATCHED THEN
    UPDATE SET
        ConnectionString = @ConnectionString,
        ServerType = @ServerType,
        Description = @Description,
        LastUsed = @LastUsed
WHEN NOT MATCHED THEN
    INSERT (Name, ConnectionString, ServerType, Description, LastUsed, CreatedOn)
    VALUES (@Name, @ConnectionString, @ServerType, @Description, @LastUsed, @CreatedOn);", connection);
    }

    public async Task DeleteConnectionAsync(string name)
    {
        await InitializeAsync();
        await using var connection = new SqlConnection(_storeConnectionString);
        await connection.ExecuteAsync("DELETE FROM dbo.McpConnections WHERE Name = @Name", new { Name = name });
    }

    public async Task UpdateLastUsedAsync(string name)
    {
        await InitializeAsync();
        try
        {
            await using var connection = new SqlConnection(_storeConnectionString);
            await connection.ExecuteAsync(@"
UPDATE dbo.McpConnections
SET LastUsed = @LastUsed
WHERE Name = @Name", new { Name = name, LastUsed = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating LastUsed for connection {Name}", name);
        }
    }
}
