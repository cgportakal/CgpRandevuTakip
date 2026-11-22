# 48 — Mobil Android 1.1.0 Build ve UAT

**Tarih:** 22.11.2026 · **Sürüm:** Android 1.1.0 (versionCode 1) · **Profil:** EAS `preview` (APK)

Bu belge, 1.11.0 özelliklerini içeren Android APK’nın üretimini, kurulumunu ve
Samsung S21 üzerindeki doğrulamasını kaydeder.

## 1. Build

```powershell
cd mobile
npx eas-cli build -p android --profile preview --non-interactive
```

- **Build ID:** `741399f5-758e-4f24-b2e0-1556fb1ccd26`
- **Sürüm:** `1.1.0` · **versionCode:** `1`
- **APK:** ~74,9 MB
- **Paket:** `com.cgportakal.cgpRandevuTakip`

### Yapılandırma

- `eas.json` → `preview.android.buildType = "apk"`.
- `app.config.js` → `GOOGLE_SERVICES_JSON` dosya değişkeni; yoksa
  `./google-services.json`.
- `expo-build-properties` → `android.usesCleartextTraffic = true`.
  Kurum içi API HTTP üzerinde çalıştığı için release APK’da zorunludur.
- EAS projesinde `GOOGLE_SERVICES_JSON` **secret file** olarak tanımlıdır;
  Firebase yapılandırması repoya commit edilmez.

## 2. Kurulum

```powershell
adb uninstall com.cgportakal.cgpRandevuTakip
adb install CGP-RandevuTakip-1.1.0.apk
adb reverse tcp:5199 tcp:5199
```

Dev build’den geçişte imza farkı nedeniyle önce kaldırma gerekir.

## 3. UAT — Samsung S21 (R5CN3C6SN)

| Akış | Sonuç |
|---|---|
| `mobile/.maestro/login.yaml` | ✅ |
| `mobile/.maestro/create-appointment.yaml` | ✅ |
| `mobile/.maestro/offline-create.yaml` | ✅ |
| `mobile/.maestro/reports.yaml` | ✅ |

Doğrulananlar:

- Giriş ve günlük liste.
- Ek misafir + oda ile randevu oluşturma; çakışmada “Yine de kaydet”.
- Uçak modunda oluşturma → “Kuyruğa alındı”.
- Bağlantı dönüşünde outbox’ın otomatik gönderilmesi; kayıt API’de görünür.
- Rapor özeti (301 görüşme / 435 kişi döneminde).

## 4. Bu build’de düzeltilenler

- Release APK’da **cleartext HTTP** engeli kaldırıldı.
- `NetInfo` geç/yanlış durumda olsa bile ağ hatasında (`ApiError.status == 0`)
  randevu **outbox’a** alınır.
- Expo push zaman aşımı artık API host’unu durdurmaz; `DevicePushSender`
  zaman aşımını uyarı olarak loglar ve sonraki turda yeniden dener.

## 5. Sınırlar

- iOS build/UAT macOS + Xcode gerektirir.
- APK “preview/internal” profille üretildi; mağaza dağıtımı için `production`
  profili (AAB) ve Play Console hesabı gerekir.
- EAS artifact bağlantısı 2026-12-06’ya kadar geçerlidir; kalıcı dağıtım için
  GitHub Release’e APK eklendi.
