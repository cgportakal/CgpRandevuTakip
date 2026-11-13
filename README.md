# CGP - Randevu Takip

[![CI](https://github.com/cgportakal/CgpRandevuTakip/actions/workflows/ci.yml/badge.svg)](https://github.com/cgportakal/CgpRandevuTakip/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/cgportakal/CgpRandevuTakip)](https://github.com/cgportakal/CgpRandevuTakip/releases/latest)

## İndir

- **Kurulum (Setup EXE):** [CGP-RandevuTakip-1.5.0-Setup.exe](https://github.com/cgportakal/CgpRandevuTakip/releases/download/v1.5.0/CGP-RandevuTakip-1.5.0-Setup.exe)
- **Taşınabilir ZIP:** [CGP-RandevuTakip-1.5.0-portable.zip](https://github.com/cgportakal/CgpRandevuTakip/releases/download/v1.5.0/CGP-RandevuTakip-1.5.0-portable.zip)
- **Tüm sürümler:** [Releases](https://github.com/cgportakal/CgpRandevuTakip/releases/latest)

> Kurulum EXE'si per-user çalışır; yönetici hakkı istemez. Taşınabilir ZIP'i yazılabilir bir klasöre açıp
> `CgpRandevuTakip.Desktop.exe` dosyasını çalıştırın. Dijital imza ayrıntıları:
> [docs/22_Imza_ve_Yayin.md](docs/22_Imza_ve_Yayin.md).

Windows 11 x64 masaüstünde çalışan, randevu/görüşme kayıtlarını yöneten, günlük listeyi
WhatsApp üzerinden paylaşan ve zamanı gelen görüşmeler için hatırlatma üreten kurumsal uygulama.

> Durum: **1.4.0 hazır** — kurulum sihirbazı, kurum profili ve logosu, canlı tema, SMTP/push/yedekleme adımları, güncelleme modu, canlı sistem durumu, kalıcı oturum, elle kilit ve arka plan bildirimleri tamamlandı.
> Sürüm: 1.5.0 · Tarih: 14.11.2026 · 295 test geçiyor

## Neden bu proje?

Görüşmeler hâlâ defter, Excel veya dağınık mesajlarla takip ediliyorsa; kayıt kayboluyor,
günlük liste hazırlamak zaman alıyor, sırası gelen kişi aranmıyor ve görüşme sonuçları
kayda geçmiyor. Bu uygulama tek ekrandan günlük akışı yönetir.

## Doküman dizini

| # | Doküman | İçerik |
|---|---------|--------|
| 00 | [Vizyon ve Kapsam](docs/00_Vizyon_ve_Kapsam.md) | Problem, hedefler, paydaşlar, kapsam, kısıtlar |
| 01 | [Analiz ve Eksikler](docs/01_Analiz_ve_Eksikler.md) | Çelişkiler, eksik gereksinimler, MVP, acil maddeler |
| 02 | [Gereksinimler](docs/02_Gereksinimler.md) | FR/NFR, iş kuralları, kullanıcı hikâyeleri, kabul kriterleri |
| 03 | [Mimari ve Veri Modeli](docs/03_Mimari_ve_Veri_Modeli.md) | Katmanlar, teknoloji, ERD, akışlar, güvenlik |
| 04 | [Proje Planı ve Süreç](docs/04_Proje_Plani_ve_Surec.md) | SDLC, sprintler, DoR/DoD, test, risk, sürüm |
| 05 | [KVKK ve Veri Güvenliği](docs/05_KVKK_ve_Veri_Guvenligi.md) | Veri envanteri, saklama, üçüncü taraflar, haklar |
| 06 | [Karar Kaydı ve Açık Sorular](docs/06_Karar_Kaydi_ve_Sorular.md) | Alınan kararlar, cevap bekleyen sorular |
| 07 | [Sprint 0 Sonuç Raporu](docs/07_Sprint0_Raporu.md) | İskelet, testler, spike durumu, Sprint 1'e devir |
| 08 | [Sprint 1 Sonuç Raporu](docs/08_Sprint1_Raporu.md) | Giriş, roller, tema, kategoriler, spike sonuçları, Sprint 2'ye devir |
| 09 | [Sprint 2 Sonuç Raporu](docs/09_Sprint2_Raporu.md) | Görüşme CRUD, günlük liste, arama, host yönetimi, UI doğrulaması |
| 10 | [Sprint 3 Sonuç Raporu](docs/10_Sprint3_Raporu.md) | Sonuç/durum geçişleri, tekrar randevu, kullanıcı yönetimi, oturum kilidi |
| 11 | [Sprint 3.5 Sonuç Raporu](docs/11_Sprint3_5_Raporu.md) | CRUD doğrulaması, silinenleri geri alma, 30 gün temizliği, host filtresi |
| 12 | [Sprint 4 Sonuç Raporu](docs/12_Sprint4_Raporu.md) | Hatırlatıcı motoru, bildirim, push, tepsi, sessiz saatler |
| 13 | [Sprint 4.5 UAT Raporu](docs/13_Sprint4_5_UAT_Raporu.md) | Gerçek toast/push/tray/auto-start doğrulaması |
| 14 | [Push Kurulum Rehberi](docs/14_Push_Kurulum_Rehberi.md) | ntfy ve Telegram kurulumu, test |
| 15 | [E-posta ile Şifre Sıfırlama](docs/15_Email_Sifre_Sifirlama_Rehberi.md) | SMTP ayarları, beni hatırla, kurtarma akışı |
| 16 | [Sprint 5 Sonuç Raporu](docs/16_Sprint5_Raporu.md) | PNG, WhatsApp, alıcı rehberi, günlük tetikleme |
| 17 | [Sprint 6A/6B Raporu](docs/17_Sprint6A_6B_Raporu.md) | Excel içe aktarma, anonimleştirme, güncelleme |
| 18 | [Sprint 6C Raporu](docs/18_Sprint6C_Raporu.md) | Drive for Desktop yedeği, liste/indirme |
| 19 | [UAT Raporu](docs/19_UAT_Raporu.md) | Gerçek kullanım doğrulaması |
| 20 | [Taşınabilir Kullanım](docs/20_Portable_Kullanim.md) | Portable mod ve veri klasörü |
| 22 | [İmza ve Yayın](docs/22_Imza_ve_Yayin.md) | Dijital imza, paketleme, release |
| 23 | [Oturum ve Arka Plan](docs/23_Oturum_ve_Arka_Plan.md) | Kalıcı oturum, elle kilit, arka plan bildirimleri |
| 24 | [Kurulum Sihirbazı](docs/24_Kurulum_Sihirbazi.md) | Adımlar, zorunlu alanlar, logo, güncelleme modu |
| 25 | [1.4.1 Kurulum UAT](docs/25_UAT_1.4_Kurulum.md) | Kurulum, 1.3.0 → 1.4.1 yükseltme, veri koruma |
| 26 | [Kullanım Kılavuzu](docs/26_Kullanici_Kilavuzu.md) | Son kullanıcı akışları, bildirim, yedek, güncelleme |
| 27 | [1.5.0 Güncelleme UAT](docs/27_UAT_1.5_Guncelleme.md) | SHA-256, geri dönüş, kaldırma, taşınabilir UAT |

## Alınan temel kararlar

- **Teknoloji:** .NET 8 + WPF + SQLite (gelecekte MAUI ile mobil için çekirdek katman yeniden kullanılacak)
- **Kullanım:** Tek bilgisayar, tek kullanıcı oturumu; yerel kullanıcı adı/şifre + roller
- **WhatsApp:** MVP'de yarı otomatik (PNG üret + panoya kopyala + WhatsApp'ı aç); resmî Cloud API sonraki faz
- **Bildirim:** Windows toast + tepside çalışma + ntfy/Telegram ile telefona push
- **Günlük liste alıcıları:** Kurum içi personel (yerel alıcı rehberi)

## Önerilen geliştirme sırası

1. Sprint 0-3 (tamamlandı): iskelet, giriş, tema, kategoriler, görüşme CRUD, günlük liste, detay, sonuç, kullanıcı yönetimi, oturum kilidi
2. Sprint 3.5 (tamamlandı): CRUD doğrulaması, silinenleri geri alma, 30 gün temizliği, host filtresi
3. Sprint 4 (tamamlandı): hatırlatıcı motoru + Windows/push bildirim + 4 kapatma aksiyonu + tepsi + sessiz saatler
4. Sprint 5 (tamamlandı): WhatsApp paylaşımı (PNG + metin + günlük tetikleme) + alıcı rehberi
5. Sprint 6: yerel yedekleme/geri yükleme, denetim kaydı, kurulum paketi, UAT
6. Faz 2: Google Drive, raporlar, tekrarlayan görüşmeler, DB şifreleme

## Hızlı başlangıç (geliştirme)

```powershell
# .NET 8 SDK kurulu olmalı
dotnet --version

dotnet build CgpRandevuTakip.sln -c Debug
dotnet test  CgpRandevuTakip.sln -c Debug
dotnet run --project src/CgpRandevuTakip.Desktop
```

- Veritabanı: `%LocalAppData%\CgpRandevuTakip\data.db`
- Kurum adı: Ayarlar > Görünüm ve genel; üst çubukta, kontrol panelinde ve günlük liste görselinde kullanılır. Boş bırakılırsa `CGP` gösterilir.
- Eski sürümden gelen veri ilk açılışta otomatik olarak yeni veri klasörüne taşınır.
- Migration eklemek için: `dotnet ef migrations add <Ad> --project src/CgpRandevuTakip.Infrastructure --startup-project src/CgpRandevuTakip.Desktop --output-dir Persistence/Migrations`
- İlk açılışta yönetici hesabı kurulum sihirbazıyla oluşturulur.

### Tek tıkla çalıştırma

Kök klasördeki dosyalara çift tıklayabilirsin:

- **`Calistir-Debug.cmd`**: Debug derlemesi yapar, açık uygulamayı kapatır ve uygulamayı başlatır.
- **`Derle.cmd`**: Release derlemesi yapar, testleri çalıştırır ve sonucu gösterir.

- Sprint 0 sonuçları: [docs/07_Sprint0_Raporu.md](docs/07_Sprint0_Raporu.md)
- Sprint 1 sonuçları: [docs/08_Sprint1_Raporu.md](docs/08_Sprint1_Raporu.md)
- Sprint 2 sonuçları: [docs/09_Sprint2_Raporu.md](docs/09_Sprint2_Raporu.md)
- Sprint 3 sonuçları: [docs/10_Sprint3_Raporu.md](docs/10_Sprint3_Raporu.md)
- Sprint 3.5 sonuçları: [docs/11_Sprint3_5_Raporu.md](docs/11_Sprint3_5_Raporu.md)
- Sprint 4 sonuçları: [docs/12_Sprint4_Raporu.md](docs/12_Sprint4_Raporu.md)
- Sprint 4.5 UAT: [docs/13_Sprint4_5_UAT_Raporu.md](docs/13_Sprint4_5_UAT_Raporu.md)
- Push kurulumu: [docs/14_Push_Kurulum_Rehberi.md](docs/14_Push_Kurulum_Rehberi.md)

## Alınan ürün kararları

1. **Ürün adı:** CGP - Randevu Takip (teknik ad `CgpRandevuTakip`)
2. **Yedek dosya adı:** `CGP_RandevuTakip_Yedek_2026-11-06_14-30-00.zip`
3. **"Sırası gelen kişi":** randevudan **30 dk ve 5 dk önce** bildirim
4. **Kurum türü:** Eğitim kurumu (öğrenci/veli terminolojisi)

## Ürün kimliği

| Alan | Değer |
|------|-------|
| Ürün adı | CGP - Randevu Takip |
| Teknik ad | `CgpRandevuTakip` |
| Kurum türü | Eğitim kurumu |
| Yedek öneki | `CGP_RandevuTakip_Yedek_` |
| Varsayılan tarih formatı | `gg/AA/yyyy` (ekran), `yyyy-AA-gg_SS-dd-ss` (dosya) |

