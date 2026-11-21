# 44 — Masaüstü Senkronizasyonu Devir Notu

**Tarih:** 21.11.2026 · **Sürüm:** 1.10.0 · **Durum:** P1–P4 kod tamamlandı; P3/P4 UAT ve P5 sıradaki

Bu belge, masaüstü ↔ API senkronizasyonunu devralacak oturum için mimariyi, mevcut
durumu ve yapılacakları özetler. Ayrıntılı plan: [docs/42](42_Masaustu_Veri_Senkronizasyonu.md).

## 1. Mevcut durum

| Aşama | Durum |
|---|---|
| P1 — API istemcisi + DPAPI oturum | ✅ |
| P2 — Okuma yolu (manifest/ETag, liste, detay, arama, silinenler, raporlar, SQLite önbellek) | ✅ |
| P3 — Yazma yolu + outbox + çevrimdışı görünürlük | ✅ (saha UAT bekliyor) |
| P4 — SignalR canlı yenileme | ✅ (saha UAT bekliyor) |
| P5 — Çakışma yönetimi | ⏳ |
| P6 — Açılış/kapanış ve dayanıklılık | ⏳ |
| P7 — Uçtan uca UAT + dokümantasyon | ⏳ |

**Test:** 429/429 · **Master:** güncel hash `git log -1` · **Kilit:** yok.

## 2. Mimari

| Bileşen | Görev |
|---|---|
| `Desktop/Services/Api/CgpApiClient.cs` | Tüm HTTP okuma/yazma uçları, JWT, hata modeli |
| `Desktop/Services/Api/ApiConnectionService.cs` | Oturum, token yenileme, 401 tekrarı, çıkış |
| `Desktop/Services/Sync/ServerReadService.cs` | API okuma + `SyncCacheEntries` önbelleği + 30 sn çevrimdışı penceresi |
| `Desktop/Services/Sync/OutboxService.cs` | `SyncOutboxItems` kuyruğu; sıralı gönderim, kimlik eşleme, yeniden deneme |
| `Desktop/Services/Sync/OutboxFlushService.cs` | Açılışta ve 30 sn’de bir kuyruğu gönderir |
| `Desktop/Services/Sync/PendingAppointmentOverlay.cs` | Bekleyen tekil/seri işlemleri listeye ve detaya uygular |
| `Desktop/Services/Sync/Synced*Service.cs` | Okuma/yazma fakadeleri: okuma modu açıksa API, değilse yerel |
| `Desktop/Services/Sync/DesktopRealtimeClient.cs` | SignalR `/hubs/events`, süresiz yeniden bağlanma |
| `Desktop/Services/Sync/SyncCacheStore.cs` | `SyncCacheEntries` SQLite önbelleği |

### Veritabanı (masaüstü)

- `SyncCacheEntries`: sunucu okuma yanıtları (JSON + ETag).
- `SyncOutboxItems`: bekleyen/başarısız yazma istekleri (`Operation`, `EntityId`, `Payload`,
  `Attempts`, `LastError`, `State`).
- `Appointments.ClientRequestId`: idempotency (API ve masaüstü, tekil indeks).

Migration’lar uygulama açılışında otomatik uygulanır (`DesktopSyncCache`, `SyncOutbox`,
`AppointmentClientRequest`).

## 3. Davranış

- **Okuma modu açık + oturum var:** okumalar API’den; yazmalar önce API’ye.
- **Ağ hatası:** yazma outbox’a; kullanıcıya “kuyruğa alındı”; 30 sn’de bir gönderim.
- **Bekleyen kayıt:** listede/detayda görünür; create başarılı olunca geçici kimlik sunucu
  kimliğine çevrilir (update/delete/outcome/bulk-move payload’ları dahil).
- **SignalR:** `appointmentChanged` / `seriesChanged` / `contentChanged` → 400 ms debounce ile
  liste yenileme; içerik olayında filtreler tazelenir.
- **Okuma modu kapalı:** eski yerel davranış aynen korunur.
- **Başarısız kuyruk:** “Sunucu bağlantısı” penceresinde sayı + “Yeniden dene”.

## 4. Diğer bilgisayarda başlangıç

```powershell
git pull --ff-only
node scripts/bootstrap.mjs --check
node coordination/lock.mjs status
```

Uygulama için `v1.10.0` Setup/portable; kaynaktan çalıştırma:

```powershell
$env:CGP_API_DATA="$PWD\src\CgpRandevuTakip.Api\data"
dotnet run --project src\CgpRandevuTakip.Api --urls http://0.0.0.0:5199

cd web; $env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5199'; npm run dev
cd mobile; $env:EXPO_PUBLIC_API_URL='http://<LAN-IP>:5199'; npx expo start
```

İlk API açılışında `POST /v1/auth/setup`. Masaüstünde Ayarlar → Sunucu bağlantısı → giriş →
**Okuma modu**.

## 5. UAT (P3/P4)

1. API kapalıyken randevu oluştur → listede hemen görünür, sayaç artar.
2. Düzenle, sonuç gir, sil → liste/detay anında yansır.
3. Seri oluştur (birden çok satır görünür), ekle, düzenle, iptal et.
4. Toplu seç → taşı / sil.
5. API’yi aç → 30 sn içinde kuyruk boşalır; web’de **tek** kayıt olur.
6. Web’de değişiklik yap → masaüstü elle yenilemeden güncellenir (SignalR).
7. API’yi 30 sn kapat/aç → SignalR kendiliğinden geri bağlanır.
8. Sunucu hesabından çıkış/giriş → bağlantı durur/başlar.

## 6. Bilinen sınırlar / riskler

- Sunucuda var olan bir serinin **çevrimdışı** iptal/düzenlemesi, bağlantı gelene kadar
  listede görsel olarak uygulanmaz (liste DTO’sunda seri kimliği yok). Bekleyen serilerde
  tüm işlemler görünür.
- P5’e kadar çakışma algılama yok: aynı kayıt iki istemcide düzenlenirse son yazan kazanır.
- Tam yedekler kullanıcı parola özetlerini içerir; DPAPI sırları makineye bağlıdır.
- **Sunucu veritabanı yedeği yok** (merkezi `CGP_API_DATA/cgp.db` için ayrı çözüm gerekli).
- Okuma modu üretimde açılmadan önce P5/P6 ve saha UAT tamamlanmalı.

## 7. Sıradaki işler

1. **P3/P4 UAT** ve bulguların düzeltilmesi.
2. **P5 çakışma:** API’ye `expectedUpdatedAt`/`rowVersion`; `409 Conflict` + güncel DTO;
   masaüstünde “Sunucudaki sürümü koru / Yerel değişikliği uygula”; outbox `conflict` durumu.
3. **P6 dayanıklılık:** açılış sırası, periyodik pull, ağ algılama, kapanışta kuyruk.
4. **P7 UAT:** üç istemci arasında uçtan uca senaryolar.
5. **Sunucu yedeği** ve mağaza/iOS işleri.
