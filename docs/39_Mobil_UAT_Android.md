# 39 — Android Mobil UAT

**Tarih:** 17.11.2026 · **Sürüm:** mobil 1.0.0 (Expo SDK 58) · **API:** 1.8.2 · **Platform:** Android 15 (API 35) emülatör

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

## 5. Sınırlar

- **Push bildirimi test edilemedi:** Expo Go, Android’de SDK 53’ten sonra uzak bildirimleri
  desteklemiyor. FCM/APNs testi için EAS dev build (ve FCM kimlik bilgileri) gerekir.
- **iOS test edilemedi:** Xcode yalnızca macOS’ta çalışır; bu makine Windows.
- Emülatör iki kez Windows `RADAR_PRE_LEAK_64` kaydıyla kapandı. Pencereli mod ve 3 GB RAM ile
  daha kararlı çalıştı; UAT adımları yeniden başlatılarak tamamlandı.
- Çevrimdışıyken tekrarlayan seri oluşturma tasarım gereği devre dışı (“seri hariç”).
- Tarih/saat ve süre alanları varsayılan değerlerle kullanıldı; ayrıntılı form doğrulaması
  (geçersiz telefon, çakışma) bu turun kapsamı dışında.
