# CURRENT — Devir Durumu

**Son güncelleme:** 2026-11-18 10:15 (UTC+3)
**Son commit:** Güncel hash için `git log -1 --oneline` (bu dosya commit edildikçe değişir)
**Dal:** `agent/cfr-lenovo/content-logo` (PR bekliyor)
**Durum:** Yeşil — **377 test**; içerik konsolundan logo yükleme eklendi. Mobil push/iOS testleri
ortam gereksinimi nedeniyle sırada.

## Bu turda tamamlanan

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

## Sıradaki iş (öncelik sırası)

1. **Mobil push testi (FCM/APNs)** — EAS dev build + FCM kimlik bilgileri gerekir; Expo Go uzak
   bildirimi desteklemiyor. Android emülatörü ve SDK kurulu (`D:\Android\Sdk`, AVD `cgp_uat`).
2. **iOS UAT** — Xcode yalnızca macOS’ta çalışır; Mac’te EAS dev build ile çevrimdışı kuyruk,
   canlı olay ve push doğrulanmalı.
3. **İçerik logosu UAT** — web konsolundan gerçek logo yükleme, önizleme ve `contentChanged`
   sonrası istemci yenilemesi.

## Aktif kilit

Yok. (CFR-LENOVO 1.8.0 release tamamlandı; kilit bırakıldı. Kontrol: `node coordination/lock.mjs status`)

## Dikkat edilecekler

- **`master` korumalı:** doğrudan push `GH006` ile reddedilir. `agent/<makine>/<konu>` dalı aç,
  PR gönder, `gh pr checks --watch`, sonra `gh pr merge <no> --squash --delete-branch`.
  Yalnızca squash merge kabul edilir (merge commit ve rebase reddedilir). PR `BEHIND` ise
  önce `gh pr update-branch <no>`.
- **Ortam:** Yeni makinede `node scripts/bootstrap.mjs`; `node_modules`/`bin`/`obj` taşınmaz.
  Masaüstü (WPF) yalnızca Windows’ta derlenir; macOS’ta API/web/mobil geliştirilir.
- **Masaüstü verisi ayrıdır:** desktop `%LOCALAPPDATA%` SQLite, sunucu `CGP_API_DATA/cgp.db`.
  Desktop senkronizasyonu henüz yok; SignalR/push sunucu tarafını etkilemez.
- **Masaüstü zorunlu güncelleme testi:** `UpdateService` politikayı yerel API’den değil GitHub
  `master/version-policy.json` ve `releases/latest` üzerinden okur. Test için geçici düşük sürüm
  derlemesi yeterlidir; yerel API politikası etkilemez.
- **Android emülatör:** SDK `D:\Android\Sdk`, JDK `D:\Android\jdk17`, AVD `cgp_uat`
  (API 35, 3 GB, pencere açık `-gpu auto`). `-no-window` modunda emülatör iki kez Windows
  `RADAR_PRE_LEAK_64` kaydıyla kapandı; pencereli mod daha kararlı. Emülatör ana makineye
  `10.0.2.2` ile erişir; `mobile/.env` yerine `EXPO_PUBLIC_API_URL` ortam değişkeni kullanın.
- **Devir:** Android SDK/JDK yalnızca `cafer-desktop` makinesinde `D:\Android` altında kurulu.
  Diğer makinede zorunlu değildir; web/API/masaüstü için `node scripts/bootstrap.mjs` yeterlidir.
  Android UAT gerekirse `docs/35` bölüm 7’deki adımlar izlenir.
- **Mobil push:** Expo Go Android’de SDK 53+ uzak bildirimleri desteklemez; FCM/APNs testi için
  EAS dev build gerekir.
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

Logo yükleme dalı: yerel **377/377** test, web build, mobil `tsc --noEmit` başarılı.
PR kontrolleri merge öncesi `gh pr checks --watch` ile doğrulanacak.
Son release: **1.8.2 Setup-only** başarılı; Android UAT tamamlandı.
`master` korumalı: doğrudan push reddedilir; force-push/silme kapalı; üç kontrol + güncel dal + yalnızca squash.
