using System.Globalization;
using System.IO;
using System.Text;
using CgpRandevuTakip.Application.Reports;
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

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(';') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
            return text;

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
