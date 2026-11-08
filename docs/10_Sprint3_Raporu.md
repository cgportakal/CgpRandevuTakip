# 10 — Sprint 3 Sonuç Raporu

**Sprint:** 3 · **Tarih:** 07.11.2026 · **Durum:** Tamamlandı
**Sürüm:** 0.4.0

---

## 1. Sprint hedefi

> "Kaydın yaşam döngüsü: görüşme sonucu, durum geçişleri, tekrar randevu,
> kullanıcı yönetimi ve 30 dakika hareketsizlik kilidi."

Hedef **karşılandı**. Kullanıcı görüşme sonucunu girebiliyor, görüşmeyi
kapatıp yeniden açabiliyor, tekrar randevu oluşturabiliyor; yönetici kullanıcı
ekleyip rol/şifre yönetebiliyor; 30 dakika işlem yapılmazsa oturum kilitleniyor
ve şifreyle açılıyor.

---

## 2. Tamamlanan çıktılar

| # | Çıktı | Kanıt |
|---|-------|-------|
| 1 | Görüşme sonucu kaydı (katılım, değerlendirme, tekrar, kapanış) | `AppointmentOutcomeService`, `AppointmentOutcomeWindow` |
| 2 | Durum makinesi: Geldi / Gelmedi / Ertelendi / İptal / Tamamlandı | `Appointment.ApplyOutcome` |
| 3 | Kapanmış görüşmeyi yeniden açma (Tamamlandı hariç) | `Appointment.Reopen` |
| 4 | Kapanışta bekleyen hatırlatıcıların iptali | `AppointmentRepository.SaveOutcomeAsync` |
| 5 | Kapanış bilgisi: tarih/saat + kapatan kullanıcı | `ClosedByUserId`, `ClosedByName` |
| 6 | Detayda sonuç kartı ve "Sonuç gir" akışı | `AppointmentDetailWindow`, `AppointmentOutcomeDialogService` |
| 7 | Detaydan "Tekrar randevu oluştur" (7 gün sonrası taslak) | `AppointmentDialogService.ShowRepeatAsync` |
| 8 | Düzenleme ekranında sonuç kartı | `AppointmentEditorViewModel` |
| 9 | Günlük listede "Sıradaki görüşme" şeridi | `AppointmentService.GetNextAppointmentAsync` |
| 10 | Kullanıcı yönetimi: ekleme, rol, aktiflik, giriş izni | `UserService`, `UsersPage` |
| 11 | Şifre sıfırlama; kilit sayacı ve kilidin temizlenmesi | `UserService.ResetPasswordAsync` |
| 12 | Son aktif yönetici koruması ve kendini pasifleştirme engeli | `CountActiveLoginAdminsAsync` |
| 13 | 30 dk hareketsizlik kilidi ve kilit ekranı | `SessionLockPolicy`, `SessionLockService`, `LockWindow` |
| 14 | Tüm pencerelerden (modal diyaloglar dahil) etkinlik izleme | `InputManager.PreProcessInput` |
| 15 | Kullanıcılar ekranı yalnızca yönetici gezinmesinde | `ShellViewModel.ShowUsersNav` |
| 16 | Sprint 3 migration'ı | `20261107040550_Sprint3_OutcomeAndLock` |
| 17 | 32 yeni test (toplam 135) | `dotnet test` → 135/135 |

---

## 3. İş kuralları ve uygulama kararları

- **Sonuç kaydı** `AppointmentOutcome` üzerinden tutulur. Katılım `Attendance`
  enum'ıdır: `Arrived`, `NoShow`, `Postponed`, `Cancelled`.
- **Geldi + kapandı** → görüşme `Completed` olur; kapanış zamanı ve kapatan
  kullanıcı yazılır.
- **Gelmedi** durumu kapanış seçimine göre açık veya kapalı kalabilir.
- **Ertelendi** görüşme açık kalır; sonraki açıklama `PostNotes` alanına yazılır.
- **İptal** görüşmeyi kapatır; kapalı `NoShow` / `Postponed` / `Cancelled`
  görüşmeler yeni sonuç girildiğinde otomatik yeniden açılır.
- **Tamamlanmış** görüşmenin katılım sonucu değiştirilemez; yalnızca "Geldi"
  sonucu tekrar kaydedilebilir.
- **Kapanışta** bekleyen hatırlatıcılar `Cancelled` durumuna alınır; gönderilmiş
  hatırlatıcılar korunur.
- **Tekrar randevu** kaynak görüşmenin kişi, telefon, süre, host, kategori,
  yöntem ve hatırlatıcı seçimlerini devralır; tarih 7 gün sonrası, saat bir
  sonraki yarım saat olarak önerilir. Kaynak görüşme değiştirilmez.
- **Kullanıcı adı** `UsernamePolicy` ile normalize edilir (küçük harf, 3-32
  karakter). Yönetici ve sekreter giriş yapabilmelidir; yalnızca host
  girişsiz olabilir ve bu durumda yer tutucu parola özeti (`"!"`) saklanır.
- **Şifre sıfırlama** PBKDF2 ile yeniden özetlenir; hatalı giriş sayacı ve
  hesap kilidi temizlenir.
- **Son aktif yönetici** pasifleştirilemez, rolü düşürülemez veya girişi
  kapatılamaz. Kullanıcı kendi hesabını pasifleştiremez.
- **Oturum kilidi** 30 dakika hareketsizlikte tetiklenir; sayaç 30 saniyede bir
  kontrol edilir. Etkinlik fare, klavye ve dokunma girişleriyle tüm
  pencerelerden izlenir. Kilit açma mevcut kullanıcının şifresiyle yapılır ve
  `SessionUnlocked` denetim kaydı yazılır. Kilit ekranından çıkış da mümkündür.
- **Denetim** olayları: `OutcomeSaved`, `UserCreated`, `UserUpdated`,
  `UserDeactivated`, `PasswordChanged`, `SessionLocked`, `SessionUnlocked`.
- **Yetki:** sonuç girme yönetici, sekreter ve görüşmenin host'u ile sınırlıdır;
  kullanıcı yönetimi yalnızca yöneticidedir.

---

## 4. UI doğrulaması (UI Automation + ekran görüntüsü)

| Kontrol | Sonuç | Kanıt |
|---------|-------|-------|
| İlk kurulum | `admin` / Cafer Yönetici hesabı oluşturuldu | `31-setup.png` |
| Giriş ve ana kabuk | Ana pencere açıldı, "Kullanıcılar" gezinmesi göründü | `33-users.png` |
| Kullanıcılar sayfası | Yönetici listelendi; rol, son giriş ve durum sütunları doğru | `35-users-page.png` |
| Yeni kullanıcı | `test.host` / Test Host oluşturuldu, liste yenilendi | `37-users-saved.png` |
| Yeni görüşme | "UI Test", 07/11/2026 08:00, 30 dk, Veli Görüşmesi | `40-editor-filled.png`, `41-after-save.png` |
| Sıradaki görüşme şeridi | "Sıradaki: 08:00 - UI Test" göründü | `41-after-save.png` |
| Detay ekranı | Sonuç kartı "Henüz sonuç girilmedi" ve "Sonuç gir" düğmesi | `43-detail.png` |
| Sonuç penceresi | Katılım, değerlendirme, tekrar ve kapanış alanları dolu | `44-outcome-window.png`, `45-outcome-filled.png` |
| Sonuç kaydı | Geldi + kapandı → **Tamamlandı**, "Kapatıldı: 07/11/2026 07:22 · Cafer Yönetici" | `46-outcome-saved-detail.png` |
| Tekrar randevu | Taslak 14/11/2026 08:00, kişi/telefon/host/kategori devralındı | `47-repeat-editor.png` |
| Tekrar kaydı | Yeni randevu oluştu; liste "Sıradaki: 14/11/2026 08:00 - UI Test" | `48-repeat-saved.png` |
| Oturum kilidi | Hareketsizlik sonrası kilit ekranı açıldı (butonlar kırpılmıyor) | `49-lock-window.png` |
| Kilidi açma | `Admin1234` ile kilit kapandı, ana ekran geri geldi | `50-after-unlock.png` |
| Çökme günlüğü | 0 yeni kayıt | — |

> **Not:** 30 dakika kilit UI testi pratik olmadığı için geçici olarak
> `SessionLockPolicy.DefaultIdleTimeout = 15 sn` ve kontrol aralığı `2 sn`
> kullanıldı. Test sonrası değerler **30 dakika / 30 saniye** olarak geri
> alındı; tanılama günlüğü kodu kaldırıldı.

Ekran görüntüleri: `C:\Users\Cafer\AppData\Local\Temp\opencode\sprint3-shots\`

---

## 5. Testler

| Alan | Test sayısı | Kapsam |
|------|-------------|--------|
| Domain | 37 | Telefon, hatırlatıcı, yedek adı, görüşme durum makinesi, `Reopen` |
| Application | 91 | Auth, kategori, ayar, görüşme, host, sonuç, kullanıcı, kilit politikası |
| Infrastructure | 7 | Migration, veritabanı başlatma, PBKDF2 |
| **Toplam** | **135** | **135/135 başarılı** |

Yeni test dosyaları:

- `AppointmentOutcomeServiceTests` — 6 test (Geldi/kapandı, Gelmedi, Ertelendi,
  yetki, iki kez kayıt, görünürlük).
- `UserServiceTests` — 10 test (kullanıcı adı normalizasyonu, tekrar eden ad,
  yönetici yetkisi, girişsiz host, son yönetici, kendini pasifleştirme,
  pasifleştirme denetimi, şifre sıfırlama, eşleşmeyen şifre, yetkisiz liste).
- `SessionLockPolicyTests` — 6 test (eşik, özel süre, devre dışı, kalan süre).
- `AppointmentTests` — 8 yeni durum geçişi testi.
- `AppointmentServiceTests` — 2 yeni "sıradaki görüşme" testi.

`dotnet build CgpRandevuTakip.sln -c Debug` → **0 uyarı / 0 hata**.

---

## 6. Bulunan ve düzeltilen hatalar

| # | Belirti | Düzeltme |
|---|---------|----------|
| 1 | Kilit penceresinin alt düğmeleri kırpılıyordu | `SizeToContent="Height"` + `MinHeight="440"` |
| 2 | Sonuç kaydında kapatıcı adı boş geliyordu (test kullanıcısı DB'de yoktu) | Testte gerçek sekreter kullanıcısı oluşturuldu; DTO kullanıcı adını DB'den çözüyor |
| 3 | Modal diyalog açıkken etkinlik izlenmiyordu | `InputManager.PreProcessInput` ile tüm pencereler kapsandı |
| 4 | Oturum kilidi tanılaması üretim kodunda kalma riski | Geçici süreler ve `DebugLog` geri alındı, build/test ile doğrulandı |

> 2. maddedeki kök neden uygulama değil test verisiydi; üretim akışında
> `ClosedByUserId` → `User.DisplayName` çözümlemesi doğru çalışıyor.

---

## 7. Bilinçli olarak ertelenenler

| Konu | Gerekçe | Hedef |
|------|---------|-------|
| Windows OS toast / push | Bildirim paketi Sprint 4 | Sprint 4 |
| Hatırlatıcı arka plan zamanlayıcısı | Sprint 4 reminder motoru | Sprint 4 |
| WhatsApp PNG + pano + açma akışı | Paylaşım Sprint 5 | Sprint 5 |
| Alıcı rehberi ve günlük tetikleme | Paylaşımla birlikte | Sprint 5 |
| Yedekleme/geri yükleme ekranı | Sprint 6 | Sprint 6 |
| Kurulum paketi ve UAT | Sprint 6 | Sprint 6 |
| Değişiklik geçmişi (FR-DET-08) | P2; denetim kaydı altyapısı hazır | Faz 2 |
| Eşzamanlı değişiklik koruması (FR-EDIT-05) | Tek kullanıcı oturumu | Faz 2 |

---

## 8. Sprint 4'e devir

1. Hatırlatıcı arka plan zamanlayıcısı: `ReminderDispatch` kayıtlarını due
   zamanına göre işleme, uygulama kapalıyken telafi.
2. Windows toast bildirimi + uygulama içi aksiyon penceresi (4 seçenek).
3. ntfy/Telegram push; isimsiz içerik ve sessiz saatler.
4. Kilit ekranında bildirim içeriğinin gizlenmesi (BR-21, NFR-SEC-08).
5. Hatırlatıcı aksiyonlarının sonuç/durum makinesiyle bağlanması
   (`ReminderActionTaken` denetimi hazır).
6. Açık ürün soruları (Q-03, Q-05, Q-10, Q-14, Q-15, Q-17) yanıtlanırsa
   ayarlara işlenmesi.
