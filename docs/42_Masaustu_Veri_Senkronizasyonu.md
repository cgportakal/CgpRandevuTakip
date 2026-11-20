# 42 — Masaüstü Veri Senkronizasyonu (Devir Planı)

**Tarih:** 19.11.2026 · **Durum:** P1 tamamlandı; P2 (okuma yolu) kod tarafı tamamlandı, manuel UAT sıradaki · **Kaynak karar:** [docs/41](41_Tasarim_ve_Veri_Paritesi.md) §4

Bu belge, masaüstü uygulamasının API ile tam senkronize çalışması için yapılacak işleri ve
teknik kararları diğer bilgisayara devretmek için yazıldı. Yeni oturumda önce
`coordination/CURRENT.md`, sonra bu belge okunmalı.

## 1. Hedef

Tek doğruluk kaynağı API’dir. Web ve mobil zaten API’yi kullanır. Masaüstü:

- Çevrimiçiyken doğrudan API’den okur/yazar.
- Çevrimdışıyken yerel SQLite önbelleği ve **outbox** kuyruğu ile çalışır.
- Bağlantı gelince kuyruk sırayla gönderilir; sunucudaki değişiklikler SignalR ile alınır.
- Aynı randevu üç istemcide anında görünür.

Mevcut durum: masaüstü yerel `%LOCALAPPDATA%\CgpRandevuTakip\data.db` (veya portable
`CGP-Data\data.db`) veritabanını kullanır. P1 oturum yönetimi ve P2 okuma yolu eklendi;
okuma modu açıkken listeler/raporlar API'den okunur. Yazma yolu (P3) henüz yereldir.

## 2. API sözleşmesi (mevcut uçlar)

| Amaç | Uç |
|---|---|
| Giriş / yenileme / çıkış | `POST /v1/auth/login`, `POST /v1/auth/refresh`, `POST /v1/auth/logout` |
| Oturum bilgisi | `GET /v1/auth/me` |
| İçerik manifesti | `GET /v1/content/manifest` (ETag destekli) |
| Günlük liste | `GET /v1/appointments/day?date=YYYY-MM-DD` |
| Arama / sonraki / silinen | `GET /v1/appointments/search`, `/next`, `/deleted` |
| Randevu detayı | `GET /v1/appointments/{id}` |
| Oluştur / güncelle / sil / geri al | `POST /v1/appointments`, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/restore` |
| Sonuç | `GET/PUT /v1/appointments/{id}/outcome` |
| Seri | `POST /v1/series/preview`, `POST /v1/series`, `POST /v1/series/{id}/cancel` |
| Raporlar | `GET /v1/reports/summary`, `GET /v1/reports/kind/{kind}` |
| Sağlık / politika | `GET /v1/health`, `GET /v1/version-policy` |
| Canlı olaylar | SignalR `/hubs/events` (`appointmentChanged`, `seriesChanged`, `reminderDue`, `contentChanged`) |

Kimlik: JWT Bearer; `AuthTokensResponse { accessToken, refreshToken, expiresInSeconds, user }`.
SignalR bağlantısı `accessTokenFactory` + `withCredentials: false` ile kurulur (bkz. web/mobil).

## 3. Masaüstünde dokunulacak yerler

| Bileşen | Görev |
|---|---|
| `Infrastructure/Persistence/AppDbContext.cs` | Önbellek/outbox tabloları ve migration |
| `Infrastructure/Persistence/AppPaths.cs` | Veritabanı yolu; sunucu ayarları dosyası |
| `Desktop/Services/SettingsRepository.cs` | Sunucu adresi, oturum, sync durumu |
| `Desktop/ViewModels/ShellViewModel.cs` | Sayfa yönlendirme ve sync durum göstergesi |
| `Desktop/ViewModels/*` | Repository yerine sync facade (API + önbellek) |
| `Desktop/App.xaml.cs` | DI kayıtları, startup pull, SignalR başlatma |
| `Desktop/Services/ReminderDispatcher.cs` | Sunucu bağlıyken **devre dışı** (çift hatırlatma olmasın) |

Yeni servisler (öneri):

```
Services/Api/CgpApiClient.cs        HttpClient + JWT + refresh
Services/Api/ApiModels.cs           API DTO eşlemeleri
Services/Sync/SyncService.cs        pull/push orkestrasyonu
Services/Sync/OutboxService.cs      kuyruk ekle/gönder/retry
Services/Sync/RealtimeClient.cs     SignalR bağlantısı
Services/Sync/SyncState.cs          son sync, çevrimiçi, bekleyen sayısı
```

## 4. Veri modeli eklemeleri

Mevcut yerel tablolar korunur; üzerine sync alanları eklenir:

- `SyncOutbox`: `Id`, `Operation` (`appointment.create/update/delete/outcome/series.cancel`),
  `EntityId`, `Payload`, `CreatedAt`, `Attempts`, `LastError`, `State`.
- `SyncMeta`: `Key`, `Value` (`last_pull_at`, `server_url`, `user_id`, `schema_version`).
- Randevu tablosuna: `ServerId` (API kimliği), `UpdatedAtUtc`, `SyncState`
  (`synced/pending/conflict`). Yerel kimlik ile sunucu kimliği eşlenir.
- İçerik (kategori/görüşmeci/kurum) sunucudan gelir; yerel düzenleme yalnızca çevrimdışı
  önbellek olarak tutulur ve yetkili kullanıcı için API’ye gönderilir.

Migration: `dotnet ef migrations add DesktopSync` (Infrastructure projesi).
Mevcut veriler ilk açılışta `ServerId = null` ile “pending” işaretlenir; kullanıcıya
“yerel verileri sunucuya aktar” seçeneği sunulur (varsayılan: aktar).

## 5. İş paketleri (sırayla)

### P1 — API istemcisi ve oturum — **tamamlandı (19.11.2026)**
- [x] `CgpApiClient`: `HttpClient`, JSON, hata modeli (`ErrorResponse`), timeout.
- [x] Login/refresh; token’ları DPAPI ile sakla (`DpapiSecretProtector` deseni).
- [x] `GET /v1/auth/me` + rol; 401’de tek seferlik refresh, olmazsa oturum kapat.
- [x] Ayarlar: sunucu adresi (varsayılan `http://127.0.0.1:5199`), “Bağlantıyı test et”.
- [x] Birim testleri: token yenileme, 401 tekrarı, hatalı URL.

Uygulama: `Desktop/Services/Api/` (`CgpApiClient`, `ApiSessionStore`, `ApiConnectionService`,
`ApiModels`), `ServerConnectionWindow` + `ServerConnectionViewModel`, Ayarlar → “Sunucu
bağlantısını yönet”. Token’lar `sync.api.*` anahtarlarıyla DPAPI şifreli saklanır. 13 yeni test.

### P2 — Okuma yolu — **kod tamamlandı (20.11.2026), manuel UAT sıradaki**
- [x] Manifest çekimi + ETag önbelleği; kategori/görüşmeci/kurum adları API'den.
- [x] `GET /appointments/day` ile günlük liste; mevcut `AppointmentListPage`'e bağla.
- [x] Detay, arama, silinenler, rapor uçları.
- [x] Yerel SQLite'ı salt okunur önbellek olarak doldur (çevrimdışı görünüm).
- [ ] UAT: web'de oluşturulan randevu masaüstünde "Yenile" ile görünür.

Uygulama: `Desktop/Services/Sync/` (`ServerReadService`, `SyncCacheStore`, `Synced*Service`).
Okumalar `sync.read.enabled` ayarına bağlıdır ve **varsayılan kapalıdır**; Ayarlar →
"Sunucu bağlantısını yönet" → "Okuma modu (P2)" ile açılır. Açıkken günlük liste,
detay, arama, silinenler, raporlar ve manifest API'den okunur; ağ hatasında son
başarılı yanıt `SyncCacheEntries` tablosundan gösterilir (30 sn boyunca sunucu
yeniden denenmez). Manifest `If-None-Match`/304 desteklidir. Yazma işlemleri (randevu,
kategori, görüşmeci düzenleme dahil) P3'e kadar yerelde kalır; bu nedenle okuma modu
açıkken masaüstünde oluşturulan kayıtlar sunucuya gönderilmez ve sunucu listesinde görünmez.
Yerel API'ye karşı canlı doğrulama yapıldı: giriş, manifest (ETag/304), günlük liste,
arama ve detay yanıtları istemciyle okundu; manuel arayüz UAT'i sıradaki adımdır.

### P3 — Yazma yolu + outbox
- [ ] Oluştur/güncelle/sil/sonuç/seri işlemlerini `OutboxService` üzerinden yürüt.
- [ ] Çevrimiçiyse hemen gönder; değilse kuyruğa al ve UI’da “bekliyor” göster.
- [ ] Idempotency: `ClientRequestId` (GUID) API’ye eklenir; aynı istek iki kez işlenmez.
- [ ] Sıralama: aynı randevunun create → update → outcome zinciri korunur.
- [ ] UAT: uçak modunda oluştur, bağlantı gelince sunucuya gider.

### P4 — SignalR
- [ ] `Microsoft.AspNetCore.SignalR.Client` ekle; süresiz geri çekilmeli yeniden bağlanma
      (mobil `realtime.ts` deseni).
- [ ] `appointmentChanged` → ilgili günü yenile; `contentChanged` → manifest yenile;
      `seriesChanged` → seri listesini yenile.
- [ ] UAT: web’de değişiklik masaüstünde elle yenilemeden görünür.

### P5 — Çakışma yönetimi
- [ ] API’ye `expectedUpdatedAt` (veya `rowVersion`) ekle; uyuşmazlıkta `409 Conflict` +
      güncel DTO döndür.
- [ ] Masaüstünde dialog: “Sunucudaki sürümü koru” / “Yerel değişikliği uygula”.
- [ ] Outbox’ta çakışan kayıt `conflict` durumuna geçer; kullanıcı çözene kadar tekrar denenmez.
- [ ] UAT: iki istemcide aynı randevu düzenlenir; ikinci kayıt çakışma dialog’u görür.

### P6 — Açılış/kapanış ve dayanıklılık
- [ ] Açılışta: oturum → manifest → outbox flush → günlük liste.
- [ ] Arka planda 30–60 sn’lik pull (yalnızca çevrimiçiyken) + SignalR.
- [ ] Ağ durumu: `NetworkAvailabilityChanged` veya periyodik sağlık kontrolü.
- [ ] Kapanışta outbox dosyaya yazılır; uygulama çökerse veri kaybı olmaz.
- [ ] Sunucu bağlıyken yerel `ReminderDispatcher` kapatılır.

### P7 — UAT ve dokümantasyon
- [ ] Aynı randevu: masaüstü oluştur → web/mobil anında görür; tersi de.
- [ ] Çevrimdışı oluştur/düzenle → bağlantı gelince eşitlenir.
- [ ] Çakışma dialog’u, rapor eşitliği (7 tür), içerik manifesti.
- [ ] `docs/43_Masaustu_Senkronizasyon_UAT.md` + `CURRENT` güncelle.
- [ ] 412+ test; CI yeşil; PR akışı.

## 6. Bilinen riskler ve kararlar

- **Çift hatırlatma:** Masaüstü yerel hatırlatıcısı ile API worker’ı aynı randevu için iki
  bildirim üretebilir. Sunucuya bağlıyken masaüstü hatırlatıcısı kapatılmalı.
- **Tek kullanıcı varsayımı:** Masaüstü oturum kilidi (`SessionLockService`) yerel kalır;
  sunucu tarafında kullanıcı/rol yetkisi API’den gelir.
- **Yerel veri:** İlk geçişte mevcut randevular sunucuya aktarılmalı; kopya oluşmaması için
  `ClientRequestId` ve telefon+tarih eşleştirmesi kullanılmalı. Aktarım öncesi yedek zorunlu.
- **Raporlar:** Masaüstü raporları API’den alınacak; yerel SQL sorguları yalnızca çevrimdışı
  önbellek için kalacak. Rapor türleri `design/tokens.json` ile aynı olmalı.
- **Güvenlik:** Token’lar DPAPI; HTTPS önerilir; sunucu adresi kullanıcı tarafından değiştirilebilir
  ama yalnızca Admin.
- **Sürüm:** Masaüstü `AppConstants.Version` ve API `version-policy` uyumlu kalmalı.

## 7. Diğer bilgisayarda başlangıç

```powershell
git pull --ff-only
node scripts/bootstrap.mjs --check
node coordination/lock.mjs status
# boşsa:
node coordination/lock.mjs acquire --machine cfr-lenovo --task "desktop sync" --branch agent/cfr-lenovo/desktop-sync
git switch -c agent/cfr-lenovo/desktop-sync
```

Gerekli paketler (masaüstü projesine eklenecek):

```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="8.*" />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.*" />
```

Test ortamı: API `http://127.0.0.1:5199`, `CGP_API_DATA` ayrı bir UAT klasörü; web `npm run dev`;
masaüstü Debug. İlk kurulumda `POST /v1/auth/setup` ile admin oluşturulur.

## 8. Kabul kriterleri

1. Masaüstünde oluşturulan randevu web ve mobilde **elle yenileme olmadan** görünür.
2. Web/mobilde yapılan değişiklik masaüstünde görünür.
3. Uçak modunda yapılan işlemler bağlantı gelince kaybolmadan eşitlenir.
4. Aynı kayıt iki istemcide düzenlenirse çakışma dialog’u çıkar; seçim kaydedilir.
5. Üç istemcide 7 rapor türü ve etiketleri aynıdır.
6. Masaüstü çevrimdışıyken günlük liste ve detay açılır; “çevrimdışı” göstergesi görünür.
7. Mevcut yerel veriler ilk geçişte kaybolmadan sunucuya aktarılır; yedek alınmıştır.
