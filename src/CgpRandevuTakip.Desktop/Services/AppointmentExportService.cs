using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QColors = QuestPDF.Helpers.Colors;
using QFonts = QuestPDF.Helpers.Fonts;
using QuestPDF.Infrastructure;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>Günlük listeyi Excel, CSV ve PDF olarak dışa aktarır; yazdırma belgesi üretir.</summary>
public interface IAppointmentExportService
{
    void ExportExcel(
        string path,
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows);

    void ExportCsv(
        string path,
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows);

    void ExportPdf(
        string path,
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows);

    bool Print(
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows);
}

public sealed class AppointmentExportService : IAppointmentExportService
{
    private static readonly string[] Headers =
    {
        "Saat", "Görüşmeci", "Kişi", "Telefon", "Konu", "Yöntem", "Durum"
    };

    static AppointmentExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportExcel(
        string path,
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Günlük Liste");

        sheet.Cell(1, 1).Value = $"{institution} · {date:dd/MM/yyyy} günlük görüşme listesi";
        sheet.Range(1, 1, 1, Headers.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14);

        for (var index = 0; index < Headers.Length; index++)
            sheet.Cell(3, index + 1).Value = Headers[index];

        var headerRange = sheet.Range(3, 1, 3, Headers.Length);
        headerRange.Style.Font.SetBold();
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAD3");
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        var rowIndex = 4;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.TimeLabel;
            sheet.Cell(rowIndex, 2).Value = row.HostName;
            sheet.Cell(rowIndex, 3).Value = row.PersonFullName;
            sheet.Cell(rowIndex, 4).Value = row.PhoneE164;
            sheet.Cell(rowIndex, 5).Value = row.CategoryName;
            sheet.Cell(rowIndex, 6).Value = row.MethodText;
            sheet.Cell(rowIndex, 7).Value = row.StatusText;
            rowIndex++;
        }

        if (rows.Count == 0)
            sheet.Cell(4, 1).Value = "Bu tarihte görüşme yok.";

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(3);
        workbook.SaveAs(path);
    }

    public void ExportCsv(
        string path,
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{institution} · {date:dd/MM/yyyy} günlük görüşme listesi");
        builder.AppendLine(string.Join(';', Headers));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(';', new[]
            {
                Escape(row.TimeLabel),
                Escape(row.HostName),
                Escape(row.PersonFullName),
                Escape(row.PhoneE164),
                Escape(row.CategoryName),
                Escape(row.MethodText),
                Escape(row.StatusText)
            }));
        }

        if (rows.Count == 0)
            builder.AppendLine("Bu tarihte görüşme yok.");

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public void ExportPdf(
        string path,
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(text => text.FontSize(9).FontFamily(QFonts.Lato));

                page.Header().Column(column =>
                {
                    column.Item().Text($"{institution} · {date:dd/MM/yyyy} günlük görüşme listesi")
                        .FontSize(15).SemiBold();
                    column.Item().Text(BuildSummary(rows)).FontSize(9).FontColor(QColors.Grey.Darken2);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(45);
                        columns.RelativeColumn(2.1f);
                        columns.RelativeColumn(2.3f);
                        columns.RelativeColumn(1.7f);
                        columns.RelativeColumn(2.1f);
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(1.4f);
                    });

                    table.Header(header =>
                    {
                        foreach (var title in new[] { "Saat", "Görüşmeci", "Kişi", "Telefon", "Konu", "Yöntem", "Durum" })
                            header.Cell().Background(QColors.Green.Lighten3).Padding(4).Text(title).SemiBold();
                    });

                    foreach (var row in rows)
                    {
                        AddCell(table, row.TimeLabel);
                        AddCell(table, row.HostName);
                        AddCell(table, row.PersonFullName);
                        AddCell(table, row.PhoneE164);
                        AddCell(table, row.CategoryName);
                        AddCell(table, row.MethodText);
                        AddCell(table, row.StatusText);
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

    public bool Print(
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows)
    {
        var document = BuildFlowDocument(institution, date, rows);
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return false;

        dialog.PrintDocument(
            ((IDocumentPaginatorSource)document).DocumentPaginator,
            "CGP Günlük Liste");
        return true;
    }

    private static void AddCell(TableDescriptor table, string? text)
        => table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).Text(text ?? "—");

    private static string BuildSummary(IReadOnlyList<AppointmentListItemDto> rows)
    {
        var completed = rows.Count(x => x.Status == AppointmentStatus.Completed);
        var noShow = rows.Count(x => x.Status == AppointmentStatus.NoShow);
        return $"{rows.Count} görüşme · {completed} tamamlandı · {noShow} gelmedi";
    }

    private static FlowDocument BuildFlowDocument(
        string institution,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            PageWidth = 1122,
            PageHeight = 793,
            PagePadding = new Thickness(32),
            ColumnWidth = double.PositiveInfinity
        };

        document.Blocks.Add(new Paragraph(new Run($"{institution} · {date:dd/MM/yyyy} günlük görüşme listesi"))
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        document.Blocks.Add(new Paragraph(new Run(BuildSummary(rows)))
        {
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        var widths = new[] { 45.0, 170.0, 190.0, 130.0, 170.0, 85.0, 90.0 };
        foreach (var width in widths)
            table.Columns.Add(new TableColumn { Width = new GridLength(width) });

        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        var header = new TableRow { Background = Brushes.LightGray };
        foreach (var title in Headers)
            header.Cells.Add(new TableCell(new Paragraph(new Run(title)) { FontWeight = FontWeights.SemiBold }));
        group.Rows.Add(header);

        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            foreach (var value in new[]
                     {
                         row.TimeLabel, row.HostName, row.PersonFullName,
                         row.PhoneE164, row.CategoryName, row.MethodText, row.StatusText
                     })
            {
                tableRow.Cells.Add(new TableCell(new Paragraph(new Run(value ?? "—"))
                {
                    Margin = new Thickness(2, 1, 2, 1)
                }));
            }

            group.Rows.Add(tableRow);
        }

        if (rows.Count == 0)
        {
            var empty = new TableRow();
            empty.Cells.Add(new TableCell(new Paragraph(new Run("Bu tarihte görüşme yok."))));
            group.Rows.Add(empty);
        }

        document.Blocks.Add(table);
        return document;
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(';') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
            return text;

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
