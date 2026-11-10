using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.AuditLogs;

public sealed record AuditLogFilter(
    string? UserId,
    AuditAction? Action,
    DateTime? FromLocal,
    DateTime? ToLocal);

public sealed record AuditLogEntryDto(
    string Id,
    DateTime CreatedAtLocal,
    string UserName,
    AuditAction Action,
    string? EntityType,
    string? EntityId,
    string? DetailsJson)
{
    public string CreatedAtText => CreatedAtLocal.ToString("dd/MM/yyyy HH:mm:ss");

    public string ActionText => AuditActionText.Get(Action);

    public string EntityText => string.IsNullOrWhiteSpace(EntityType) ? "—" : EntityType;

    public string EntityIdText => string.IsNullOrWhiteSpace(EntityId) ? "—" : EntityId;

    public string DetailsText => string.IsNullOrWhiteSpace(DetailsJson) ? "—" : DetailsJson;
}

public sealed record AuditLogPageResult(
    IReadOnlyList<AuditLogEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    string? Error = null)
{
    public int PageCount => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < PageCount;

    public string SummaryText
        => TotalCount == 0
            ? "Kayıt bulunamadı."
            : $"{TotalCount} kayıt · sayfa {Page}/{PageCount}";

    public static AuditLogPageResult Fail(string error)
        => new(Array.Empty<AuditLogEntryDto>(), 0, 1, 50, error);
}

public interface IAuditLogService
{
    Task<AuditLogPageResult> QueryAsync(
        AuditLogFilter filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}

public static class AuditActionText
{
    public static string Get(AuditAction action) => action switch
    {
        AuditAction.LoginSucceeded => "Giriş başarılı",
        AuditAction.LoginFailed => "Giriş başarısız",
        AuditAction.Logout => "Çıkış",
        AuditAction.AppointmentCreated => "Görüşme eklendi",
        AuditAction.AppointmentUpdated => "Görüşme güncellendi",
        AuditAction.AppointmentDeleted => "Görüşme silindi",
        AuditAction.AppointmentRestored => "Görüşme geri alındı",
        AuditAction.OutcomeSaved => "Görüşme sonucu kaydedildi",
        AuditAction.ReminderActionTaken => "Hatırlatıcı aksiyonu",
        AuditAction.CategoryCreated => "Kategori eklendi",
        AuditAction.CategoryUpdated => "Kategori güncellendi",
        AuditAction.CategoryDeactivated => "Kategori pasifleştirildi",
        AuditAction.SettingsChanged => "Ayarlar değişti",
        AuditAction.BackupCreated => "Yedek oluşturuldu",
        AuditAction.BackupRestored => "Yedek geri yüklendi",
        AuditAction.SharePrepared => "Paylaşım hazırlandı",
        AuditAction.FirstRunCompleted => "İlk kurulum tamamlandı",
        AuditAction.CategoryDeleted => "Kategori silindi",
        AuditAction.ThemeChanged => "Tema değişti",
        AuditAction.PasswordChanged => "Parola değişti",
        AuditAction.UserCreated => "Kullanıcı eklendi",
        AuditAction.UserUpdated => "Kullanıcı güncellendi",
        AuditAction.UserDeactivated => "Kullanıcı pasifleştirildi",
        AuditAction.SessionLocked => "Oturum kilitlendi",
        AuditAction.SessionUnlocked => "Oturum açıldı",
        AuditAction.AppointmentPurged => "Silinen görüşme temizlendi",
        AuditAction.PasswordResetRequested => "Parola sıfırlama istendi",
        AuditAction.PasswordResetCompleted => "Parola sıfırlama tamamlandı",
        AuditAction.PasswordResetFailed => "Parola sıfırlama başarısız",
        AuditAction.RememberLoginUsed => "Beni hatırla ile giriş",
        AuditAction.AppointmentAnonymized => "Eski görüşme anonimleştirildi",
        _ => action.ToString()
    };
}
