using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Abstractions;

/// <summary>
/// Rapor için hafif satır görünümü. Tarih/saat UTC'dir; yerel güne çevrim
/// rapor servisinde yapılır.
/// </summary>
public sealed record ReportGuestRow(
    string PersonId,
    string FullName,
    string PhoneE164,
    Attendance? Attendance,
    bool IsPrimary);

public sealed record ReportRow(
    string AppointmentId,
    string PersonId,
    string PersonFullName,
    string PhoneE164,
    string HostUserId,
    string HostName,
    string CategoryId,
    string CategoryName,
    string CategoryColorHex,
    DateTime StartsAtUtc,
    int DurationMinutes,
    AppointmentStatus Status,
    bool IsClosed,
    bool WillRepeat,
    string? RoomId = null,
    string? RoomName = null,
    string? HostTitleName = null,
    IReadOnlyList<string>? ParticipantUserIds = null,
    IReadOnlyList<string>? ParticipantNames = null,
    IReadOnlyList<ReportGuestRow>? Guests = null)
{
    public int GuestCount => Guests?.Count ?? 1;

    public int ParticipantCount => ParticipantUserIds?.Count ?? 1;

    public string HostNameWithTitle => string.IsNullOrWhiteSpace(HostTitleName)
        ? HostName
        : $"{HostName} ({HostTitleName})";
}

/// <summary>Rapor sorgusu; salt-okunur, soft-delete edilmiş kayıtları döndürmez.</summary>
public interface IReportQueries
{
    Task<IReadOnlyList<ReportRow>> GetRowsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryId,
        string? hostUserId,
        string? roomId = null);
}
