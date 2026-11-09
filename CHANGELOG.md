# Değişiklik Günlüğü

Tüm önemli değişiklikler bu dosyada tutulur. Biçim: [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/),
sürümleme: [SemVer](https://semver.org/lang/tr/).

## [0.6.0] — 2026-11-09 · Sprint 5

### Eklendi
- Günlük liste PNG'si gerçek randevu verilerinden, 2x çözünürlükte üretiliyor
- Paylaşım penceresi: görsel önizleme, düzenlenebilir mesaj, alıcı seçimi
- Panoya kopyalama ve `whatsapp://send?text=...` ile WhatsApp'ı açma
- Alıcı rehberi: ekleme, düzenleme, pasifleştirme
- Görüşme detayından WhatsApp metni paylaşımı
- Günlük paylaşım saati hatırlatıcısı ve kaçırılan paylaşım uyarısı
- `WhatsAppShareLog` kaydı

## [0.5.4] — 2026-11-09 · Kimlik ve kurtarma

### Eklendi
- "Beni hatırla": 30 gün geçerli, DPAPI korumalı oturum token'ı ile otomatik giriş
- Kullanıcı e-posta adresi alanı (şifre sıfırlama için)
- SMTP ayarları ve "Test e-postası gönder"
- "Şifremi unuttum": e-posta ile 6 haneli kod, 15 dk geçerlilik, 5 deneme sınırı
- Yönetici şifre sıfırlama fallback'i korunuyor
- `Sprint4_6_AuthRecovery` migration

### Not
- E-posta ile sıfırlama için yöneticinin SMTP ayarlarını ve kullanıcı e-postalarını bir kez tanımlaması gerekir.

## [Yayınlanmamış]

### Eklendi
- `Calistir-Debug.cmd`: tek tıkla Debug derleme + uygulamayı başlatma
- `Derle.cmd`: tek tıkla Release derleme + test çalıştırma

## [0.5.3] — 2026-11-08

### Düzeltildi
- Sessiz saatlerde Windows Action Center toast sessiz ses ile gönderiliyor

## [0.5.2] — 2026-11-08 · Sprint 4.5 UAT

### Eklendi
- Windows Action Center toast entegrasyonu; başarısız olursa uygulama içi toast'a düşer
- Ayarlarda "Test bildirimi gönder" (ntfy/Telegram) ve "Windows toast testi"
- Push kurulum rehberi

### Düzeltildi
- Sessiz saat başlangıç/bitiş değerleri doğru ayar anahtarlarından okunuyor
- "Kapatınca tepside çalış" ayarı anında uygulanıyor
- İkinci açılış mevcut pencereyi öne getiriyor; yeni süreç sessizce kapanıyor
- Ana pencere modal olmayan `Show()` ile yönetiliyor; tepsiye küçültme güvenilir
- Hatırlatıcı aksiyonları `IClock` kullanıyor

## [0.5.1] — 2026-11-08

### Eklendi
- Uygulama, pencere ve tepsi simgesi olarak `CGPRandevuTakip.ico` kullanılıyor

## [0.5.0] — 2026-11-08 · Sprint 4

### Eklendi
- Hatırlatıcı motoru: zamanı gelen hatırlatıcılar 15 saniyede bir işlenir
- Windows bildirimi (uygulama içi topmost toast) ve 4 seçenekli aksiyon penceresi
- "Sırası gelen" arama hatırlatıcıları: 30 dk ve 5 dk önce
- Push bildirimi: ntfy ve Telegram; varsayılan kapalı ve isimsiz
- Tepsi simgesi ve "kapatınca tepside çalış"
- Windows açılışında başlatma (giriş ekranı veya tepside gizli)
- Sessiz saatler: kullanıcı seçimli, varsayılan 22:00–07:00
- Sırların DPAPI ile şifreli saklanması
- Hatırlatıcı aksiyonları için denetim kaydı

### Değiştirildi
- `ReminderDispatch` şemasına `Kind` (Appointment / Call) eklendi

### Düzeltildi
- Uygulama kapalıyken kaçırılan hatırlatıcılar açılışta telafi edilir; geçmiş randevular `Missed` olur
- Aynı hatırlatıcının iki kez gönderilmesi kanal timestamp'leri ile engellenir

## [0.4.1] — 2026-11-08 · Sprint 3.5

### Eklendi
- Silinen görüşmeler ekranı; yönetici 30 gün içindeki kayıtları görüp geri alabilir
- Süresi dolan silinmiş görüşmeler için otomatik kalıcı temizlik (açılışta ve 6 saatte bir)
- Günlük listede host filtresi
- GitHub Actions CI: Windows üzerinde Release build ve testler; test sonuçları artifact olarak yüklenir
- Dependabot ile NuGet ve GitHub Actions güncelleme takibi

### Düzeltildi
- Geri alınan randevunun iptal edilmiş gelecek hatırlatıcıları yeniden etkinleştirilir
- Silme ve geri alma işlemleri `IClock` üzerinden tutarlı UTC zamanı kullanır
- Görüşme detayındaki silme onayı 30 günlük geri alma penceresini belirtir
- Henüz etkinleşmemiş günlük paylaşım düğmesi kullanıcıdan gizlendi
- Bildirim ayarlarına Sprint 4 etkinleştirme notu eklendi

### Değiştirildi
- NuGet bağımlılıkları güncellendi: EF Core 9.0.22, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.14, xUnit 2.9.3
- Teknik ad, çözüm/proje/klasör ve assembly adları `CgpRandevuTakip` oldu; yedek ve kullanıcıya görünen metinlerdeki marka CGP olarak kaldı
- Kurum adı ayarlardan kaydedildiğinde üst çubuk ve kontrol paneli yeniden başlatmadan güncellenir; boşsa `CGP` gösterilir
- Eski yerel veri klasörü ilk açılışta yeni `CgpRandevuTakip` klasörüne tek seferlik taşınır; mevcut veritabanı ve kullanıcılar korunur

### İyileştirildi
- Liste yenilemeleri `RangeObservableCollection.ReplaceWith` ile tek `Reset` bildirimi kullanır; `Clear + foreach` tekrarları kaldırıldı
- Kontrol paneli kategori sayılarını tek sorgudan üretir; kategori, ayar ve kullanıcı sayısı paralel yüklenir
- Görüşme düzenleyici kategori/görüşmeci/ayar aramalarını paralel yükler; günlük liste özeti tek geçişte hesaplanır
- Kullanılmayan görüşmeci servisi bağımlılığı günlük liste modelinden kaldırıldı

## [0.4.0] — 2026-11-07 · Sprint 3

### Eklendi
- Görüşme sonucu: katılım (Geldi/Gelmedi/Ertelendi/İptal), değerlendirme, tekrar ve kapanış
- Durum makinesi: `Appointment.ApplyOutcome` ve kapanmış kaydı yeniden açma (`Reopen`)
- Kapanış bilgisi: kapanış zamanı ve kapatan kullanıcı; detayda sonuç kartı
- Detaydan "Sonuç gir" ve "Tekrar randevu oluştur" akışları
- Tekrar randevu taslağı: kişi/telefon/host/kategori/yöntem/hatırlatıcı devralınır, tarih +7 gün önerilir
- Düzenleme ekranında sonuç kartı ve kapanmış görüşme uyarısı
- Günlük listede "Sıradaki görüşme" şeridi
- Kullanıcı yönetimi: ekleme, rol güncelleme, aktif/pasif, giriş izni, şifre sıfırlama
- Son aktif yönetici koruması ve kendini pasifleştirme engeli
- 30 dakika hareketsizlik kilidi, kilit ekranı ve şifreyle açma
- Tüm pencerelerden (modal diyaloglar dahil) etkinlik izleme
- `20261107040550_Sprint3_OutcomeAndLock` migration'ı
- 32 yeni test (toplam 135)

### Değişti
- Kapanışta bekleyen hatırlatıcılar `Cancelled` durumuna alınır; gönderilmişler korunur
- Ana kabuğa yalnızca yöneticinin gördüğü "Kullanıcılar" gezinmesi eklendi
- "Sprint 3 kapsamı" bilgisi ana kabuk alt bilgisine işlendi
- Sürüm `0.3.0` → `0.4.0`

### Düzeltildi
- Kilit penceresinin alt düğmelerinin kırpılması (`SizeToContent="Height"`)
- Modal diyalog açıkken oturum etkinliğinin izlenmemesi
- Kullanıcı listesinde ham DTO metninin görünmesi

### Notlar
- 30 dk kilit UI testi geçici olarak 15 sn / 2 sn ile yapıldı; test sonrası
  üretim değerleri **30 dakika / 30 saniye** olarak geri alındı.
- Windows OS toast/push Sprint 4'e, WhatsApp paylaşımı Sprint 5'e, yedekleme Sprint 6'ya ertelendi.

## [0.3.0] — 2026-11-07 · Sprint 2

### Eklendi
- Görüşme oluşturma, düzenleme ve silme (soft delete, 30 gün geri alma)
- Günlük liste: tarih gezinme, bugün, kategori/durum filtreleri, özet satırı
- Arama: ad, soyad, telefon, kategori ve host; arama doluyken tarih filtresi kaldırılır
- Görüşme detay penceresi ve listeden açık "Detay" düğmesi
- Telefon E.164 normalizasyonu ve aynı kişinin tekrar kullanımı
- Host çakışma uyarısı; onaylanırsa `IgnoreConflicts` ile kayıt
- Çoklu hatırlatıcı (1 gün / 1 saat / 30 dk) ve güncellemede yeniden planlama
- Görüşmeci rolü yalnızca kendi görüşmelerini görür; randevu oluşturamaz
- Host (görüşmeci) yönetimi: girişsiz host, otomatik kullanıcı adı, aktif/pasif
- Günlük Liste varsayılan sayfa; "Görüşmeciler" yalnızca yönetici/sekreter
- PerMonitorV2 DPI farkındalığı (`app.manifest`)
- 24 yeni test (toplam 103)

### Değişti
- Ana kabuk gezinmesi: Günlük Liste / Görüşmeciler / Kategoriler / Kontrol Paneli / Ayarlar
- Tarih biçimi tüm ekranlarda `gg/AA/yyyy` olarak sabitlendi
- Sürüm `0.2.0` → `0.3.0`

### Düzeltildi
- Karanlık temada ComboBox ve DatePicker beyaz görünümü
- Görüşmeciler listesinde "Ad soyad" sütununun sıfıra sıkışması
- Detay penceresinin salt okunur `Run.Text` bağlaması nedeniyle çökmesi
- Karanlık temada kaydırma çubuğunun beyaz şerit olarak görünmesi
- 150% DPI ölçeğinde pencere taşması

### Notlar
- Windows OS toast/push Sprint 4'e, 30 dk hareketsizlik kilidi ve kullanıcı yönetimi Sprint 3'e ertelendi.

## [0.2.0] — 2026-11-06 · Sprint 1

### Eklendi
- İlk kurulum sihirbazı; varsayılan şifre olmadan yönetici hesabı oluşturma
- Kullanıcı adı/şifre ile giriş, rol bazlı oturum (Yönetici, Sekreter, Görüşmeci)
- 5 hatalı denemede 15 dakika hesap kilidi ve kalan süre bilgisi
- Şifre değiştirme ekranı ve PBKDF2-SHA256 doğrulaması
- Koyu / açık / otomatik tema; otomatik modda Windows temasını izleme
- Kategori yönetimi: ekleme, düzenleme, sıralama, pasifleştirme, kullanım kontrolü
- Ayarlar: kurum adı, yedek klasörü (yazılabilirlik testi), bildirim/push tercihleri,
  varsayılan hatırlatıcı ve günlük paylaşım saati
- Hakkında bölümü: ürün adı, sürüm, telif, veritabanı ve günlük yolları
- Denetim kaydı altyapısı (giriş, kategori, ayar, tema olayları)
- Tek örnek (single-instance) koruması ve çökme günlüğü
- S-01: günlük liste PNG üretimi (`RenderTargetBitmap`), panoya kopyalama, WhatsApp açma
- S-02: hatırlatıcı aksiyon penceresi ve toast önizleme
- 79 birim/entegrasyon testi (tümü geçiyor)

### Değişti
- Sprint 0 durum ekranı yerine gezinmeli ana kabuk (Kontrol Paneli, Kategoriler, Ayarlar)
- Sürüm `0.1.0` → `0.2.0`

### Notlar
- Windows OS toast ve telefon push Sprint 4'e ertelendi.
- 30 dakika hareketsizlik kilidi (FR-AUTH-05) ve kullanıcı yönetimi Sprint 2/3'e ertelendi.

## [0.1.0] — 2026-11-06 · Sprint 0

### Eklendi
- `CgpRandevuTakip.sln` ve 5 proje: Domain, Application, Infrastructure, Desktop (WPF), Tests
- Domain modeli: Appointment, Person, Category, User, ReminderDispatch, AppointmentOutcome,
  Recipient, Setting, AuditLog, WhatsAppShareLog, BackupRecord
- Telefon E.164 normalizasyonu, hatırlatıcı hesaplama, yedek dosya adı kuralları
- EF Core 8 + SQLite, ilk migration ve eğitim kurumu varsayılan kategorileri
- PBKDF2-SHA256 parola özetleme ve Türkiye saat dilimi
- WPF açılış kabuğu (Sprint 0 durum ekranı)
- 36 birim/entegrasyon testi (tümü geçiyor)

## [Yayınlanmadı] — Analiz ve tasarım

### Eklendi
- Proje vizyonu, kapsam ve paydaş analizi (`docs/00`)
- Gap analizi, çelişkiler, eksik gereksinimler ve MVP tanımı (`docs/01`)
- Fonksiyonel/fonksiyonel olmayan gereksinimler, iş kuralları, kabul kriterleri (`docs/02`)
- Katmanlı mimari, veri modeli, entegrasyon akışları (`docs/03`)
- Sprint planı, DoR/DoD, test stratejisi, risk kaydı (`docs/04`)
- KVKK ve veri güvenliği çerçevesi (`docs/05`)
- Karar kaydı ve açık sorular (`docs/06`)

### Karar
- Teknoloji: .NET 8 + WPF + SQLite
- WhatsApp: MVP'de yarı otomatik
- Bildirim: Windows toast + ntfy/Telegram push
- Tek bilgisayar, yerel kullanıcı/rol
