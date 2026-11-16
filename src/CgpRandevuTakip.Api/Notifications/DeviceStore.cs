using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Api.Infrastructure;
using Microsoft.Data.Sqlite;

namespace CgpRandevuTakip.Api.Notifications;

public sealed record DeviceRecord(
    string Id,
    string UserId,
    string Platform,
    string? Token,
    string? Endpoint,
    string? P256dh,
    string? Auth,
    string? AppVersion,
    DateTime UpdatedAtUtc);

/// <summary>
/// Push cihaz kayıtları. Masaüstü şemasına dokunmamak için tablo ham SQL ile tutulur.
/// </summary>
public sealed class DeviceStore
{
    private readonly string _connectionString;

    public DeviceStore(ApiEnvironment environment)
    {
        _connectionString = $"Data Source={environment.DatabasePath}";

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ApiDevices (
                Id TEXT NOT NULL PRIMARY KEY,
                UserId TEXT NOT NULL,
                Platform TEXT NOT NULL,
                Token TEXT NULL,
                Endpoint TEXT NULL,
                P256dh TEXT NULL,
                Auth TEXT NULL,
                AppVersion TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                LastSeenAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ApiDevices_UserId ON ApiDevices(UserId);
            CREATE INDEX IF NOT EXISTS IX_ApiDevices_Platform ON ApiDevices(Platform);
            """;
        command.ExecuteNonQuery();
    }

    public async Task<DeviceRecord> UpsertAsync(
        string userId,
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var platform = request.Platform.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow.ToString("O");
        var id = Guid.NewGuid().ToString("N");

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var delete = connection.CreateCommand())
        {
            delete.CommandText = """
                DELETE FROM ApiDevices
                WHERE UserId = $userId AND Platform = $platform
                  AND (
                    ($token IS NOT NULL AND Token = $token)
                    OR ($endpoint IS NOT NULL AND Endpoint = $endpoint)
                  )
                """;
            delete.Parameters.AddWithValue("$userId", userId);
            delete.Parameters.AddWithValue("$platform", platform);
            delete.Parameters.AddWithValue("$token", (object?)request.Token ?? DBNull.Value);
            delete.Parameters.AddWithValue("$endpoint", (object?)request.Endpoint ?? DBNull.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO ApiDevices
                    (Id, UserId, Platform, Token, Endpoint, P256dh, Auth, AppVersion, CreatedAtUtc, UpdatedAtUtc, LastSeenAtUtc)
                VALUES
                    ($id, $userId, $platform, $token, $endpoint, $p256dh, $auth, $appVersion, $now, $now, $now)
                """;
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$userId", userId);
            insert.Parameters.AddWithValue("$platform", platform);
            insert.Parameters.AddWithValue("$token", (object?)request.Token ?? DBNull.Value);
            insert.Parameters.AddWithValue("$endpoint", (object?)request.Endpoint ?? DBNull.Value);
            insert.Parameters.AddWithValue("$p256dh", (object?)request.P256dh ?? DBNull.Value);
            insert.Parameters.AddWithValue("$auth", (object?)request.Auth ?? DBNull.Value);
            insert.Parameters.AddWithValue("$appVersion", (object?)request.AppVersion ?? DBNull.Value);
            insert.Parameters.AddWithValue("$now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return new DeviceRecord(
            id,
            userId,
            platform,
            request.Token,
            request.Endpoint,
            request.P256dh,
            request.Auth,
            request.AppVersion,
            DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<DeviceRecord>> ListAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = userId is null
            ? "SELECT Id, UserId, Platform, Token, Endpoint, P256dh, Auth, AppVersion, UpdatedAtUtc FROM ApiDevices"
            : "SELECT Id, UserId, Platform, Token, Endpoint, P256dh, Auth, AppVersion, UpdatedAtUtc FROM ApiDevices WHERE UserId = $userId";

        if (userId is not null)
            command.Parameters.AddWithValue("$userId", userId);

        var result = new List<DeviceRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new DeviceRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                DateTime.TryParse(reader.GetString(8), out var updated) ? updated : DateTime.UtcNow));
        }

        return result;
    }

    public async Task DeleteAsync(
        string userId,
        string? token,
        string? endpoint,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM ApiDevices
            WHERE UserId = $userId
              AND (($token IS NOT NULL AND Token = $token) OR ($endpoint IS NOT NULL AND Endpoint = $endpoint))
            """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$token", (object?)token ?? DBNull.Value);
        command.Parameters.AddWithValue("$endpoint", (object?)endpoint ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ApiDevices WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
