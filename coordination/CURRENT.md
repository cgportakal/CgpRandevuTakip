# CURRENT — Devir Durumu

**Son güncelleme:** 2026-11-17 00:55 (UTC+3)
**Son commit:** Güncel hash için `git log -1 --oneline` (bu dosya commit edildikçe değişir)
**Dal:** `master` (origin ile eşit)
**Durum:** Yeşil — 359 test, CI 3/3

## Bu turda tamamlanan

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

1. **İçerik yönetim konsolu** — kategori/görüşmeci/kurum/ayar düzenleme, yayınlama,
   `contentChanged` SignalR olayı ve geri alma.
2. **Zorunlu güncelleme ekranı** — `VersionPolicy.minMobileVersion` / `minDesktopVersion`
   ile eski istemciyi yönlendirme.
3. **Mobil cihaz UAT’i** — çevrimdışı kuyruk, canlı olay, push (EAS dev build gerekir).
4. **Dependabot `minimatch` (web build zinciri)** — moderate; Expo/Next güncellemesiyle çözülecek.

## Aktif kilit

Yok. (Kontrol: `node coordination/lock.mjs status`)

## Dikkat edilecekler

- **Masaüstü verisi ayrıdır:** desktop `%LOCALAPPDATA%` SQLite, sunucu `CGP_API_DATA/cgp.db`.
  Desktop senkronizasyonu henüz yok; SignalR/push sunucu tarafını etkilemez.
- **Rapor yolu:** `/v1/reports/kind/{kind}` kullan. `/v1/reports/Summary` yanlışlıkla
  `/v1/reports/summary` ucuna düşer (route çakışması).
- **SignalR CORS:** istemci `withCredentials: false` kullanmalı; aksi halde `AllowAnyOrigin` reddedilir.
- **Mobil `App.tsx`:** ekran navigasyonu (today/detail/add/reports) korunmalı; basit tek ekrana
  indirmeyin. `TodayScreen` prop’ları: `onOpenAppointment`, `onAdd`, `onReports`, `onManifest`.
- **`web/AGENTS.md`:** Next.js `next dev` tarafından üretilir; silmeyin (yeniden oluşur).
- **Sırlar:** `src/CgpRandevuTakip.Api/data/`, `*.key`, `vapid.json`, `signing.thumbprint` commit edilmez.
- **UI testleri:** yalnızca `CGP_UI_TESTS=1` ile ve yayınlanmış EXE üzerinde çalışır.

## Çalıştırma

```powershell
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

`43963401634` — .NET, Web (Next.js), Mobil (Expo) üçü de başarılı.
