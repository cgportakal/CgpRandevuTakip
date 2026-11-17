# 30 — API ve Mobil Çalıştırma

## 1. API

```powershell
# Geliştirme (veri: src\CgpRandevuTakip.Api\data)
dotnet run --project src\CgpRandevuTakip.Api --urls http://0.0.0.0:5199
```

Ortam değişkenleri:

| Değişken | Açıklama |
|---|---|
| `CGP_API_DATA` | Veri klasörü (varsayılan `./data`) |
| `CGP_JWT_KEY` | JWT anahtarı (≥32 bayt). Verilmezse `data/jwt.key` üretilir |
| `CGP_API_SECRET_KEY` | Sunucu sırları için AES anahtarı (base64, 32 bayt) |

İlk kurulum (yalnızca bir kez):

```powershell
$body = @{
  username='admin'; displayName='Ad Soyad'; email='admin@kurum.local'
  password='Admin12345'; confirmPassword='Admin12345'; deviceName='kurulum'
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri http://127.0.0.1:5199/v1/auth/setup `
  -ContentType 'application/json; charset=utf-8' `
  -Body ([Text.Encoding]::UTF8.GetBytes($body))
```

Swagger: `http://127.0.0.1:5199/swagger`

### Üretim notları

- HTTPS’i ters proxy (Caddy/Cloudflare Tunnel) sonlandırmalı.
- `Cors:AllowedOrigins` üretimde web adresine kısıtlanmalı.
- `CGP_API_DATA` yedeklenmeli (`cgp.db` + `branding/`).
- Tek kurum için SQLite yeterli; eşzamanlı yazma artarsa PostgreSQL’e geçilir.

## 2. Mobil (Expo)

```powershell
cd mobile
npm install
npx expo start
```

Sunucu adresi:

```powershell
# Fiziksel cihaz / LAN
$env:EXPO_PUBLIC_API_URL='http://192.168.1.20:5199'; npx expo start
```

- Android emülatörü varsayılan olarak `http://10.0.2.2:5199` kullanır.
- iOS simülatörü `http://127.0.0.1:5199` kullanır.
- Telefon ve bilgisayar aynı ağda olmalı; güvenlik duvarında 5199 açık olmalı.

Mevcut ekranlar:

| Ekran | Durum |
|---|---|
| Giriş | API oturumu + güvenli saklama (SecureStore) |
| Günlük liste | Tarih gezinme, kartlar, çevrimdışı önbellek, yenileme |
| Çıkış | Yenileme belirtecini iptal eder |

Sonraki adımlar: randevu oluşturma/düzenleme/sonuç, çevrimdışı yazma kuyruğu,
bildirim izni ve FCM/APNs.

## 3. Web (Next.js)

```powershell
cd web
npm install
$env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5199'
npm run dev
```

- `http://127.0.0.1:3000` adresinde açılır.
- Giriş, günlük liste (tarih/arama), yeni görüşme + tekrarlayan seri, detay + görüşme sonucu,
  silme/seri iptali ve 7 rapor türü hazırdır.
- Çevrimdışıyken son gün listesi `localStorage` önbelleğinden gösterilir.
- Üretim: `npm run build` + `npm run start`; HTTPS’i ters proxy sonlandırmalı ve
  `Cors:AllowedOrigins` web adresine kısıtlanmalı.

## 4. SignalR ve bildirimler

- Hub: `http://127.0.0.1:5199/hubs/events` (JWT; WebSocket query `access_token`).
- Olaylar: `appointmentChanged`, `seriesChanged`, `reminderDue`.
- Web/mobil bağlıyken günlük liste ve raporlar otomatik yenilenir; hatırlatma baloncuğu gösterilir.

### 4.1 Web Push (tarayıcı)

- VAPID anahtarları ilk açılışta `data/vapid.json` içinde üretilir; `GET /v1/push/public-key` ile istemciye verilir.
- Web’de üst çubuktaki **“Bildirimleri aç”** düğmesi izin ister, service worker’ı (`/sw.js`) kaydeder ve aboneliği `POST /v1/devices` ile sunucuya yazar.
- Chrome’da izin daha önce reddedildiyse site ayarlarından sıfırlanmalıdır (🔒 > Bildirimler > İzin ver).
- HTTPS gerekir; `localhost`/`127.0.0.1` geliştirmede kabul edilir.

### 4.2 Mobil Push (FCM/APNs)

- Mobil, **Expo Push** üzerinden FCM/APNs’e köprülenir; sunucuda sertifika tutmak gerekmez.
- Gerekenler: `eas init` ile `projectId`, ardından **geliştirme derlemesi** (`eas build --profile development`). Expo Go’da uzak push desteklenmez.
- Cihaz belirteci girişte `POST /v1/devices` ile kaydedilir; çıkışta silinir.
- Expo hesabı özel anahtar istiyorsa `Expo__AccessToken` ortam değişkeni verilir.

### 4.3 ntfy / Telegram

```json
"Push": {
  "Enabled": true,
  "Provider": "ntfy",
  "Topic": "cgp-bildirim",
  "Token": "",
  "ChatId": ""
}
```

```powershell
$env:Push__Enabled='true'
$env:Push__Provider='telegram'
$env:Push__Token='123456:ABC...'
$env:Push__ChatId='123456789'
```

- Worker 15 saniyede bir çalışır; sessiz saatlerde push göndermez, uygulama içi olay sessiz iletilir.
- Cihaz push’u (web/mobil) ayrıca çalışır; ntfy/Telegram yapılandırması zorunlu değildir.
- `POST /v1/push/test` yalnızca oturum sahibinin cihazlarına test bildirimi gönderir.

## 5. İçerik yönetimi (web)

- Yönetici olarak giriş yapın; sol menüden **İçerik Yönetimi**'ni açın.
- **Kategoriler:** ekleme, düzenleme, renk, sıralama, aktif/pasif ve kullanılmayan kaydı silme.
- **Görüşmeciler:** ekleme, ad/isActive düzenleme; istenirse giriş hesabı oluşturma.
- **Kurum ve Ayarlar:** kurum profili, çalışma/sessiz saatler, hatırlatıcı, bildirim ve push bayrakları.
- **Yayınla ve Geri Al:** yayın notu ile sürüm oluşturma, son 20 sürümü görme ve seçilen sürüme dönme.
- Yayın/geri alma sonrası `contentChanged` SignalR olayı gönderilir; web ve mobil istemciler manifesti yeniler.

## 6. Sonraki adımlar

- Zorunlu güncelleme ekranı.
- Mobil cihaz UAT’i (çevrimdışı kuyruk + canlı olay + push).


