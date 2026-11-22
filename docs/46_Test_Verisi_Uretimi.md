# 46 — UAT Test Verisi Üretimi (1000+ kayıt)

**Tarih:** 22.11.2026 · **Durum:** Tamamlandı · **Betik:** [`scripts/seed-test-data.mjs`](../scripts/seed-test-data.mjs)

Masaüstü, web ve mobil aynı veriyi görsün diye test kayıtları **API veritabanına** yazılır.
API tek doğruluk kaynağıdır; masaüstü okuma modunda API'den okur, web ve mobil zaten API'yi
kullanır.

## 1. Ön koşullar

```powershell
# API (ayrı terminal)
$env:CGP_API_DATA="$PWD\src\CgpRandevuTakip.Api\data"
dotnet run --project src\CgpRandevuTakip.Api --urls http://127.0.0.1:5199
```

İlk çalıştırmada veritabanı boşsa betik `POST /v1/auth/setup` ile yönetici oluşturur:

| Alan | Değer |
|---|---|
| Kullanıcı adı | `admin` |
| Şifre | `CgpDemo!2026` |
| Ad soyad | Demo Yönetici |

Mevcut bir yönetici varsa betik onunla giriş yapar; farklı hesap için
`--admin-user` / `--admin-pass` verin.

## 2. Üretim

```powershell
node scripts/seed-test-data.mjs --count 1000
```

İsteğe bağlı:

```powershell
node scripts/seed-test-data.mjs --count 1500 --base-url http://127.0.0.1:5199
node scripts/seed-test-data.mjs --admin-user yonetici --admin-pass "..."
node scripts/seed-test-data.mjs --no-series --force
```

Betik deterministik `clientRequestId` (`demo-2026-11-22-0001`, …) kullanır; yeniden
çalıştırıldığında aynı kayıtları ikinci kez açmaz. Durum dosyası
`scripts/.seed-demo-state.json` (git'e girmez).

## 3. Üretilenler

- **Geçmiş / bugün / gelecek:** yaklaşık −90…+90 gün; boş günler ve yoğun günler karışık.
  Bugün ~105, yarın ~65 kayıt (liste ve arama performansı için).
- **Kişiler:** 240 farklı kişi, gerçekçi ad/telefon; bazı kişiler birden fazla görüşmede.
- **Görüşmeciler:** 8 host (`Demet Yalçın`, `Kerem Öztürk`, `Nazlı Güneş`, `Serkan Aksoy`,
  `Pınar Doğan`, `Emre Kurt`, `Gizem Polat`, `Onur Tekin`).
- **Kategoriler:** mevcut 7 kategori (Veli, Öğrenci, Rehberlik, Bilgi Talebi, Şikâyet,
  Disiplin, Diğer).
- **Karışık içerik:** 15/30/45/60 dk süre, Telefon / Yüz yüze / WhatsApp / İnternet,
  öğrenci adı, açıklama, farklı hatırlatıcı aralıkları (yok, 30, 60, 1440, 1440+60+30).
- **Durumlar:** geçmişte tamamlandı / gelmedi / iptal / ertelendi / geldi-açık ve
  **geçmişte planlı** (düzenleme ve yeni tarihe taşıma testi).
- **Çakışmalar:** aynı host + aynı saatte bilinçli çakışan kayıtlar ve aynı kişi çakışması.
  Bunlar `ignoreConflicts` ile yazılır; normal kayıtlar çakışmayacak şekilde planlanır.
- **Seriler:** 8 seri (haftalık / iki haftada bir / aylık), 5–8 oturum.
- **Silinenler:** 18 kayıt yumuşak silinir (30 gün içinde geri alma testi).
- **Hatırlatıcılar:** bugün için 30/60 dk önce tetiklenecek 4 kayıt, yarın için 1440 dk
  (bir gün önce) hatırlatması olan kayıt. Sunucu `ReminderWorker` 15 sn'de bir çalışır;
  durumlar `ReminderDispatches` tablosunda izlenir.

## 4. İstemcilerde görünürlük

### Masaüstü

1. API çalışıyor olmalı.
2. Ayarlar → **Sunucu bağlantısını yönet** → sunucu adresi `http://127.0.0.1:5199`,
   yönetici hesabıyla giriş.
3. **Okuma modu (P2)** açık olmalı (`sync.read.enabled = 1`).
4. Günlük liste “Sunucu: bağlı” rozetini gösterir; tarih okları ile geçmiş/gelecek günlere
   gidilir. Kayıtlar API'den okunur, yazmalar API'ye gider (bağlantı yoksa outbox).

### Web

```powershell
cd web
$env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5199'
npm run dev
```

`http://localhost:3000` → yönetici hesabıyla giriş.

### Mobil

```powershell
adb reverse tcp:5199 tcp:5199
adb reverse tcp:8081 tcp:8081
cd mobile
$env:EXPO_PUBLIC_API_URL='http://127.0.0.1:5199'
npx expo start --dev-client --host localhost
```

Dev client bağlantısı: `cgp://expo-development-client/?url=http%3A%2F%2F127.0.0.1%3A8081`
(EAS dev build veya Expo Go).

## 5. Doğrulama örnekleri

```powershell
# Günlük liste sayısı (Bearer token ile)
GET /v1/appointments/day?date=2026-11-22

# 6 aylık arama (1073 kayıt, ~25 ms)
GET /v1/appointments/search?from=2026-08-01&to=2027-02-28

# Rapor özeti
GET /v1/reports/summary
```

22.11.2026 doğrulaması: **1073 görünür kayıt**, bugün **105**, yarın **68**, 8 seri,
17 silinmiş kayıt, 2092 hatırlatıcı kaydı. Masaüstü (1.10.2, okuma modu), web ve Android
cihaz aynı listeyi gösterdi.

## 6. Temizleme

Test verisi yalnızca yerel API veritabanındadır (`src/CgpRandevuTakip.Api/data/`, git'e
girmez). Sıfırlamak için API'yi durdurup `cgp.db`, `cgp.db-shm`, `cgp.db-wal` dosyalarını
silin; API yeniden açıldığında boş şema ve 7 varsayılan kategori oluşur. Ardından betiği
tekrar çalıştırın (`--force`).

> **Not:** `app.daily_share_time` (masaüstü günlük liste paylaşımı) ve sunucu hatırlatma
> saatleri bu veriden bağımsızdır; hatırlatıcı testi için kayıtların saatleri ve
> `ReminderDispatches.DueAtUtc` alanları kullanılır.
