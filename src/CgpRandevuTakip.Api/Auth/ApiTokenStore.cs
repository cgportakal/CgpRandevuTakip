using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CgpRandevuTakip.Api.Auth;

/// <summary>
/// Yenileme belirteçlerini (refresh token) yalnızca SHA-256 özetiyle saklar.
/// Masaüstü veritabanı şemasına dokunmamak için tablo ham SQL ile oluşturulur.
/// </summary>
public sealed class ApiTokenStore
{
    private readonly string _connectionString;

    public ApiTokenStore(Infrastructure.ApiEnvironment environment)
    {
        _connectionString = $"Data Source={environment.DatabasePath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ApiRefreshTokens (
                Id TEXT NOT NULL PRIMARY KEY,
                UserId TEXT NOT NULL,
                TokenHash TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                ExpiresAtUtc TEXT NOT NULL,
                RevokedAtUtc TEXT NULL,
                DeviceName TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ApiRefreshTokens_TokenHash ON ApiRefreshTokens(TokenHash);
            CREATE INDEX IF NOT EXISTS IX_ApiRefreshTokens_UserId ON ApiRefreshTokens(UserId);
            """;
        command.ExecuteNonQuery();
    }

    public string Create(string userId, string? deviceName, TimeSpan lifetime)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ApiRefreshTokens (Id, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc, DeviceName)
            VALUES ($id, $userId, $hash, $created, $expires, $device)
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$hash", Hash(token));
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$expires", DateTime.UtcNow.Add(lifetime).ToString("O"));
        command.Parameters.AddWithValue("$device", (object?)deviceName ?? DBNull.Value);
        command.ExecuteNonQuery();

        return token;
    }

    public string? Validate(string token)
    {
        var hash = Hash(token);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId FROM ApiRefreshTokens
            WHERE TokenHash = $hash AND RevokedAtUtc IS NULL AND ExpiresAtUtc > $now
            """;
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

        return command.ExecuteScalar() as string;
    }

    public void Revoke(string token)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ApiRefreshTokens SET RevokedAtUtc = $now
            WHERE TokenHash = $hash AND RevokedAtUtc IS NULL
            """;
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$hash", Hash(token));
        command.ExecuteNonQuery();
    }

    public void RevokeAll(string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ApiRefreshTokens SET RevokedAtUtc = $now
            WHERE UserId = $userId AND RevokedAtUtc IS NULL
            """;
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$userId", userId);
        command.ExecuteNonQuery();
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
