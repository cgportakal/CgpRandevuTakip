# 11 — Sprint 3.5 Sonuç Raporu

**Sprint:** 3.5 · **Tarih:** 08.11.2026 · **Durum:** Tamamlandı
**Sürüm:** 0.4.1

---

## 1. Sprint hedefi

> "Sprint 4'e geçmeden önce mevcut ekleme, düzenleme ve silme akışlarını doğrulamak;
> önceki sprintlerden atlanan geri alma, host filtresi ve veri bakımı işlerini kapatmak."

Hedef **karşılandı**. Yönetici silinen görüşmeleri 30 gün içinde geri alabiliyor,
gelecekteki hatırlatıcılar yeniden planlanıyor, süresi dolan kayıtlar otomatik
temizleniyor ve günlük listede host filtresi çalışıyor.

---

## 2. Tamamlanan çıktılar

| # | Çıktı | Kanıt |
|---|-------|-------|
| 1 | Silinen görüşmeler ekranı | `DeletedAppointmentsWindow`, `DeletedAppointmentsViewModel` |
| 2 | 30 gün içinde geri alma ve süre kontrolü | `AppointmentService.RestoreAsync` |
| 3 | Geri almada iptal edilmiş gelecek hatırlatıcıların yeniden etkinleştirilmesi | `AppointmentRepository.RestoreAsync` |
| 4 | 30 günü dolan kayıtların kalıcı temizliği | `PurgeExpiredDeletedAsync`, `DeletedAppointmentCleanupService` |
| 5 | Günlük listede host filtresi | `AppointmentListViewModel`, `AppointmentQuery` |
| 6 | Silme/geri alma işlemlerinde `IClock` tutarlılığı | `SoftDeleteAsync(id, nowUtc)` |
| 7 | Henüz etkinleşmemiş paylaşım düğmesinin gizlenmesi | `CanShareDailyList` |
| 8 | Bildirim ayarlarına Sprint 4 bilgi notu | `SettingsPage.xaml` |
| 9 | Doküman ve sürüm güncellemesi | README, CHANGELOG, docs/02, docs/03, docs/04, docs/06 |
| 10 | 6 yeni otomatik test (toplam 146) | `dotnet test` → 146/146 |

---

## 3. CRUD doğrulaması

UI Automation ile gerçek uygulama üzerinde aşağıdaki akış çalıştırıldı:

| Adım | Sonuç | Kanıt |
|------|-------|-------|
| Admin girişi | Ana pencere açıldı | — |
| Silinenler penceresi | Boş durum doğru göründü | `70-deleted-empty.png` |
| Yeni görüşme | `CRUD Test 35` oluşturuldu | `71-editor-filled.png`, `72-created.png` |
| Düzenleme | Açıklama değiştirilerek kaydedildi | `73-edit-filled.png`, `74-edited.png` |
| Silme | Onay sonrası kayıt listeden kalktı | — |
| Silinenler | Kayıt, silinme zamanı ve kalan gün ile listelendi | `75-deleted-list.png` |
| Geri alma | Kayıt geri alındı; durum mesajı göründü | `76-restored-list.png` |
| Listeye dönüş | Kayıt günlük listede yeniden göründü | `77-restored-list.png` |
| Host filtresi | "Tüm görüşmeciler" filtresi araç çubuğunda göründü | `77-restored-list.png` |

Ekran görüntüleri:
`C:\Users\Cafer\AppData\Local\Temp\opencode\sprint35-shots\`

---

## 4. İş kuralları ve uygulama kararları

- Silinen görüşmeler **30 gün** soft delete olarak saklanır (D-39).
- Geri alma yalnızca yöneticiye açıktır; süresi dolmuş kayıt reddedilir.
- Geri alınan randevu hâlâ gelecekteyse iptal edilmiş hatırlatıcılar `Pending` durumuna döner.
- Geçmiş randevularda hatırlatıcılar yeniden etkinleştirilmez.
- 30 günü dolan kayıtlar açılışta ve 6 saatlik aralıkla kalıcı silinir; her kayıt için
  `AppointmentPurged` denetim kaydı yazılır (D-41).
- Host filtresi yönetici ve sekreterde görünür; host rolü yalnızca kendi kayıtlarıyla sınırlıdır (D-40).
- Günlük paylaşım düğmesi Sprint 5'e kadar gizlenir; yarım akış gösterilmez.

---

## 5. Testler

| Alan | Test sayısı | Kapsam |
|------|-------------|--------|
| Domain | 37 | Telefon, hatırlatıcı, yedek adı, durum makinesi |
| Application | 102 | Auth, kategori, ayar, görüşme, host, sonuç, kullanıcı, kilit, geri alma, host filtresi |
| Infrastructure | 7 | Migration, veritabanı başlatma, PBKDF2 |
| **Toplam** | **146** | **146/146 başarılı** |

Yeni testler:

- `Search_FiltersByHost`
- `GetDeleted_ReturnsOnlyDeletedAppointmentsForAdmin`
- `Restore_ReplansCancelledFutureReminders`
- `Restore_ReactivatesOnlyLatestReminderPerOffset`
- `Restore_RejectsDeletionOlderThanThirtyDays`
- `PurgeExpiredDeleted_RemovesOnlyOlderThanThirtyDays`

`dotnet build CgpRandevuTakip.sln -c Debug` → **0 uyarı / 0 hata**.

---

## 6. Bilinçli olarak ertelenenler

| Konu | Gerekçe | Hedef |
|------|---------|-------|
| Tepsi simgesi / otomatik başlangıç | Sprint 3.5 kritik kapsamına alınmadı | Sprint 4 veya 6 |
| Serilog / yapılandırılmış günlük | Mevcut çökme günlüğü yeterli; kapsam dışı | Sprint 6 |
| Denetim kaydı görüntüleme ekranı | Sprint 6 denetim kapsamı | Sprint 6 |
| Ayarleri varsayılana döndürme | P2 | Faz 2 |
| Değişiklik geçmişi / eşzamanlılık koruması | P2 | Faz 2 |

---

## 7. Sprint 4'e devir

1. Hatırlatıcı arka plan motoru ve açılış telafisi.
2. Windows toast + 4 seçenekli aksiyon penceresi.
3. ntfy/Telegram push, isimsiz içerik ve sessiz saatler.
4. Kilit ekranında PII gizleme.
5. Gerçek Windows toast / ntfy / WhatsApp spike doğrulamaları.
