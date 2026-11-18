# 41 — Tasarım ve Veri Paritesi

**Tarih:** 18.11.2026 · **Kapsam:** web, mobil ve masaüstü arasında ortak tasarım dili ve veri mimarisi

## 1. Amaç

Üç istemcinin aynı kurumsal kimliği, aynı metinleri ve aynı rapor dilini kullanması; verinin
tek kaynaktan yönetilmesi. Tasarım platforma özgü kalır (masaüstünde yoğun masaüstü düzeni,
mobilde dokunma hedefleri), ancak renk, tipografi, terimler ve rapor türleri ortaktır.

## 2. Tasarım tokenları

Tek kaynak: [`design/tokens.json`](../design/tokens.json).

| Token | Açık tema | Koyu tema (masaüstü) |
|---|---|---|
| Marka / accent | `#2E7D5B` | `#74C69D` |
| Arka plan | `#F4F7F5` | `#0E1512` |
| Yüzey | `#FFFFFF` | `#141D18` |
| Metin | `#12211A` | `#EEF6F1` |
| Soluk metin | `#5D7166` | `#96AB9E` |
| Kenarlık | `#DDE5E0` | `#27382E` |
| Tehlike | `#B4433B` | `#E19089` |
| Uyarı | `#9A6B1C` | `#DDBB77` |
| Bilgi | `#2F6F8F` | — |
| Boşluk | 4 / 8 / 16 / 24 / 32 | aynı |
| Köşe yarıçapı | 8 / 12 / 16 / pill | aynı |

Eşlemeler:

- **Web:** `web/src/app/globals.css` → CSS değişkenleri (`--primary`, `--surface`, ...).
- **Mobil:** `mobile/src/theme.ts` → `colors`, `spacing`, `radius`.
- **Masaüstü:** `Themes/Light.xaml` ve `Themes/Dark.xaml` → `SolidColorBrush` anahtarları.

Koyu tema yalnızca masaüstünde platform tercihi olarak sunulur; web/mobil açık temadır.

## 3. Ortak terimler

| Kavram | Kullanım |
|---|---|
| Günlük Liste | Günün randevuları (masaüstü, web) |
| Raporlar | Rapor merkezi |
| Görüşme | Randevu yerine kullanılan kurumsal terim |
| Görüşmeci | Randevuyu yapan personel |
| Kategori | Görüşme türü |
| Katılım | Geldi / tamamlandı / gelmedi / iptal |
| Kişi geçmişi | Aynı kişinin geçmiş görüşmeleri |
| Denetim | Audit / aktivite kayıtları |

Rapor türleri ve etiketleri (`design/tokens.json` → `reports`):

| Tür | Etiket |
|---|---|
| `Summary` | Özet |
| `HostPerformance` | Görüşmeci |
| `CategoryTrend` | Kategori |
| `Attendance` | Katılım |
| `Repeat` | Tekrar |
| `PersonHistory` | Kişi geçmişi |
| `AuditActivity` | Denetim |

## 4. Veri mimarisi kararı

**API tek doğruluk kaynağıdır.** Web ve mobil bunu zaten kullanır. Masaüstü için seçilen model:

- Masaüstü, API’den okur/yazar (JWT oturumu).
- Bağlantı yoksa yerel SQLite önbelleği + outbox kuyruğu kullanır.
- Bağlantı gelince kuyruk gönderilir, sunucudaki değişiklikler SignalR (`/hubs/events`) ile alınır.
- Çakışma: sunucu son sürümdür; masaüstü gönderimi `expectedUpdatedAt` ile yapılır, çakışmada
  kullanıcıya “sunucudaki sürümü koru / yereli uygula” seçeneği sunulur.

### Aşamalar

Ayrıntılı iş paketleri, API sözleşmesi ve kabul kriterleri:
[docs/42_Masaustu_Veri_Senkronizasyonu.md](42_Masaustu_Veri_Senkronizasyonu.md).

1. **Tasarım/metin/rapor paritesi** — tamamlandı (bu belge).
2. **Masaüstü API istemcisi** — oturum, manifest, günlük liste, randevu CRUD, raporlar.
3. **Çevrimdışı önbellek + outbox** — mobildeki desenin masaüstüne uyarlanması.
4. **SignalR + çakışma yönetimi** — canlı yenileme ve sürüm kontrolü.
5. **UAT** — aynı randevunun üç istemcide anlık görünmesi, çevrimdışı açma/kapama, rapor eşitliği.

## 5. Platforma özgü tasarım ilkeleri

- **Masaüstü:** geniş tablolar, klavye kısayolları, toplu işlemler, yazdırma/dışa aktarma.
- **Web:** tarayıcı genişliğine uyum, tek sayfa navigasyon, hızlı rapor filtreleri.
- **Mobil:** tek elle kullanım, 44 px dokunma hedefleri, çevrimdışı öncelikli akış, alt eylem çubuğu.

Ortak olan: renk paleti, tipografi ölçeği, terimler, rapor türleri, durum etiketleri
(`Planlandı`, `Tamamlandı`, `Gelmedi`, `İptal`) ve hata/bilgi dili.
