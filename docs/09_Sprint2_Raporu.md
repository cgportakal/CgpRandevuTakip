# 09 — Sprint 2 Sonuç Raporu

**Sprint:** 2 · **Tarih:** 07.11.2026 · **Durum:** Tamamlandı
**Sürüm:** 0.3.0

---

## 1. Sprint hedefi

> "Görüşme kaydı oluşturma, günlük liste, arama, kişi/telefon normalizasyonu,
> host yönetimi ve rol bazlı görünürlük."

Hedef **karşılandı**. Kullanıcı günlük listeyi açıp yeni görüşme ekleyebiliyor,
kaydı düzenleyip detayını görüntüleyebiliyor; arama tarih filtresinden bağımsız
çalışıyor; host listesi giriş bilgisi olmadan da yönetilebiliyor.

---

## 2. Tamamlanan çıktılar

| # | Çıktı | Kanıt |
|---|-------|-------|
| 1 | Görüşme oluşturma / düzenleme / silme | `AppointmentService`, `AppointmentEditorWindow` |
| 2 | Günlük liste (tarih gezinme, bugün, özet) | `AppointmentListPage`, `AppointmentListViewModel` |
| 3 | Detay penceresi (salt okunur, düzenle/sil kısayolları) | `AppointmentDetailWindow` |
| 4 | Arama: ad, soyad, telefon, kategori, host | `AppointmentRepository.SearchAsync` |
| 5 | Kategori ve durum filtreleri | `AppointmentListViewModel` |
| 6 | Telefon E.164 normalizasyonu ve kişi tekrar kullanımı | `PhoneNumber.TryNormalize`, `PersonRepository` |
| 7 | Host çakışma tespiti ve onaylı zorla kayıt | `FindHostConflictsAsync`, `IgnoreConflicts` |
| 8 | Çoklu hatırlatıcı (1 gün / 1 saat / 30 dk) | `ReminderScheduleCalculator`, `ReminderDispatch` |
| 9 | Güncellemede hatırlatıcı yeniden planlama | `AppointmentService.UpdateAsync` |
| 10 | Rol görünürlüğü: görüşmeci yalnızca kendi kayıtları | `AppointmentService.GetDayAsync/SearchAsync` |
| 11 | Host yönetimi (girişsiz host üretimi dahil) | `HostService`, `HostsPage` |
| 12 | Günlük Liste varsayılan sayfa; "Görüşmeciler" yalnızca yönetici/sekreter | `ShellViewModel`, `MainWindow` |
| 13 | PerMonitorV2 DPI farkındalığı | `app.manifest` |
| 14 | Karanlık tema ComboBox / DatePicker | `Themes/Styles.xaml` |
| 15 | 103 birim/entegrasyon testi | `dotnet test` → 103/103 |

---

## 3. İş kuralları ve uygulama kararları

- **Host listesi** `User` tablosunda tutulur. Girişsiz host `IsLoginEnabled=false`,
  yer tutucu parola özeti (`"!"`, 1 iterasyon) ve otomatik `host-<slug>` kullanıcı adı ile oluşturulur.
- **Arama** metni doluyken tarih filtresi uygulanmaz; metin boşken seçili gün kullanılır.
- **Çakışma** aynı host + kesişen saat üzerinden hesaplanır. Kullanıcı onaylarsa
  `IgnoreConflicts=true` ile tekrar kayıt denenir. Aynı kişi + kesişen saat ayrıca işaretlenir.
- **Güncellemede** bekleyen hatırlatıcılar iptal edilip yeniden üretilir; daha önce
  bildirilmiş offsetler tekrar planlanmaz.
- **Silme** yalnızca yöneticiye açıktır ve 30 gün geri alma penceresi olacak şekilde
  soft delete yapılır.
- **Görüşmeci** kendi görüşmelerini görür, yeni randevu oluşturamaz.
- **Tarih biçimi** tüm ekranlarda `gg/AA/yyyy`; WPF `DatePicker` dahil.

---

## 4. UI doğrulaması (UI Automation + ekran görüntüsü)

| Kontrol | Sonuç |
|---------|-------|
| İlk kurulum sihirbazı | Yönetici hesabı oluşturuldu, giriş ekranına geçildi |
| Giriş | `admin` ile ana kabuk açıldı |
| Günlük Liste varsayılan sayfa | Açılışta yüklendi, boş durum metni ve hızlı ekleme göründü |
| Görüşmeciler ekranı | Host eklendi; ad, kullanıcı adı, giriş ve durum sütunları göründü |
| Host listesi sütun genişlikleri | Sabit sütunlar daraltıldı; "Ad soyad" okunur hâle geldi |
| Yeni görüşme formu | Ad/soyad/telefon dolduruldu, host ve kategori varsayılan seçildi |
| Kaydet | Liste yenilendi, satır göründü |
| Detay penceresi | Satır seçilip "Detay" ile açıldı, düzenle/sil düğmeleri rol kuralına uygun |
| Karanlık tema | ComboBox ve DatePicker koyu yüzeyde okunur; DatePicker'da beyaz alan kalmadı |
| DPI (%150 senaryosu) | `app.manifest` ile PerMonitorV2; pencere taşması giderildi |
| Çökme günlüğü | 0 kayıt |

> Gerçek WhatsApp yapıştırma ve işletim sistemi toast doğrulaması manuel/ileri sprint
> gerektirir (bkz. Sprint 1 raporu ve D-23).

---

## 5. Testler

| Alan | Test sayısı | Kapsam |
|------|-------------|--------|
| Domain | 29 | Telefon, hatırlatıcı, yedek adı, varlık kuralları |
| Application | 67 | Auth, kategori, ayar, görüşme, host |
| Infrastructure | 7 | Migration, veritabanı başlatma, PBKDF2 |
| **Toplam** | **103** | **103/103 başarılı** |

`dotnet build` → 0 uyarı / 0 hata.

---

## 6. Bulunan ve düzeltilen UI hataları

| # | Belirti | Düzeltme |
|---|---------|----------|
| 1 | Günlük liste araç çubuğu tek satırda taşıyor, liste ~600 px aşağı itiliyordu | İki satırlı araç çubuğu, `TextWrapping="NoWrap"` |
| 2 | Karanlık temada ComboBox beyaz görünüyordu | Özel `ComboBox`/`ComboBoxItem` template'i |
| 3 | 150% DPI'da pencere ekran dışına taşıyordu | `app.manifest` + PerMonitorV2 |
| 4 | Görüşmeciler listesinde "Ad soyad" sütunu sıfıra sıkışıyordu | Sabit sütun genişlikleri azaltıldı, `MinWidth` verildi |
| 5 | DatePicker karanlık temada beyaz kutu olarak görünüyordu | Özel `DatePicker`/`DatePickerTextBox` template'i |
| 6 | Tarih `7.11.2026` biçiminde görünüyordu | Kültürün `ShortDatePattern` değeri `dd/MM/yyyy` yapıldı |
| 7 | Liste satırı yalnızca çift tıklamayla açılıyordu | Açık "Detay" düğmesi ve erişilebilir adlar eklendi |
| 8 | Detay penceresi açılırken uygulama çöküyordu | `Run.Text` bağlamaları salt okunur özelliklerde `Mode=OneWay` yapıldı |
| 9 | Karanlık temada kaydırma çubuğu beyaz şerit olarak görünüyordu | Özel `ScrollBar` template'i (dikey + yatay) |

---

## 7. Bilinçli olarak ertelenenler

| Konu | Gerekçe | Hedef |
|------|---------|-------|
| 30 dk hareketsizlik kilidi (FR-AUTH-05) | Kilit ekranı ve oturum akışı Sprint 3 | Sprint 3 |
| Kullanıcı yönetimi / rol atama | Host yönetimi hazır; tam ekran Sprint 3 | Sprint 3 |
| Windows OS toast / push | Bildirim paketi Sprint 4 | Sprint 4 |
| WhatsApp PNG + pano + açma akışının listeye bağlanması | Paylaşım Sprint 5 | Sprint 5 |
| Alıcı rehberi | Paylaşımla birlikte | Sprint 5 |
| Yedekleme/geri yükleme | Sprint 6 | Sprint 6 |

---

## 8. Sprint 3'e devir

1. Görüşme sonucu (tamamlandı / gelmedi / ertelendi / iptal) ve kapatma akışı.
2. 30 dakika hareketsizlik kilidi ve kilit ekranı.
3. Kullanıcı yönetimi: rol atama, şifre sıfırlama, son yönetici koruması.
4. Hatırlatıcı motorunun arka plan zamanlayıcısıyla çalıştırılması (Sprint 4 hazırlığı).
5. Liste ekranında toplu işlemler ve klavye kısayolları (UAT geri bildirimi).
6. Açık ürün soruları (Q-03, Q-05, Q-10, Q-14, Q-15, Q-17) yanıtlanırsa ayarlara işlenmesi.
