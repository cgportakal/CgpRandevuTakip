using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;

namespace CgpRandevuTakip.Application.Appointments;

/// <summary>Seri isteği: sıklık ve ilk randevu dahil toplam kayıt sayısı.</summary>
public sealed record SeriesRequest(SeriesFrequency Frequency, int Count)
{
    public bool IsValid
        => Count is >= SeriesScheduleCalculator.MinCount and <= SeriesScheduleCalculator.MaxCount
           && Enum.IsDefined(Frequency);
}

/// <summary>Seri önizlemesinde her tarihin durumu.</summary>
public enum SeriesSlotState
{
    Create = 0,
    Conflict = 1,
    Past = 2
}

public sealed record SeriesSlotDto(
    int Index,
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    SeriesSlotState State,
    string? Note,
    IReadOnlyList<AppointmentConflictDto> Conflicts)
{
    public bool WillCreate => State == SeriesSlotState.Create;

    public string DateText => StartsAtLocal.ToString("dd/MM/yyyy");

    public string TimeText => $"{StartsAtLocal:HH:mm} – {EndsAtLocal:HH:mm}";

    public string PositionText => $"{Index}. görüşme";

    public string StateText => State switch
    {
        SeriesSlotState.Create => "Oluşturulacak",
        SeriesSlotState.Conflict => "Çakışıyor",
        SeriesSlotState.Past => "Geçmiş",
        _ => "—"
    };
}

public sealed record SeriesPlanDto(
    IReadOnlyList<SeriesSlotDto> Slots,
    string? Error = null)
{
    public int CreateCount => Slots.Count(x => x.WillCreate);

    public int SkippedCount => Slots.Count - CreateCount;

    public bool CanSave => Error is null && CreateCount > 0;

    public string Summary => Error is not null
        ? Error
        : SkippedCount == 0
            ? $"{CreateCount} görüşme oluşturulacak."
            : $"{CreateCount} görüşme oluşturulacak · {SkippedCount} tarih atlanacak.";
}

/// <summary>Seri oluşturma sonucu; hangi tarihin neden atlandığı korunur.</summary>
public sealed record SeriesCreateResult(
    bool Success,
    string? Error,
    string? SeriesId,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyList<SeriesSlotDto> Slots)
{
    /// <summary>Sunucu bağlantısı olmadığı için işlem kuyruğa alındı.</summary>
    public bool Queued { get; init; }

    public static SeriesCreateResult Fail(string error, IReadOnlyList<SeriesSlotDto>? slots = null)
        => new(false, error, null, 0, slots?.Count ?? 0, slots ?? Array.Empty<SeriesSlotDto>());

    public static SeriesCreateResult Ok(
        string seriesId,
        int createdCount,
        int skippedCount,
        IReadOnlyList<SeriesSlotDto> slots)
        => new(true, null, seriesId, createdCount, skippedCount, slots);

    public static SeriesCreateResult QueuedOk(
        string seriesId,
        int createdCount,
        IReadOnlyList<SeriesSlotDto> slots)
        => new(true, null, seriesId, createdCount, 0, slots) { Queued = true };

    public string Summary => Success
        ? SkippedCount == 0
            ? $"{CreatedCount} görüşme oluşturuldu."
            : $"{CreatedCount} görüşme oluşturuldu · {SkippedCount} tarih atlandı."
        : Error ?? "Seri oluşturulamadı.";
}

/// <summary>Seri iptali sonucu: yalnızca gelecekteki planlanmış kayıtlar iptal edilir.</summary>
public sealed record SeriesCancelResult(
    bool Success,
    int CancelledCount,
    int SkippedCount,
    string? Error)
{
    /// <summary>Sunucu bağlantısı olmadığı için işlem kuyruğa alındı.</summary>
    public bool Queued { get; init; }

    public static SeriesCancelResult Fail(string error) => new(false, 0, 0, error);

    public static SeriesCancelResult Ok(int cancelled, int skipped)
        => new(true, cancelled, skipped, null);

    public static SeriesCancelResult QueuedOk()
        => new(true, 0, 0, null) { Queued = true };

    public string Summary => Success
        ? SkippedCount == 0
            ? $"{CancelledCount} görüşme iptal edildi."
            : $"{CancelledCount} görüşme iptal edildi · {SkippedCount} kayıt değişmedi."
        : Error ?? "Seri iptal edilemedi.";
}

/// <summary>Seriye sonradan kayıt ekleme isteği (işlem başına 1–12).</summary>
public sealed record SeriesAppendRequest(int Count)
{
    public bool IsValid => Count is >= 1 and <= SeriesScheduleCalculator.MaxCount;
}

public sealed record SeriesAppendPlanDto(
    IReadOnlyList<SeriesSlotDto> Slots,
    int NewSeriesCount,
    string? Error = null)
{
    public int CreateCount => Slots.Count(x => x.WillCreate);

    public int SkippedCount => Slots.Count - CreateCount;

    public bool CanSave => Error is null && CreateCount > 0;

    public string Summary => Error is not null
        ? Error
        : SkippedCount == 0
            ? $"{CreateCount} yeni görüşme eklenecek."
            : $"{CreateCount} yeni görüşme eklenecek · {SkippedCount} tarih atlanacak.";
}

public sealed record SeriesAppendResult(
    bool Success,
    string? Error,
    int CreatedCount,
    int SkippedCount,
    int NewSeriesCount,
    IReadOnlyList<SeriesSlotDto> Slots)
{
    /// <summary>Sunucu bağlantısı olmadığı için işlem kuyruğa alındı.</summary>
    public bool Queued { get; init; }

    public static SeriesAppendResult Fail(string error, IReadOnlyList<SeriesSlotDto>? slots = null)
        => new(false, error, 0, 0, 0, slots ?? Array.Empty<SeriesSlotDto>());

    public static SeriesAppendResult Ok(
        int created,
        int skipped,
        int newSeriesCount,
        IReadOnlyList<SeriesSlotDto> slots)
        => new(true, null, created, skipped, newSeriesCount, slots);

    public static SeriesAppendResult QueuedOk(int count)
        => new(true, null, count, 0, 0, Array.Empty<SeriesSlotDto>()) { Queued = true };

    public string Summary => Success
        ? SkippedCount == 0
            ? $"{CreatedCount} görüşme eklendi. Seri artık {NewSeriesCount} kayıt."
            : $"{CreatedCount} görüşme eklendi · {SkippedCount} tarih atlandı. Seri artık {NewSeriesCount} kayıt."
        : Error ?? "Seriye kayıt eklenemedi.";
}

/// <summary>
/// Seri düzenleme isteği. Yalnızca gelecekteki planlanmış kayıtlar etkilenir;
/// geçmiş, kapanmış ve iptal edilmiş kayıtlar korunur.
/// </summary>
public sealed record SeriesUpdateInput(
    string HostUserId,
    string CategoryId,
    int DurationMinutes,
    AppointmentMethod Method,
    string? Description,
    string? StudentFirstName,
    string? StudentLastName,
    TimeSpan? TimeOfDay,
    IReadOnlyList<int> ReminderOffsetsMinutes);

public sealed record SeriesUpdateResult(
    bool Success,
    string? Error,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Sunucu bağlantısı olmadığı için işlem kuyruğa alındı.</summary>
    public bool Queued { get; init; }

    public static SeriesUpdateResult Fail(string error)
        => new(false, error, 0, 0, Array.Empty<string>());

    public static SeriesUpdateResult Ok(int updated, int skipped, IReadOnlyList<string> warnings)
        => new(true, null, updated, skipped, warnings);

    public static SeriesUpdateResult QueuedOk()
        => new(true, null, 0, 0, Array.Empty<string>()) { Queued = true };

    public string Summary => Success
        ? SkippedCount == 0
            ? $"{UpdatedCount} görüşme güncellendi."
            : $"{UpdatedCount} görüşme güncellendi · {SkippedCount} kayıt atlandı."
        : Error ?? "Seri güncellenemedi.";
}
