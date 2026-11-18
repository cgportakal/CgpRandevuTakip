# 39 — Android Mobil UAT

**Tarih:** 17–18.11.2026 · **Sürüm:** mobil 1.0.0 (Expo SDK 58) · **API:** 1.8.2 · **Platform:** Android 15 (API 35) emülatör + **Samsung Galaxy S21 (SM-G991B)**

## 1. Amaç

Mobil uygulamanın gerçek bir Android çalışma zamanında temel akışlarını doğrulamak:

- Giriş ve oturum kalıcılığı
- Günlük liste ve çevrimdışı önbellek
- Çevrimdışı kayıt kuyruğu ve bağlantı gelince eşitleme
- SignalR canlı olayları ve kopma sonrası yeniden bağlanma
- Zorunlu güncelleme ekranı
- Raporlar ekranı

## 2. Kurulan ortam

Bu makinede Android SDK/emülatör yoktu; aşağıdaki bileşenler kuruldu (Xcode yalnızca macOS’ta
çalıştığı için iOS derlemesi bu makinede yapılamaz):

| Bileşen | Konum / sürüm |
|---|---|
| Temurin JDK 17 | `D:\Android\jdk17` (portable, `JAVA_HOME`) |
| Android cmdline-tools | `D:\Android\Sdk\cmdline-tools\latest` |
| Platform tools / ADB | `D:\Android\Sdk\platform-tools` (1.0.41) |
| Emulator | `D:\Android\Sdk\emulator` (WHPX etkin) |
| Sistem imajı | `system-images;android-35;google_apis;x86_64` |
| AVD | `cgp_uat` (Pixel 6, Android 15, 3 GB RAM) |
| Expo CLI | `mobile` içinde `npx expo` (SDK 58) |
| Expo Go | `host.exp.exponent` (emülatöre otomatik kuruldu) |

Çalıştırma:

```powershell
$env:ANDROID_HOME='D:\Android\Sdk'
$env:JAVA_HOME='D:\Android\jdk17'
$env:PATH="D:\Android\Sdk\platform-tools;D:\Android\Sdk\emulator;$env:JAVA_HOME\bin;$env:PATH"
$env:EXPO_PUBLIC_API_URL='http://10.0.2.2:5199'
cd mobile; npx expo start --android
```

`expo-dev-client` kurulduğu için CLI varsayılan olarak geliştirme derlemesini seçebilir.
Expo Go ile açmak için `npx expo start --go --android`, dev build ile açmak için
`npx expo start --dev-client --android` kullanın.

API UAT verisi `%TEMP%\opencode\cgp-mobile-uat`, ilk kurulum `POST /v1/auth/setup` ile
`admin / Admin12345` olarak yapıldı. Emülatör ana makineye `10.0.2.2` üzerinden erişir.

## 3. Sonuçlar

| Kontrol | Sonuç |
|---|---|
| Giriş | ✅ `admin` ile giriş, kurum adı ve kullanıcı üst çubukta |
| Oturum kalıcılığı | ✅ Uygulama/emülatör yeniden başlatıldığında oturum korundu |
| Günlük liste | ✅ Tarih, görüşme sayısı, saat, kategori, yöntem, durum |
| Çevrimdışı önbellek | ✅ “Çevrimdışı · son kaydedilen liste gösteriliyor” |
| Çevrimdışı kayıt | ✅ “Kuyruğa alındı · Bağlantı gelince görüşme sunucuya gönderilecek.” |
| Kuyruk eşitleme | ✅ Ağ açılınca kayıt sunucuya gitti (`Zeynep Cevrimdisi`, 20:00, `+905550000002`) |
| Listede bekleyen sayacı | ✅ “1 bekleyen” → eşitleme sonrası kayboldu |
| SignalR canlı olay | ✅ API’den eklenen kayıt elle yenilemeden listede göründü (6 → 7) |
| API kesintisi sonrası canlı olay | ✅ 60 sn kesintiden sonra yeniden bağlandı (7 → 8) |
| Zorunlu güncelleme | ✅ Mevcut 1.0.0 / gerekli 9.9.9 / son 9.9.9 |
| Raporlar | ✅ Özet: 8 görüşme, tamamlanma/gelmedi/iptal, ortalama 30 dk |

Ekran görüntüleri: `%TEMP%\opencode\cgp-shots\mobile-*.png` (commit edilmez).

## 4. Bulunan sorunlar ve düzeltmeler

**1) SignalR kopunca bir daha bağlanmıyordu.** `withAutomaticReconnect([0, 2, 5, 10, 20 sn])`
denemeleri tükenince bağlantı kalıcı olarak kopuk kalıyor; API 60 sn kapalı kaldığında canlı
olaylar gelmiyordu.

- `mobile/src/realtime.ts`: bağlantı kapanınca üstel geri çekilmeyle (2 → 30 sn, süresiz)
  yeniden deneme eklendi; başarılı bağlantıda sayaç sıfırlanıyor. `stopRealtime` zamanlayıcıyı
  temizliyor.
- UAT: 60 sn API kesintisi sonrası canlı olay yeniden çalıştı.

**2) Zorunlu güncelleme ekranında Türkçe metinler bozuktu.** `ForcedUpdateScreen.tsx`
UTF-8 yerine çift kodlanmış metin içeriyordu (“GÃ¼ncelleme gerekli”).

- Metinler doğru Türkçe ile değiştirildi; ekran görüntüsüyle doğrulandı.

## 5. Push testi (FCM/APNs) hazırlığı

Expo Go Android’de SDK 53+ uzak bildirimleri desteklemez; ayrıca `expo-device` emülatörde
`isDevice = false` döndürür. Gerçek push testi için:

1. **Fiziksel cihaz** (Android veya iPhone) kullanın. Emülatörde `expo-device` `isDevice = false`
   döndüğü için push kaydı hiç başlamaz.
2. **EAS projesi bağlıdır:** `@cgportakal/cgp-randevu-takip` (`app.json` →
   `expo.extra.eas.projectId = 0e759428-9702-43f5-9783-7b8662bd4afa`). Yeni makinede yalnızca
   `npx eas-cli login` gerekir; `eas init` tekrar çalıştırılmaz. Alternatif olarak
   `EXPO_PUBLIC_EAS_PROJECT_ID` ortam değişkeni kullanılabilir.
3. **FCM kimlik bilgilerini EAS projesine ekleyin** (Android): Firebase projesinden
   `google-services.json` indirin, `npx eas credentials` → Android → Push Notifications
   adımından yükleyin. iOS için APNs anahtarı aynı menüden eklenir.
4. **EAS dev build alın:** `npx eas build --profile development --platform android`.
   `expo-dev-client` bağımlılığı `mobile/package.json` içinde hazır (SDK 58 uyumlu), ayrıca
   kurulum gerekmez. Build çıktısındaki APK bağlantısını cihazda açıp kurun.
5. API adresini fiziksel cihazdan erişilebilir yapın:
   `EXPO_PUBLIC_API_URL=http://<bilgisayar-ip>:5199`; Windows güvenlik duvarında 5199 açık olmalı.
6. Uygulamada giriş yapın; `POST /v1/devices` ile `platform=android` cihaz kaydı oluşmalı.
7. `POST /v1/push/test` (Bearer) çağırın veya sunucudaki test bildirimi akışını kullanın;
   bildirimin cihaza geldiğini doğrulayın.
8. EAS/FCM kimlik bilgileri yoksa test **bloke** kalır; kod tarafında ek değişiklik gerekmez.

## 6. Gerçek cihaz UAT (Samsung Galaxy S21)

**Cihaz:** SM-G991B · Android 15 (SDK 35) · One UI 7.0 · USB hata ayıklama.

**Kurulum:** `expo prebuild` + yerel Gradle `assembleDebug` (arm64) ile dev client üretildi ve
`adb install` ile kuruldu. USB üzerinden `adb reverse tcp:5199` / `tcp:8081`; uygulama
`http://127.0.0.1:5199` adresini kullandı. Metro `--host localhost` ile çalıştırıldı.

| Kontrol | Sonuç |
|---|---|
| Giriş / oturum kalıcılığı | ✅ Uygulama yeniden açıldığında oturum korundu |
| Cihaz kaydı | ✅ `GET /v1/devices` → 1 Android (`appVersion 1.0.0`) |
| Push — uygulama açık | ✅ “Test bildirimi: push bağlantısı çalışıyor.” |
| Push — arka plan | ✅ Bildirim geldi |
| Push — süreç öldürülmüş (`am kill`) | ✅ Bildirim geldi |
| Bildirime dokunma | ✅ Uygulama açıldı (soğuk başlatma dahil) |
| Çevrimdışı kayıt | ✅ “Kuyruğa alındı”, “1 bekleyen” |
| Bağlantı gelince eşitleme | ✅ Otomatik gönderildi, sayaç temizlendi (2 → 3) |
| SignalR canlı olay | ✅ API’den eklenen kayıt elle yenilemeden göründü (3 → 4) |
| 45 sn API kesintisi sonrası | ✅ Yeniden bağlandı (4 → 5) |
| Zorunlu güncelleme | ✅ Mevcut 1.0.0 / gerekli 9.9.9 / son 9.9.9 |
| Raporlar | ✅ 4 görüşme özeti |
| Hatırlatma balonu | ✅ “Sıradaki kişiyi arama zamanı · Kuyruk Testi · 23:00” |

### Bulunan sorunlar ve düzeltmeler

**1) Çevrimdışı kayıt, çakışma nedeniyle kuyrukta takılıyordu.** Kuyruktan gönderilen
`appointment.create` çağrısı `ignoreConflicts` göndermediği için sunucudaki çakışma hatası
(“Host’un bu saatte başka bir görüşmesi var…”) kaydı sürekli bekletiyordu.

- Çevrimdışı kayıt kuyruğa `ignoreConflicts: true` ile alınır; kullanıcı kaydı zaten bilerek
  oluşturmuştur ve bağlantı yokken çakışma denetimi yapılamaz.
- Form üstündeki bilgi metni “çakışma denetimi atlanır” olarak güncellendi.

**2) Bağlantı geri gelince kuyruk kendiliğinden gönderilmiyordu.** `flushOutbox` yalnızca
pull-to-refresh veya canlı olay tetiklemesiyle çalışıyordu.

- `TodayScreen`: çevrimdışı → çevrimiçi geçişinde otomatik `flushOutbox` + liste tazeleme.
- Kuyruk gönderim hatası artık ekran hatası olarak gösterilir (sessiz kalmaz).

**3) Dev client soğuk açılışta Metro adresi.** İlk açılış LAN IP’sini kaydettiği için uygulama
kapanınca “Unable to load script” görülebildi.

- USB testlerinde `npx expo start --dev-client --host localhost` + `adb reverse` kullanılır.
- `CI=1` modunda Metro dosya değişikliklerini izlemez; JS değişikliğinden sonra Metro yeniden
  başlatılmalıdır.

**4) Samsung uyku davranışı.** Ekran test sırasında kapandı; `adb shell svc power stayon true`
ve uzun ekran zaman aşımı kullanıldı. Push’un güvenilirliği için uygulama pil ayarı
**Kısıtlanmamış** olmalı ve “Kullanılmayan uygulamaları uyut” listesinden çıkarılmalıdır.

## 7. Sınırlar

- **Expo Go’da uzak push yok:** Android’de SDK 53+ uzak bildirimleri desteklemez; push testi
  yerel dev client ile yapıldı. EAS **production** imzalı APK/AAB bu turda üretilmedi; dev client
  Metro’ya bağımlıdır.
- Samsung’da pil optimizasyonu (“Kısıtlanmamış”) ve uyku listesi ayarları test edilmedi; push
  güvenilirliği için kullanıcıya bırakılan manuel adımdır.
- **iOS test edilemedi:** Xcode yalnızca macOS’ta çalışır; bu makine Windows.
- Emülatör iki kez Windows `RADAR_PRE_LEAK_64` kaydıyla kapandı. Pencereli mod ve 3 GB RAM ile
  daha kararlı çalıştı; UAT adımları yeniden başlatılarak tamamlandı.
- Çevrimdışıyken tekrarlayan seri oluşturma tasarım gereği devre dışı (“seri hariç”).
- Tarih/saat ve süre alanları varsayılan değerlerle kullanıldı; ayrıntılı form doğrulaması
  (geçersiz telefon, çakışma) bu turun kapsamı dışında.
