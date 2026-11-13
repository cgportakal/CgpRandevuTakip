using System.Net;
using System.Net.Mail;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Settings;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>SMTP ile e-posta gönderimi. Ayarlar yönetici tarafından yapılır.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly ISettingsService _settings;

    public SmtpEmailSender(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<OperationResult> SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync();
        return await SendWithSettingsAsync(settings, toAddress, subject, body, cancellationToken);
    }

    /// <summary>
    /// Kurulum sihirbazı gibi henüz kaydedilmemiş ayarlarla test göndermek için.
    /// </summary>
    public async Task<OperationResult> SendWithSettingsAsync(
        AppSettings settings,
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost)
            || string.IsNullOrWhiteSpace(settings.SmtpFromAddress))
        {
            return OperationResult.Fail(
                "E-posta gönderimi yapılandırılmamış. Yönetici Ayarlar > E-posta bölümünü doldurmalıdır.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(
                    settings.SmtpFromAddress,
                    string.IsNullOrWhiteSpace(settings.SmtpFromName)
                        ? "CGP - Randevu Takip"
                        : settings.SmtpFromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(toAddress);

            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.SmtpUseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
                client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);

            await client.SendMailAsync(message, cancellationToken);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"E-posta gönderilemedi: {ex.Message}");
        }
    }
}
