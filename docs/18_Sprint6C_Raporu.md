# 18 — Sprint 6C Raporu: Denetim Kaydı Ekranı

**Sürüm:** 0.9.0 · **Tarih:** 10.11.2026
**Kapsam:** FR-AUD-01…04.

---

## 1. Ekran

- Sol menüde **Denetim Kaydı** (yalnızca yönetici görür).
- Sayfa salt-okunur; kayıt ekleme, düzenleme veya silme yok.
- Üstte filtre paneli, ortada özet/sayfalama, altta kayıt listesi.

### Filtreler
- Kullanıcı (tümü veya tek kullanıcı)
- İşlem (30 denetim işleminin Türkçe adı)
- Başlangıç ve bitiş tarihi (yerel gün)
- Filtrele / Temizle / Yenile

### Liste
- Tarih (ss:dd:sn), kullanıcı, işlem, varlık türü, kayıt no, detay.
- Detay alanı kısaltılır; tam içerik tooltip'te gösterilir.
- Sayfa boyutu 25 / 50 / 100; önceki/sonraki sayfa.
- Kayıt yoksa boş durum mesajı.

## 2. Yetki ve güvenlik
- Menü yalnızca yöneticide görünür.
- `AuditLogService` yönetici olmayan çağrılarda boş sonuç ve hata mesajı döndürür.
- Kayıtlar uygulama arayüzünden değiştirilemez/silinemez.
- Loglarda kişi adı/telefon/açıklama tutulmaz; yalnızca işlem, kullanıcı, varlık ve teknik detay yer alır.

## 3. Sorgu
- `IAuditLogRepository` + `AuditLogService` katmanları eklendi.
- Filtreler veritabanı sorgusuna çevrilir; sayfalama `Skip/Take` ile yapılır.
- Kullanıcı kimlikleri tek sorguda görünen adlara çözülür; silinmiş kullanıcılar için "Bilinmeyen kullanıcı", oturumsuz kayıtlar için "Sistem" gösterilir.

## 4. Test
- 206/206 otomatik test.
  - Sıralama ve kullanıcı adı çözümleme
  - İşlem ve kullanıcı filtresi
  - Yerel tarih aralığı filtresi
  - Yönetici olmayan erişimin reddi
  - Ters tarih aralığının reddi
- UI:
  - Sayfa açıldı, 249 kayıt / 5 sayfa göründü.
  - "Yedek geri yüklendi" filtresi uygulandı; liste 1 kayda düştü.
- Derleme: 0 uyarı / 0 hata, CI yeşil.

## 5. Sıradaki adım
1. Kurulum paketi (6D) ve sürüm 1.0.0.
2. UAT: DPI, 24 saat, 1000 kayıt performansı, log PII kontrolü.
