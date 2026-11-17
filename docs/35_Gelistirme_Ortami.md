# 35 — Geliştirme Ortamı (Windows + macOS)

**Amaç:** Projeyi ikinci bilgisayarda sıfırdan, aynı sürümlerle ve eksiksiz
çalıştırabilmek. Git yalnızca kaynak kodu taşır; `node_modules`, `bin/obj`,
veritabanı ve anahtarlar taşınmaz — bunlar **yeniden üretilir**.

## 1. Makine yetenekleri

| Bileşen | Windows | macOS |
|---|---|---|
| Masaüstü (WPF, .NET 8) | ✅ Geliştirme + test + Setup EXE | ❌ Derlenmez (WindowsDesktop hedefi) |
| API (ASP.NET Core) | ✅ | ✅ |
| Web (Next.js) | ✅ | ✅ |
| Mobil (Expo) | ✅ Android | ✅ iOS + Android |
| iOS derleme/TestFlight | ❌ | ✅ (Xcode + Apple Developer) |
| UI testleri (FlaUI) | ✅ | ❌ |

macOS'ta masaüstü kodu okunabilir/incelenebilir; **derleme ve UAT Windows'ta** yapılır.

## 2. Gereksinimler

| Araç | Sürüm | Windows | macOS |
|---|---|---|---|
| Git | güncel | `winget install Git.Git` | `brew install git` |
| .NET SDK | **8.0.x** | `winget install Microsoft.DotNet.SDK.8` | `brew install --cask dotnet-sdk` |
| Node.js | **22 LTS** (`.nvmrc`) | `winget install OpenJS.NodeJS.LTS` | `brew install node@22 && brew link --overwrite node@22` |
| npm | Node ile | ✅ | ✅ |
| GitHub CLI | opsiyonel | `winget install GitHub.cli` | `brew install gh` |
| Inno Setup 6 | Windows | `winget install JRSoftware.InnoSetup` | — |
| Xcode + CLT | iOS | — | App Store + `xcode-select --install` |
| CocoaPods / watchman | iOS/Metro | — | `brew install cocoapods watchman` |
| Android SDK | Android | `cmdline-tools` veya Android Studio (bkz. bölüm 7) | — |
| EAS CLI | mobil derleme | `npm i -g eas-cli` | `npm i -g eas-cli` |

## 3. Tek seferlik kurulum

```bash
git clone https://github.com/cgportakal/CgpRandevuTakip.git
cd CgpRandevuTakip
node scripts/bootstrap.mjs
```

Script:

- Git/Node/npm/.NET/gh sürümlerini kontrol eder.
- Windows'ta Inno Setup'ı, macOS'ta Xcode/CocoaPods/watchman'i raporlar.
- `dotnet restore` (Windows: tüm çözüm; macOS: API + API testleri).
- `web` ve `mobile` için `npm ci` çalıştırır (lockfile'dan deterministik).
- `web/.env.local` ve `mobile/.env` yoksa örnekten oluşturur (gitignore'da).
- Eksik sistem aracı varsa kurulum komutlarını gösterir.

Seçenekler:

```bash
node scripts/bootstrap.mjs --check     # sadece rapor (eksik varsa çıkış kodu 1)
node scripts/bootstrap.mjs --install   # eksikleri winget/brew ile kur
node scripts/bootstrap.mjs --force     # node_modules'ü yeniden kur
```

### Hızlı başlangıç dosyaları

```bash
# macOS (çift tık da olur)
bash basla.command          # --yes / --check
```

```bat
:: Windows
basla.cmd                   :: --yes / --check
```

Bu dosyalar sırayla: temiz çalışma ağacı kontrolü → `git pull --ff-only` →
(Node yoksa kurulum teklifi) → `bootstrap.mjs` → `CURRENT.md` özeti → aktif kilit.
Elle kullanım: `BASLANGIC.md`.

## 4. Günlük akış

```bash
git pull --ff-only
node coordination/lock.mjs status
node coordination/lock.mjs acquire --machine <makine> --task "<iş>"
# ... çalış ...
dotnet test CgpRandevuTakip.sln -c Release     # Windows
cd web && npm run build
cd mobile && npx tsc --noEmit
node coordination/lock.mjs release --machine <makine>
```

## 5. Neler git'e girmez, nasıl yeniden üretilir?

| Gitignore'da | Neden | Yeniden üretim |
|---|---|---|
| `node_modules/` | Platforma özgü, büyük | `npm ci` (lockfile commit'li) |
| `bin/`, `obj/` | Derleme çıktısı | `dotnet restore` + `dotnet build` |
| `src/CgpRandevuTakip.Api/data/` | DB, JWT/VAPID anahtarı, logo | API ilk açılışta üretir |
| `*.db`, `*.db-wal` | Kullanıcı verisi | Yeni kurulumda boş; gerekirse yedekten |
| `*.key`, `vapid.json` | Sır | İlk açılışta üretilir |
| `.env*`, `.env.local` | Ortama özgü | `.env.example` → kopyala |
| `dist/`, `*.zip` | Yayın çıktısı | `Kurulum-Olustur.ps1` |
| `TestResults/`, `coverage/` | Test çıktısı | Testler |

> **Önemli:** `node_modules` veya `bin/obj` klasörlerini makineler arasında
> kopyalamayın; sürüm/platform uyuşmazlığı üretir. Lockfile + `npm ci` / `dotnet restore` yeterlidir.

### Veri taşıma (gerekirse)

- **Sunucu verisi:** `src/CgpRandevuTakip.Api/data/` ( DB + `branding/`) güvenli kanaldan
  (şifreli arşiv) taşınır; git'e eklenmez.
- **Masaüstü verisi:** `%LOCALAPPDATA%\CgpRandevuTakip` veya `CGP-Data`; uygulamanın
  yedekleme özelliğiyle alınır.
- **Sırlar:** JWT/VAPID anahtarları taşınırsa oturumlar/push abonelikleri korunur; taşınmazsa
  yeniden üretilir ve istemciler yeniden giriş yapar.

## 6. Ortam değişkenleri

| Değişken | Nerede | Örnek |
|---|---|---|
| `CGP_API_DATA` | API | `D:\Cgp\api-data` |
| `CGP_JWT_KEY` | API (opsiyonel) | 32+ bayt; yoksa `data/jwt.key` |
| `CGP_API_SECRET_KEY` | API (opsiyonel) | base64 32 bayt |
| `Push__Enabled`, `Push__Provider`, `Push__Topic`, `Push__Token`, `Push__ChatId` | API | ntfy/Telegram |
| `Expo__AccessToken` | API (opsiyonel) | Expo hesabı |
| `NEXT_PUBLIC_API_URL` | Web | `http://127.0.0.1:5199` |
| `EXPO_PUBLIC_API_URL` | Mobil | Fiziksel cihazda `http://<LAN-IP>:5199` |

## 7. Mobil / iOS / Android

- `mobile/.env` içindeki adres cihazdan erişilebilir olmalı; güvenlik duvarında 5199 açık.
- Android emülatörü: `http://10.0.2.2:5199`; iOS simülatörü: `http://127.0.0.1:5199`.
- `.env` yerine `EXPO_PUBLIC_API_URL` ortam değişkeni de kullanılabilir (ortam değişkeni önceliklidir).
- Uzak push için EAS geliştirme derlemesi: `npx eas init`, `npx eas build --profile development`.
- `mobile/eas.json` profilleri hazırdır (`development`, `preview`, `production`).
- iOS için Apple Developer hesabı ve Xcode gerekir (yalnızca macOS).
- Expo Go, Android’de SDK 53+ **uzak push** desteklemez; push testi için EAS dev build gerekir.

### Android SDK (Windows, Android Studio olmadan)

Android Studio kurmadan yalnızca komut satırı araçlarıyla emülatör kurulumu:

1. **JDK 17** (portable ZIP): <https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jdk/hotspot/normal/eclipse>
2. **cmdline-tools**: <https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip>
3. Zip’leri açıp `cmdline-tools` klasörünü `Sdk\cmdline-tools\latest` olarak yerleştirin.

```powershell
$root = 'D:\Android'
$env:JAVA_HOME = "$root\jdk17"
$sdk = "$root\Sdk"
$sdkManager = "$sdk\cmdline-tools\latest\bin\sdkmanager.bat"
$avdManager = "$sdk\cmdline-tools\latest\bin\avdmanager.bat"

# Lisanslar (sorulara y)
& $sdkManager "--sdk_root=$sdk" --licenses

# Paketler (~3 GB indirme)
& $sdkManager "--sdk_root=$sdk" 'platform-tools' 'emulator' 'platforms;android-35' 'system-images;android-35;google_apis;x86_64'

# AVD
& $avdManager create avd -n cgp_uat -k 'system-images;android-35;google_apis;x86_64' -d 'pixel_6' --force

# Hızlandırma kontrolü (WHPX/Hyper-V)
& "$sdk\emulator\emulator.exe" -accel-check
```

Emülatörü başlatma ve uygulamayı açma:

```powershell
$env:ANDROID_HOME = $sdk
$env:PATH = "$sdk\platform-tools;$sdk\emulator;$env:JAVA_HOME\bin;$env:PATH"
& "$sdk\emulator\emulator.exe" -avd cgp_uat -no-audio -no-boot-anim -gpu auto -memory 3072

$env:EXPO_PUBLIC_API_URL = 'http://10.0.2.2:5199'
cd mobile
npx expo start --android   # Expo Go'yu emülatöre kurar ve projeyi açar
```

Notlar:

- `emulator -accel-check` “WHPX is installed and usable” demeli; aksi halde Windows Hypervisor
  Platform/Hyper-V etkinleştirilmelidir.
- `-no-window` modu bu makinede Windows `RADAR_PRE_LEAK_64` kaydıyla iki kez kapandı; pencereli
  mod ve `-memory 3072` daha kararlı.
- Emülatör ilk açılışta 1–2 dakika sürer; `adb wait-for-device` ve `sys.boot_completed` ile bekleyin.
- Ayrıntılı Android UAT: [docs/39](39_Mobil_UAT_Android.md).

## 8. CI ve dal koruması

- **CI** üç iş çalıştırır: .NET, Web (Next.js), Mobil (TypeScript).
- `master` korumalıdır: **doğrudan push reddedilir**, force-push/silme kapalı, üç kontrol zorunlu.
- Akış: `agent/<makine>/<konu>` dalı → PR → `gh pr checks --watch` → squash merge.

## 9. Sorun giderme

| Belirti | Çözüm |
|---|---|
| `dotnet` 8 bulunamıyor | .NET 8 SDK kurun; `global.json` 8 ister |
| `npm ci` lockfile hatası | `package-lock.json` commit'li mi kontrol edin; `npm install` ile düzeltip lockfile'ı commit'leyin |
| macOS'ta `CgpRandevuTakip.Desktop` derlenmiyor | Beklenen; WPF Windows-only. API/web/mobil kullanın |
| Expo cihazdan API'ye ulaşamıyor | `EXPO_PUBLIC_API_URL` LAN IP; güvenlik duvarı 5199 |
| Emülatör kendiliğinden kapanıyor | Pencere açık `-gpu auto -memory 3072` kullanın; `-no-window` bu makinede kararsız |
| Web'de bildirim izni yok | Chrome site ayarları > Bildirimler > İzin ver; HTTPS/localhost |
| Port 5199/3000 dolu | `Get-NetTCPConnection -LocalPort 5199` / `lsof -i :5199` |
