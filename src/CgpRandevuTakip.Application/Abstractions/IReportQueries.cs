using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

/// <summary>
/// Rapor için hafif satır görünümü. Tarih/saat UTC'dir; yerel güne çevrim
/// rapor servisinde yapılır.
/// </summary>
public sealed record ReportRow(
    string AppointmentId,
    string PersonId,
    string HostUserId,
    string HostName,
    string CategoryId,
    string CategoryName,
    string CategoryColorHex,
    DateTime StartsAtUtc,
    int DurationMinutes,
    AppointmentStatus Status,
    bool IsClosed,
    bool WillRepeat);

/// <summary>Rapor sorgusu; salt-okunur, soft-delete edilmiş kayıtları döndürmez.</summary>
public interface IReportQueries
{
    Task<IReadOnlyList<ReportRow>> GetRowsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryId,
        string? hostUserId);
}
