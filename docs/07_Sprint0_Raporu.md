# 07 — Sprint 0 Sonuç Raporu

**Sprint:** 0 · **Tarih:** 06.11.2026 · **Durum:** Tamamlandı (spike'lar hariç)

---

## 1. Sprint hedefi

> "Çalışan, derlenen, test edilen ve veritabanı oluşturan bir teknik iskelet; riskli
> varsayımların (WhatsApp pano, toast, push, yedek) doğrulanması."

İskelet hedefi **karşılandı**. Riskli varsayımların canlı doğrulaması (spike S-01…S-05)
Sprint 1'in ilk yarısına taşındı; bunlar için gerçek WhatsApp/telefon/ntfy hesabı gerekir.

---

## 2. Tamamlananlar

| # | Çıktı | Kanıt |
|---|-------|-------|
| 1 | `CgpRandevuTakip.sln` + 5 proje | `dotnet build` → 0 hata, 0 uyarı |
| 2 | Katmanlı yapı (Domain / Application / Infrastructure / Desktop / Tests) | Proje referansları tek yönlü |
| 3 | Domain modeli: 12 varlık, 7 enum, telefon value object | `src/CgpRandevuTakip.Domain` |
| 4 | Hatırlatıcı hesaplama kuralları (1440/60/30, geçmiş atlama) | `ReminderScheduleCalculator` |
| 5 | Yedek dosya adı kuralı | `BackupFileNameBuilder` |
| 6 | EF Core + SQLite + ilk migration | `Persistence/Migrations/..._InitialCreate.cs` |
| 7 | Eğitim kurumu varsayılan 7 kategori | `DatabaseInitializer` |
| 8 | PBKDF2-SHA256 parola özetleme (210k iterasyon) | `Pbkdf2PasswordHasher` |
| 9 | Türkiye saat dilimi (`Europe/Istanbul`) | `SystemClock` |
| 10 | WPF açılış kabuğu (tema, durum kartları, veritabanı yolu) | Ekran görüntüsü yerine çalışan exe |
| 11 | 36 birim/entegrasyon testi | `dotnet test` → 36/36 başarılı |
| 12 | `.gitignore`, `CHANGELOG.md`, doküman seti | Depo kökü |

### Çalıştırma

```powershell
dotnet build CgpRandevuTakip.sln -c Debug
dotnet test  CgpRandevuTakip.sln -c Debug
dotnet run --project src/CgpRandevuTakip.Desktop
```

- Veritabanı: `%LocalAppData%\CgpRandevuTakip\data.db`
- Yedek klasörü (henüz kullanılmıyor): `Belgeler\CgpRandevuTakip\Yedekler`
- Uygulama ilk açılışta şemayı migration ile kurar ve 7 kategoriyi ekler.

---

## 3. Teknik doğrulamalar

| Kontrol | Sonuç |
|---------|-------|
| `dotnet build` | Başarılı, 0 uyarı / 0 hata |
| `dotnet test` | 36 başarılı / 0 başarısız |
| Uygulama açılışı | Süreç ayakta kaldı, `data.db` + WAL oluştu |
| Migration | İlk migration üretildi ve in-memory SQLite'ta uygulandı |
| DB konumu | `%LocalAppData%\CgpRandevuTakip\data.db` |

---

## 4. Spike durumu

| Spike | Durum | Not |
|-------|-------|-----|
| S-01 WhatsApp Desktop'a pano görseli yapıştırma | **Yapılmadı** | Ortam hazır: WhatsApp Desktop + test telefonu |
| S-02 Toast / kilit ekranı | **Yapılmadı** | Ortam hazır; Windows bildirim izni kontrol edilecek |
| S-03 ntfy push | **Yapılmadı** | ntfy topic + telefon gerekir |
| S-04 SQLite online backup | **Yapılmadı** | Yedek servisi Sprint 6'da; teknik risk düşük |
| S-05 1000 kayıt listesi | **Yapılmadı** | Liste ekranı Sprint 2'de ölçülecek |

> Spike'lar tamamlanmadan Sprint 5 (paylaşım) ve Sprint 4 (bildirim) kapatılmaz.
> S-01 başarısız olursa paylaşım akışı "görseli dosya olarak kaydet + ekle" adımına döner.

---

## 5. Sprint 1'e devredilenler

1. İlk yönetici hesabı oluşturma (first-run) ve giriş ekranı
2. Rol bazlı yetki kontrolü (`ICurrentUser`, servis katmanı)
3. Tema altyapısı (Koyu / Açık / Otomatik) ve ayar kalıcılığı
4. Kategori yönetim ekranı (CRUD, renk, sıralama, pasifleştirme)
5. Ayarlar kabuğu ve Hakkında
6. Spike S-01 ve S-02'nin yapılması, sonuçların bu rapora işlenmesi

---

## 6. Retrospektif notları

**İyi:**
- Katmanlar ve testler ilk günden ayrıldı; iş kuralları UI'sız test edilebiliyor.
- Migration'ın erken kurulması, sonradan şema değişikliğini güvenli hâle getirdi.
- Yedek adı ve saat dilimi gibi "küçük" kararlar kodda tek yerde toplandı.

**İyileştirme:**
- Spike'lar kod yazmadan önce yapılmalıydı; gerçek entegrasyon riski hâlâ açık.
- Uygulama açılışında hata günlüğü (Serilog) henüz yok; Sprint 1'de eklenecek.
- Tek örnek (single instance) ve tepsi simgesi henüz yok; Sprint 1'de eklenecek.

**Aksiyon:**
- Sprint 1'in ilk 2 günü spike S-01/S-02'ye ayrılacak; sonuç rapora yazılacak.
