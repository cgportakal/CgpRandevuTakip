using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Desktop.Spikes;

/// <summary>
/// Günlük liste görselini gerçek bir WPF görsel ağacından üretir (PrintScreen kullanılmaz).
/// PII içeren gerçek kayıtlar Sprint 5'te bağlanacak; bu sürüm spike amaçlı örnek satırlar kullanır.
/// </summary>
public static class ListImageRenderer
{
    private sealed record SampleRow(string Time, string Person, string Host, string Category, string Status);

    public static BitmapSource RenderSample(string institutionName, DateTime date)
    {
        var rows = new[]
        {
            new SampleRow("09:00", "Örnek Veli", "Ayşe Yılmaz", "Veli Görüşmesi", "Planlandı"),
            new SampleRow("09:30", "Örnek Öğrenci", "Mehmet Demir", "Öğrenci Görüşmesi", "Planlandı"),
            new SampleRow("10:00", "Örnek Veli", "Ayşe Yılmaz", "Rehberlik/Danışmanlık", "Onaylandı"),
            new SampleRow("10:30", "Örnek Veli", "Zeynep Kaya", "Bilgi Talebi", "Planlandı"),
            new SampleRow("11:00", "Örnek Öğrenci", "Mehmet Demir", "Disiplin/İnceleme", "Planlandı"),
            new SampleRow("11:30", "Örnek Veli", "Zeynep Kaya", "Şikâyet", "Planlandı")
        };

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
            Text = $"{date:dd/MM/yyyy} · {rows.Length} görüşme · Oluşturma {DateTime.Now:HH:mm}",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = Brush("#5D7166"),
            Margin = new Thickness(0, 6, 0, 24)
        });

        var table = new StackPanel();
        table.Children.Add(BuildHeader());

        foreach (var row in rows)
            table.Children.Add(BuildRow(row));

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

        root.Measure(new Size(1000, double.PositiveInfinity));
        root.Arrange(new Rect(new Size(1000, root.DesiredSize.Height)));
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth),
            (int)Math.Ceiling(root.ActualHeight),
            96,
            96,
            PixelFormats.Pbgra32);

        bitmap.Render(root);
        bitmap.Freeze();
        return bitmap;
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static Grid BuildHeader()
    {
        var grid = CreateGrid();
        grid.Background = Brush("#F6FAF7");

        AddCell(grid, 0, "SAAT", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 1, "AD SOYAD", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 2, "HOST", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 3, "KATEGORİ", 11, "#5D7166", FontWeights.SemiBold);
        AddCell(grid, 4, "DURUM", 11, "#5D7166", FontWeights.SemiBold);

        return grid;
    }

    private static Grid BuildRow(SampleRow row)
    {
        var grid = CreateGrid();
        grid.Background = Brushes.White;

        AddCell(grid, 0, row.Time, 13, "#12211A", FontWeights.SemiBold);
        AddCell(grid, 1, row.Person, 13, "#12211A", FontWeights.Normal);
        AddCell(grid, 2, row.Host, 13, "#33463C", FontWeights.Normal);
        AddCell(grid, 3, row.Category, 12, "#2E7D5B", FontWeights.SemiBold);
        AddCell(grid, 4, row.Status, 12, "#2E7D5B", FontWeights.SemiBold);

        return grid;
    }

    private static Grid CreateGrid()
    {
        var grid = new Grid
        {
            Margin = new Thickness(0)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

        return grid;
    }

    private static void AddCell(Grid grid, int column, string text, double size, string color, FontWeight weight)
    {
        var cell = new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
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
