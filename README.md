# CGP - Randevu Takip

[![CI](https://github.com/cgportakal/CgpRandevuTakip/actions/workflows/ci.yml/badge.svg)](https://github.com/cgportakal/CgpRandevuTakip/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/cgportakal/CgpRandevuTakip)](https://github.com/cgportakal/CgpRandevuTakip/releases/latest)

## İndir

> **Son sürüm: 1.11.0** · 22.11.2026 · 438 test geçiyor

- **Kurulum (Setup EXE):** [CGP-RandevuTakip-1.11.0-Setup.exe](https://github.com/cgportakal/CgpRandevuTakip/releases/download/v1.11.0/CGP-RandevuTakip-1.11.0-Setup.exe)
- **Taşınabilir (ZIP):** [CGP-RandevuTakip-1.11.0-portable.zip](https://github.com/cgportakal/CgpRandevuTakip/releases/download/v1.11.0/CGP-RandevuTakip-1.11.0-portable.zip)
- **Tüm sürümler:** [Releases](https://github.com/cgportakal/CgpRandevuTakip/releases/latest)

> Kurulum EXE'si per-user çalışır; yönetici hakkı istemez. Taşınabilir ZIP'i yazılabilir bir klasöre açıp
> `CgpRandevuTakip.Desktop.exe` dosyasını çalıştırın. Dijital imza ayrıntıları:
> [docs/22_Imza_ve_Yayin.md](docs/22_Imza_ve_Yayin.md).

Windows 11 x64 masaüstünde çalışan, randevu/görüşme kayıtlarını yöneten, günlük listeyi
WhatsApp üzerinden paylaşan ve zamanı gelen görüşmeler için hatırlatma üreten kurumsal uygulama.

> Durum: **1.11.0 hazır** — görüşme odaları, yönetilen ünvanlar, çoklu görüşmeci ve
> misafir başına bağımsız sonuç. Masaüstü, web ve mobil aynı kayıtları görür; çevrimdışı
> kuyruk ve çakışma çözümü korunur. 1.082 kayıtla API, web Playwright, masaüstü 1.11.0
> ve Samsung S21 Maestro UAT’i tamamlandı; iOS Mac bekliyor.

### Son yenilikler

- **Görüşme odaları ve ünvanlar (1.11.0):** her görüşmecinin kendi odası + 3 ortak oda;
  oda/host/misafir çakışma kontrolü. Yönetilen ünvan listesi, adın altında/parantez içinde.
- **Çoklu görüşmeci ve misafir (1.11.0):** bir randevuda “Katılımcılar” ve ek misafirler;
  her misafirin katılım/sonuç/notu bağımsız; randevu durumu otomatik türetilir.
- **Dayanıklılık (1.10.2):** açılışta oturum → manifest → outbox → günlük liste; sunucu modunda
  60 sn’de bir pull; ağ hatasında çevrimdışı durumu; yerel hatırlatıcı devre dışı.
- **Çakışma yönetimi (1.10.1):** aynı randevu iki istemcide düzenlenirse eski sürüm `409` alır;
  masaüstü “yerel uygula / sunucuyu koru” seçeneği sunar. Çevrimdışı kuyrukta çakışan kayıt
  `conflict` durumunda tutulur ve “Çakışanları çöz” ile karara bağlanır.
- **Masaüstü yazma yolu + outbox:** randevu, sonuç, seri, toplu silme/taşıma ve geri alma
  işlemleri API’ye gider; bağlantı yoksa kalıcı kuyruğa alınır ve 30 sn’de bir gönderilir.
- **Çevrimdışı görünürlük:** bekleyen tekil ve seri kayıtlar listede/detayda görünür;
  geçici kimlikler sunucu kimlikleriyle eşlenir.
- **SignalR:** sunucudaki randevu/seri/içerik değişiklikleri masaüstünde elle yenilemeden görünür.
- **Idempotency:** aynı istek (`ClientRequestId`) ikinci kez kayıt oluşturmaz.
- **Yedekleme (1.9.0):** seçmeli yedek/geri yükleme, içerik özeti, boş yedek koruması,
  kullanıcı/ayar koruyan birleştirme.

- **Seçmeli yedek:** randevular, kullanıcılar, ayarlar, denetim/paylaşım geçmişi, içerik
  sürümleri, alıcılar ve kurum logosu ayrı ayrı seçilebilir; seçilmeyen bölümler dosyaya yazılmaz.
- **Yedek önizlemesi:** randevu/kişi/kullanıcı/ayar sayıları, ilk–son randevu ve son değişiklik
  zamanı; boş yedek ve şifre çözülememe uyarıları.
- **Güvenli geri yükleme:** mevcut veritabanı dosyası değiştirilmez; seçilen bölümler işlem
  içinde birleştirilir. Kullanıcılar ve ayarlar varsayılan olarak korunur, kurulum sihirbazı açılmaz.
- **Bağlantı ayarları:** sunucu adresi ve okuma modu yedeğe dahil; oturum belirteçleri asla yedeklenmez.
- **Masaüstü P2 okuma yolu:** günlük liste, arama, detay, silinenler ve raporlar API’den okunur;
  ağ hatasında son başarılı yanıt önbellekten gösterilir.

### Kurulum ve kaldırma

| Konu | Davranış |
|---|---|
| Kurulum türü | Kullanıcı bazlı (`%LOCALAPPDATA%\Programs\CGP Randevu Takip`), yönetici hakkı gerekmez |
| Veri | `%LOCALAPPDATA%\CgpRandevuTakip` (veritabanı, ayarlar, günlükler) |
| Yedekler | `Belgeler\CgpRandevuTakip\Yedekler` |
| Kaldırma | Program dosyaları, kısayollar, başlangıç kaydı, bildirim kayıtları ve güncelleme anlık görüntüsü silinir |
| Veriler | Kaldırırken sorulur; **varsayılan olarak korunur**. **Yedekler hiçbir durumda silinmez.** |

### Platformlar

| Platform | Durum | Not |
|---|---|---|
| Masaüstü (Windows) | **1.11.0** | Tam özellik, çevrimdışı, sunucu senkronu (P3–P6), oda/ünvan/çoklu misafir, seçmeli yedek/geri yükleme |
| Sunucu API’si | **v1** | Ortak iş kuralları, JWT, içerik manifesti, oda/ünvan/katılımcı/misafir |
| Mobil (Android/iOS) | **1.0.0** | Giriş, günlük liste, oda/katılımcı/ek misafir, detay/sonuç, raporlar, çevrimdışı kuyruk, push (Expo; Android doğrulandı) |
| Web | **1.11.0** | Giriş, günlük liste/oda filtresi, oda/katılımcı/misafir, detay/sonuç, 7 rapor türü (Next.js) |

Ayrıntılar: [docs/29_Platform_Mimarisi.md](docs/29_Platform_Mimarisi.md) ·
[docs/30_Api_ve_Mobil_Kurulum.md](docs/30_Api_ve_Mobil_Kurulum.md)

### Geliştirici devri (iki bilgisayar)

Bu depo iki bilgisayarda iki ajanla geliştiriliyor. Çakışmayı önlemek için:

- **Durum:** [coordination/CURRENT.md](coordination/CURRENT.md)
- **Protokol:** [coordination/PROTOCOL.md](coordination/PROTOCOL.md)
- **Kilit:** `node coordination/lock.mjs status` · `acquire --machine <ad> --task "..."`
- **Hızlı başlangıç:** [BASLANGIC.md](BASLANGIC.md) · macOS `bash basla.command` · Windows `basla.cmd`
- **Akış:** `agent/<makine>/<konu>` dalı → PR → yeşil CI → squash merge (`master` korumalı).

Ajanlar için giriş kuralları: [AGENTS.md](AGENTS.md).

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
| 28 | [1.5.1 Bakım UAT](docs/28_UAT_1.5.1_Bakim.md) | Başlangıç onarımı, kaldırma temizliği, yedek koruma |
| 29 | [Platform Mimarisi](docs/29_Platform_Mimarisi.md) | Web/mobil, senkronizasyon, içerik, bildirim, fazlar |
| 30 | [API ve Mobil Kurulum](docs/30_Api_ve_Mobil_Kurulum.md) | Sunucu, Expo, ortam değişkenleri |
| 31 | [Raporlar](docs/31_Raporlar.md) | Rol kapsamı, KPI'lar, grafikler, CSV/PDF |
| 32 | [Tekrarlayan Seri](docs/32_Tekrarlayan_Seri.md) | Sıklıklar, ay sonu kırpması, iptal kapsamı |
| 33 | [1.6.0 UAT](docs/33_UAT_1.6.0.md) | Kurulum, migration, rapor/seri senaryoları, geri dönüş |
| 34 | [1.7.0 UAT](docs/34_UAT_1.7.0.md) | Rapor türleri, Excel, seri yönetimi, API/mobil, UI testleri |
| 35 | [Geliştirme Ortamı](docs/35_Gelistirme_Ortami.md) | Windows + macOS kurulum, bağımlılıklar, ortam değişkenleri |
| 36 | [1.8.0 UAT](docs/36_UAT_1.8.0.md) | İçerik konsolu, yayın/geri alma, zorunlu güncelleme, ilerleme penceresi |
| 37 | [Güncelleme İlerleme UAT](docs/37_UAT_1.8.0_Guncelleme_Ilerleme.md) | Gerçek Setup indirmesi, yüzde/MB, doğrulama, kurulum |
| 38 | [Zorunlu Güncelleme UAT](docs/38_UAT_Zorunlu_Guncelleme.md) | Web/masaüstü bloklayıcı ekran, sürüm politikası, sürüm notları |
| 39 | [Android Mobil UAT](docs/39_Mobil_UAT_Android.md) | Emülatör kurulumu, çevrimdışı kuyruk, SignalR, zorunlu güncelleme |
| 40 | [İçerik Logosu UAT](docs/40_Icerik_Logosu_UAT.md) | Logo yükleme, önizleme, anonim logo ucu |
| 41 | [Tasarım ve Veri Paritesi](docs/41_Tasarim_ve_Veri_Paritesi.md) | Ortak tokenlar, terimler, rapor türleri, masaüstü senkron mimarisi |
| 42 | [Masaüstü Veri Senkronizasyonu](docs/42_Masaustu_Veri_Senkronizasyonu.md) | API istemcisi, çevrimdışı önbellek, outbox, SignalR, çakışma, UAT planı |
| 43 | [Yedekleme ve Geri Yükleme](docs/43_Yedekleme_ve_Geri_Yukleme.md) | Seçmeli yedek/geri yükleme, içerik özeti, boş yedek koruması, bağlantı ayarları |
| 44 | [Masaüstü Senkronizasyonu Devir](docs/44_Masaustu_Senkronizasyon_Devir.md) | P1–P4 mimarisi, outbox/overlay/SignalR, UAT ve P5–P7 planı |
| 45 | [Masaüstü Senkronizasyon Saha UAT](docs/45_Masaustu_Senkronizasyon_UAT.md) | P3/P4/P5: çevrimdışı kuyruk, SignalR, çakışma çözümü |
| 46 | [Test Verisi Üretimi](docs/46_Test_Verisi_Uretimi.md) | 1000+ kayıt, oda/ünvan/misafir zenginleştirme, doğrulama |
| 47 | [Görüşme Odaları, Ünvanlar, Misafirler](docs/47_Gorusme_Odalari_Unvanlar_Misafirler.md) | Veri modeli, iş kuralları, API, UAT sonuçları |

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
5. Sprint 6 (tamamlandı): yerel yedekleme/geri yükleme, denetim kaydı, kurulum paketi, UAT
6. Faz 2 (tamamlandı): Google Drive, raporlar, tekrarlayan görüşmeler; DB şifreleme sonraki faz

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

