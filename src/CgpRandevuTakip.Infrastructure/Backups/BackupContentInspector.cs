using System.Globalization;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Settings;
using Microsoft.Data.Sqlite;

namespace CgpRandevuTakip.Infrastructure.Backups;

/// <summary>
/// Bir yedekteki (veya geçici olarak açılmış) SQLite veritabanının içeriğini
/// salt okunur olarak özetler. Eski yedeklerde eksik tablolar olabileceği için
/// her sorgu öncesinde tablo varlığı kontrol edilir.
/// </summary>
public static class BackupContentInspector
{
    public static Task<BackupContentSummary> ReadAsync(
        string databasePath,
        bool hasLogo,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();

        var userCount = Count(connection, "Users");
        var personCount = Count(connection, "Persons");
        var categoryCount = Count(connection, "Categories");
        var appointmentCount = Count(connection, "Appointments");
        var deletedAppointments = ColumnExists(connection, "Appointments", "IsDeleted")
            ? (int)Count(connection, "Appointments", "IsDeleted = 1")
            : 0;
        var outcomeCount = Count(connection, "AppointmentOutcomes");
        var reminderCount = Count(connection, "ReminderDispatches");
        var auditCount = Count(connection, "AuditLogs");
        var settingCount = Count(connection, "Settings");
        var shareLogCount = Count(connection, "WhatsAppShareLogs");
        var contentVersionCount = Count(connection, "ContentVersions");
        var recipientCount = Count(connection, "Recipients");
        var backupRecordCount = Count(connection, "BackupRecords");

        var firstAppointment = MinDate(connection, "Appointments", "StartsAtUtc");
        var lastAppointment = MaxDate(connection, "Appointments", "StartsAtUtc");

        var lastChange = Max(
            MaxDate(connection, "Appointments", "UpdatedAtUtc"),
            MaxDate(connection, "Settings", "UpdatedAtUtc"),
            MaxDate(connection, "AuditLogs", "CreatedAtUtc"),
            MaxDate(connection, "ContentVersions", "PublishedAtUtc"));

        var settings = ReadSettings(connection);
        bool Has(string key) => settings.TryGetValue(key, out var value)
                                && !string.IsNullOrWhiteSpace(value);

        var summary = new BackupContentSummary(
            userCount,
            personCount,
            categoryCount,
            appointmentCount,
            appointmentCount - deletedAppointments,
            deletedAppointments,
            outcomeCount,
            reminderCount,
            auditCount,
            settingCount,
            shareLogCount,
            contentVersionCount,
            recipientCount,
            backupRecordCount,
            ToLocal(firstAppointment),
            ToLocal(lastAppointment),
            ToLocal(lastChange),
            HasInstitutionSettings: Has(SettingsKeys.InstitutionEmail)
                                     || Has(SettingsKeys.InstitutionPhone)
                                     || Has(SettingsKeys.InstitutionAddress),
            HasSmtpSettings: Has(SettingsKeys.SmtpHost),
            HasPushSettings: Has(SettingsKeys.PushToken) || Has(SettingsKeys.PushTopic),
            HasBackupSettings: Has(SettingsKeys.BackupDirectory)
                               || Has(SettingsKeys.BackupPassword)
                               || Has(SettingsKeys.BackupEncryptionEnabled),
            HasDriveSettings: Has(SettingsKeys.GoogleClientId)
                              || Has(SettingsKeys.GoogleRefreshToken)
                              || Has(SettingsKeys.GoogleDriveEnabled),
            HasConnectionSettings: Has(BackupSettingKeys.SyncBaseUrlKey),
            hasLogo);

        return Task.FromResult(summary);
    }

    private static Dictionary<string, string> ReadSettings(SqliteConnection connection)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!TableExists(connection, "Settings"))
            return result;

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Settings;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                result[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        }

        return result;
    }

    private static int Count(SqliteConnection connection, string table, string? where = null)
    {
        if (!TableExists(connection, table))
            return 0;

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"" +
                              (where is null ? ";" : $" WHERE {where};");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        if (!TableExists(connection, table))
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.FieldCount > 1
                && string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime? MinDate(SqliteConnection connection, string table, string column)
    {
        if (!ColumnExists(connection, table, column))
            return null;

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT MIN(\"{column}\") FROM \"{table}\";";
        return ParseDate(command.ExecuteScalar());
    }

    private static DateTime? MaxDate(SqliteConnection connection, string table, string column)
    {
        if (!ColumnExists(connection, table, column))
            return null;

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT MAX(\"{column}\") FROM \"{table}\";";
        return ParseDate(command.ExecuteScalar());
    }

    private static DateTime? ParseDate(object? value)
    {
        if (value is null or DBNull)
            return null;

        if (value is DateTime dateTime)
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (!DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return null;
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    private static DateTime? Max(params DateTime?[] values)
    {
        DateTime? result = null;
        foreach (var value in values)
        {
            if (value is null)
                continue;

            if (result is null || value > result)
                result = value;
        }

        return result;
    }

    private static DateTime? ToLocal(DateTime? utc)
        => utc is null
            ? null
            : TimeZoneInfo.ConvertTimeFromUtc(utc.Value, TimeZoneInfo.Local);
}
