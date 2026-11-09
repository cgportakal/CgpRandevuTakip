using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Spikes;

/// <summary>
/// Günlük liste görselini gerçek bir WPF görsel ağacından üretir (PrintScreen kullanılmaz).
/// Görsel 2x çözünürlükte üretilir; buton, imleç ve uygulama chrome'u içermez.
/// </summary>
public static class ListImageRenderer
{
    private const double ContentWidth = 1000;

    public static BitmapSource RenderDailyList(
        string institutionName,
        DateTime date,
        IReadOnlyList<AppointmentListItemDto> rows)
    {
        var title = AppConstants.ResolveInstitutionName(institutionName);
        var stack = new StackPanel();
        var root = new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(40, 32, 40, 36),
            Child = stack
        };

        stack.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#2E7D5B")
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Günlük Görüşme Listesi",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#12211A"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"{date:dd/MM/yyyy} · {rows.Count} görüşme · Oluşturma {DateTime.Now:HH:mm}",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = Brush("#5D7166"),
            Margin = new Thickness(0, 6, 0, 24)
        });

        var table = new StackPanel();
        table.Children.Add(BuildHeader());

        if (rows.Count == 0)
        {
            table.Children.Add(new Border
            {
                Padding = new Thickness(16, 24, 16, 24),
                Child = new TextBlock
                {
                    Text = "Bu tarihte görüşme yok.",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    Foreground = Brush("#5D7166"),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            });
        }
        else
        {
            foreach (var row in rows)
                table.Children.Add(BuildRow(row));
        }

        stack.Children.Add(new Border
        {
            BorderBrush = Brush("#DCE8E0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = table
        });

        stack.Children.Add(new TextBlock
        {
            Text = "CGP - Randevu Takip · Bu liste kurum içi kullanım içindir.",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            Foreground = Brush("#5D7166"),
            Margin = new Thickness(0, 16, 0, 0)
        });

        root.Measure(new Size(ContentWidth, double.PositiveInfinity));
        root.Arrange(new Rect(new Size(ContentWidth, root.DesiredSize.Height)));
        root.UpdateLayout();

        // 2x çözünürlük: 192 DPI.
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth * 2),
            (int)Math.Ceiling(root.ActualHeight * 2),
            192,
            192,
            PixelFormats.Pbgra32);

        bitmap.Render(root);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Tanılama ekranı için örnek satırlarla görsel üretir.</summary>
    public static BitmapSource RenderSample(string institutionName, DateTime date)
    {
        var rows = new List<AppointmentListItemDto>
        {
            Sample("09:00", "Örnek Veli", "Ayşe Yılmaz", "Veli Görüşmesi"),
            Sample("09:30", "Örnek Öğrenci", "Mehmet Demir", "Öğrenci Görüşmesi"),
            Sample("10:00", "Örnek Veli", "Ayşe Yılmaz", "Rehberlik/Danışmanlık"),
            Sample("10:30", "Örnek Veli", "Zeynep Kaya", "Bilgi Talebi")
        };

        return RenderDailyList(institutionName, date, rows);
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static AppointmentListItemDto Sample(
        string time,
        string person,
        string host,
        string category)
    {
        var date = DateTime.Today;
        var parsed = TimeSpan.Parse(time);

        return new AppointmentListItemDto(
            Guid.NewGuid().ToString("N"),
            date.Add(parsed),
            date.Add(parsed).AddMinutes(30),
            30,
            person.Split(' ')[0],
            person.Split(' ').Last(),
            "+905550000000",
            "host",
            host,
            "category",
            category,
            "#2E7D5B",
            AppointmentMethod.FaceToFace,
            AppointmentStatus.Scheduled,
            false,
            false,
            null);
    }

    private static Grid BuildHeader()
    {
        var grid = CreateGrid();
        grid.Background = Brush("#F6FAF7");

        AddCell(grid, 0, "SAAT", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 1, "AD SOYAD", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 2, "GÖRÜŞMECİ", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 3, "KATEGORİ", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 4, "YÖNTEM", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 5, "DURUM", 11, "#5D7166", FontWeights.SemiBold);

        return grid;
    }

    private static Grid BuildRow(AppointmentListItemDto row)
    {
        var grid = CreateGrid();
        grid.Background = Brushes.White;

        AddCell(grid, 0, row.TimeLabel, 13, "#12211A", FontWeights.SemiBold);
        AddCell(grid, 1, row.PersonFullName, 13, "#12211A", FontWeights.Normal);
        AddCell(grid, 2, row.HostName, 13, "#33463C", FontWeights.Normal);
        AddCell(grid, 3, row.CategoryName, 12, "#2E7D5B", FontWeights.SemiBold);
        AddCell(grid, 4, row.MethodText, 12, "#33463C", FontWeights.Normal);
        AddCell(grid, 5, row.StatusText, 12, "#2E7D5B", FontWeights.SemiBold);

        return grid;
    }

    private static Grid CreateGrid()
    {
        var grid = new Grid { Margin = new Thickness(0) };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        return grid;
    }

    private static void AddCell(
        Grid grid,
        int column,
        string text,
        double size,
        string color,
        FontWeight weight)
    {
        var cell = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            BorderBrush = Brush("#DCE8E0"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = size,
                FontWeight = weight,
                Foreground = Brush(color),
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
