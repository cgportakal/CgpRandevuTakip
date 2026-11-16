using System.Globalization;
using System.IO;
using System.Text;
using CgpRandevuTakip.Application.Reports;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QColors = QuestPDF.Helpers.Colors;
using QFonts = QuestPDF.Helpers.Fonts;
using QuestPDF.Infrastructure;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>Rapor özetini CSV ve PDF olarak dışa aktarır.</summary>
public interface IReportExportService
{
    void ExportCsv(string path, string institution, ReportSummaryDto summary);

    void ExportPdf(string path, string institution, ReportSummaryDto summary);

    void ExportExcel(string path, string institution, ReportResultDto report);
}

public sealed class ReportExportService : IReportExportService
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportCsv(string path, string institution, ReportSummaryDto summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{institution} · Rapor {summary.RangeText}");
        builder.AppendLine();

        builder.AppendLine("ÖZET;DEĞER");
        builder.AppendLine($"Toplam görüşme;{summary.Total}");
        builder.AppendLine($"Tamamlanan;{summary.Completed} ({summary.CompletionRateText})");
        builder.AppendLine($"Gelmedi;{summary.NoShow} ({summary.NoShowRateText})");
        builder.AppendLine($"İptal;{summary.Cancelled} ({summary.CancelRateText})");
        builder.AppendLine($"Ertelenen;{summary.Postponed}");
        builder.AppendLine($"Planlanan;{summary.Scheduled}");
        builder.AppendLine($"Tekil kişi;{summary.UniquePersons}");
        builder.AppendLine($"Tekrar gelen kişi;{summary.ReturningPersons}");
        builder.AppendLine($"Tekrar talebi;{summary.RepeatRequested}");
        builder.AppendLine($"Ortalama süre;{summary.AverageDurationText}");
        builder.AppendLine($"Toplam süre;{summary.TotalDurationText}");
        builder.AppendLine();

        builder.AppendLine($"{summary.BucketLabel.ToUpper(Culture)};TOPLAM;TAMAMLANDI;GELMEDİ");
        foreach (var point in summary.Points)
            builder.AppendLine($"{Escape(point.Label)};{point.Total};{point.Completed};{point.NoShow}");
        builder.AppendLine();

        builder.AppendLine("KATEGORİ;TOPLAM;TAMAMLANDI;GELMEDİ;PAY");
        foreach (var category in summary.Categories)
            builder.AppendLine(
                $"{Escape(category.CategoryName)};{category.Total};{category.Completed};{category.NoShow};" +
                $"{Share(category.Total, summary.Total)}");
        builder.AppendLine();

        builder.AppendLine("GÖRÜŞMECİ;TOPLAM;TAMAMLANDI;GELMEDİ;İPTAL;PAY");
        foreach (var host in summary.Hosts)
            builder.AppendLine(
                $"{Escape(host.HostName)};{host.Total};{host.Completed};{host.NoShow};{host.Cancelled};" +
                $"{Share(host.Total, summary.Total)}");
        builder.AppendLine();

        builder.AppendLine("SAAT;TOPLAM");
        foreach (var hour in summary.Hours)
            builder.AppendLine($"{hour.Label};{hour.Total}");

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public void ExportPdf(string path, string institution, ReportSummaryDto summary)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(text => text.FontSize(9).FontFamily(QFonts.Lato));

                page.Header().Column(column =>
                {
                    column.Item().Text($"{institution} · Görüşme raporu").FontSize(16).SemiBold();
                    column.Item().Text(summary.RangeText).FontSize(10).FontColor(QColors.Grey.Darken2);
                });

                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Row(row =>
                    {
                        Metric(row, "Toplam", summary.Total.ToString(Culture));
                        Metric(row, "Tamamlanan", summary.CompletionRateText);
                        Metric(row, "Gelmedi", summary.NoShowRateText);
                        Metric(row, "İptal", summary.CancelRateText);
                    });

                    column.Item().Text(
                            $"{summary.UniquePersons} kişi · {summary.ReturningPersons} tekrar gelen · " +
                            $"{summary.RepeatRequested} tekrar talebi · Ortalama {summary.AverageDurationText} · " +
                            $"Toplam {summary.TotalDurationText}")
                        .FontSize(9)
                        .FontColor(QColors.Grey.Darken2);

                    Section(column, summary.BucketLabel, table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(60);
                        });

                        Header(table, new[] { "Dönem", "Toplam", "Tamamlanan", "Gelmedi" });
                        foreach (var point in summary.Points)
                        {
                            AddCell(table, point.Label);
                            AddCell(table, point.Total.ToString(Culture));
                            AddCell(table, point.Completed.ToString(Culture));
                            AddCell(table, point.NoShow.ToString(Culture));
                        }
                    });

                    Section(column, "Kategori dağılımı", table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.4f);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(50);
                        });

                        Header(table, new[] { "Kategori", "Toplam", "Tamamlanan", "Gelmedi", "Pay" });
                        foreach (var category in summary.Categories)
                        {
                            AddCell(table, category.CategoryName);
                            AddCell(table, category.Total.ToString(Culture));
                            AddCell(table, category.Completed.ToString(Culture));
                            AddCell(table, category.NoShow.ToString(Culture));
                            AddCell(table, Share(category.Total, summary.Total));
                        }
                    });

                    Section(column, "Görüşmeci dağılımı", table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.4f);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(50);
                        });

                        Header(table, new[] { "Görüşmeci", "Toplam", "Tamamlanan", "Gelmedi", "İptal", "Pay" });
                        foreach (var host in summary.Hosts)
                        {
                            AddCell(table, host.HostName);
                            AddCell(table, host.Total.ToString(Culture));
                            AddCell(table, host.Completed.ToString(Culture));
                            AddCell(table, host.NoShow.ToString(Culture));
                            AddCell(table, host.Cancelled.ToString(Culture));
                            AddCell(table, Share(host.Total, summary.Total));
                        }
                    });

                    if (summary.Hours.Count > 0)
                    {
                        column.Item().Text("Saat dağılımı").FontSize(11).SemiBold();
                        column.Item().Text(
                                string.Join("   ",
                                    summary.Hours.Select(x => $"{x.Label} · {x.Total}")))
                            .FontSize(9)
                            .FontColor(QColors.Grey.Darken2);
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(path);
    }

    private static void Metric(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Text(label.ToUpper(Culture)).FontSize(8).FontColor(QColors.Grey.Darken1);
            column.Item().Text(value).FontSize(14).SemiBold();
        });
    }

    private static void Section(ColumnDescriptor column, string title, Action<TableDescriptor> build)
    {
        column.Item().Text(title).FontSize(11).SemiBold();
        column.Item().Table(build);
    }

    private static void Header(TableDescriptor table, IReadOnlyList<string> titles)
        => table.Header(header =>
        {
            foreach (var title in titles)
                header.Cell().Background(QColors.Green.Lighten3).Padding(4).Text(title).SemiBold();
        });

    private static void AddCell(TableDescriptor table, string? text)
        => table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3)
            .Text(text ?? "—");

    private static string Share(int value, int total)
        => total == 0 ? "—" : $"{value * 100d / total:0.#}%";

    public void ExportExcel(string path, string institution, ReportResultDto report)
    {
        using var workbook = new XLWorkbook();

        var summary = report.Summary;
        var overview = workbook.Worksheets.Add("Özet");
        overview.Cell(1, 1).Value = $"{institution} · {KindText(report.Kind)}";
        overview.Range(1, 1, 1, 4).Merge().Style.Font.SetBold().Font.SetFontSize(14);
        overview.Cell(2, 1).Value = summary.RangeText;
        overview.Cell(3, 1).Value = $"Toplam {summary.Total} görüşme · Tamamlanma {summary.CompletionRateText} · " +
                                    $"Gelmedi {summary.NoShowRateText} · İptal {summary.CancelRateText} · " +
                                    $"Ortalama {summary.AverageDurationText}";

        WriteHeader(overview, 5, summary.BucketLabel, "Toplam", "Tamamlandı", "Gelmedi");
        var row = 6;
        foreach (var point in summary.Points)
        {
            overview.Cell(row, 1).Value = point.Label;
            overview.Cell(row, 2).Value = point.Total;
            overview.Cell(row, 3).Value = point.Completed;
            overview.Cell(row, 4).Value = point.NoShow;
            row++;
        }

        overview.Columns().AdjustToContents();

        var categories = workbook.Worksheets.Add("Kategori");
        WriteHeader(categories, 1, "Kategori", "Toplam", "Tamamlandı", "Gelmedi", "Pay");
        row = 2;
        foreach (var category in summary.Categories)
        {
            categories.Cell(row, 1).Value = category.CategoryName;
            categories.Cell(row, 2).Value = category.Total;
            categories.Cell(row, 3).Value = category.Completed;
            categories.Cell(row, 4).Value = category.NoShow;
            categories.Cell(row, 5).Value = Share(category.Total, summary.Total);
            row++;
        }

        categories.Columns().AdjustToContents();

        var hostsSheet = workbook.Worksheets.Add("Görüşmeci");
        WriteHeader(hostsSheet, 1, "Görüşmeci", "Toplam", "Tamamlandı", "Gelmedi", "İptal");
        row = 2;
        foreach (var host in summary.Hosts)
        {
            hostsSheet.Cell(row, 1).Value = host.HostName;
            hostsSheet.Cell(row, 2).Value = host.Total;
            hostsSheet.Cell(row, 3).Value = host.Completed;
            hostsSheet.Cell(row, 4).Value = host.NoShow;
            hostsSheet.Cell(row, 5).Value = host.Cancelled;
            row++;
        }

        hostsSheet.Columns().AdjustToContents();

        var hours = workbook.Worksheets.Add("Saat");
        WriteHeader(hours, 1, "Saat", "Toplam");
        row = 2;
        foreach (var hour in summary.Hours)
        {
            hours.Cell(row, 1).Value = hour.Label;
            hours.Cell(row, 2).Value = hour.Total;
            row++;
        }

        hours.Columns().AdjustToContents();

        switch (report.Kind)
        {
            case ReportKind.HostPerformance:
            {
                var sheet = workbook.Worksheets.Add("Performans");
                WriteHeader(sheet, 1, "Görüşmeci", "Toplam", "Tamamlandı", "Geldi", "Gelmedi", "İptal", "Tamamlanma", "Gelmedi oranı", "Ort. süre");
                row = 2;
                foreach (var item in report.HostPerformance)
                {
                    sheet.Cell(row, 1).Value = item.HostName;
                    sheet.Cell(row, 2).Value = item.Total;
                    sheet.Cell(row, 3).Value = item.Completed;
                    sheet.Cell(row, 4).Value = item.Arrived;
                    sheet.Cell(row, 5).Value = item.NoShow;
                    sheet.Cell(row, 6).Value = item.Cancelled;
                    sheet.Cell(row, 7).Value = item.CompletionRateText;
                    sheet.Cell(row, 8).Value = item.NoShowRateText;
                    sheet.Cell(row, 9).Value = item.AverageDurationText;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                break;
            }

            case ReportKind.CategoryTrend:
            {
                var sheet = workbook.Worksheets.Add("Kategori Trendi");
                WriteHeader(sheet, 1, "Kategori", "Toplam");
                row = 2;
                foreach (var trend in report.CategoryTrends)
                {
                    sheet.Cell(row, 1).Value = trend.CategoryName;
                    sheet.Cell(row, 2).Value = trend.Total;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                break;
            }

            case ReportKind.Attendance:
            {
                var sheet = workbook.Worksheets.Add("Katılım");
                WriteHeader(sheet, 1, "Tarih", "Gün", "Geldi", "Tamamlandı", "Gelmedi", "İptal", "Ertelendi", "Planlandı", "Toplam");
                row = 2;
                foreach (var day in report.Attendance)
                {
                    sheet.Cell(row, 1).Value = day.DateLocal;
                    sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
                    sheet.Cell(row, 2).Value = day.WeekdayText;
                    sheet.Cell(row, 3).Value = day.Arrived;
                    sheet.Cell(row, 4).Value = day.Completed;
                    sheet.Cell(row, 5).Value = day.NoShow;
                    sheet.Cell(row, 6).Value = day.Cancelled;
                    sheet.Cell(row, 7).Value = day.Postponed;
                    sheet.Cell(row, 8).Value = day.Scheduled;
                    sheet.Cell(row, 9).Value = day.Total;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                break;
            }

            case ReportKind.Repeat:
            {
                var sheet = workbook.Worksheets.Add("Tekrar");
                WriteHeader(sheet, 1, "Kişi", "Telefon", "Görüşme", "Son görüşme", "Aralık (gün)");
                row = 2;
                foreach (var item in report.RepeatPersons)
                {
                    sheet.Cell(row, 1).Value = item.PersonName;
                    sheet.Cell(row, 2).Value = item.Phone;
                    sheet.Cell(row, 3).Value = item.VisitCount;
                    sheet.Cell(row, 4).Value = item.LastVisitText;
                    sheet.Cell(row, 5).Value = item.DaysSinceLastVisit;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                break;
            }

            case ReportKind.PersonHistory:
            {
                var sheet = workbook.Worksheets.Add("Kişi Geçmişi");
                WriteHeader(sheet, 1, "Kişi", "Telefon", "Tarih", "Görüşmeci", "Kategori", "Durum");
                row = 2;
                foreach (var person in report.PersonHistory)
                {
                    foreach (var entry in person.Entries)
                    {
                        sheet.Cell(row, 1).Value = person.PersonName;
                        sheet.Cell(row, 2).Value = person.Phone;
                        sheet.Cell(row, 3).Value = entry.DateText;
                        sheet.Cell(row, 4).Value = entry.HostName;
                        sheet.Cell(row, 5).Value = entry.CategoryName;
                        sheet.Cell(row, 6).Value = entry.StatusText;
                        row++;
                    }
                }

                sheet.Columns().AdjustToContents();
                break;
            }

            case ReportKind.AuditActivity:
            {
                if (report.AuditActivity is null)
                    break;

                var actions = workbook.Worksheets.Add("Denetim Özet");
                WriteHeader(actions, 1, "Aksiyon", "Adet");
                row = 2;
                foreach (var action in report.AuditActivity.Actions)
                {
                    actions.Cell(row, 1).Value = action.ActionName;
                    actions.Cell(row, 2).Value = action.Count;
                    row++;
                }

                actions.Columns().AdjustToContents();

                var entries = workbook.Worksheets.Add("Denetim Kayıtları");
                WriteHeader(entries, 1, "Zaman", "Kullanıcı", "Aksiyon", "Varlık", "Kimlik");
                row = 2;
                foreach (var entry in report.AuditActivity.Entries)
                {
                    entries.Cell(row, 1).Value = entry.CreatedAtText;
                    entries.Cell(row, 2).Value = entry.UserName;
                    entries.Cell(row, 3).Value = entry.ActionName;
                    entries.Cell(row, 4).Value = entry.EntityText;
                    entries.Cell(row, 5).Value = entry.EntityId ?? "—";
                    row++;
                }

                entries.Columns().AdjustToContents();
                break;
            }
        }

        workbook.SaveAs(path);
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, params string[] titles)
    {
        for (var index = 0; index < titles.Length; index++)
            sheet.Cell(row, index + 1).Value = titles[index];

        var range = sheet.Range(row, 1, row, titles.Length);
        range.Style.Font.SetBold();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAD3");
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static string KindText(ReportKind kind) => kind switch
    {
        ReportKind.HostPerformance => "Görüşmeci performans raporu",
        ReportKind.CategoryTrend => "Kategori trend raporu",
        ReportKind.Attendance => "Katılım raporu",
        ReportKind.Repeat => "Tekrar görüşme raporu",
        ReportKind.PersonHistory => "Kişi geçmişi raporu",
        ReportKind.AuditActivity => "Denetim/aktivite raporu",
        _ => "Görüşme özeti"
    };

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(';') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
            return text;

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
