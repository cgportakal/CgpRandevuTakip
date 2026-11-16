# 29 — Platform Mimarisi: Masaüstü + Web + Mobil

**Tarih:** 16.11.2026 · **Durum:** Faz 0-1 başladı (API + mobil iskelet)

## 1. Alınan kararlar

| Konu | Karar |
|---|---|
| Kapsam | **Tek kurum**, kendi sunucusu |
| Özellik | Web ve mobilde **masaüstü ile tam eşitlik**; mobilde mobil-özgü iyileştirmeler |
| Çevrimdışı | Mobilde **zorunlu** (okuma + yazma kuyruğu) |
| Mobil | **React Native / Expo** (EAS Build + EAS Update) |
| Web | React / Next.js (PWA) |
| Sunucu | ASP.NET Core API, mevcut `Domain` + `Application` katmanları aynen |
| Veritabanı | Sunucuda SQLite (v1) → gerekirse PostgreSQL |
| Bulut | Zorunlu değil; Supabase yalnızca yönetilen Postgres/Storage için opsiyonel |
| iOS | Mac + Apple Developer hesabı ile EAS Build |

## 2. Neden bu mimari?

Masaüstü uygulamasının iş kuralları `Application` katmanında ve **saf .NET**:
randevu çakışması, hatırlatıcı planı, yetki, doğrulama. Bu katmanı API içinde
barındırmak, web/mobil için kural **kopyalamadan** birebir uyum sağlar.

```
Desktop (WPF)  ─┐
Web (Next.js)  ─┼─ HTTPS/JSON ─► ASP.NET Core API ─► SQLite / PostgreSQL
Mobile (Expo)  ─┘        │            (Domain + Application)
                         ├─ SignalR (anlık içerik)
                         ├─ Push (ntfy/Telegram → FCM/APNs)
                         └─ Zamanlayıcı (hatırlatma worker)
```

## 3. Sunucu (mevcut)

- `src/CgpRandevuTakip.Api` — .NET 8 Minimal API.
- Kimlik: mevcut PBKDF2 doğrulaması + **JWT erişim (30 dk)** + **yenileme belirteci (30 gün, döndürmeli)**.
- Veri: `CGP_API_DATA` altında `cgp.db` (masaüstü şemasıyla aynı migrations).
- Uçlar:

| Uç | Açıklama |
|---|---|
| `GET /v1/health` | Sürüm, şema, sunucu saati |
| `GET /v1/auth/status` | İlk kurulum gerekli mi |
| `POST /v1/auth/setup` | İlk yönetici |
| `POST /v1/auth/login` · `/refresh` · `/logout` | Oturum |
| `GET /v1/auth/me` | Kullanıcı |
| `GET /v1/appointments/day` · `/search` · `/next` · `/{id}` | Okuma |
| `POST/PUT/DELETE /v1/appointments` | Yazma, çakışma sonucu `409` |
| `GET/PUT /v1/appointments/{id}/outcome` | Görüşme sonucu |
| `GET /v1/categories` · `/v1/hosts` | Katalog |
| `GET /v1/content/manifest` | Sürümlü içerik (ETag) |
| `GET /v1/content/logo` | Kurum logosu |

## 4. İçerik güncelleme (anlık)

`/v1/content/manifest` tek pakette: kurum profili, ayarlar, kategoriler,
görüşmeciler, özellik bayrakları, sürüm politikası. `ETag` + `version` ile
istemci önbelleği doğrular.

- Faz 2: `SignalR` hub (`content.updated`) → bağlı istemciler anında yeniler.
- Faz 5: web admin konsolu → önizleme → yayınla → geri al.
- Aynı manifest tüm platformlarda aynı ekran/metni üretir.

## 5. Çevrimdışı ve senkronizasyon (Faz 2)

Ortak protokol (uygulanacak):

| Uç | Açıklama |
|---|---|
| `GET /v1/sync/changes?since=<cursor>&limit=500` | `SyncVersion` sırasıyla upsert + tombstone |
| `POST /v1/sync/push` | Toplu değişiklik, `clientMutationId` ile idempotent |

- Her tabloya eklenecek: `UpdatedAtUtc`, `SyncVersion`, `DeletedAtUtc`, `OriginDeviceId`.
- Masaüstü: mevcut SQLite + `SyncOutbox`; çevrimdışı çalışmaya devam eder.
- Mobil: `expo-sqlite` + outbox; bağlantı gelince gönderir.
- Çakışma: randevu saati/görüşmeci çakışması kullanıcıya; diğer alanlarda sürüm kontrolü + denetim.
- Şu an mobilde **okuma önbelleği** hazır; yazma kuyruğu Faz 2.

## 6. Bildirimler

- **SignalR (uygulandı):** `/hubs/events` — randevu/seri değişiklikleri ve hatırlatmalar bağlı
  istemcilere anlık iletilir. Web ve mobil açıkken liste otomatik yenilenir, hatırlatma baloncuğu çıkar.
- **Sunucu hatırlatma worker’ı (uygulandı):** 15 saniyede bir due hatırlatmaları işler; sessiz saatlerde
  push göndermez. Kanallar:
  - **Web Push (VAPID)** — tarayıcı service worker’ı; üst çubuktaki “Bildirimleri aç”.
  - **Expo Push (FCM/APNs köprüsü)** — mobil cihaz belirteci; EAS geliştirme/mağaza derlemesi gerekir.
  - **ntfy / Telegram** — yedek/opsiyonel kanal (`Push` yapılandırması).
- **Cihaz kayıtları:** `POST /v1/devices` (web/mobil), çıkışta silinir; süresi dolan web abonelikleri otomatik temizlenir.
- **Masaüstü:** kendi yerel veritabanı ve `ReminderDispatcher`’ı ile çalışmaya devam eder.
- iOS için Apple Developer ($99/yıl); Expo kullanıldığında APNs anahtarı EAS tarafından yönetilir.

## 7. Güncelleme stratejisi

| Kanal | Yöntem |
|---|---|
| Sunucu | Blue/green dağıtım |
| Web | Otomatik dağıtım + cache bust |
| Mobil (JS/UI) | **EAS Update** (mağaza onayı yok) |
| Mobil (native) | EAS Build → TestFlight / Play |
| Masaüstü | Mevcut güncelleyici (SHA-256 + geri dönüş) |

`VersionPolicy` alanları (`minMobileVersion`, `minDesktopVersion`) zorunlu
güncelleme ekranını tetikler. Davranış değişiklikleri önce **feature flag** ile
sunucudan açılır; böylece tüm platformlar aynı gün aynı davranışı alır.

## 8. Dağıtım seçenekleri

| Seçenek | Artı | Eksi |
|---|---|---|
| **Kurum içi mini PC/NAS** + Cloudflare Tunnel | Veri kurumda, düşük maliyet | İnternet kesintisi, bakım |
| Küçük VPS (2 vCPU/4 GB) + Docker | Erişim kolay, yedek kolay | Aylık maliyet |
| Supabase (Postgres + Storage) + API | Yönetilen yedek, kolay ölçek | KVKK/bölge ve maliyet |

Öneri: v1’de kendi sunucu + SQLite (mevcut), kullanıcı sayısı artınca Postgres.
Supabase yalnızca yönetilen Postgres/Storage istenirse devreye alınır; iş kuralları
yine .NET API’de kalır.

## 9. Fazlar ve mevcut durum

| Faz | İçerik | Durum |
|---|---|---|
| 0 | Mimari, sözleşme, API iskeleti | **Tamamlandı** |
| 1 | Kimlik/JWT, randevu/katalog/içerik uçları, mobil iskelet | **Tamamlandı (v1)** |
| 2 | Çift yönlü senkron + outbox + çakışma + sunucu hatırlatma | **Tamamlandı (mobil)** |
| 3 | Web (Next.js PWA) tam CRUD | **Beta (v1)** |
| 4 | Mobil tam CRUD + çevrimdışı yazma + FCM/APNs + mağaza | **Push hazır; mağaza/UAT sürüyor** |
| 5 | İçerik konsolu + SignalR + bildirim worker | **SignalR + push (v1) tamam** |
| 6 | Pilot, izleme, KVKK, yedekleme | Planlı |

## 10. Riskler

- **KVKK:** veri buluta/ sunucuya çıkıyor; saklama, bölge, erişim, denetim gerekir.
- **iOS:** Mac + Apple Developer hesabı; APNs yapılandırması.
- **Çift bildirim:** tek üretim noktası + `NotificationDeliveries` idempotency.
- **Senkron çakışması:** sürüm + kullanıcı onayı; sessiz veri kaybı yok.
- **Eski istemciler:** API `v1` sözleşmesi + minimum sürüm politikası.
