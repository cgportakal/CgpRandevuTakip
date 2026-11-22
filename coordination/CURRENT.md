# CURRENT — Devir Durumu

**Son güncelleme:** 2026-11-22 03:05 (UTC+3)
**Son commit:** Güncel hash için `git log -1 --oneline` (bu dosya commit edildikçe değişir)
**Dal:** `master` (origin ile eşit)
**Durum:** Yeşil — **432 test**. **1.10.2 yayında** (Setup-only); kurulu sürüm 1.10.2.
P6 tamamlandı. Sıradaki: **P7 uçtan uca UAT** (web UI + mobil + masaüstü).

## Bu turda tamamlanan

- **1.10.2 yayını:** `v1.10.2` GitHub Release (Setup-only). Assetler: Setup (~67,1 MB), Dev.cer,
  SHA256SUMS, version-policy (`latestDesktopVersion: 1.10.2`); kurulu sürüm 1.10.2 doğrulandı.
- **P6 — açılış/pull/ağ/hatırlatıcı:** `SyncMaintenanceService` eklendi. Açılışta oturum →
  manifest → outbox flush → günlük liste (8 sn zaman aşımı); sunucu modunda 60 sn’de bir pull ve
  `ServerSyncTickMessage` ile liste tazeleme; ağ hatası çevrimdışı durumuna yazılıyor. Yerel
  `ReminderDispatcher` sunucu modunda çalışmıyor (çift bildirim önlenir).
- **P3/P4/P5 saha UAT’i:** Portatif masaüstü 1.10.1 + izole API ile çevrimdışı oluşturma/silme ve
  otomatik eşitleme, SignalR ekle/güncelle/sil + 45 sn kesinti sonrası yeniden bağlanma, çevrimiçi
  çakışma (sunucuyu koru / yerele uygula) ve outbox çakışması (“Çakışanları çöz”) doğrulandı
  ([docs/45](docs/45_Masaustu_Senkronizasyon_UAT.md)). Kritik hata yok.
- **Manifest sağlamlaştırması:** `ServerReadService.GetManifestAsync` eşzamanlı kategori/görüşmeci
  isteklerinde tek sıraya alındı (aynı önbellek anahtarına eşzamanlı yazma yarışını önler).
- **1.10.1 yayını:** `v1.10.1` GitHub Release (Setup-only). Assetler: Setup (~67,1 MB), Dev.cer,
  SHA256SUMS, version-policy (`latestDesktopVersion: 1.10.1`); kurulu sürüm 1.10.1 doğrulandı.
- **P5 — çakışma yönetimi (cafer-desktop):** `AppointmentInput.ExpectedUpdatedAtUtc` ve
  `AppointmentDetailDto.UpdatedAtUtc`; API eski sürümde `409 Conflict` + güncel DTO döndürüyor.
  Masaüstü düzenleyicisi “yerel uygula / sunucuyu koru / iptal” dialog’u gösteriyor; outbox
  çakışan kaydı `conflict` durumunda tutuyor ve “Çakışanları çöz” ile karar verilebiliyor.
  Yeni testler: API sürüm çakışması + outbox conflict/çözüm.
- **1.10.0 yayını:** `v1.10.0` GitHub Release; assetler Setup (~66,9 MB), portable (~94,9 MB),
  Dev.cer, SHA256SUMS, version-policy (`latestDesktopVersion/latestWebVersion: 1.10.0`).
  README Setup/portable bağlantıları HTTP 200; master CI ve Release işi başarılı.
- **1.10.0 sürüm hazırlığı:** `Directory.Build.props`, `AppConstants`, installer ve
  `Kurulum-Olustur.ps1` 1.10.0; `version-policy.json` + API `appsettings` latestDesktop/latestWeb
  1.10.0; web `APP_VERSION` 1.10.0; README sürüm/indirme/platform tablosu; docs/44 devir notu.

- **P4 — SignalR (masaüstü):** `Microsoft.AspNetCore.SignalR.Client` 8.0.23;
  `DesktopRealtimeClient` (`/hubs/events`, JWT, 2–30 sn üstel geri çekilme).
  `appointmentChanged` / `seriesChanged` / `contentChanged` olayları 400 ms debounce ile
  günlük listeyi yeniliyor; içerik olayında kategori/görüşmeci filtreleri de tazeleniyor.
  Oturum açma/çıkış ve uygulama açılış/kapanışında başlatılıp durduruluyor. 1 test.

- **P3 — yazma yolu, outbox ve çevrimdışı görünürlük (tamamlandı):** `SyncOutboxItems`
  tablosu/migration, `OutboxService`, `OutboxFlushService` (30 sn), `PendingAppointmentOverlay`.
  Tekil randevu create/update/delete, görüşme sonucu, seri oluşturma/ekleme/düzenleme/iptali,
  toplu silme/taşıma ve geri alma API yolları; ağ yoksa kuyruk. Çevrimdışı tekil ve seri
  kayıtlar listede/detayda görünür; geçici kimlik sunucu kimliğine eşlenir; başarısız kayıtlar
  “Sunucu bağlantısı” penceresinden yeniden denenebilir. `Appointment.ClientRequestId` + tekil
  indeks/migration ile idempotency. 13 outbox + 3 API testi.
- **1.9.0 yayını:** `v1.9.0` GitHub Release; assetler Setup (~66,9 MB), portable (~94,9 MB),
  Dev.cer, SHA256SUMS, version-policy (`latestDesktopVersion/latestWebVersion: 1.9.0`).
  README indirme bağlantıları HTTP 200; master CI ve Release işi başarılı.
- **1.9.0 sürüm hazırlığı:** `Directory.Build.props`, `AppConstants`, installer ve
  `Kurulum-Olustur.ps1` 1.9.0; `version-policy.json` + API `appsettings` latestDesktop/latestWeb
  1.9.0; web `APP_VERSION` 1.9.0; README indirme bağlantıları ve platform tablosu.
- **Yedekleme/geri yükleme yenilemesi:** manifest v2 (bölümler, ayar grupları, sayılar,
  ilk/son randevu, son değişiklik); seçmeli yedek (seçilmeyen bölümler dosyaya yazılmaz,
  `VACUUM`); içerik inceleme + uyarılar; boş yedek engeli + ileri düzey onay; geri yükleme
  artık `data.db` üzerine yazmıyor, transaction içinde birleştiriyor. Kullanıcılar/ayarlar
  varsayılan kapalı; “Ayarlar” seçilirse grup bazlı yazılıyor. “Sunucu bağlantısı” grubu
  (`sync.api.base_url`, okuma modu) yedeğe giriyor; oturum belirteçleri girmiyor. Logo
  seçilebiliyor. Gerçek v1.2.1 yedeği (0 kullanıcı) ile mevcut kullanıcı/ayarların korunduğu
  doğrulandı ([docs/43](docs/43_Yedekleme_ve_Geri_Yukleme.md)). 9 yedek + 4 VM testi eklendi.

- **Masaüstü P2 okuma yolu:** `Desktop/Services/Sync/` altında `ServerReadService`,
  `SyncCacheStore`, `SyncedAppointmentService`, `SyncedReportService`,
  `SyncedCategoryService`, `SyncedHostService`. `CgpApiClient`'a manifest (ETag/304),
  günlük liste, arama, detay, silinenler, rapor uçları eklendi. Yanıtlar
  `SyncCacheEntries` tablosunda (yeni `DesktopSyncCache` migration) önbelleğe alınır;
  ağ hatasında son başarılı yanıt gösterilir ve 30 sn sunucu denenmez. Ayarlar →
  “Sunucu bağlantısını yönet” → **Okuma modu (P2)** anahtarı (varsayılan kapalı);
  günlük listede “Sunucu: bağlı / çevrimdışı — önbellek” rozeti. 8 yeni test.
- **Yayın hazırlığı:** `mobile`’a SDK 58 uyumlu **`expo-dev-client`** eklendi; EAS development
  build artık doğrudan alınabilir. `docs/39` §5 adımları (EAS login, `eas init`, FCM kimlik
  bilgileri, cihaz) ve aşağıdaki “Diğer bilgisayarda devam” bölümü güncellendi.
- **1.8.5 yayını:** `v1.8.5` GitHub Release (Setup-only). Assetler: Setup (~66,9 MB), Dev.cer,
  SHA256SUMS, version-policy (`latestDesktopVersion: 1.8.5`); kurulu sürüm 1.8.5 olarak doğrulandı.
- **Liste/rapor hizaları:** tüm masaüstü `DataGrid` metin sütunlarına `DataGridCellText` (dikey
  ortalı), satırlara en az 40 px yükseklik; görüşmeci/kategori rozetleri ve rapor KPI kartları /
  tablo satırları dikey ortalandı. Portatif test verisiyle Kullanıcılar, Görüşmeciler ve Raporlar
  ekran görüntüleriyle doğrulandı.
- **1.8.5 sürüm hazırlığı:** sürüm dosyaları, `version-policy`/API `appsettings` latestDesktop
  1.8.5; README Setup bağlantısı ve platform tablosu güncellendi.
- **1.8.4 yayını:** `v1.8.4` GitHub Release yayınlandı (Setup-only). Assetler: Setup (~66,9 MB),
  Dev.cer, SHA256SUMS, version-policy (`latestDesktopVersion: 1.8.4`); README Setup bağlantısı
  HTTP 200; kurulu sürüm 1.8.4 olarak doğrulandı.
- **1.8.4 sürüm hazırlığı:** sürüm dosyaları, `version-policy`/API `appsettings` latestDesktop 1.8.4;
  README Setup bağlantısı ve platform tablosu güncellendi.
- **Kullanıcılar listesi hizası:** `DataGrid` satır yüksekliği 44’e sabitlendi; metin sütunları ve
  durum rozeti dikey ortalandı (`UsersPage.xaml`).
- **Masaüstü senkronizasyonu P1:** API istemcisi (`CgpApiClient`), DPAPI şifreli oturum saklama
  (`ApiSessionStore`), giriş/yenileme/401 tekrarı ve çıkış (`ApiConnectionService`); Ayarlar →
  “Sunucu bağlantısını yönet” penceresi (adres testi, sunucu hesabıyla giriş). 13 yeni test
  ([docs/42](docs/42_Masaustu_Veri_Senkronizasyonu.md) P1).
- **1.8.3 yayını:** `v1.8.3` GitHub Release yayınlandı (Setup-only; portable yok). Assetler:
  Setup (`CGP-RandevuTakip-1.8.3-Setup.exe`, ~66,8 MB), Dev.cer, SHA256SUMS, version-policy
  (`latestDesktopVersion/latestWebVersion: 1.8.3`); README Setup bağlantısı HTTP 200; release
  notları Türkçe özetlendi.
- **1.8.3 sürüm hazırlığı:** `Directory.Build.props`, `AppConstants`, installer ve
  `Kurulum-Olustur.ps1` 1.8.3; `version-policy.json` + API `appsettings` latestDesktop/latestWeb
  1.8.3; web `APP_VERSION` 1.8.3; README sürüm/indirme bağlantıları ve platform tablosu güncellendi.
- **Ortam yeniden üretimi:** `scripts/bootstrap.mjs` artık JDK 17+, Android SDK (`ANDROID_HOME`,
  `D:\Android\Sdk` veya varsayılan), `adb`, platform 35 ve EAS CLI durumunu raporluyor; kurulum
  ipuçları ve mobil dev build komutları eklendi. `docs/35` gereksinim tablosu güncellendi.
- **Ortak tasarım sistemi:** `design/tokens.json` (açık/koyu palet, tipografi, boşluk, yarıçap,
  rapor etiketleri) eklendi; web CSS değişkenleri, mobil `theme.ts` ve masaüstü `Light.xaml`
  aynı değerlere hizalandı ([docs/41](docs/41_Tasarim_ve_Veri_Paritesi.md)).
- **Rapor paritesi:** rapor etiketleri üç istemcide eşitlendi (Özet, Görüşmeci, Kategori, Katılım,
  Tekrar, Kişi geçmişi, Denetim); mobilde eksik `CategoryTrend` ve `Attendance` görünümleri eklendi.
- **Veri mimarisi kararı:** masaüstü API’ye bağlanacak, çevrimdışıyken yerel önbellek + outbox
  kullanacak; aşamalar `docs/41` §4’te.

- **Gerçek cihaz UAT (Samsung SM-G991B, Android 15 / One UI 7):** yerel dev client
  (`expo prebuild` + Gradle `assembleDebug`, arm64) USB ile kuruldu; `adb reverse` ile API/Metro
  bağlandı. Push açık/arka plan/süreç öldürülmüş senaryolarında geldi; bildirime dokunma soğuk
  başlatmada uygulamayı açtı. Çevrimdışı kuyruk, otomatik eşitleme, canlı olay, 45 sn API
  kesintisi sonrası yeniden bağlanma, zorunlu güncelleme, raporlar ve hatırlatma balonu doğrulandı
  ([docs/39](docs/39_Mobil_UAT_Android.md) §6).
- **Çevrimdışı kuyruk düzeltmeleri:** çakışan çevrimdışı kayıt sunucu hatasıyla takılıyordu;
  kuyruğa `ignoreConflicts` ile alınıyor ve form “çakışma denetimi atlanır” uyarısı gösteriyor.
  Bağlantı dönüşünde kuyruk otomatik gönderiliyor; gönderim hataları ekranda gösteriliyor.
- **EAS projesi (cafer-desktop):** `npx eas init` ile `@cgportakal/cgp-randevu-takip` oluşturuldu
  ve `mobile/app.json` içine bağlandı (`projectId: 0e759428-9702-43f5-9783-7b8662bd4afa`).
  EAS CLI 27.10.0 kurulu, oturum `gokmen_portakal` hesabıyla açık. `google-services.json` /
  `*.jks` gitignore’a eklendi.
- **Devir hazırlığı:** `mobile`’a SDK 58 uyumlu **`expo-dev-client`** eklendi; EAS development
  build artık doğrudan alınabilir. `docs/39` §5 adımları (EAS login, `eas init`, FCM kimlik
  bilgileri, cihaz) ve aşağıdaki “Diğer bilgisayarda devam” bölümü güncellendi.
- **Flaky render testi:** 50 satırlık PNG render testi CI ilk render gecikmesiyle 10,4 sn sürüp
  başarısız oluyordu; ölçüm öncesi ısınma render’ı ve 10 sn eşiği eklendi.
- **Android ortamı (CFR-LENOVO):** Temurin JDK 17.0.21 (`D:\Android\jdk17`), Android SDK
  (`D:\Android\Sdk`: platform-tools 37.0.1, emulator 37.2.13, platforms;android-35,
  google_apis x86_64), `cgp_uat` AVD (Pixel 6, 3 GB); `emulator -accel-check` WHPX kullanılabilir;
  `adb` ve emulator açılışı doğrulandı. EAS CLI 27.10.0 global kuruldu.
- **Dependabot picomatch:** GHSA-fc5q-39xc-vjpf **withdrawn**; yamalı sürüm yok ve yalnızca
  build araçlarında kullanılıyor. Alert “inaccurate” gerekçesiyle dismissed.
- **İçerik logosu UAT:** web konsolundan 160×80 PNG yüklendi; önizleme, `?v=` URL, anonim
  `GET /v1/content/logo` ve API testi doğrulandı. Bulunan 401 önizleme hatası düzeltildi
  ([docs/40](docs/40_Icerik_Logosu_UAT.md)).
- **Mobil push hazırlığı:** `EXPO_PUBLIC_EAS_PROJECT_ID` fallback’i ve eksik proje kimliğinde
  konsol uyarısı; `mobile/.env.example` ve `docs/39` FCM/APNs test adımları güncellendi.
- **İçerik logosu yükleme:** `POST /v1/admin/content/logo` (Admin, PNG/JPG/BMP, en fazla 4 MB);
  `branding/institution-logo.*` saklama, eski dosyayı temizleme, `ContentLogoUpdated` denetimi,
  sürüm sorgulu logo URL’si ve `contentChanged` olayı. Web konsolunda logo önizleme + dosya yükleme.
- **1.8.2 yayını:** `v1.8.2` GitHub Release (Setup-only; portable yok). Assetler: Setup, Dev.cer,
  SHA256SUMS, version-policy (`latestDesktopVersion: 1.8.2`); README Setup bağlantısı HTTP 200.
- **Android ortamı:** Temurin JDK 17 (`D:\Android\jdk17`), Android SDK (`D:\Android\Sdk`:
  platform-tools, emulator, API 35, google_apis x86_64), `cgp_uat` AVD (Pixel 6, 3 GB);
  WHPX hızlandırma etkin; Expo Go SDK 58 emülatöre kuruldu.
- **Mobil UAT (Android 15):** giriş, oturum kalıcılığı, günlük liste, çevrimdışı önbellek,
  çevrimdışı kayıt kuyruğu + eşitleme, SignalR canlı olay, 60 sn API kesintisi sonrası yeniden
  bağlanma, zorunlu güncelleme ekranı ve raporlar doğrulandı
  ([docs/39](docs/39_Mobil_UAT_Android.md)).
- **Mobil SignalR düzeltmesi:** yeniden bağlanma denemeleri tükenince bağlantı kalıcı kopuyordu;
  süresiz üstel geri çekilme eklendi (`mobile/src/realtime.ts`).
- **Mobil zorunlu güncelleme ekranı:** çift kodlanmış Türkçe metinler düzeltildi
  (`ForcedUpdateScreen.tsx`).
- **Dokümantasyon:** `docs/35` Android SDK/emülatör kurulumu ve emülatör tuzakları; `docs/39`
  Android UAT; README durum satırı ve belge tablosu güncellendi.

- **Zorunlu güncelleme UAT’i (web + masaüstü):** web istemcisi 1.0.0 / `minWebVersion` 9.9.9 →
  bloklayıcı ekran mevcut–gerekli–son sürümü doğru gösterdi; masaüstü 1.0.0 test derlemesi
  `ForcedUpdateWindow` ile mevcut 1.0.0 / gerekli 1.5.0 / son v1.8.1 gösterdi, “Çıkış” uygulamayı
  kapattı ([docs/38](docs/38_UAT_Zorunlu_Guncelleme.md)).
- **Sürüm notu biçimlendirmesi:** `ReleaseNotesFormatter` ham Markdown’ı düz metne çeviriyor;
  `ForcedUpdateWindow` ve `ReleaseNotesWindow` kullanıyor; 6 birim testi eklendi.
- **API sürüm politikası:** `appsettings.json` `latestDesktopVersion` 1.8.0 → **1.8.1**.
- **1.8.1 yayını:** `v1.8.1` GitHub Release yayınlandı (Setup-only; portable yok). Assetler: Setup, Dev.cer, SHA256SUMS, version-policy.json; latestDesktop 1.8.1. Release workflow parametre splat düzeltmesi (#25).
- **1.8.0 yayını:** `v1.8.0` etiketi push edildi; release workflow başarılı. GitHub Release assetleri: Setup (66,8 MB), portable (94,9 MB), SHA256SUMS, Dev.cer, version-policy.json. README indirme bağlantıları HTTP 200.
- **1.8.0 sürüm hazırlığı:** `AppConstants`/assembly/installer `1.8.0`; `version-policy.json` latest sürümleri 1.8.0; README indirme bağlantıları güncellendi; `docs/36_UAT_1.8.0.md` eklendi. Yerel Setup + portable + SHA256 üretildi.
- **Güncelleme indirme ilerlemesi:** `UpdateProgressWindow`; yüzdelik ProgressBar, MB bilgisi ve aşama metinleri. İsteğe bağlı ve zorunlu güncelleme akışları bu pencereyi kullanıyor.
- **Zorunlu güncelleme ekranı:** anonim `GET /v1/version-policy`; web ve mobilde minimum sürüm altındaysa bloklayıcı ekran; masaüstünde giriş öncesi `ForcedUpdateWindow`.
- **Sürüm politikası kaynağı:** `version-policy.json` release asset'i + `master` yedeği; API `VersionPolicy` ve `latestWebVersion`; web `NEXT_PUBLIC_APP_VERSION`, mobil `APP_VERSION`.
- **İçerik konsolu manuel UAT (API + web):** kategori ekleme/düzenleme/sıralama/pasifleştirme/silme, görüşmeci ekleme/güncelleme/pasifleştirme, giriş hesaplı görüşmeci, kurum/ayar kaydı, v1→v2 yayın, v1'e geri alma, manifest doğrulama, görüşmeci rolünde `/v1/admin/content` → 403, `contentChanged` ile web başlığının otomatik yenilenmesi.
- **Geri alma davranışı doğrulandı:** snapshot'ta olmayan kategoriler silinmez, pasifleştirilir; böylece randevu referansları korunur.
- **Web içerik yönetim konsolu:** `/v1/admin/content` (yalnızca Admin); kategori ekleme/düzenleme/sıralama/pasifleştirme/silme, görüşmeci ekleme/düzenleme, kurum profili ve genel ayarlar.
- **Yayınlama ve geri alma:** `ContentVersions` tablosu (`ContentPublishing` migration), yayın notu ile sürüm oluşturma, son 20 sürüm, seçilen sürüme geri dönme. Yayın/geri alma sonrası `contentChanged` SignalR olayı.
- **Web SignalR:** `contentChanged` aboneliği eklendi; işleyiciler sunucuya yanlış sonuç döndürmeyecek şekilde düzeltildi.
- **1.8.1 sürüm hazırlığı:** sürüm/assembly/installer 1.8.1; `version-policy.json` latestDesktop 1.8.1; README Setup 1.8.1; release workflow ara sürümlerde portable üretmeyecek şekilde koşullu; Setup-only paket yerel üretilip 1.8.1 kurulumu doğrulandı.
- **Güncelleme ilerleme UAT’i:** gerçek `v1.8.0` Setup indirmesiyle doğrulandı (63,7 MB, %1→%98, MB/ilerleme çubuğu, “İndirme doğrulanıyor”, “Kurulum başlatılıyor”; veri korundu). Doğrulama/geri dönüş UI thread’ini bloke ediyordu; `Task.Run` + `Task.Yield` + kısa son aşama beklemesiyle düzeltildi ([docs/37](docs/37_UAT_1.8.0_Guncelleme_Ilerleme.md)).
- **Hızlı başlangıç:** `BASLANGIC.md`, macOS `basla.command`, Windows `basla.cmd` /
  `scripts/basla.ps1` (temiz ağaç kontrolü → pull → Node kontrolü → bootstrap → durum).
- **Ortam yeniden üretimi:** `scripts/bootstrap.mjs` (Windows + macOS; `--check`, `--install`),
  `global.json` (.NET 8), `.nvmrc` (Node 22), `.editorconfig`, VS Code önerileri,
  `docs/35_Gelistirme_Ortami.md`, `.env.example` dosyaları, `mobile/eas.json`.
- **Branch protection → rulesets:** `master-koruma` (silme/force-push kapalı, doğrusal geçmiş,
  PR zorunlu, 3 kontrol + dal güncel, yalnızca squash, bypass yok) ve `etiket-koruma`
  (`v*` etiketleri silinemez/taşınamaz). Klasik branch protection kaldırıldı; tek kaynak rulesets.
  Ayrıntı: `PROTOCOL.md` → “GitHub koruması (rulesets)”.
- **Devir düzeni:** `coordination/` (CURRENT, PROTOCOL, Node kilit aracı), kök `AGENTS.md`
  ve README bağlantıları; iki bilgisayarda çakışmasız çalışma akışı.
- **SignalR:** `/hubs/events` (JWT, WebSocket `access_token`); `appointmentChanged`,
  `seriesChanged`, `reminderDue`. Web/mobil açıkken liste otomatik yenileniyor.
- **Sunucu hatırlatma worker’ı:** 15 sn tick, sessiz saat, SignalR + cihaz push + ntfy/Telegram.
- **Web Push (VAPID):** service worker, “Bildirimleri aç”, cihaz kaydı, test bildirimi.
- **Mobil push (Expo → FCM/APNs):** izin, token, girişte kayıt / çıkışta silme.
- **Web uygulaması (`web/`):** giriş, günlük liste, yeni görüşme + seri, detay/sonuç, 7 rapor türü.
- **Rapor ucu düzeltmesi:** tür uçları `/v1/reports/kind/{kind}` (summary ile çakışma giderildi).
- Dokümanlar: `docs/29`, `docs/30`, `docs/31`, README, CHANGELOG.

## Diğer bilgisayarda devam (kopyala-çalıştır)

```powershell
git pull --ff-only
node scripts/bootstrap.mjs --check      # eksik varsa: --install
node coordination/lock.mjs status       # boşsa kendi kilidini al
```

**Yayın:** `v1.10.0` hazır (Setup + portable). Uygulamayı denemek için GitHub Releases →
`CGP-RandevuTakip-1.10.0-Setup.exe` (veya portable ZIP); kaynaktan çalıştırmak için:

```powershell
# API (ayrı terminal)
$env:CGP_API_DATA="$PWD\src\CgpRandevuTakip.Api\data"
dotnet run --project src\CgpRandevuTakip.Api --urls http://0.0.0.0:5199

# Web (ayrı terminal)
cd web
$env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5199'
npm run dev

# Mobil (ayrı terminal)
cd mobile
$env:EXPO_PUBLIC_API_URL='http://<LAN-IP>:5199'
npx expo start
```

İlk API açılışında `POST /v1/auth/setup` ile yönetici oluşturulur; yerel veritabanı ve sırlar
repoda değildir. Yedekleme davranışı: [docs/43](docs/43_Yedekleme_ve_Geri_Yukleme.md).

**Sıradaki işin devri:** 1.10.0 yayınlandı. Sıradaki iş **P3/P4 saha UAT’i**:
çevrimdışı tekil/seri oluştur-güncelle-sil-taşı, kuyruğun sırayla gönderimi ve SignalR canlı
yenileme ([docs/44](docs/44_Masaustu_Senkronizasyon_Devir.md) §5). Ardından **P5 çakışma**
([docs/42](docs/42_Masaustu_Veri_Senkronizasyonu.md)).

Kendi kilidini al:

```powershell
node coordination/lock.mjs acquire --machine cafer-desktop --task "P3/P4 UAT" --branch agent/cafer-desktop/desktop-sync-uat
git switch -c agent/cafer-desktop/desktop-sync-uat
```

**Hazırlık durumu (21.11.2026):** `master` 1.10.0 sürümünde, 429/429 test, CI yeşil,
`bootstrap --check` tüm araçları buluyor (JDK 17, Android SDK, platform 35, adb, EAS CLI).
API yerel veritabanı ve sırlar repoda değil; ilk çalıştırmada `POST /v1/auth/setup` gerekir.

Android ortamı `D:\Android` altında kurulu; yeni terminalde:

```powershell
$env:JAVA_HOME='D:\Android\jdk17'
$env:ANDROID_HOME='D:\Android\Sdk'
$env:PATH="$env:JAVA_HOME\bin;$env:ANDROID_HOME\platform-tools;$env:ANDROID_HOME\emulator;$env:PATH"
emulator -list-avds                          # cgp_uat yoksa aşağıdaki komutla oluşturun
avdmanager create avd -n cgp_uat -k "system-images;android-35;google_apis;x86_64" -d pixel_6
emulator -avd cgp_uat -no-audio -no-boot-anim -gpu auto -memory 3072
adb devices
```

Push testi için sırayla (FCM hesabı kullanıcıya aittir):

1. `cd mobile` → `npx eas-cli login` (bu makinede oturum açık; diğer makinede gerekir)
2. EAS projesi bağlı: `@cgportakal/cgp-randevu-takip`
   (`app.json` → `extra.eas.projectId = 0e759428-9702-43f5-9783-7b8662bd4afa`)
3. `npx eas credentials` → Android → FCM (`google-services.json`; Firebase’den indirilir)
4. Fiziksel cihazı bağla; dev build APK’sını kur
5. `EXPO_PUBLIC_API_URL=http://<LAN-IP>:5199` → `npx expo start --dev-client`
   (Expo Go ile açmak için `--dev-client` yerine `--go`)
6. Giriş → `POST /v1/devices` kaydı → `POST /v1/push/test`

Yerel API veritabanı (`src/CgpRandevuTakip.Api/data/`) makineye özeldir ve commit edilmez;
ilk çalıştırmada `POST /v1/auth/setup` gerekir. Ayrıntı: [docs/39](docs/39_Mobil_UAT_Android.md) §5.

## Sıradaki iş (öncelik sırası)

1. **Yedekleme UAT’i** — seçmeli yedek/geri yükleme akışını gerçek kurulumda doğrula:
   yalnızca ayarları yedekle → değiştir → geri yükle; randevular ve kullanıcılar korunmalı,
   kurulum sihirbazı açılmamalı. Boş yedek onayı ve eski (v1) yedek denenmeli
   ([docs/43](docs/43_Yedekleme_ve_Geri_Yukleme.md)).
2. **Masaüstü veri senkronizasyonu** — P1 ✅, P2 ✅, P3 ✅, P4 ✅, P5 ✅, P6 ✅.
   Sıradaki: **P7 uçtan uca UAT** — web UI + mobil + masaüstü aynı randevu, içerik/seri olayları,
   rapor eşitliği ve kapanış dayanıklılığı.
   Ayrıntı: [docs/42](docs/42_Masaustu_Veri_Senkronizasyonu.md),
   [docs/45](docs/45_Masaustu_Senkronizasyon_UAT.md),
   [docs/44](docs/44_Masaustu_Senkronizasyon_Devir.md).
3. **iOS UAT** — Xcode yalnızca macOS’ta çalışır; Mac’te EAS dev build ile çevrimdışı kuyruk,
   canlı olay ve push doğrulanmalı.
4. **Mağaza derlemesi** — EAS `production` profiliyle imzalı AAB/APK; sürüm numarası ve
   `eas.json` `appVersionSource: remote` ayarı gözden geçirilmeli.
5. **İçerik logosu UAT notları** — gerçek kurum verisiyle son kontrol (opsiyonel); temel akış
   tamamlandı.

## Aktif kilit

Yok. (Gerçek cihaz UAT tamamlandı; kilit bırakıldı. Kontrol: `node coordination/lock.mjs status`)

## Dikkat edilecekler

- **`master` korumalı:** doğrudan push `GH006` ile reddedilir. `agent/<makine>/<konu>` dalı aç,
  PR gönder, `gh pr checks --watch`, sonra `gh pr merge <no> --squash --delete-branch`.
  Yalnızca squash merge kabul edilir (merge commit ve rebase reddedilir). PR `BEHIND` ise
  önce `gh pr update-branch <no>`.
- **Ortam:** Yeni makinede `node scripts/bootstrap.mjs`; `node_modules`/`bin`/`obj` taşınmaz.
  Masaüstü (WPF) yalnızca Windows’ta derlenir; macOS’ta API/web/mobil geliştirilir.
- **Masaüstü verisi ayrıdır:** desktop `%LOCALAPPDATA%` SQLite, sunucu `CGP_API_DATA/cgp.db`.
  Okuma modu (`sync.read.enabled`, varsayılan kapalı) açıkken okumalar API'den yapılır ve
  `SyncCacheEntries` tablosuna önbelleklenir; yazmalar API'ye gider, bağlantı yoksa
  `SyncOutboxItems` kuyruğuna alınır. **P3/P4 UAT tamamlanana kadar üretimde açılmamalıdır.**
  Sunucu oturumu açma/çıkarma önbelleği temizler.
- **Masaüstü zorunlu güncelleme testi:** `UpdateService` politikayı yerel API’den değil GitHub
  `master/version-policy.json` ve `releases/latest` üzerinden okur. Test için geçici düşük sürüm
  derlemesi yeterlidir; yerel API politikası etkilemez.
- **Android emülatör:** SDK `D:\Android\Sdk`, JDK `D:\Android\jdk17`, AVD `cgp_uat`
  (API 35, 3 GB, pencere açık `-gpu auto`). `-no-window` modunda emülatör iki kez Windows
  `RADAR_PRE_LEAK_64` kaydıyla kapandı; pencereli mod daha kararlı. Emülatör ana makineye
  `10.0.2.2` ile erişir; `mobile/.env` yerine `EXPO_PUBLIC_API_URL` ortam değişkeni kullanın.
- **Devir:** Android SDK/JDK her iki makinede `D:\Android` altında kurulu (CFR-LENOVO ve
  cafer-desktop). Yeni terminalde `JAVA_HOME`/`ANDROID_HOME` kullanıcı ortam değişkenlerinden gelir;
  `sdkmanager` için `JAVA_HOME` gerekir. Android UAT adımları `docs/35` bölüm 7’de.
- **Mobil push:** Expo Go Android’de SDK 53+ uzak bildirimleri desteklemez; FCM/APNs testi için
  dev client gerekir. Gerçek cihazda doğrulandı (`docs/39` §6).
- **Dev client + USB:** Fiziksel cihazda `npx expo start --dev-client --host localhost` ve
  `adb reverse tcp:5199 tcp:8081` kullanın. Metro’yu `CI=1` ile çalıştırırsanız dosya
  değişikliklerini izlemez; JS değişikliğinden sonra yeniden başlatın.
- **Rapor yolu:** `/v1/reports/kind/{kind}` kullan. `/v1/reports/Summary` yanlışlıkla
  `/v1/reports/summary` ucuna düşer (route çakışması).
- **SignalR CORS:** istemci `withCredentials: false` kullanmalı; aksi halde `AllowAnyOrigin` reddedilir.
- **Mobil `App.tsx`:** ekran navigasyonu (today/detail/add/reports) korunmalı; basit tek ekrana
  indirmeyin. `TodayScreen` prop’ları: `onOpenAppointment`, `onAdd`, `onReports`, `onManifest`.
- **`web/AGENTS.md`:** Next.js `next dev` tarafından üretilir; silmeyin (yeniden oluşur).
- **İçerik konsolu:** `/v1/admin/content` uçları yalnızca Admin. Düzenlemeler canlı DB'ye yazılır;
  `Yayınla` anlık görüntü alır ve `contentChanged` gönderir. `Geri al` seçilen sürümün
  kategori/görüşmeci/kurum-ayarlarını yükler ve yeni sürüm olarak yayınlar.
- **İçerik sürümleri:** `ContentVersions` tablosu; `ContentPublishing` migration. Admin/sekretarya
  hesapları geri almada değiştirilmez; yalnızca `Host` rolü geri yüklenir. Logo dosyası kapsam dışı.
- **Geri alma güvenliği:** snapshot'ta olmayan kategoriler **silinmez, pasifleştirilir**; görüşmelerdeki
  kategori referansları korunur. Aynı şekilde snapshot dışı görüşmeciler silinmez, pasifleştirilir.
- **Sürüm politikası:** API `VersionPolicy` ve kök `version-policy.json` aynı tutulmalı. Release
  workflow `version-policy.json` dosyasını asset olarak yükler. Web build'inde
  `NEXT_PUBLIC_APP_VERSION`, mobilde `src/config.ts` `APP_VERSION` güncellenmeli.
- **Zorunlu güncelleme:** web/mobilde giriş öncesi, masaüstünde `EnsureSupportedDesktopVersionAsync`
  ile giriş öncesi kontrol edilir. Ağ hatasında uygulama açılmaya devam eder; politika alınamazsa
  zorlama yapılmaz.
- **Sırlar:** `src/CgpRandevuTakip.Api/data/`, `*.key`, `vapid.json`, `signing.thumbprint` commit edilmez.
- **UI testleri:** yalnızca `CGP_UI_TESTS=1` ile ve yayınlanmış EXE üzerinde çalışır.

## Çalıştırma

```powershell
# İlk kurulum / doğrulama
node scripts/bootstrap.mjs          # --check / --install / --force
node scripts/bootstrap.mjs --check
bash basla.command                  # macOS (veya çift tık)
basla.cmd                           # Windows

# API (Windows)
$env:CGP_API_DATA="$PWD\src\CgpRandevuTakip.Api\data"
dotnet run --project src\CgpRandevuTakip.Api --urls http://0.0.0.0:5199

# Web
cd web
$env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5199'
npm run dev

# Mobil
cd mobile
$env:EXPO_PUBLIC_API_URL='http://<bilgisayar-ip>:5199'
npx expo start

# Testler
dotnet test CgpRandevuTakip.sln -c Release
```

## Doğrulama kısayolları

- Health: `GET /v1/health`
- Push anahtarı: `GET /v1/push/public-key`
- Hub negotiate: `POST /hubs/events/negotiate?negotiateVersion=1` (Bearer)
- Push testi: `POST /v1/push/test` (Bearer; yalnızca kendi cihazlarına)

## Son CI

Bu dal: yerel **432/432** test (335 uygulama + 79 masaüstü/VM + 16 API + 2 UI), web build ve
mobil `tsc` başarılı. Master CI ✅ ve **Release v1.10.2** ✅ (PR #70 / tag `v1.10.2`).
Son release: **1.10.2** (Setup-only); README Setup bağlantısı HTTP 200; kurulu sürüm 1.10.2.
Android emülatör ve **Samsung S21 gerçek cihaz UAT** tamamlandı.
Dependabot picomatch uyarısı **withdrawn** olduğu için dismissed; kod bağımlılığı değiştirilmedi.
Android SDK/JDK/adb/emulator `D:\Android` altında; AVD `cgp_uat` hazır; `expo-dev-client` eklendi.
`master` korumalı: doğrudan push reddedilir; force-push/silme kapalı; üç kontrol + güncel dal + yalnızca squash.
