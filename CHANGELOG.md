# Değişiklik Günlüğü

Tüm önemli değişiklikler bu dosyada tutulur. Biçim: [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/),
sürümleme: [SemVer](https://semver.org/lang/tr/).

## [1.8.4] — 2026-11-19 · Sunucu bağlantısı ve kullanıcı listesi

### Eklendi
- **Masaüstü API istemcisi (P1):** JWT giriş/yenileme/çıkış, DPAPI şifreli oturum saklama, 401’de tek seferlik yenileme, sunucu adresi ve bağlantı testi; Ayarlar’dan açılan “Sunucu bağlantısı” penceresi ([docs/42](docs/42_Masaustu_Veri_Senkronizasyonu.md)).

### Düzeltildi
- **Kullanıcılar listesi:** satır yüksekliği sabitlendi; ad soyad, kullanıcı adı, rol, son giriş ve durum bilgileri satır içinde dikey ortalandı.

### Test
- **390/390** test; Release 0 uyarı / 0 hata.

### Paket
- Setup-only sürüm; taşınabilir ZIP yayınlanmadı (son portable **1.8.0**).

## [1.8.3] — 2026-11-18 · Tasarım paritesi ve mobil düzeltmeler

### Eklendi
- **Ortak tasarım tokenları:** `design/tokens.json` (açık/koyu palet, tipografi, boşluk, yarıçap, rapor etiketleri); web CSS, mobil tema ve masaüstü XAML kaynakları hizalandı ([docs/41](docs/41_Tasarim_ve_Veri_Paritesi.md)).
- **Mobil rapor paritesi:** `CategoryTrend` ve `Attendance` raporları mobilde görüntüleniyor; rapor etiketleri üç istemcide ortak.
- **EAS projesi:** `@cgportakal/cgp-randevu-takip` (projectId `0e759428-9702-43f5-9783-7b8662bd4afa`) `app.json`’a bağlandı; `google-services.json` yapılandırması ve Firebase/keystore dosyaları için gitignore kuralları eklendi.
- **Mobil dev build altyapısı:** SDK 58 uyumlu `expo-dev-client` bağımlılığı eklendi; EAS development build ve `npx expo start --dev-client` için ek kurulum gerekmiyor.
- **Mobil push hazırlığı:** `EXPO_PUBLIC_EAS_PROJECT_ID` desteği ve eksik proje kimliğinde konsol uyarısı; FCM/APNs test adımları `docs/39` içine eklendi.
- **İçerik logosu yükleme:** web içerik konsolundan PNG/JPG/BMP (en fazla 4 MB) logo yükleme; `POST /v1/admin/content/logo`, `branding/institution-logo.*` saklama, eski dosyayı temizleme, sürüm sorgulu (`?v=...`) logo URL’si ve `contentChanged` bildirimi.

### Düzeltildi
- **Performans testi:** 50 satırlık PNG render testi CI’daki ilk WPF render gecikmesiyle flaky oluyordu; ölçüm öncesi ısınma render’ı eklendi ve eşik 10 sn’ye çekildi.
- **Logo önizlemesi:** `/v1/content/logo` ucu kimlik doğrulaması istediği için web konsolundaki `<img>` önizlemesi 401 alıyordu; logo ucu anonim erişime açıldı ve API testi eklendi ([docs/40](docs/40_Icerik_Logosu_UAT.md)).
- **Mobil SignalR yeniden bağlanma:** otomatik denemeler tükenince bağlantı kalıcı kopuyordu; üstel geri çekilmeyle (2 → 30 sn, süresiz) yeniden bağlanma eklendi ([docs/39](docs/39_Mobil_UAT_Android.md)).
- **Çevrimdışı kayıt kuyruğu:** Çakışan kayıt sunucu hatasıyla kuyrukta takılıyordu; çevrimdışı oluşturulan kayıt `ignoreConflicts` ile gönderiliyor ve form “çakışma denetimi atlanır” uyarısı gösteriyor.
- **Bağlantı dönüşünde eşitleme:** Çevrimdışı → çevrimiçi geçişinde kuyruk otomatik gönderiliyor ve liste tazeleniyor; kuyruk hataları ekranda gösteriliyor.
- **Mobil zorunlu güncelleme ekranı:** Türkçe metinler çift kodlanmıştı (“GÃ¼ncelleme gerekli”); doğru UTF-8 metinlerle değiştirildi.

### Doğrulama
- **Android UAT (Expo Go / API 35 emülatör):** giriş, oturum kalıcılığı, çevrimdışı önbellek, çevrimdışı kuyruk ve eşitleme, SignalR canlı olay, 60 sn kesinti sonrası yeniden bağlanma, zorunlu güncelleme, raporlar ([docs/39](docs/39_Mobil_UAT_Android.md)).
- **Gerçek cihaz UAT (Samsung SM-G991B, Android 15):** yerel dev client + USB (`adb reverse`), cihaz kaydı, push (açık / arka plan / süreç öldürülmüş), bildirime dokunma, çevrimdışı kuyruk + otomatik eşitleme, canlı olay, API kesintisi sonrası yeniden bağlanma, zorunlu güncelleme, raporlar.
- iOS testi ortam gereksinimi nedeniyle yapılamadı.

### Paket
- Setup-only sürüm; taşınabilir ZIP yayınlanmadı (son portable **1.8.0**).

## [1.8.2] — 2026-11-17 · Sürüm notları ve zorunlu güncelleme UAT

### Düzeltildi
- **Sürüm notları biçimlendirmesi:** masaüstü güncelleme pencereleri GitHub’dan gelen ham Markdown’ı (`##`, `**`, bağlantı) düz metin olarak gösteriyordu; `ReleaseNotesFormatter` ile başlık/liste/kalın/bağlantı işaretleri sadeleştiriliyor ([docs/38](docs/38_UAT_Zorunlu_Guncelleme.md)).
- **API sürüm politikası:** `appsettings.json` içindeki `latestDesktopVersion` 1.8.0’da kalmıştı; **1.8.1** olarak düzeltildi.

### Doğrulama
- **Zorunlu güncelleme UAT’i:** web (1.0.0 → gerekli 9.9.9) ve masaüstü (1.0.0 → gerekli 1.5.0, son v1.8.1) akışları doğrulandı.
- **377/377** test; Release 0 uyarı / 0 hata.

### Paket
- Setup-only sürüm; taşınabilir ZIP yayınlanmadı (son portable **1.8.0**).

## [1.8.1] — 2026-11-17 · Güncelleme ilerleme düzeltmesi

### Düzeltildi
- **Güncelleme ilerleme penceresi:** SHA-256 doğrulaması, geri dönüş anlık görüntüsü ve ZIP çıkarma arka plana alındı; pencere donmuyor ve “İndirme doğrulanıyor / Kurulum başlatılıyor” aşamaları görünüyor. Gerçek `v1.8.0` Setup indirmesiyle UAT tamamlandı ([docs/37](docs/37_UAT_1.8.0_Guncelleme_Ilerleme.md)).
- **Mobil CI audit:** düzeltmesi yayınlanmamış upstream `picomatch` advisory’si CI’yı bloklamıyor; Dependabot takip ediyor.

### Test
- **371/371** test; Release 0 uyarı / 0 hata.
- Web build ve mobil `tsc --noEmit` başarılı.

### Paket
- Setup-only sürüm; taşınabilir ZIP yayınlanmadı (son portable **1.8.0**).

## [1.8.0] — 2026-11-17 · İçerik yönetimi, zorunlu güncelleme

### Eklendi
- **Web Push (VAPID):** `GET /v1/push/public-key`, `public/sw.js`, üst çubukta “Bildirimleri aç”, abonelik kaydı ve test bildirimi. VAPID anahtarları ilk açılışta üretilip saklanır.
- **Mobil push (Expo → FCM/APNs):** `expo-notifications` izni, Expo push belirteci, girişte `POST /v1/devices`, çıkışta silme.
- **Cihaz kayıtları:** `GET/POST/DELETE /v1/devices`, `POST /v1/push/test`; web abonelikleri süresi dolunca otomatik temizlenir.
- **Sunucu worker:** hatırlatmaları SignalR + cihaz push + ntfy/Telegram üzerinden iletir; sessiz saatlerde push göndermez.
- **SignalR:** `/hubs/events` hub’ı (JWT, WebSocket `access_token`); `appointmentChanged`, `seriesChanged`, `contentChanged`, `reminderDue` olayları. Web ve mobil bağlıyken günlük liste/raporlar otomatik yenilenir.
- **Web canlı bildirim:** sağ altta hatırlatma baloncuğu, “Canlı” bağlantı durumu.
- **Mobil canlı olay:** SignalR bağlantısı (TextEncoder polyfill), gün listesi otomatik yenileme, hatırlatma banner’ı.
- API testlerine hub kimlik doğrulaması, push anahtarı ve cihaz kaydı kontrolleri eklendi (**10 API testi**).
- **Web uygulaması (`web/`, Next.js 16):** giriş, günlük liste (tarih/arama), yeni görüşme ve tekrarlayan seri, detay + görüşme sonucu, silme/seri iptali, 7 rapor türü, çevrimdışı okuma önbelleği.
- **Web içerik yönetim konsolu:** kategori, görüşmeci, kurum profili ve genel ayarları düzenleme; yayın notu ile sürüm oluşturma, son 20 sürümü görme ve seçilen sürüme geri dönme.
- **İçerik yayınlama:** `POST /v1/admin/content/publish` ve `/versions/{id}/restore`; `ContentVersions` tablosu ve `ContentPublishing` migration'ı; yayın/geri alma sonrası `contentChanged` SignalR olayı.
- **Zorunlu güncelleme ekranı:** API `GET /v1/version-policy` (anonim) ve manifest `versionPolicy`; web/mobil minimum sürümün altındaysa bloklayıcı güncelleme ekranı, masaüstünde giriş öncesi `ForcedUpdateWindow`.
- **Güncelleme indirme ilerlemesi:** ortak `UpdateProgressWindow`; yüzdelik `ProgressBar`, indirilen/toplam MB, doğrulama, paket açma ve kurulum başlatma aşama metinleri. Zorunlu ve isteğe bağlı güncelleme akışlarında kullanılır.
- **Sürüm politikası:** `version-policy.json` release asset'i; masaüstü güncelleyici minimum sürümü bu dosyadan okur (release asset yoksa `master` dosyasına düşer). Web/mobil sürüm sabitleri (`NEXT_PUBLIC_APP_VERSION`, `APP_VERSION`) politika ile karşılaştırılır.
- **CI:** web için `npm ci` + `npm run build` işi.

### Düzeltildi
- **SignalR CORS:** istemci `withCredentials: false` kullanıyor; `AllowAnyOrigin` ile preflight hatası giderildi.
- **Rapor uçları çakışması:** `GET /v1/reports/{kind}` ile `GET /v1/reports/summary` aynı yola düşüyordu; tür uçları `/v1/reports/kind/{kind}` olarak ayrıldı. Mobil ve web istemcileri güncellendi.
- Web rapor ekranında eksik `summary` koruması ve “Günlük dağılım dağılım” başlığı düzeltildi.
- Web SignalR istemcisi `contentChanged` olayını dinliyor; işleyiciler sunucuya yanlış sonuç döndürmeyecek şekilde düzeltildi.

### Düzeltildi (önceki)
- Dependabot NuGet grubu yalnızca minor/patch güncellemeleri alacak şekilde ayrıldı; net10 gerektiren major sürümler beklemede.
- `Swashbuckle.AspNetCore` 10.3.1'e yükseltildi; OpenAPI v2 güvenlik şeması `OpenApiSecuritySchemeReference` ile uyarlandı. Swagger UI ve şema üretimi doğrulandı.

### Sürüm
- Masaüstü sürümü **1.8.0**; assembly, installer, `Kurulum-Olustur.ps1` ve sürüm politikası aynı sürüme çekildi.

## [1.7.0] — 2026-11-16 · Rapor türleri, seri yönetimi, API ve mobil

### Eklendi
- **Rapor türleri:** görüşmeci performansı, kategori trendi, günlük katılım, tekrar görüşme, kişi geçmişi (ad/telefon arama) ve denetim/aktivite raporu.
- **Excel dışa aktarma:** rapor türüne özel sayfalarla `.xlsx` çıktısı (ClosedXML).
- **Seri yönetimi:** seriye 1–12 kayıt ekleme ve gelecek kayıtları düzenleme (görüşmeci, kategori, süre, yöntem, saat, açıklama). Çakışan saatler atlanır, geçmiş/kapalı kayıtlar korunur.
- **Seri çapası ve sıklığı:** `SeriesAnchorLocal` + `SeriesFrequency` alanları (`SeriesAnchorFrequency` migration) ve `SeriesUpdated`/`SeriesExtended` denetim kayıtları.
- **API:** `GET /v1/reports/{kind}`, `POST /v1/appointments/series/preview`, `POST /v1/appointments/series`, `POST /v1/appointments/series/{id}/cancel`.
- **Mobil:** yerel SQLite şeması (appointments/outbox/sync_state), gün senkronizasyonu, çevrimdışı yazma kuyruğu (create/update/outcome/series-cancel), detay, yeni görüşme (seri dahil) ve rapor ekranları.
- **UI testleri:** tüm XAML dosyaları için kaynak/tür tutarlılık testleri (Desktop.Tests) + FlaUI/UIA3 uçtan uca giriş testi (UiTests, `CGP_UI_TESTS=1`).
- **Release otomasyonu:** `v*` etiketinde Windows runner'da build + test + Inno Setup + GitHub Release (opsiyonel PFX secret'ları ile imzalama).

### Değişti
- Rapor sorguları için `(Status, StartsAtUtc)` ve `(CategoryId, StartsAtUtc)` indeksleri (`ReportIndexes` migration).
- Rapor performans testi: 2.000 satırlık aralık üst süre sınırıyla doğrulanır.

## [1.6.0] — 2026-11-16 · Raporlar ve tekrarlayan görüşme serisi

### Eklendi
- **Raporlar sayfası:** tarih aralığı (bu ay, geçen ay, bu yıl, son 30 gün veya özel), KPI kartları
  (toplam, tamamlanma, gelmedi, iptal, ortalama süre), günlük/aylık dağılım grafiği, kategori ve
  görüşmeci tabloları, saat dağılımı. Silinmiş görüşmeler rapora girmez.
- **Rol kapsamı:** yönetici ve sekreter tüm veriyi, görüşmeci yalnızca kendi görüşmelerini görür;
  kapsam servis katmanında zorlanır.
- **Rapor dışa aktarma:** `;` ayraçlı UTF-8 BOM CSV ve A4 PDF (özet + kategori/görüşmeci tabloları).
- **Tekrarlayan görüşme serisi:** haftalık, 2 haftada bir veya aylık; 2–12 kayıt. Çakışan/geçmiş
  tarihler onay sonrası atlanır ve seri kısmi oluşturulur. Aylık seride ay sonu kırpması uygulanır
  (31 Ocak → 28 Şubat → 31 Mart).
- **Seri iptali:** görüşme detayından yalnızca gelecek tarihli ve planlanmış seri kayıtlarını iptal
  eder; geçmiş/kapalı kayıtlar korunur.
- **Veri modeli:** `Appointments` tablosuna `SeriesId`, `SeriesIndex`, `SeriesCount` sütunları ve
  `SeriesId` indeksi (migration: `AppointmentSeries`).
- **Denetim kaydı:** `SeriesCreated` (31) ve `SeriesCancelled` (32).
- Dokümanlar: `docs/31_Raporlar.md`, `docs/32_Tekrarlayan_Seri.md`.
- **Testler:** 333 test (304 uygulama + 26 Desktop + 3 API). Yeni: seri takvimi, seri oluşturma/iptal
  kapsamı, rapor oranları, host kapsamı, gün/ay kovaları ve yerel gün sınırı.

### Sürüm
- Masaüstü sürümü **1.6.0**; derleme, installer ve güncelleme akışı aynı sürüme çekildi.

## [Unreleased] — Platform (API + mobil)

### Eklendi
- **Sunucu API’si** (`src/CgpRandevuTakip.Api`): mevcut `Domain` + `Application` katmanlarını barındırır; JWT erişim (30 dk) + döndürmeli yenileme belirteci (30 gün), AES-GCM sunucu sırları.
- **Uçlar:** kurulum/giriş/yenileme/çıkış, randevu gün/arama/detay/oluştur/güncelle/sil/geri al, görüşme sonucu, kategori, görüşmeci, sürümlü içerik manifesti (ETag), logo, sağlık.
- **Mobil (Expo / React Native)** iskeleti: giriş (SecureStore), günlük liste, tarih gezinme, çevrimdışı okuma önbelleği (SQLite), oturum yenileme.
- **API smoke testleri** (3) ve CI’da mobil TypeScript kontrolü; toplam **309 test**.
- Dokümanlar: `docs/29_Platform_Mimarisi.md`, `docs/30_Api_ve_Mobil_Kurulum.md`.

### Notlar
- Masaüstü uygulaması değişmedi; 1.5.1 olarak çalışmaya devam eder.
- Mobilde yazma kuyruğu (outbox), web uygulaması, SignalR ve FCM/APNs sonraki fazlarda.

## [1.5.1] — 2026-11-16 · Başlangıç onarımı ve temiz kaldırma

### Düzeltildi
- **Windows Script Host hatası:** Taşınabilir başlatıcı (VBS) hedef EXE yoksa sessizce çıkıyor; `On Error Resume Next` ve `FileExists` kontrolü eklendi. Silinen/taşınan portable kopyalar artık açılışta hata penceresi göstermiyor.
- **Ölü başlangıç girdileri:** Açılışta hedefi bulunmayan VBS ve boş/geçersiz `Run` değeri otomatik temizleniyor; kurulu sürüm kendi kaydını onarıyor. Tek otomatik başlatma girdisi kuralı uygulanıyor.
- **Bildirim kayıtları:** `HKCU\Software\Classes\AppUserModelId` altında biriken CGP kayıtları, ilişkili COM sınıfı ve ikon klasörleri temizleniyor (açılışta ölü olanlar, kaldırmada tümü).
- **VBS kodlaması:** Türkçe karakterli yollarda bozulmaması için UTF-16 LE (BOM) yazılıyor; tırnak kaçışı düzeltildi.

### Kaldırma
- Program dosyaları, kısayollar, `Run` kaydı, başlangıç VBS/LNK, bildirim kayıtları ve `updates` anlık görüntüsü temizleniyor.
- Kaldırma sırasında **veri silme onayı** eklendi (varsayılan: koru). Sessiz kaldırmada veri korunur.
- **Yedekler hiçbir durumda silinmiyor** (`Belgeler\CgpRandevuTakip`).
- Installer artık boş `Run` değeri oluşturmuyor.

### Süreç
- **Taşınabilir ZIP yalnızca kararlı sürümlerde** üretiliyor; `Kurulum-Olustur.ps1` varsayılan olarak ZIP oluşturmuyor, `-Portable` ile açılıyor.
- README indirme bölümü sürüm ve kaldırma davranışıyla güncellendi.

### Test
- **306 test** (280 uygulama + 26 Desktop). Yeni: başlangıç betiği içeriği, portable/kurulu mod, ölü hedef onarımı, boş `Run` temizliği, yol ayrıştırma.

## [1.5.0] — 2026-11-14 · Güncelleme güvenliği, geri dönüş ve kalite

### Güncelleme
- **SHA-256 doğrulama:** İndirilen Setup/ZIP, GitHub Releases `digest` değeriyle doğrulanır. Özet yoksa veya eşleşmezse güncelleme başlatılmaz.
- **Geri dönüş:** Kurulum öncesi program dosyalarının anlık görüntüsü alınır. Güncelleme uygulanamazsa açılışta otomatik olarak önceki sürüme dönülür; Ayarlar > Güncelleme > **Önceki sürüme dön** ile elle de yapılabilir. Veri klasörü korunur.
- **Taşınabilir güncelleme:** Süreç kapanışı beklenir; kopyalama başarısız olursa anlık görüntüden geri yüklenir ve sonuç bir sonraki açılışta bildirilir.
- **Güncelleme günlüğü:** Denetim, indirme, doğrulama ve kurulum adımları `logs/update.log` dosyasına yazılır; Ayarlar’dan açılır.
- **Sürüm notları:** Güncelleme kontrolünde GitHub release notları uygulama içinde görüntülenir.
- **Kaldırma:** Uninstall, HKCU başlangıç kaydını ve taşınabilir başlangıç VBS’ini temizler; kullanıcı verisi korunur.

### Kalite
- **Desktop test projesi:** `CgpRandevuTakip.Desktop.Tests` eklendi (logo servisi, geri dönüş anlık görüntüsü, PNG üretimi).
- **Performans:** 50 satırlık PNG üretimi ve 1000 kayıtlı gün sorgusu için eşikli testler.
- **Sürüm tutarlılığı:** `AppConstants.Version` ile derleme sürümü testle doğrulanıyor.
- **SHA-256 testleri:** Özet normalizasyonu ve dosya doğrulaması.
- **Tanılama paketi:** Kişisel veri içermeyen ZIP (günlükler + sistem bilgisi) masaüstüne kaydedilir.
- **Günlük saklama:** 90 günden eski günlükler açılışta temizlenir.
- **Erişilebilirlik:** Ayarlar alanlarına ekran okuyucu adları eklendi.

### Ürün / UX
- **Kullanım kılavuzu:** Uygulama içi `Yardım` penceresi ve `docs/26_Kullanici_Kilavuzu.md`.
- **Kontrol paneli:** Son 7 günün görüşme/tamamlanan/gelmedi özeti.
- **Yedek doğrulama:** Yedekleme sayfasında seçili yedeği doğrulama düğmesi.

### Test
- **295 test** (280 uygulama + 15 Desktop), Release 0 uyarı / 0 hata.

## [1.4.2] — 2026-11-14 · Kurulum sihirbazı tutarlılık düzeltmeleri

### Düzeltildi
- **Kurum e-postası → SMTP:** Adım 1’de girilen kurum e-postası, SMTP gönderen adresi ve test alıcısı boşsa otomatik doldurulur. SMTP adımı atlansa bile tamamlamada gönderen adresi boşsa kurum e-postası kullanılır.
- **"Beni hatırla" ve şifre değişimi:** Sihirbazda veya Ayarlar > Şifre değiştir akışında şifre değişince kayıtlı kimlik yeni şifreyle güncellenir; çıkış sonrası giriş formu eski şifreyle gelmez.
- **Yetki:** Ayarlar’daki “Kurulum sihirbazını yeniden çalıştır” düğmesi yalnızca yöneticide etkin.
- **Başlangıç modu:** Sihirbaza “Windows açılışında başlat” + “Gizli başlat / Giriş ekranı açık” seçimi eklendi; kullanıcının mevcut modu ezilmiyor.
- **Zorunlu kurum bilgileri:** Ayarlar kaydında da kurum adı, e-posta, telefon ve adres zorunlu; eksikse satır içi hata gösterilir.
- **Kapatma onayı:** Sihirbaz X ile kapatılırken “Değişiklikler kaydedilmedi, çıkılsın mı?” onayı istenir.
- **Logo temizliği:** Kayıt başarısız olursa ve önceden logo yoksa, bu çalıştırmada kopyalanan logo dosyası silinir.
- **Giriş ekranı:** Sürüm bilgisi eklendi; “Beni hatırla (30 gün)” etiketi güncel oturum davranışına göre düzeltildi.
- **Bildirim yardımı:** ntfy ve Telegram kurulum adımları numaralı ve ayrıntılı hale getirildi.

### Test
- 269/269 test.
- Kurum e-postası → SMTP ön doldurma; atlanan SMTP’de gönderen adresinin kurum e-postasına düşmesi doğrulandı.
- Güncelleme modunda şifre değişimi → çıkış → kayıtlı kimlikle (yazmadan) giriş doğrulandı.
- Ayarlar’da boş telefonla kaydetmenin engellenmesi, başlangıç modunun korunması ve kapatma onayı doğrulandı.

## [1.4.1] — 2026-11-14 · Kurulum sihirbazı rötuşları

### Düzeltildi
- "Atla" ile geçilen adımlar kenar çubuğunda **Atlandı** olarak işaretlenir; o adımda girilen yarım değerler kaydedilmez, mevcut ayarlar korunur.
- Özet ekranı atlanan SMTP, telefon bildirimi ve yedekleme adımlarını değerlerden bağımsız olarak **Atlandı** gösterir.
- Ayarlar ekranında kurum e-posta ve telefon alanları tam genişliğe alındı; uzun e-posta adresleri kırpılmıyor.
- EXE dosya özelliklerinde sürüm bilgisi `1.0.0` yerine **1.4.1** olarak görünüyor (`Directory.Build.props`).

### Test
- 269/269 test.
- SMTP adımına değer girip "Atla" ile geçme senaryosu uçtan uca doğrulandı; değerlerin kaydedilmediği veritabanından kontrol edildi.

## [1.4.0] — 2026-11-13 · Kurulum sihirbazı ve kurum logosu

### Eklendi
- **8 adımlı kurulum sihirbazı:** Hoş geldiniz → Kurum bilgileri → Yönetici hesabı → Görünüm/tercihler → E-posta (SMTP) → Telefon bildirimleri → Yedekleme → Özet. Zorunlu alanlar tamamlanmadan ilerlenemez; SMTP, push ve yedekleme adımları atlanabilir.
- **Kurum profili:** Kurum adı, e-posta, telefon ve adres zorunlu; wizard ve Ayarlar aynı doğrulamayı kullanır.
- **Yönetici e-postası zorunlu:** Şifre sıfırlama ve bilgilendirme için kullanılır; ilk yönetici oluşturulurken kaydedilir.
- **Canlı tema:** Tema seçildiği anda uygulanır; sihirbaz iptal edilirse önceki temaya dönülür.
- **Ayarlar’dan yeniden çalıştırma:** Sihirbaz güncelleme modunda açılır; yönetici e-postası ve isteğe bağlı olarak şifresi değiştirilebilir, şifre değişiminde oturum korunur.
- **Kurum logosu:** Sihirbazda ve Ayarlar’da seçilebilir/kaldırılabilir; veri klasöründe saklanır ve günlük WhatsApp liste PNG’sinin en üstünde görünür.
- **Canlı durum ekranları:** Eski sprint notları kaldırıldı; kontrol panelinde son yedek, bildirim durumu, veri klasörü ve sürüm bilgisi gösterilir. Ana pencere, giriş ve hatırlatıcı önizlemesi güncel metinlere kavuştu.

### Test
- 269/269 test.
- Temiz veriyle ilk kurulum, opsiyonel adımların atlanması, özet ve tamamlama doğrulandı.
- Güncelleme modu, e-posta değişimi, canlı tema ve iptalde tema geri dönüşü doğrulandı.
- Logo seçimi, kalıcı kayıt, kaldırma ve günlük liste PNG’sinde logo görünümü doğrulandı.

## [1.3.0] — 2026-11-13 · Kalıcı oturum ve arka plan bildirimleri

### Eklendi
- **Süresiz oturum:** Başarılı girişten sonra oturum, açık çıkış yapılana kadar geçerli. Token süresi yok; uygulama ve bilgisayar yeniden başlasa da otomatik giriş yapılır.
- **Çıkış:** Kalıcı token iptal edilir; sonraki açılışta giriş formu gösterilir.
- **Şifre değişimi/sıfırlama:** Kalıcı oturum token'ı geçersiz olur.
- **“Beni hatırla”:** Kutu korunur ve varsayılan işaretlidir. Süresiz oturum her durumda geçerlidir; kutu yalnızca çıkış sonrası kullanıcı adı/şifre ön doldurmayı yönetir.
- **Otomatik kilit kaldırıldı:** 30 dakika hareketsizlikte şifre sorulmaz.
- **Elle kilitleme:** Kenar çubuğunda “Kilitle” düğmesi. Şifre ile açılır; kilit durumu uygulama/PC yeniden başlasa da korunur.
- **Arka plan bildirimleri:** Windows açılışında gizli (`--tray`) başlatma. Kurulu sürümde HKCU Run, taşınabilir sürümde Başlangıç klasörü kısayolu.
- **Varsayılan otomatik başlatma:** Açık ve gizli mod. Bildirimler, hatırlatıcılar, otomatik yedek ve saklama servisleri arka planda çalışır.

### Not
- Bilgisayarda Windows oturumu kapalıysa uygulama çalışmaz; toast/push üretilemez. Windows açılışında gizli başlatma bu süreyi en aza indirir.
- Kalıcı oturum, aynı Windows hesabını kullanan herkesin uygulamaya erişebilmesi anlamına gelir.

### Test
- 235/235 test.
- Otomatik başlatma kaydı `--tray` ile doğrulandı.
- Elle kilitleme, kilit durumunun yeniden başlatmada korunması ve şifreyle açılması doğrulandı.
- `--tray` ile açılışta görünür pencere açılmadı; oturum otomatik açıldı.

## [1.2.1] — 2026-11-11 · Dijital imza ve indirme sayfası

### Eklendi
- **Kod imzalama hattı:** `Imza-Sertifikasi-Olustur.ps1` geliştirme sertifikası üretir; `Kurulum-Olustur.ps1` uygulama EXE'sini ve Setup EXE'yi imzalar.
- Üretim için PFX/CA sertifikası desteği (`-PfxPath`, `-CertificateThumbprint`).
- README ana sayfasına **İndir** bölümü: Setup EXE ve taşınabilir ZIP bağlantıları.
- `docs/22_Imza_ve_Yayin.md` yayın/imza rehberi.
- GitHub deposu public; Releases üzerinden indirilebilir.

### Önemli
- Bu sürümdeki imza **geliştirme (self-signed)** sertifikasıyladır. Windows, sertifika güvenilir değilse
  “Bilinmeyen yayıncı” gösterebilir. Üretim için CA'dan alınmış kod imzalama sertifikası kullanılmalıdır.

## [1.2.0] — 2026-11-11 · Kararlar ve yeni özellikler

### Eklendi
- **Excel’den içe aktarma:** `.xlsx` şablonu, görüşmeci/kategori eşleme, doğrulama, hatalı satır raporu ve toplu ekleme. Dışa aktar menüsünden şablon indirilebilir.
- **2 yıl saklama politikası:** 2 yıldan eski görüşmeler günlük olarak anonimleştirilir; kişi adı/telefonu, açıklama ve öğrenci bilgileri temizlenir. Eski kişinin daha yeni görüşmesi varsa kişi korunur.
- **Google Drive for Desktop:** OAuth yerine senkronize Drive klasörü. Yedekler klasöre kopyalanır; yedek listesinde Drive dosyaları görünür ve geri yükleme için indirilir.
- **Otomatik güncelleme:** Public GitHub Releases kontrolü; kurulu sürümde sessiz Setup, taşınabilir sürümde ZIP ile yerinde güncelleme. Ayarlar’dan manuel kontrol.
- **Toast tıklaması:** Bildirime tıklanınca ilgili hatırlatıcı aksiyon penceresi açılır.
- **WhatsApp iptal bilgisi:** Ziyaretçi iptalinde kişiye WhatsApp mesajı hazırlanır (pano + WhatsApp aç).
- **Erteleme:** Sonuç ekranında “Ertelendi” seçilince yeni tarih/saat zorunlu, onay istenir ve hatırlatıcılar yeniden planlanır.
- **Bildirimlerde kişi adı:** Windows toast ve push bildirimlerinde kişi adı gösterilir (ürün sahibi onayı).
- **Hatırlatıcı tekrarı:** İlk gösterim + 3 tekrar (toplam 4), 10’ar dakika arayla.
- **Excel/CSV/PDF/yazdırma** önceki sürümden korunur.

### Değişti
- **Q19:** Ağ sürücüsü özelliği eklenmedi.
- **Q23:** İptal eden kişiye WhatsApp bilgilendirmesi eklendi.
- **Q24:** Excel içe aktarma eklendi.
- GitHub deposu **public** yapıldı (otomatik güncelleme için).

### Test
- 233/233 otomatik test.
- Excel ayrıştırma gerçek veritabanı host/kategori adlarıyla doğrulandı.
- Google Drive klasör kopyalama/listeleme/indirme testleri.
- Setup ve taşınabilir paketler.

## [1.1.2] — 2026-11-11 · Dışa aktar menüsü teması

### Düzeltildi
- “Dışa aktar” düğmesinin açtığı bağlam menüsü koyu temaya uyarlandı; beyaz arka plan ve okunmayan yazılar giderildi.
- Menü öğeleri için koyu arka plan, açık metin ve vurgu rengi eklendi.

## [1.1.1] — 2026-11-11 · Sistem saat dilimi

### Düzeltildi
- Tüm UTC ↔ yerel saat dönüşümleri artık **Windows sistem saat dilimini** kullanır (önceden sabit `Europe/Istanbul` idi).
- Yedek dosya adları ve manifest zamanları, yedek geçmişi, randevu saatleri, hatırlatıcılar, denetim kaydı ve günlük paylaşım saatleri sistem saat dilimine göre gösterilir/hesaplanır.
- İşletim sistemi saat dilimi değiştirilirse uygulama yeniden başlatıldığında yeni dilim geçerli olur.

## [1.1.0] — 2026-11-10 · Taşınabilir mod

### Eklendi
- **Taşınabilir mod:** `portable.flag` veya `--portable` ile veriler EXE yanındaki `CGP-Data` klasöründe tutulur.
- `CGP-Data` içinde veritabanı, loglar, geçici dosyalar ve yedekler (`Yedekler`) bulunur.
- Taşınabilir modda otomatik başlatma (HKCU Run) devre dışıdır.
- Taşınabilir ve normal kurulum aynı anda çalışabilir (ayrı tek örnek kilidi).
- Taşınabilir ZIP içinde `portable.flag` ve `PORTABLE-BENIOKU.txt` bulunur.

### Not
- Klasör yazılamıyorsa uygulama güvenli biçimde kullanıcı profiline döner.

## [1.0.0] — 2026-11-10 · MVP sürümü

### Eklendi
- Per-user kurulum paketi (Inno Setup) ve taşınabilir ZIP
- Kurulum betiği: `Kurulum-Olustur.cmd`
- Sprint 6 UAT raporu: [docs/19_UAT_Raporu.md](docs/19_UAT_Raporu.md)

### Sürüm kapsamı
- Sprint 0–5, 4.5 UAT, 4.6 kimlik/kurtarma, manuel düzeltmeler
- Sprint 6A/6B: yedekleme, geri yükleme, otomatik yedek
- Sprint 6C: denetim kaydı ekranı
- Faz 2: şifreli yedek, Google Drive, Excel/CSV/PDF/yazdırma, BitLocker göstergesi

### Not
- Google Drive gerçek uçtan uca testi, kullanıcı OAuth istemci bilgilerini girdiğinde yapılacak.
- `dist\CGP-RandevuTakip-1.0.0-Setup.exe` üretildi; sessiz kurulum, çalıştırma ve kaldırma test edildi.

## [0.10.0] — 2026-11-10 · Şifreli yedek, Drive, dışa aktarma

### Eklendi
- **Yedek şifreleme:** AES-256-GCM + PBKDF2; `.cgpbak` dosyaları. Parola DPAPI ile saklanır, otomatik yedeklerde kullanılır.
- **Google Drive:** OAuth 2.0 + PKCE ile bağlanma, `drive.file` kapsamı, klasör oluşturma ve şifreli yedeği yükleme. Bağlantı token'ı DPAPI ile saklanır.
- **Excel (`.xlsx`)** ve **CSV** dışa aktarma; Günlük Liste'deki mevcut filtreyi kullanır.
- **PDF raporu** (QuestPDF, A4 yatay) ve **yazdırma** (Windows yazdırma diyaloğu).
- **BitLocker göstergesi:** Ayarlar'da disk şifreleme durumu ve yeniden kontrol.

### Güvenlik
- Drive'a yükleme yalnızca şifreli yedeklerle yapılabilir.
- Şifreli yedeklerin parolası kaybolursa geri yükleme yapılamaz.
- Dışa aktarılan Excel/PDF/CSV dosyaları kişisel veri içerir; yalnızca kullanıcı kaydeder.

### Test
- 221/221 otomatik test.
- Excel/CSV/PDF üretimi doğrulandı (PDF `%PDF`, xlsx açıldı).
- Google Drive gerçek uçtan uca testi, kullanıcı OAuth istemci bilgileri girilince yapılacak.

## [0.9.0] — 2026-11-10 · Sprint 6C: denetim kaydı ekranı

### Eklendi
- Yöneticiye özel, salt-okunur **Denetim Kaydı** sayfası
- Kullanıcı, işlem ve tarih aralığı filtreleri
- Sayfalama (25/50/100 kayıt), toplam kayıt ve sayfa özeti
- İşlem adlarının Türkçe karşılıkları; teknik detay için tooltip
- Denetim kaydı arayüzden değiştirilemez veya silinemez

### Yetki
- Denetim kayıtlarını yalnızca yönetici görüntüleyebilir; menü diğer rollerde gizlenir ve servis yetkiyi ayrıca doğrular.

## [0.8.0] — 2026-11-10 · Sprint 6A/6B: yedekleme ve geri yükleme

### Eklendi
- ZIP yedeği: `data.db` + `settings.json` + `manifest.json`; manifestte sürüm, şema, kayıt sayıları ve SHA-256
- Yedekleme sayfası: manuel yedek (tüm roller), yedek geçmişi, doğrulama durumu, klasörü açma
- Son 30 yedeğin saklanması
- Günde 2 otomatik yedek: 12:30 öğle yedeği + öğleden sonra günlük liste paylaşımı sonrası; paylaşım yoksa 18:00
- Geri yükleme: ZIP bütünlüğü, şema uyumu, SHA-256 ve SQLite `integrity_check` doğrulaması
- Geri yükleme öncesi otomatik güvenlik yedeği ve uygulamanın yeniden başlatılması

### Değişti
- Kurum adı varsayılanı **BEK Okulları**; Ayarlar'dan değiştirilebilir
- Çalışma saatleri (varsayılan 06:00–20:45) ve "mesai dışı görüşme" izni Ayarlar'dan yönetilir
- Push sağlayıcı seçimi (ntfy/Telegram) Ayarlar'dan yapılır; seçili sağlayıcı kullanılır

### Güvenlik
- Geri yükleme yalnızca yönetici rolüne açıktır
- Yedek `settings.json` dosyasına SMTP parolası ve push token/chat id yazılmaz

## [0.7.0] — 2026-11-10 · Toplu işlemler

### Eklendi
- Günlük listede her satırın başında seçim kutusu; başlıktan tümünü seçme
- Seçili görüşmeleri toplu silme (yönetici) ve toplu taşıma (yönetici/sekretör)
- Toplu taşımada "saatleri koru" veya "tümünü aynı saate taşı" seçenekleri; çakışmada onay
- Görüşmeci rolünde toplu seçim sütunu gizlenir

### Değişti
- Yeni görüşmede varsayılan tarih/saat: bugün + 1 saat (15 dk dilime yuvarlanır, 06:00–20:45 sınırlarına uyar)
- Yeni görüşmede veya saat değiştirilerek geçmişe alınan görüşmede "geçmiş tarih" uyarısı

### Test
- 187/187 test

## [0.6.4] — 2026-11-10

### Düzeltildi
- Liste satırlarında fare üzerine gelindiğinde tüm satırlarda (1., 2., 3. …) hover rengi görünüyor;
  DataGrid seviyesindeki `RowBackground`/`AlternatingRowBackground` ayarları kaldırıldı

## [0.6.3] — 2026-11-10

### Düzeltildi
- "Beni hatırla" yalnızca uygulama açılışında otomatik giriş yapıyor; çıkış sonrası giriş formu gösteriliyor ve önceden dolduruluyor

## [0.6.2] — 2026-11-10 · Manuel hata düzeltmeleri 2

### Düzeltildi
- Saat seçenekleri 06:00–20:45 aralığına çekildi
- Günlük liste sütun sırası: Görüşmeci, Kişi, Saat, Konu, Yöntem, Durum; sütun sıralaması açık
- Görüşmeciler/Kategoriler/Kullanıcılar listelerinde tüm sütunlar sıralanabilir
- Ziyaretçi iptal aksiyonu pencereyi kapatmıyordu; `Show()` ile açılan pencerede `DialogResult` hatası giderildi
- Günlük paylaşım mesajına görüşme detayları ve özet eklendi
- Ayarlar ekranındaki iki sütunun yüksekliği dengelendi
- "Beni hatırla" çıkış sonrası kullanıcı adı/şifreyi giriş formuna geri dolduruyor

## [0.6.1] — 2026-11-09 · Manuel hata düzeltmeleri

### Düzeltildi
- Liste satırlarında hover/seçim görünürlüğü güçlendirildi; sütun başlıkları hizalandı
- Pasif kategoriler yeni görüşme ekranında görünmüyor; düzenlemede mevcut pasif kategori korunuyor
- Görüşme düzenlemede kategori değişikliği artık kaydediliyor (navigation/FK çakışması giderildi)
- Saat alanı serbest metin yerine 15 dakikalık seçim listesi oldu
- Yeni görüşme varsayılan süresi 1 saat oldu
- Özel hatırlatıcı: saat/dakika seçimi ve 1 dk–7 gün aralığı
- Üst çubuktaki tarih bugünün listesini açar; marka/kurum adı kullanıcı alanına gider
- Günlük listedeki tarih alanı takvim seçicisi oldu
- "Host" kullanıcı metinleri "Görüşmeci" olarak değiştirildi
- Tanılama ekranındaki sahte S-01/S-02 butonları gerçek paylaşım/toast araçlarıyla değiştirildi
- Ziyaretçi iptali servis testi ve aksiyon akışı doğrulandı

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

## [Planlama] — Analiz ve tasarım

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
