# 47 — Görüşme Odaları, Ünvanlar ve Çoklu Misafir

**Tarih:** 22.11.2026 · **Durum:** Tamamlandı · **Sürüm:** 1.11.0 (API yaması: 1.11.1)

Bu belge, görüşme odaları, yönetilen ünvanlar, çoklu görüşmeci ve misafir
başına bağımsız değerlendirme özelliklerinin veri modelini, iş kurallarını,
API yüzeyini ve UAT sonuçlarını açıklar.

## 1. Veri modeli

| Tablo | Amaç |
|---|---|
| `HostTitles` | Yönetilen ünvan listesi; admin tanımlar |
| `MeetingRooms` | Host odası + ortak odalar; `Kind`, `HostUserId`, `IsActive` |
| `AppointmentHosts` | Randevuya katılan görüşmeciler; birincil işaretli |
| `AppointmentGuests` | Misafirler; her misafirin katılım/sonuç/not/kapanışı |

Mevcut alanlar korunur:

- `Appointment.HostUserId` → **birincil görüşmeci**
- `Appointment.PersonId` → **birincil misafir**
- `Appointment.RoomId` → fiziksel oda (boşsa birincil hostun odası)
- `AppointmentOutcome` → randevu düzeyindeki çatı özet

### Backfill

`DatabaseInitializer` idempotent olarak:

1. Üç ortak oda yoksa oluşturur (`Oda 1/2/3`).
2. Her aktif host ve randevuda kullanılan host için oda açar.
3. Eski randevulara birincil host, birincil misafir ve oda atar.
4. Varsa eski `AppointmentOutcome` verisini birincil misafire kopyalar.

## 2. İş kuralları

### Oda

- `FaceToFace` randevuda oda zorunludur.
- Telefon/online/WhatsApp’ta oda opsiyoneldir.
- Aynı odada aynı saatte ikinci randevu `ConflictKind.Room` üretir.
- Host odası pasifleştirilirse gelecek randevu varsa engellenir.

### Görüşmeci

- Birincil host + en fazla 10 katılımcı.
- Katılımcı host randevuyu görür ve sonuç girebilir.
- Aynı saatte birincil veya katılımcı host doluysa `ConflictKind.Host`.

### Misafir

- Bir randevuda en fazla 10 misafir; tam olarak bir birincil misafir.
- Aynı kişi aynı randevuya iki kez eklenemez.
- Aynı saatte aynı misafir başka randevuda varsa `ConflictKind.Guest`.
- Her misafirin `Attendance`, `Summary`, `PostNotes`, `WillRepeat`, `IsClosed`
  alanları bağımsızdır.

### Randevu durumu

Misafir sonuçlarından otomatik türetilir:

- Hepsi `NoShow` → `NoShow`
- Hepsi `Cancelled` → `Cancelled`
- Hepsi `Postponed` → `Postponed`
- En az bir `Arrived` → `Arrived` / kapanışta `Completed`
- Kapanışta tüm misafirlerin katılımı dolu olmalıdır.

Eski tek-sonuç akışı birincil misafire uygulanır; geriye uyum korunur.

## 3. API

### Okuma

```text
GET /v1/titles?activeOnly=true|false
GET /v1/rooms?activeOnly=true|false&hostUserId=...
```

### Admin

```text
POST/PUT/DELETE /v1/admin/content/titles
POST /v1/admin/content/titles/{id}/active
POST/PUT/DELETE /v1/admin/content/rooms
POST /v1/admin/content/rooms/{id}/active
```

### Randevu

`AppointmentInput` ek alanları:

```json
{
  "roomId": "...",
  "participantHostUserIds": ["..."],
  "guests": [
    { "firstName": "Ali", "lastName": "Veli", "phone": "0532...", "isPrimary": true },
    { "firstName": "Ayşe", "lastName": "Veli", "phone": "0532...", "isPrimary": false }
  ]
}
```

`AppointmentDetailDto` ve liste DTO’ları oda, katılımcılar, misafirler ve
misafir sonuçlarını taşır.

### Sonuç

```json
{
  "attendance": "Arrived",
  "summary": "Genel değerlendirme",
  "willRepeat": false,
  "postNotes": null,
  "isClosed": true,
  "guestOutcomes": [
    { "guestId": "...", "attendance": "Arrived", "summary": "...", "willRepeat": false, "postNotes": null, "isClosed": true },
    { "guestId": "...", "attendance": "NoShow", "summary": "...", "willRepeat": false, "postNotes": null, "isClosed": true }
  ]
}
```

`guestOutcomes` gönderilmezse eski davranış: birincil misafir + randevu.

## 4. İstemciler

| İstemci | Kapsam |
|---|---|
| Masaüstü | Oda seçimi, katılımcı listesi, ek misafirler, detay, misafir başına sonuç, liste/detay oda sütunu, host ünvanı |
| Web | Aynı akışlar + oda filtresi + rapor oda tablosu + ünvan/oda içerik yönetimi |
| Mobil | Oda/katılımcı/ek misafir, çevrimdışı kuyruk, detayda misafir başına sonuç |

## 5. UAT sonuçları

**Ortam:** izole API veritabanı, `scripts/seed-test-data.mjs` + `scripts/seed-meeting-rooms.mjs`.

| Kontrol | Sonuç |
|---|---|
| Görünür randevu | **1.082** |
| Ünvan | 6; 8 hosta atandı |
| Oda | 11 (8 host + 3 ortak) |
| Çoklu misafirli randevu | Var |
| Çoklu görüşmecili randevu | Var |
| Rapor toplamı = arama toplamı | 1.082 / 1.082 |
| Oda çakışması | `Room` türüyle döndü |
| Günlük liste | ~23 ms |
| Arama | ~140 ms |
| Rapor | ~147 ms |
| .NET testleri | **438/438** |
| Web Playwright | Geçti (oluşturma + detay + rapor + içerik) |
| Mobil `tsc` | Geçti |

### Masaüstü 1.11.0 saha UAT’i

Kurulu sürüm: **1.11.0** (`%LOCALAPPDATA%\Programs\CGP Randevu Takip`).

- Sunucu bağlantısı `http://127.0.0.1:5199` ile kuruldu; okuma modu açık.
- Günlük liste: **112 görüşme**, “**Sunucu: bağlı**” rozeti.
- Liste satırlarında **oda** ve **+N görüşmeci / +N misafir** göründü.
- Çoklu misafirli detay: `Dilek Aydın - Geldi`, `Ali Çelik - Gelmedi`; oda ve
  katılımcı ünvanı göründü.
- Düzenleyici: **Oda**, **Katılımcılar**, **Ek misafirler**, **+ Misafir ekle** alanları.
- Yeni randevu: 2 misafir + 2 görüşmeci + oda ile oluşturuldu; API’de
  `guestCount=2`, `participantCount=2`, `roomName=Demet Yalçın Odası` doğrulandı.

### Mobil Maestro / Samsung S21 UAT’i

Cihaz: **R5CN3C6SN** (Samsung S21), Expo dev build.

| Akış | Sonuç |
|---|---|
| `login.yaml` | Geçti |
| `create-appointment.yaml` | Geçti (ek misafir + oda + kaydet) |
| `offline-create.yaml` | Geçti (uçak modu → kuyruk → bağlantı → rapor) |
| `reports.yaml` | Geçti |

- Çevrimdışı oluşturulan kayıt, uygulama yeniden açıldığında otomatik gönderildi
  (`05558880033` → API’de görünür).
- Mobil raporda **301 görüşme / 435 kişi** özeti görüntülendi.
- Üretim APK’sı **1.1.0** olarak EAS `preview` ile alındı ve Samsung S21’e kuruldu;
  cleartext HTTP, `GOOGLE_SERVICES_JSON` secret file ve ağ hatasında outbox desteği
  eklendi ([docs/48](48_Mobil_1.1.0_Build.md)).

### Sınırlar

- iOS UAT macOS/Xcode gerektirir; Android emülatör + Samsung S21 doğrulandı.
- Masaüstü WPF UI otomasyonu kalıcı testlere eklenmedi; bu turda UI Automation ile
  manuel saha UAT’i yapıldı.
- Maestro CLI Windows’ta `maestro.zip` + JDK 17 ile çalıştırıldı; CI’a eklenmedi
  (gerçek cihaz/emülatör gerektirir).

## 6. Yeniden üretim

```powershell
# API (izole veri)
$env:CGP_API_DATA="$PWD\..\cgp-uat-rooms"
dotnet run --project src\CgpRandevuTakip.Api --urls http://127.0.0.1:5199

# 1.000+ temel kayıt
node scripts/seed-test-data.mjs --count 1000 --force

# Oda/ünvan/çoklu misafir zenginleştirme
node scripts/seed-meeting-rooms.mjs --force

# Doğrulama
node scripts/verify-uat.mjs --samples 25
```
