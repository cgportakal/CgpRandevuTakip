using System.Globalization;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Users;

namespace CgpRandevuTakip.Application.Settings;

/// <summary>
/// Kurulum sihirbazı ve ayarlar güncelleme akışının ortak doğrulaması.
/// WPF'ten bağımsızdır; birim testleriyle doğrulanır.
/// </summary>
public static class SetupValidator
{
    public static readonly int[] ReminderMinutes = { 30, 60, 1440 };

    public static string? ValidateInstitution(
        string? name,
        string? email,
        string? phone,
        string? address)
        => InstitutionProfilePolicy.ValidateName(name, required: true)
           ?? InstitutionProfilePolicy.ValidateEmail(email, required: true)
           ?? InstitutionProfilePolicy.ValidatePhone(phone, required: true)
           ?? InstitutionProfilePolicy.ValidateAddress(address, required: true);

    public static string? ValidateAdmin(
        string? displayName,
        string? username,
        string? email,
        string? password,
        string? confirmPassword,
        bool requirePassword = true)
    {
        var name = displayName?.Trim() ?? string.Empty;
        if (name.Length < 3)
            return "Ad soyad en az 3 karakter olmalıdır.";
        if (name.Length > 100)
            return "Ad soyad en fazla 100 karakter olabilir.";

        var usernameError = UsernamePolicy.Validate(UsernamePolicy.Normalize(username));
        if (usernameError is not null)
            return usernameError;

        if (!EmailAddressPolicy.TryNormalize(email, out _))
            return "Yönetici e-posta adresi zorunludur ve geçerli olmalıdır.";

        if (!requirePassword)
            return null;

        var passwordError = PasswordPolicy.Validate(password ?? string.Empty);
        if (passwordError is not null)
            return passwordError;

        return string.Equals(password, confirmPassword, StringComparison.Ordinal)
            ? null
            : "Şifreler birbiriyle eşleşmiyor.";
    }

    public static string? ValidateNewPassword(
        string? currentPassword,
        string? newPassword,
        string? confirmPassword)
    {
        if (string.IsNullOrEmpty(currentPassword))
            return "Şifre değiştirmek için mevcut şifrenizi girin.";
        if (string.IsNullOrEmpty(newPassword))
            return "Yeni şifre zorunludur.";

        var passwordError = PasswordPolicy.Validate(newPassword);
        if (passwordError is not null)
            return passwordError;

        return string.Equals(newPassword, confirmPassword, StringComparison.Ordinal)
            ? null
            : "Yeni şifreler birbiriyle eşleşmiyor.";
    }

    public static string? ValidatePreferences(
        int reminderMinutes,
        string? workHoursStart,
        string? workHoursEnd,
        string? dailyShareTime)
    {
        if (!ReminderMinutes.Contains(reminderMinutes))
            return "Varsayılan hatırlatıcı 30 dakika, 1 saat veya 1 gün olmalıdır.";

        if (!TryParseTime(workHoursStart, out var start) || !TryParseTime(workHoursEnd, out var end))
            return "Çalışma saatleri SS:dd biçiminde olmalıdır.";

        if (start >= end)
            return "Çalışma saati başlangıcı bitişten önce olmalıdır.";

        return TryParseTime(dailyShareTime, out _)
            ? null
            : "Günlük paylaşım saati SS:dd biçiminde olmalıdır.";
    }

    public static string? ValidateSmtp(
        string? host,
        string? port,
        string? fromAddress,
        string? username,
        string? smtpPassword)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        if (!int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort)
            || parsedPort is < 1 or > 65535)
        {
            return "SMTP portu 1–65535 arasında olmalıdır.";
        }

        if (!EmailAddressPolicy.TryNormalize(fromAddress, out _))
            return "SMTP gönderen adresi zorunludur ve geçerli olmalıdır.";

        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(smtpPassword))
            return "SMTP kullanıcı adı girildiğinde parola da zorunludur.";

        return null;
    }

    public static string? ValidatePush(
        bool enabled,
        string? provider,
        string? topic,
        string? token,
        string? chatId)
    {
        if (!enabled)
            return null;

        var normalized = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("ntfy" or "telegram"))
            return "Push sağlayıcısı ntfy veya Telegram olmalıdır.";

        if (normalized == "ntfy")
        {
            return string.IsNullOrWhiteSpace(topic)
                ? "ntfy topic adı zorunludur."
                : null;
        }

        if (string.IsNullOrWhiteSpace(token))
            return "Telegram bot token zorunludur.";
        if (string.IsNullOrWhiteSpace(chatId) || !long.TryParse(chatId.Trim(), out _))
            return "Telegram chat id sayısal olmalıdır.";

        return null;
    }

    public static string? ValidateBackup(
        string? backupDirectory,
        bool encryptionEnabled,
        string? password,
        string? confirmPassword,
        bool driveEnabled,
        string? driveFolder)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            return "Yedek klasörü seçilmelidir.";

        var backupPassword = password ?? string.Empty;
        if (backupPassword.Length is > 0 and < 8)
            return "Yedek parolası en az 8 karakter olmalıdır.";

        if (encryptionEnabled && backupPassword.Length == 0)
            return "Yedek şifreleme açıkken parola zorunludur.";

        if (encryptionEnabled
            && !string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return "Yedek parolası ve doğrulaması aynı olmalıdır.";
        }

        if (!driveEnabled)
            return null;

        if (string.IsNullOrWhiteSpace(driveFolder))
            return "Google Drive klasörü seçilmelidir.";

        if (!encryptionEnabled)
            return "Google Drive'a yükleme için yedek şifreleme açık olmalıdır.";

        return null;
    }

    public static bool TryParseTime(string? value, out TimeSpan time)
        => TimeSpan.TryParseExact(value?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time);
}
