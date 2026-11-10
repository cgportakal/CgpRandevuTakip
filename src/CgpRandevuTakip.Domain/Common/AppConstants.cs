namespace CgpRandevuTakip.Domain.Common;

/// <summary>Ürün kimliği ve sürüm sabitleri.</summary>
public static class AppConstants
{
    public const string ProductName = "CGP - Randevu Takip";
    public const string ProductCode = "CGP_RandevuTakip";
    public const string Version = "1.2.1";
    public const int SchemaVersion = 1;
    public const string DefaultTheme = "Auto";
    public const string Copyright = "Tüm hakları saklıdır.";

    /// <summary>Kurum adı ayarlarda boşsa arayüzde gösterilecek kısa marka metni.</summary>
    public const string DefaultInstitutionName = "CGP";

    /// <summary>Boş kurum adını marka metnine düşürür; doluysa kırpılmış değeri döndürür.</summary>
    public static string ResolveInstitutionName(string? institutionName)
        => string.IsNullOrWhiteSpace(institutionName)
            ? DefaultInstitutionName
            : institutionName.Trim();
}
