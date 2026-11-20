# 43 — Yedekleme ve Geri Yükleme

**Tarih:** 20.11.2026 · **Sürüm:** masaüstü 1.8.5+ · **Kapsam:** masaüstü yerel yedekleri

Bu belge, seçmeli yedekleme/geri yükleme davranışını ve 20.11.2026’da düzeltilen
“geri yükleme sonrası kurulum sihirbazı” hatasının kök nedenini açıklar.

## 1. Eski davranış ve sorun

Önceki sürümlerde yedek, **tüm `data.db` dosyasının kopyası** idi ve geri yükleme bu
dosyayı mevcut dosyanın üzerine yazıyordu. Sonuçlar:

- Kullanıcılar, ayarlar (SMTP, ntfy/Telegram, Drive, çalışma saatleri) ve randevular
  yedek anına **birlikte** dönüyordu.
- Kullanıcı tablosu boş bir yedek geri yüklenirse `IsFirstRunAsync()` true oluyor,
  uygulama **ilk kurulum sihirbazını** açıyordu. 13.11.2026 tarihli gerçek bir yedekte
  `UserCount: 0` görüldü; bu yedek “doğrulandı” sayılıyor ve sihirbazı tetikliyordu.
- Yedeğin boş/dolu olduğu, son randevu tarihi ve son değişiklik zamanı bilinmiyordu.
- `settings.json` yedeğe yazılıyor ancak geri yüklemede hiç okunmuyordu.

## 2. Yeni yedek biçimi (manifest v2)

Yedek ZIP’i:

| Girdi | İçerik |
|---|---|
| `data.db` | Seçilen bölümleri içeren SQLite kopyası |
| `settings.json` | Seçilen grupların **gizli olmayan** anahtarları (bilgi amaçlı) |
| `manifest.json` | Ürün/sürüm/şema, SHA-256, seçim ve içerik özeti |
| `branding/institution-logo.*` | Logo seçildiyse kurum logosu |

`manifest.json` v2 alanları:

- `ManifestVersion = 2`, `Selection` (bölümler + ayar grupları)
- `Summary`: kullanıcı, kişi, kategori, randevu (aktif/silinmiş), sonuç, hatırlatma,
  denetim, ayar, paylaşım, içerik sürümü, alıcı, yedek geçmişi sayıları
- İlk/son randevu (yerel), son veri değişikliği (yerel)
- Kurum, SMTP, bildirim, yedek/Drive, sunucu bağlantısı ayarlarının varlığı
- Logo var mı

Eski (v1) yedekler okunmaya devam eder; eksik özet yedekteki veritabanından hesaplanır.

## 3. Seçmeli yedekleme

“Şimdi yedekle” bir seçenek penceresi açar. Varsayılan **tam yedektir**:

| Bölüm | Varsayılan |
|---|---|
| Randevular + kişiler + kategoriler | ✅ |
| Kullanıcılar (parola özetleri) | ✅ (uyarı gösterilir) |
| Ayarlar (8 grup) | ✅ |
| Denetim kaydı, paylaşım geçmişi, yedek geçmişi, içerik sürümleri, alıcılar | ✅ |
| Kurum logosu | ✅ |

Seçilmeyen bölümler kopyadan **silinir** ve `VACUUM` ile temizlenir; dosyada kalmaz.
Sunucu oturumu belirteçleri (`sync.api.access_token`, `refresh_token`, `expires_at`,
`user`) ve sunucu okuma önbelleği (`SyncCacheEntries`) **hiçbir zaman** yedeğe girmez.
Yalnızca sunucu adresi (`sync.api.base_url`) ve okuma modu yedeklenir.

Ayar grupları: kurum profili, SMTP, bildirimler, çalışma saatleri, yedek/Drive,
görünüm, masaüstü davranışı, sunucu bağlantısı.

## 4. Yedek önizlemesi

Yedek seçildiğinde liste altında içerik özeti gösterilir:

- `248 randevu · 12 silinmiş · 180 kişi · 3 kullanıcı · 7 kategori`
- `Son randevu: 12.03.2026 · son değişiklik: 14.03.2026 09:12`
- `kurum · SMTP · bildirim · yedek · sunucu bağlantısı`
- Uyarılar: boş yedek, kullanıcı yok, ayar yok, farklı sürüm, şifre çözülemeyebilir.

## 5. Geri yükleme (uygulamayı sıfırlamaz)

Geri yükleme artık **dosya değiştirmez**; seçilen bölümleri mevcut veritabanına
**transaction içinde** birleştirir.

| Bölüm | Davranış |
|---|---|
| Randevular | `Birleştir` (varsayılan): aynı Id varsa daha yeni `UpdatedAtUtc` kazanır; `Değiştir`: mevcut randevular silinir |
| Kişiler | Id ile güncellenir/eklenir |
| Kategoriler | Id, yoksa **ad** ile eşleştirilir; randevu referansları korunur |
| Kullanıcılar | **Varsayılan kapalı.** Seçilirse Id/ad ile eşleştirilir, mevcut hesaplar silinmez |
| Ayarlar | **Varsayılan kapalı.** Seçilen grupların anahtarları yazılır, diğerleri korunur |
| Sunucu bağlantısı | Adres + okuma modu; oturum belirteçleri asla |
| Denetim/paylaşım/yedek geçmişi/içerik sürümleri/alıcılar | Id ile yalnızca eksikler eklenir |
| Hatırlatma kayıtları | Taşınmaz; yeniden planlanır |
| Logo | Seçilirse `branding` klasörüne yazılır |

Kurallar:

- Geri yükleme öncesi **tam güvenlik yedeği** alınır.
- Kullanıcılar silinmediği için `Users.Count == 0` olamaz; **kurulum sihirbazı açılamaz**.
- Boş yedek varsayılan olarak **engellenir**; “Boş yedeği yine de geri yükle” onayı gerekir.
- Hata olursa transaction geri alınır; mevcut veri bozulmaz.
- Makineye özel anahtarlar (`backup_directory`, `backup_password`, `auto_start_*`,
  `close_to_tray`, `google_drive_folder`, `google_refresh_token`) yalnızca
  “Bu bilgisayara özel ayarları da geri yükle” işaretlenirse yazılır.
- DPAPI ile şifrelenmiş parolalar başka Windows kullanıcısında çözülemez; bu durumda
  o anahtar atlanır ve kullanıcı uyarılır (parolayı yeniden girmelidir).
- Geri yükleme sonrası ayar önbelleği temizlenir; istenirse uygulama yeniden başlatılır.

## 6. Eski yedekler

- v1 manifestli yedekler doğrulanır, içerikleri veritabanından hesaplanır.
- Yedekteki veritabanı geri yüklemeden önce geçici kopyada **güncel şemaya migrate edilir**.
- `SchemaVersion` uyuşmazlığı yine reddedilir.
- 1.2.1 sürümünden kalma gerçek bir yedekle (0 kullanıcı) yapılan doğrulamada mevcut
  kullanıcı ve ayarlar korundu; sihirbaz açılmadı.

## 7. Bağlantı ayarları (web / mobil / masaüstü)

Web ve mobil istemciler API adresini derleme/çalıştırma ortamından
(`NEXT_PUBLIC_API_URL`, `EXPO_PUBLIC_API_URL`) alır; bu değerler masaüstü
veritabanında tutulmaz. Masaüstünün ortak sunucu bağlantısı ise
`sync.api.base_url` olarak **Ayarlar → Sunucu bağlantısı** içinde saklanır ve
yedeğe “Sunucu bağlantısı” grubuyla girer. Geri yüklemede isteğe bağlıdır.

## 8. UAT

1. Kurulum sihirbazını tamamla; SMTP, ntfy/Telegram ve sunucu adresini ayarla.
2. “Şimdi yedekle” → yalnızca **Ayarlar → SMTP + Bildirimler + Bağlantı** seç.
3. Yedek listesinde içerik özetini ve uyarıları doğrula.
4. SMTP/push bilgilerini değiştir, bir randevu ekle.
5. “Geri yükle…” → yalnızca **Ayarlar** ve ilgili grupları seç; randevuları seçme.
6. Uygulama yeniden başlatıldığında **kurulum sihirbazı açılmamalı**, randevu durmalı,
   SMTP/push eski değerlerine dönmeli.
7. Boş bir yedek seç → uyarı ve onay kutusu görünmeli; onaylanmadan geri yüklenmemeli.
8. Eski bir yedek (v1) seç → özet hesaplanmalı ve geri yükleme çalışmalı.

## 9. Doğrulama

- **412/412** otomatik test (334 uygulama + 63 masaüstü/VM + 13 API + 2 UI).
- Yedekleme testleri: tam/kısmi yedek, manifest v1 uyumu, boş yedek engeli, varsayılan
  birleştirme, kullanıcı/ayar koruma, ayar grubu, bağlantı adresi/oturum belirteci ayrımı,
  `Replace` modu, kullanıcı bölümü.
- Bu makinedeki gerçek v1.2.1 yedeği (0 kullanıcı) geçici kopyada incelendi ve mevcut
  kullanıcı/ayarlar korunarak geri yüklendi; sihirbaz tetiklenmedi.
