# 13 — Sprint 4.5 UAT ve Kapanış Raporu

**Sprint:** 4.5 · **Tarih:** 08.11.2026 · **Durum:** Tamamlandı
**Sürüm:** 0.5.3

---

## 1. Sprint hedefi

> "Sprint 4'ün hatırlatma, bildirim, push, tepsi ve otomatik başlangıç akışlarını
> gerçek ortamda doğrulamak; bulunan hataları kapatmak."

Hedef **karşılandı**. Gerçek ntfy ve Telegram push'ları alındı, Windows Action Center
toast'ı çalıştı, tepsi/otomatik başlangıç ve tek örnek davranışı doğrulandı.

---

## 2. Doğrulama matrisi

| # | Senaryo | Sonuç |
|---|---|---|
| 1 | ntfy testi (telefon) | ✅ Bildirim alındı |
| 2 | Telegram testi (bot) | ✅ Bildirim alındı |
| 3 | Windows Action Center toast | ✅ `TryShow` başarılı; toast gönderildi |
| 4 | 6 dk sonrasına randevu → 5 dk arama hatırlatıcısı | ✅ Toast + aksiyon penceresi açıldı |
| 5 | "Sadece kapat" aksiyonu | ✅ Pencere kapandı |
| 6 | Kapatınca tepside çalış | ✅ Süreç arkada kaldı |
| 7 | İkinci açılış | ✅ Yeni süreç kapandı, mevcut pencere öne geldi |
| 8 | `--tray` gizli başlangıç | ✅ Giriş penceresi açılmadı |
| 9 | İkinci örnek ile gizli başlangıçtan uyandırma | ✅ Giriş penceresi açıldı |
| 10 | Otomatik başlangıç kaydı | ✅ HKCU Run anahtarı yazıldı |
| 11 | Sessiz saat varsayılanı | ✅ 22:00–07:00 |
| 12 | Ayarların kalıcılığı | ✅ Push, sessiz saat, tray, auto-start |

> Otomatik başlangıcın gerçek Windows açılışında doğrulanması için yeniden başlatma
> adımı kullanıcı tarafından yapılacaktır.

---

## 3. Düzeltilen hatalar

| # | Belirti | Düzeltme |
|---|---|---|
| 1 | Sessiz saatler 08:30–08:30 olarak kaydediliyordu | `ReadTime` doğru ayar anahtarını kullanıyor |
| 2 | Kapatınca tepside çalış bazen uygulamayı kapatıyordu | Ana pencere `ShowDialog` yerine `Show` + `Closed` ile yönetiliyor |
| 3 | İkinci açılış yalnızca mesaj gösteriyordu | Named event ile mevcut pencere öne getiriliyor |
| 4 | İkinci süreç mesaj kutusunda kalıyordu | Yeni süreç sessizce kapanıyor |
| 5 | `--tray` gizli başlangıçta pencere yoktu | Tepsi + sinyal ile giriş akışı başlatılıyor |
| 6 | Hatırlatıcı aksiyonu sistem saatini kullanıyordu | `IClock` kullanılıyor |

---

## 4. Yeni özellikler

- **Test bildirimi gönder**: ntfy/Telegram için ayarlar ekranından anlık test.
- **Windows toast testi**: Action Center toast'ını doğrulama düğmesi.
- **Push kurulum rehberi**: `docs/14_Push_Kurulum_Rehberi.md`.
- **Windows Action Center toast**: `Microsoft.Toolkit.Uwp.Notifications`; başarısızsa
  uygulama içi topmost toast.
- **Toast tıklaması**: uygulamayı öne getirir.

---

## 5. Testler

| Alan | Test sayısı |
|------|-------------|
| Domain | 41 |
| Application | 118 |
| Infrastructure | 7 |
| **Toplam** | **166** |

Yeni testler:

- `SettingsServiceTests.Save_PersistsQuietHoursAndAppBehavior`
- `SettingsServiceTests.Save_InvalidQuietHours_Fails`
- Varsayılan sessiz saat/tray/auto-start doğrulamaları

`dotnet build` → 0 uyarı / 0 hata.

---

## 6. Kalan manuel adım

1. Bilgisayarı yeniden başlat.
2. Otomatik başlangıç moduna göre:
   - **Giriş ekranı açık**: giriş penceresi gelmeli.
   - **Tepside gizli**: pencere gelmemeli, tepsi simgesi görünmeli.
3. Tepsi → Aç ile giriş ekranını doğrula.

---

## 7. Sprint 5'e devir

1. WhatsApp PNG + pano + WhatsApp açma.
2. Alıcı rehberi ve günlük gönderim saati.
3. OS toast tıklama argümanlarıyla doğrudan aksiyon penceresi açma (opsiyonel iyileştirme).

