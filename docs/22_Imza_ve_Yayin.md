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
# Ara sürüm (yalnızca Setup)
.\Kurulum-Olustur.ps1 -Version 1.5.1 -CertificateThumbprint "ABCDEF..."

# Kararlı sürüm (Setup + taşınabilir ZIP)
.\Kurulum-Olustur.ps1 -Version 1.6.0 -Portable -CertificateThumbprint "ABCDEF..."
```

İmzalama sırası:
1. `dotnet publish`
2. Uygulama EXE imzalanır
3. Taşınabilir ZIP oluşturulur (yalnızca -Portable ile; imzalı EXE ile)
4. Inno Setup EXE üretilir
5. Setup EXE imzalanır

İmza parolasını komut geçmişine yazmamak için ortam değişkeni/CI secret kullanın.

### 2.1 Otomatik yayın (GitHub Actions)

`v*` etiketi push edildiğinde `.github/workflows/release.yml` çalışır:

1. `AppConstants.Version` ile etiket sürümü karşılaştırılır (uyuşmazsa durur).
2. Release derlemesi + tüm testler çalıştırılır.
3. Inno Setup kurulur; `Kurulum-Olustur.ps1 -Portable` ile paketler üretilir.
4. GitHub Release oluşturulur ve Setup/ZIP/SHA256/Dev.cer yüklenir.

İmzalama:
- `SIGNING_PFX_BASE64` ve `SIGNING_PFX_PASSWORD` secret'ları tanımlıysa CA/üretim sertifikasıyla imzalanır.
- Tanımlı değilse self-signed geliştirme sertifikası üretilir (SmartScreen uyarısı sürebilir).

Manuel deneme: Actions > Release > Run workflow (`version` girin, `publish` kapalıyken yalnızca paket üretir).
Etiketler `v1.7.0` biçiminde olmalıdır; `-rc1` içeren sürümler **prerelease** olarak yayınlanır.

## 3. GitHub üzerinden indirme

- Ana sayfa: README “İndir” bölümü.
- Sürüm sayfası: <https://github.com/cgportakal/CgpRandevuTakip/releases/latest>
- Yüklenen dosyalar:
  - `CGP-RandevuTakip-<sürüm>-Setup.exe` (her sürüm)
  - `CGP-RandevuTakip-<sürüm>-portable.zip` (yalnızca kararlı sürümler)
  - `SHA256SUMS.txt`
  - `CGP-RandevuTakip-Dev.cer`

Yeni sürüm yayınlarken:
```powershell
gh release create v1.5.1 --title "CGP - Randevu Takip 1.5.1" --notes-file release.md `
  "dist\CGP-RandevuTakip-1.5.1-Setup.exe" `
  "dist\CGP-RandevuTakip-Dev.cer" `
  "dist\SHA256SUMS.txt"
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
