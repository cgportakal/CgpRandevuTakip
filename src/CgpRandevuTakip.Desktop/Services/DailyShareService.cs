using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Recipients;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Spikes;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Services;

public sealed record DailySharePreview(
    DateTime Date,
    string InstitutionName,
    IReadOnlyList<AppointmentListItemDto> Rows,
    BitmapSource Image,
    string ImagePath,
    string Message);

public interface IDailyShareService
{
    Task<DailySharePreview> BuildAsync(DateTime date);
    Task<OperationResult> PrepareAndOpenAsync(
        DailySharePreview preview,
        IReadOnlyList<string> recipientIds,
        bool openWhatsApp,
        string? message = null);
    Task<OperationResult> ShareAppointmentAsync(string appointmentId);
    string BuildAppointmentMessage(AppointmentDetailDto detail);
}

/// <summary>Günlük liste PNG, pano, WhatsApp açma ve paylaşım logu.</summary>
public sealed class DailyShareService : IDailyShareService
{
    private readonly IAppointmentService _appointments;
    private readonly ISettingsService _settings;
    private readonly IShareLogRepository _logs;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DailyShareService(
        IAppointmentService appointments,
        ISettingsService settings,
        IShareLogRepository logs,
        ICurrentUser currentUser,
        IClock clock)
    {
        _appointments = appointments;
        _settings = settings;
        _logs = logs;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DailySharePreview> BuildAsync(DateTime date)
    {
        var settings = await _settings.GetAsync();
        var rows = await _appointments.GetDayAsync(date);
        var bitmap = ListImageRenderer.RenderDailyList(settings.InstitutionName, date, rows);

        var directory = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"gunluk-liste-{date:yyyyMMdd}-{DateTime.Now:HHmmss}.png");
        ListImageRenderer.SavePng(bitmap, path);

        var institution = Domain.Common.AppConstants.ResolveInstitutionName(settings.InstitutionName);
        var message = $"{institution} · {date:dd/MM/yyyy} günlük görüşme listesi ({rows.Count} görüşme)";

        return new DailySharePreview(date, institution, rows, bitmap, path, message);
    }

    public async Task<OperationResult> PrepareAndOpenAsync(
        DailySharePreview preview,
        IReadOnlyList<string> recipientIds,
        bool openWhatsApp,
        string? message = null)
    {
        var shareMessage = string.IsNullOrWhiteSpace(message) ? preview.Message : message.Trim();
        var log = new WhatsAppShareLog
        {
            ShareDate = preview.Date.Date,
            RecipientIdsJson = JsonSerializer.Serialize(recipientIds),
            Method = ShareMethod.DailyList,
            ImagePath = preview.ImagePath,
            UserId = _currentUser.UserId,
            CreatedAtUtc = _clock.UtcNow
        };

        try
        {
            if (!TryCopyImage(preview.Image, preview.ImagePath))
            {
                log.Result = ShareResult.Failed;
                await _logs.AddAsync(log);
                return OperationResult.Fail(
                    "Görsel panoya kopyalanamadı. Uygulama açıkken tekrar deneyin.");
            }

            // WhatsApp açılmadan önce panonun oturmasını bekle.
            await Task.Delay(250);

            if (!openWhatsApp)
            {
                log.Result = ShareResult.Prepared;
                await _logs.AddAsync(log);
                return OperationResult.Ok();
            }

            try
            {
                Process.Start(new ProcessStartInfo(
                    $"whatsapp://send?text={Uri.EscapeDataString(shareMessage)}")
                {
                    UseShellExecute = true
                });
                log.Result = ShareResult.Opened;
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo(
                        $"https://wa.me/?text={Uri.EscapeDataString(shareMessage)}")
                    {
                        UseShellExecute = true
                    });
                    log.Result = ShareResult.Opened;
                }
                catch (Exception ex)
                {
                    log.Result = ShareResult.Failed;
                    await _logs.AddAsync(log);
                    return OperationResult.Fail($"WhatsApp açılamadı: {ex.Message}");
                }
            }

            await _logs.AddAsync(log);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            log.Result = ShareResult.Failed;
            await _logs.AddAsync(log);
            return OperationResult.Fail($"Paylaşım hazırlanamadı: {ex.Message}");
        }
    }

    public async Task<OperationResult> ShareAppointmentAsync(string appointmentId)
    {
        var detail = await _appointments.GetDetailAsync(appointmentId);
        if (detail is null)
            return OperationResult.Fail("Görüşme bulunamadı.");

        var message = BuildAppointmentMessage(detail);

        var log = new WhatsAppShareLog
        {
            AppointmentId = appointmentId,
            ShareDate = detail.StartsAtLocal.Date,
            RecipientIdsJson = "[]",
            Method = ShareMethod.AppointmentDetail,
            UserId = _currentUser.UserId,
            CreatedAtUtc = _clock.UtcNow
        };

        try
        {
            Process.Start(new ProcessStartInfo(
                $"whatsapp://send?text={Uri.EscapeDataString(message)}")
            {
                UseShellExecute = true
            });
            log.Result = ShareResult.Opened;
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    $"https://wa.me/?text={Uri.EscapeDataString(message)}")
                {
                    UseShellExecute = true
                });
                log.Result = ShareResult.Opened;
            }
            catch (Exception ex)
            {
                log.Result = ShareResult.Failed;
                await _logs.AddAsync(log);
                return OperationResult.Fail($"WhatsApp açılamadı: {ex.Message}");
            }
        }

        await _logs.AddAsync(log);
        return OperationResult.Ok();
    }

    public string BuildAppointmentMessage(AppointmentDetailDto detail)
    {
        var lines = new List<string>
        {
            $"{detail.PersonFullName}",
            $"{detail.DateLabel} · {detail.TimeRangeLabel}",
            $"Görüşmeci: {detail.HostName}",
            $"Kategori: {detail.CategoryName}",
            $"Yöntem: {detail.MethodText}"
        };

        if (!string.IsNullOrWhiteSpace(detail.Description))
            lines.Add($"Açıklama: {detail.Description}");

        if (!string.IsNullOrWhiteSpace(detail.StudentFullName))
            lines.Add($"Öğrenci: {detail.StudentFullName}");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryCopyImage(BitmapSource bitmap, string imagePath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var data = new DataObject();

                // Windows/WhatsApp uyumluluğu için birden fazla format birlikte verilir.
                data.SetImage(bitmap);
                data.SetData(DataFormats.FileDrop, new[] { imagePath });

                var pngStream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(pngStream);
                pngStream.Position = 0;
                data.SetData("PNG", pngStream, autoConvert: false);

                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch
            {
                Thread.Sleep(80);
            }
        }

        return false;
    }
}
