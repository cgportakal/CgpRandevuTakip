using System.Globalization;
using System.IO;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Domain.Enums;
using ClosedXML.Excel;

namespace CgpRandevuTakip.Desktop.Services;

public sealed record ExcelImportRow(
    int LineNumber,
    AppointmentInput Input);

public sealed record ExcelImportError(int LineNumber, string Message);

public sealed record ExcelImportResult(
    IReadOnlyList<ExcelImportRow> Rows,
    IReadOnlyList<ExcelImportError> Errors);

public interface IExcelImportService
{
    Task<ExcelImportResult> ReadAsync(string path, CancellationToken cancellationToken = default);
    void CreateTemplate(string path);
}

/// <summary>
/// Excel (.xlsx) dosyasından görüşme içe aktarma. Başlıklar Türkçedir;
/// görüşmeci ve kategori adları mevcut kayıtlarla eşleştirilir.
/// </summary>
public sealed class ExcelImportService : IExcelImportService
{
    private static readonly string[] Headers =
    {
        "Ad", "Soyad", "Telefon", "Tarih", "Saat", "Süre (dk)",
        "Görüşmeci", "Kategori", "Yöntem", "Açıklama", "Öğrenci Adı", "Öğrenci Soyadı"
    };

    private readonly IHostService _hosts;
    private readonly ICategoryService _categories;

    public ExcelImportService(IHostService hosts, ICategoryService categories)
    {
        _hosts = hosts;
        _categories = categories;
    }

    public async Task<ExcelImportResult> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<ExcelImportRow>();
        var errors = new List<ExcelImportError>();

        var hosts = await _hosts.GetSelectableHostsAsync();
        var categories = await _categories.GetAllAsync();

        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("Excel dosyasında çalışma sayfası yok.");

        var headerMap = ReadHeaders(sheet);
        if (!headerMap.ContainsKey("ad") || !headerMap.ContainsKey("soyad")
            || !headerMap.ContainsKey("telefon") || !headerMap.ContainsKey("tarih")
            || !headerMap.ContainsKey("saat") || !headerMap.ContainsKey("gorusmeci")
            || !headerMap.ContainsKey("kategori"))
        {
            throw new InvalidOperationException(
                "Başlıklar eksik. Beklenen sütunlar: " + string.Join(", ", Headers) +
                ". Şablonu indirip kullanabilirsiniz.");
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            try
            {
                var firstName = Cell(row, headerMap, "ad").Trim();
                var lastName = Cell(row, headerMap, "soyad").Trim();
                var phone = Cell(row, headerMap, "telefon").Trim();
                var date = ParseDate(row.Cell(headerMap["tarih"]), rowNumber);
                var time = ParseTime(row.Cell(headerMap["saat"]), rowNumber);
                var duration = ParseDuration(row, headerMap);
                var hostName = Cell(row, headerMap, "görüşmeci").Trim();
                var categoryName = Cell(row, headerMap, "kategori").Trim();
                var methodText = Cell(row, headerMap, "yöntem").Trim();
                var description = Cell(row, headerMap, "açıklama").Trim();
                var studentFirst = Cell(row, headerMap, "öğrenci adı").Trim();
                var studentLast = Cell(row, headerMap, "öğrenci soyadı").Trim();

                var host = hosts.FirstOrDefault(x =>
                    string.Equals(x.DisplayName, hostName, StringComparison.CurrentCultureIgnoreCase));
                if (host is null)
                    throw new InvalidDataException($"Görüşmeci bulunamadı: {hostName}");

                var category = categories.FirstOrDefault(x =>
                    string.Equals(x.Name, categoryName, StringComparison.CurrentCultureIgnoreCase));
                if (category is null)
                    throw new InvalidDataException($"Kategori bulunamadı: {categoryName}");

                if (category.IsActive is false)
                    throw new InvalidDataException($"Kategori pasif: {categoryName}");

                var input = new AppointmentInput(
                    firstName,
                    lastName,
                    phone,
                    date.Date + time,
                    duration,
                    host.Id,
                    category.Id,
                    ParseMethod(methodText),
                    string.IsNullOrWhiteSpace(description) ? null : description,
                    string.IsNullOrWhiteSpace(studentFirst) ? null : studentFirst,
                    string.IsNullOrWhiteSpace(studentLast) ? null : studentLast,
                    new[] { 30 });

                rows.Add(new ExcelImportRow(rowNumber, input));
            }
            catch (Exception ex)
            {
                errors.Add(new ExcelImportError(rowNumber, ex.Message));
            }
        }

        return new ExcelImportResult(rows, errors);
    }

    public void CreateTemplate(string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("İçe Aktarma");

        for (var index = 0; index < Headers.Length; index++)
            sheet.Cell(1, index + 1).Value = Headers[index];

        sheet.Row(1).Style.Font.SetBold();
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAD3");

        sheet.Cell(2, 1).Value = "Ali";
        sheet.Cell(2, 2).Value = "Veli";
        sheet.Cell(2, 3).Value = "0532 123 45 67";
        sheet.Cell(2, 4).Value = DateTime.Today.AddDays(1).ToString("dd.MM.yyyy");
        sheet.Cell(2, 5).Value = "10:00";
        sheet.Cell(2, 6).Value = 30;
        sheet.Cell(2, 7).Value = "Görüşmeci Adı";
        sheet.Cell(2, 8).Value = "Veli Görüşmesi";
        sheet.Cell(2, 9).Value = "Yüz yüze";
        sheet.Cell(2, 10).Value = "Örnek açıklama";
        sheet.Cell(2, 11).Value = "Öğrenci";
        sheet.Cell(2, 12).Value = "Adı";

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        var headerRow = sheet.Row(1);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = Normalize(cell.GetString());
            if (!string.IsNullOrWhiteSpace(key))
                map[key] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static string Cell(IXLRow row, IReadOnlyDictionary<string, int> map, string key)
        => map.TryGetValue(Normalize(key), out var column) ? row.Cell(column).GetString() : string.Empty;

    private static DateTime ParseDate(IXLCell cell, int row)
    {
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime();

        var text = cell.GetString().Trim();
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d.M.yyyy", "d/M/yyyy" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            return date;

        throw new InvalidDataException($"Satır {row}: tarih okunamadı ({text}).");
    }

    private static TimeSpan ParseTime(IXLCell cell, int row)
    {
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().TimeOfDay;

        if (cell.DataType == XLDataType.TimeSpan)
            return cell.GetTimeSpan();

        var text = cell.GetString().Trim();
        if (TimeSpan.TryParseExact(text, new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out var time))
            return time;

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            return date.TimeOfDay;

        throw new InvalidDataException($"Satır {row}: saat okunamadı ({text}).");
    }

    private static int ParseDuration(IXLRow row, IReadOnlyDictionary<string, int> map)
    {
        if (!map.TryGetValue("sure (dk)", out var column))
            return 30;

        var cell = row.Cell(column);
        if (cell.IsEmpty())
            return 30;

        if (cell.DataType == XLDataType.Number)
            return (int)cell.GetDouble();

        return int.TryParse(cell.GetString().Trim(), out var minutes) ? minutes : 30;
    }

    private static AppointmentMethod ParseMethod(string text)
    {
        var normalized = Normalize(text);
        return normalized switch
        {
            "telefon" => AppointmentMethod.Phone,
            "whatsapp" => AppointmentMethod.WhatsApp,
            "internet" or "online" => AppointmentMethod.Online,
            _ => AppointmentMethod.FaceToFace
        };
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ı", "i")
            .Replace("ş", "s")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ö", "o")
            .Replace("ç", "c");
}
