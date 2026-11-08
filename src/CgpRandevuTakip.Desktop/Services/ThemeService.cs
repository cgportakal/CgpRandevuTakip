using CgpRandevuTakip.Application.Settings;
using Microsoft.Win32;
using System.Windows;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>Koyu/açık/otomatik temayı uygular. Otomatik modda Windows kişiselleştirme ayarını izler.</summary>
public sealed class ThemeService
{
    private const string DarkSource = "Themes/Dark.xaml";
    private const string LightSource = "Themes/Light.xaml";

    private readonly ISettingsService _settings;
    private bool _watchingSystem;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
    }

    public AppTheme SelectedTheme { get; private set; } = AppTheme.Auto;

    public bool IsDarkEffective { get; private set; } = true;

    public event EventHandler? ThemeApplied;

    public async Task InitializeAsync()
    {
        var settings = await _settings.GetAsync();
        SelectedTheme = settings.Theme;
        Apply(SelectedTheme);
    }

    public void Apply(AppTheme theme)
    {
        SelectedTheme = theme;
        IsDarkEffective = theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => !IsSystemLightTheme()
        };

        ApplyDictionary(IsDarkEffective ? DarkSource : LightSource);
        WatchSystemTheme(theme == AppTheme.Auto);
        ThemeApplied?.Invoke(this, EventArgs.Empty);
    }

    private void WatchSystemTheme(bool watch)
    {
        if (watch && !_watchingSystem)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _watchingSystem = true;
        }
        else if (!watch && _watchingSystem)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _watchingSystem = false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (SelectedTheme != AppTheme.Auto)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() => Apply(AppTheme.Auto));
    }

    private static void ApplyDictionary(string source)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var dictionaries = app.Resources.MergedDictionaries;
        var uri = new Uri(source, UriKind.Relative);

        var existing = dictionaries.FirstOrDefault(d =>
            string.Equals(d.Source?.OriginalString, DarkSource, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Source?.OriginalString, LightSource, StringComparison.OrdinalIgnoreCase));

        var index = existing is null ? 0 : dictionaries.IndexOf(existing);
        if (existing is not null)
            dictionaries.Remove(existing);

        dictionaries.Insert(index, new ResourceDictionary { Source = uri });
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return key?.GetValue("AppsUseLightTheme") is int value && value > 0;
        }
        catch
        {
            return false;
        }
    }
}
