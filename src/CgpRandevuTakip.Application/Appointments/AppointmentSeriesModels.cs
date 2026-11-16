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
    public static SeriesCreateResult Fail(string error, IReadOnlyList<SeriesSlotDto>? slots = null)
        => new(false, error, null, 0, slots?.Count ?? 0, slots ?? Array.Empty<SeriesSlotDto>());

    public static SeriesCreateResult Ok(
        string seriesId,
        int createdCount,
        int skippedCount,
        IReadOnlyList<SeriesSlotDto> slots)
        => new(true, null, seriesId, createdCount, skippedCount, slots);

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
    public static SeriesCancelResult Fail(string error) => new(false, 0, 0, error);

    public static SeriesCancelResult Ok(int cancelled, int skipped)
        => new(true, cancelled, skipped, null);

    public string Summary => Success
        ? SkippedCount == 0
            ? $"{CancelledCount} görüşme iptal edildi."
            : $"{CancelledCount} görüşme iptal edildi · {SkippedCount} kayıt değişmedi."
        : Error ?? "Seri iptal edilemedi.";
}
