# 08 — Sprint 1 Sonuç Raporu

**Sprint:** 1 · **Tarih:** 06.11.2026 · **Durum:** Tamamlandı (OS toast ve oturum kilidi hariç)

---

## 1. Sprint hedefi

> "İlk kurulum sihirbazı, giriş/roller, tema altyapısı, kategori yönetimi ve ayarlar;
> S-01/S-02 riskli varsayımlarının doğrulanması."

Hedef **karşılandı**. Uygulama ilk açılışta yönetici hesabı oluşturuyor, giriş yapıyor,
koyu/açık/otomatik temayı uyguluyor, kategorileri yönetiyor ve ayarları kalıcı saklıyor.

---

## 2. Tamamlanan çıktılar

| # | Çıktı | Kanıt |
|---|-------|-------|
| 1 | İlk kurulum sihirbazı (Admin hesabı) | `SetupWindow`, `AuthService.CreateFirstAdminAsync` |
| 2 | Giriş ekranı, rol bilgisi, oturum | `LoginWindow`, `CurrentUser`, `UserSession` |
| 3 | 5 hatalı denemede 15 dk kilit | `AuthService`, `AuthServiceTests` |
| 4 | Şifre değiştirme (kendi hesabı) | `ChangePasswordWindow` |
| 5 | Koyu / açık / otomatik tema | `Themes/Dark.xaml`, `Themes/Light.xaml`, `ThemeService` |
| 6 | Windows teması izleme | `SystemEvents.UserPreferenceChanged` + registry |
| 7 | Kategori yönetimi | `CategoryService`, `CategoriesPage` |
| 8 | Kategori sıralama / pasifleştirme / silme kuralları | `CategoryServiceTests` |
| 9 | Ayarlar: kurum adı, yedek klasörü, bildirim, push, saat | `SettingsService`, `SettingsPage` |
| 10 | Yedek klasörü yazılabilirlik testi | `SettingsService.TestDirectory` |
| 11 | Hakkında: ürün, sürüm, telif, veri yolları | `SettingsPage` |
| 12 | Denetim kaydı | `AuditLogger`, `AuditAction` |
| 13 | Tek örnek (single instance) | `App.AcquireSingleInstance` |
| 14 | S-01 liste görseli + pano + WhatsApp açma | `ListImageRenderer`, `SpikeService` |
| 15 | S-02 hatırlatıcı ve toast önizleme | `ReminderPreviewWindow`, `ToastPreviewWindow` |
| 16 | 79 birim/entegrasyon testi | `dotnet test` → 79/79 |
| 17 | Hata günlüğü | `logs/crash-yyyyMMdd.log` |

---

## 3. Teknik doğrulamalar

| Kontrol | Sonuç |
|---------|-------|
| `dotnet build` | Başarılı, 0 uyarı / 0 hata |
| `dotnet test` | 79 başarılı / 0 başarısız |
| İlk açılış (0 kullanıcı) | Kurulum sihirbazı açıldı |
| Giriş | Test yöneticisi ile ana kabuk açıldı |
| Kategori ekranı | UI Automation ile açıldı, başlık ve aksiyonlar yüklendi |
| Ayarlar ekranı | UI Automation ile açıldı, Tanılama ve Hakkında yüklendi |
| S-01 | PNG üretildi (28.187 bayt), panoya kopyalandı, başarı mesajı göründü |
| S-02 hatırlatıcı | Pencere yüklendi, 540 × 506 px, görünür |
| S-02 toast | Toast önizleme penceresi açıldı |
| Çökme günlüğü | 0 kayıt |
| DB durumu | Test sonrası ilk kurulum durumuna geri yüklendi |

> S-01'de WhatsApp'ın gerçekten açıldığı ve pano görselinin sohbete yapıştığı **manuel**
> doğrulanmalıdır. S-02'de doğrulanan uygulama içi önizlemedir; Windows işletim sistemi
> toast'u Sprint 4'te bildirim paketiyle yapılacaktır.

---

## 4. Uygulama kararları

- Kategori adı `NOCASE` benzersizdir; silme yalnızca hiç görüşme referansı yoksa yapılır.
- Tema, `DynamicResource` sözlük değişimiyle anında uygulanır; ayar veritabanında tutulur.
- Ayarların tamamı yalnızca yönetici tarafından değiştirilebilir; tema tüm roller için açıktır.
- Parola özeti PBKDF2-SHA256, 210.000 iterasyon; düz parola loglanmaz.
- Denetim kaydı iş akışını durdurmaz (best-effort yazım).

---

## 5. Bilinçli olarak ertelenenler

| Konu | Gerekçe | Hedef |
|------|---------|-------|
| Windows OS toast / push | Bildirim paketi ve kanal yapılandırması gerekir | Sprint 4 |
| 30 dk hareketsizlik kilidi (FR-AUTH-05) | Sprint 2 kabuk işleriyle birlikte kilit ekranı | Sprint 2 |
| Kullanıcı yönetimi ekranı (FR-SET-07) | Rol atama akışı görüşme CRUD sonrası | Sprint 2/3 |
| Yönetici şifre sıfırlama | Kullanıcı yönetimiyle birlikte | Sprint 2/3 |
| WhatsApp gerçek yapıştırma testi | Elle doğrulama gerekir (S-01) | Sprint 5 öncesi |

---

## 6. Sprint 2'ye devir

1. Görüşme ekleme, detay, düzenleme ve günlük liste.
2. Kişi/telefon normalizasyonu ve çakışma uyarısı.
3. Rol bazlı görünürlük: görüşmeci yalnızca kendi kayıtlarını görür.
4. Oturum kilidi ve kullanıcı yönetimi için hazırlık.
5. S-01 manuel doğrulaması ve sonucun bu rapora işlenmesi.
