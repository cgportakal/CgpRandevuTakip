# CGP - Randevu Takip

[![CI](https://github.com/cgportakal/CgpRandevuTakip/actions/workflows/ci.yml/badge.svg)](https://github.com/cgportakal/CgpRandevuTakip/actions/workflows/ci.yml)

Windows 11 x64 masaüstünde çalışan, randevu/görüşme kayıtlarını yöneten, günlük listeyi
WhatsApp üzerinden paylaşan ve zamanı gelen görüşmeler için hatırlatma üreten kurumsal uygulama.

> Durum: **Sprint 3 tamamlandı** — görüşme sonucu, durum geçişleri, tekrar randevu, kullanıcı yönetimi ve 30 dk oturum kilidi hazır.
> Sürüm: 0.4.0 · Tarih: 07.11.2026 · 140 test geçiyor

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

## Alınan temel kararlar

- **Teknoloji:** .NET 8 + WPF + SQLite (gelecekte MAUI ile mobil için çekirdek katman yeniden kullanılacak)
- **Kullanım:** Tek bilgisayar, tek kullanıcı oturumu; yerel kullanıcı adı/şifre + roller
- **WhatsApp:** MVP'de yarı otomatik (PNG üret + panoya kopyala + WhatsApp'ı aç); resmî Cloud API sonraki faz
- **Bildirim:** Windows toast + tepside çalışma + ntfy/Telegram ile telefona push
- **Günlük liste alıcıları:** Kurum içi personel (yerel alıcı rehberi)

## Önerilen geliştirme sırası

1. Sprint 0-3 (tamamlandı): iskelet, giriş, tema, kategoriler, görüşme CRUD, günlük liste, detay, sonuç, kullanıcı yönetimi, oturum kilidi
2. Sprint 4: hatırlatıcı motoru + Windows/push bildirim + 4 kapatma aksiyonu
3. Sprint 5: WhatsApp paylaşımı (PNG + metin + günlük tetikleme) + alıcı rehberi
4. Sprint 6: yerel yedekleme/geri yükleme, denetim kaydı, kurulum paketi, UAT
5. Faz 2: Google Drive, raporlar, tekrarlayan görüşmeler, DB şifreleme

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
- Sprint 0 sonuçları: [docs/07_Sprint0_Raporu.md](docs/07_Sprint0_Raporu.md)
- Sprint 1 sonuçları: [docs/08_Sprint1_Raporu.md](docs/08_Sprint1_Raporu.md)
- Sprint 2 sonuçları: [docs/09_Sprint2_Raporu.md](docs/09_Sprint2_Raporu.md)
- Sprint 3 sonuçları: [docs/10_Sprint3_Raporu.md](docs/10_Sprint3_Raporu.md)

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
