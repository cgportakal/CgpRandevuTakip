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

## 3. Web

Faz 3’te `web/` altında Next.js (PWA) uygulaması eklenecek. Aynı API ve
`/v1/content/manifest` sözleşmesi kullanılacak.
