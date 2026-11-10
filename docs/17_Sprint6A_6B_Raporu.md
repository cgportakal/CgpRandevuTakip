# 17 — Sprint 6A/6B Raporu: Yedekleme ve Geri Yükleme

**Sürüm:** 0.8.0 · **Tarih:** 10.11.2026
**Kapsam:** FR-BKP-01…09, FR-BKP-13 (yedek geçmişi) ve 6B geri yükleme.

---

## 1. Yedek formatı

Tek ZIP dosyası:

| Dosya | İçerik |
|-------|--------|
| `data.db` | SQLite çevrimiçi kopyası (tutarlı anlık görüntü) |
| `settings.json` | Gizli olmayan ayarlar (SMTP parolası ve push token/chat id hariç) |
| `manifest.json` | Ürün adı, sürüm, şema sürümü, oluşturma zamanı, kullanıcı/kişi/görüşme sayıları, DB ve ayar SHA-256 özetleri |

Dosya adı: `CGP_RandevuTakip_Yedek_yyyy-MM-dd_HH-mm-ss.zip`.

- Varsayılan konum: `Belgeler\CgpRandevuTakip\Yedekler` (Ayarlar'dan değiştirilebilir).
- Son **30** yedek saklanır; daha eskiler silinir.
- Yedek kaydı `BackupRecords` tablosuna yazılır (boyut, SHA-256, şema, doğrulama).
- Disk üzerinde kaydı olmayan dosyalar da geçmişte görünür ve hızlı doğrulamadan geçirilir.

## 2. Otomatik yedekleme

- Günde en fazla **2** otomatik yedek.
- 1. yedek: **12:30** (öğle).
- 2. yedek: günlük genel liste WhatsApp üzerinden **öğleden sonra paylaşıldığında hemen**;
  paylaşım yoksa **18:00**'de tamamlanır.
- Durum `%LOCALAPPDATA%\CgpRandevuTakip\auto-backup-state.json` dosyasında tutulur.
- Ayarlar > Yedekleme > "Otomatik yedekleme açık (günde 2 kez)" ile kapatılabilir.

## 3. Yetkiler

| İşlem | Admin | Sekreter | Görüşmeci |
|-------|:-----:|:--------:|:---------:|
| Manuel yedek | ✔ | ✔ | ✔ |
| Yedek geçmişi görüntüleme | ✔ | ✔ | ✔ |
| Geri yükleme | ✔ | — | — |

## 4. Geri yükleme

1. ZIP açılır; `data.db` ve `manifest.json` aranır.
2. Ürün adı ve şema sürümü kontrol edilir.
3. `data.db` SHA-256 özeti manifest ile karşılaştırılır.
4. `settings.json` özeti doğrulanır.
5. Veritabanı geçici dosyaya çıkarılır; `PRAGMA integrity_check` ve `Appointments` tablosu kontrol edilir.
6. Mevcut veri için otomatik **güvenlik yedeği** alınır.
7. SQLite bağlantı havuzu temizlenir, WAL/SHM dosyaları kaldırılır, yeni `data.db` yazılır.
8. Uygulama yeniden başlatılır.

Hata durumunda mevcut veri korunur ve kullanıcıya anlaşılır mesaj gösterilir.

## 5. Test kanıtı

- Birim/entegrasyon: 201/201 test.
  - ZIP içeriği ve manifest
  - Bozuk/oynanmış yedeğin reddi (SHA-256)
  - Geri yüklemenin veriyi birebir getirmesi
  - Yönetici olmayan kullanıcının geri yükleme reddi
  - Kayıt dışı yedek dosyasının geçmişte doğrulanması
  - Otomatik yedek zamanlaması (12:30, paylaşım tetiği, 18:00, günde 2 sınırı)
- UI:
  - Yedekleme sayfası, manuel yedek, geçmiş listesi
  - Geri yükleme onayı, güvenlik yedeği, yeniden başlatma
  - Otomatik yedeğin 2 kez alınması ve üçüncü yedeğin alınmaması
- Derleme: 0 uyarı / 0 hata.

## 6. Sıradaki işler

1. ~~Denetim kaydı görüntüleme ekranı (6C)~~ — 0.9.0'da tamamlandı; [docs/18](18_Sprint6C_Raporu.md).
2. Kurulum paketi (6D), UAT ve 1.0.0.
3. Yedek şifreleme / Google Drive (Faz 2, Q-14/Q-15).
