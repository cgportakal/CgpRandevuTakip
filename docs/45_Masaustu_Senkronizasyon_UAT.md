# 45 — Masaüstü Senkronizasyon Saha UAT (P3/P4/P5)

**Tarih:** 22.11.2026 · **Sürüm:** 1.10.1 · **Kapsam:** çevrimdışı kuyruk (P3), SignalR (P4),
çakışma yönetimi (P5)

## 1. Ortam

| Bileşen | Değer |
|---|---|
| API | `http://127.0.0.1:5199`, izole veri `%TEMP%\opencode\cgp-uat-sync` |
| Web | API üzerinden simüle edildi (ikinci istemci) |
| Masaüstü | Kurulu **1.10.1**, `--portable` test kopyası |
| Masaüstü modu | Sunucu oturumu açık, **okuma modu açık** |
| Veri | `UAT Test` 11:00, `Cakisma Test` 15:00, sonradan `Cevrimdisi Kuyruk` |
| Yöntem | UI Automation + ekran görüntüsü + API doğrulama |

Gerçek kullanıcı verisine dokunulmadı; test verisi ve portatif kopya kullanıldı.

## 2. P3 — Çevrimdışı kuyruk ve overlay

| # | Senaryo | Sonuç |
|---|---|---|
| P3-1 | API kapalıyken yeni görüşme | ✅ “Kuyruğa alındı”; kayıt listede geçici olarak göründü |
| P3-2 | API açılınca otomatik gönderim | ✅ Kuyruk boşaldı, kayıt sunucuya gitti (`Cevrimdisi Kuyruk`, 06:00) |
| P3-3 | API kapalıyken silme | ✅ Silme kuyruğa alındı; API açılınca sunucudan kaldırıldı |
| P3-4 | Kuyruk kalıcılığı / diğer işlemler | ✅ Birim testleri (outbox create/update/delete/outcome/series/bulk) geçiyor |
| P3-5 | Hata/retry | ✅ Birim testleri; başarısız kayıt “Yeniden dene” ile tekrar denenebilir |

Gözlem: Çevrimdışıyken “Yeni görüşme” penceresi açılırken birkaç saniye gecikme olabilir;
pencere modal olduğu için otomasyon ilk taramada görmedi, ürün hatası değil.

## 3. P4 — SignalR canlı yenileme

| # | Senaryo | Sonuç |
|---|---|---|
| P4-1 | API’den randevu ekleme | ✅ Masaüstünde ~3 sn içinde, elle yenilemeden göründü |
| P4-2 | API’den saat güncelleme | ✅ 16:00 → 16:45 otomatik yansıdı |
| P4-3 | API’den silme | ✅ Satır otomatik kalktı, sayaç 2’ye döndü |
| P4-4 | 45 sn API kesintisi + yeniden bağlanma | ✅ Bağlantı kuruldu; yeni kayıt (`Reconnect P4`) otomatik geldi |
| P4-5 | İçerik/seri olayları | ⚠️ Bu turda saha testi yapılmadı; birim testleri mevcut |

## 4. P5 — Çakışma yönetimi

| # | Senaryo | Sonuç |
|---|---|---|
| P5-1 | Masaüstü açıkken web (API) güncellemesi; masaüstünde kaydet | ✅ “Sunucu çakışması” dialog’u açıldı |
| P5-2 | “Sunucudaki sürümü koru” | ✅ Sunucu değeri korundu (`Web degisiklik`), yerel değişiklik yazılmadı |
| P5-3 | “Yerel değişikliği uygula” | ✅ Sunucu değeri yerel değerle güncellendi (`Yerel kazanmali`) |
| P5-4 | Çevrimdışı güncelleme + web değişikliği | ✅ Kuyruk `conflict` durumuna geçti; “0 bekleyen · 1 çakışan kayıt” |
| P5-5 | Kuyruk çakışmasını çöz (sunucuyu koru) | ✅ Kuyruk temizlendi, sunucu değeri korundu (`Web son`) |
| P5-6 | Kuyruk çakışmasında yerele uygula | ✅ Birim testi (`ResolveConflict_UseLocal_RequeuesWithServerTimestamp`) |
| P5-7 | Tek istemci normal kayıt | ✅ Çakışma çıkmadı |

## 5. Bulgular ve düzeltmeler

- **Manifest eşzamanlı istekleri (sağlamlaştırma):** Kategori ve görüşmeci servisleri manifesti
  aynı anda istiyor; aynı önbellek anahtarına iki yazma SQLite kilidi/yarışına yol açabilir.
  `ServerReadService.GetManifestAsync` tek sıraya alındı (`SemaphoreSlim`). Kullanıcıya yansıyan
  bir hata gözlenmedi; önleyici düzeltme.
- **Kritik hata bulunmadı:** P3/P4/P5 akışları beklendiği gibi çalıştı.

## 6. Sınırlar

- Web arayüzü yerine API ikinci istemci olarak kullanıldı; web UI ayrı P7 UAT’inde doğrulanmalı.
- İçerik yayınlama (`contentChanged`) ve seri olayları bu turda saha testi edilmedi.
- Hızlı ardışık 10 değişiklik / UI debounce yük testi yapılmadı.
- Fiziksel çift monitör/Dpi senaryosu kapsam dışı.

## 7. Kanıtlar

Ekran görüntüleri: `%TEMP%\opencode\cgp-shots\uat\`

- `01-today-server.png`, `02-p4-created.png`, `03-p4-updated.png`, `04-p4-deleted.png`
- `09-p3-queued.png`, `10-p3-synced.png`, `11-p3-offline-delete.png`, `12-p3-delete-synced.png`
- `16-p4-reconnect.png`
- `17-p5-outbox-conflict.png`, `18-p5-resolved.png`

## 8. Sıradaki

1. **P6 — açılış/kapanış ve dayanıklılık:** pull, ağ durumu, yerel hatırlatıcı kapatma.
2. **P7 — uçtan uca UAT:** web UI + mobil + masaüstü aynı randevu; içerik/seri olayları.
