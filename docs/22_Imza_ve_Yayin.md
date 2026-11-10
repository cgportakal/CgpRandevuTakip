# 22 — Dijital İmza ve Yayın Rehberi

## 1. Geliştirme sertifikası (self-signed)

```powershell
.\Imza-Sertifikasi-Olustur.ps1
```

Bu komut:
- `CN=CGP - Randevu Takip (Gelistirme)` kod imzalama sertifikasını `Cert:\CurrentUser\My` içinde oluşturur.
- Ortak anahtarı `dist\CGP-RandevuTakip-Dev.cer` olarak dışa aktarır.
- Parmak izini `signing.thumbprint` dosyasına yazar (git'e eklenmez).

`Kurulum-Olustur.ps1` bu sertifikayı otomatik bulur ve şu dosyaları imzalar:
- `CgpRandevuTakip.Desktop.exe`
- `CGP-RandevuTakip-<sürüm>-Setup.exe`

> Self-signed sertifika varsayılan olarak güvenilir değildir. Windows “Bilinmeyen yayıncı” gösterebilir.
> Bu yalnızca geliştirme/test içindir.

## 2. Üretim imzası (CA sertifikası)

CA'dan alınan PFX dosyasıyla:

```powershell
.\Kurulum-Olustur.ps1 -Version 1.2.1 -PfxPath "C:\sertifika\cgp.pfx" -PfxPassword "..."
```

veya sertifika `Cert:\CurrentUser\My` içine kurulduysa:

```powershell
.\Kurulum-Olustur.ps1 -Version 1.2.1 -CertificateThumbprint "ABCDEF..."
```

İmzalama sırası:
1. `dotnet publish`
2. Uygulama EXE imzalanır
3. Taşınabilir ZIP oluşturulur (imzalı EXE ile)
4. Inno Setup EXE üretilir
5. Setup EXE imzalanır

İmza parolasını komut geçmişine yazmamak için ortam değişkeni/CI secret kullanın.

## 3. GitHub üzerinden indirme

- Ana sayfa: README “İndir” bölümü.
- Sürüm sayfası: <https://github.com/cgportakal/CgpRandevuTakip/releases/latest>
- Yüklenen dosyalar:
  - `CGP-RandevuTakip-<sürüm>-Setup.exe`
  - `CGP-RandevuTakip-<sürüm>-portable.zip`

Yeni sürüm yayınlarken:
```powershell
gh release create v1.2.1 --title "CGP - Randevu Takip 1.2.1" --notes-file release.md `
  "dist\CGP-RandevuTakip-1.2.1-portable.zip" `
  "dist\CGP-RandevuTakip-1.2.1-Setup.exe"
```

## 4. Doğrulama

```powershell
Get-AuthenticodeSignature "dist\CGP-RandevuTakip-1.2.1-Setup.exe"
```

- `Status = Valid`: sertifika güvenilir ve imza geçerli.
- `Status = UnknownError`: imza var ancak sertifika zinciri güvenilir değil (self-signed).
- `SignerCertificate.Subject`: imzalayan sertifika.

## 5. Notlar

- SmartScreen itibarı, CA sertifikası ve indirme sayısıyla zamanla oluşur.
- Otomatik güncelleme, GitHub Releases’teki Setup/ZIP dosyalarını kullanır.
- İmzasız/yanlış imzalı dosyaları yayınlamayın.
