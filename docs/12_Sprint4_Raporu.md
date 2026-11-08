# 12 — Sprint 4 Sonuç Raporu

**Sprint:** 4 · **Tarih:** 08.11.2026 · **Durum:** Tamamlandı
**Sürüm:** 0.5.0

---

## 1. Sprint hedefi

> "Hatırlatma vaadi: randevudan önce Windows bildirimi ve aksiyon penceresi göster,
> telefona isimsiz push gönder, uygulama kapalıyken kaçırılanları açılışta telafi et."

Hedef **karşılandı**. Zamanı gelen hatırlatıcılar arka plan motoruyla işleniyor,
30/5 dakika "sırası gelen" arama bildirimi üretiliyor, 4 seçenekli aksiyon penceresi
çalışıyor, push (ntfy/Telegram), tepsi, otomatik başlangıç ve sessiz saatler hazır.

---

## 2. Tamamlanan çıktılar

| # | Çıktı | Kanıt |
|---|-------|-------|
| 1 | `ReminderKind` (Appointment / Call) ve migration | `Sprint4_ReminderKind` |
| 2 | Hatırlatıcı motoru | `ReminderDispatcher`, `ReminderService` |
| 3 | Due sorgusu ve kanal timestamp güncellemeleri | `AppointmentRepository` |
| 4 | 30/5 dk "sırası gelen" arama hatırlatıcısı | `ReminderScheduleCalculator.CalculateCallReminders` |
| 5 | Windows bildirimi (topmost toast) | `ReminderToastWindow` |
| 6 | 4 seçenekli aksiyon penceresi | `ReminderActionWindow`, `ReminderActionViewModel` |
| 7 | Push: ntfy + Telegram | `PushNotifier` |
| 8 | DPAPI sır koruması | `DpapiSecretProtector`, `ISecretProtector` |
| 9 | Sessiz saatler (kullanıcı seçimli) | `NotificationPolicy` + ayarlar |
| 10 | Tepsi + kapatınca tepside çalış | `TrayService` |
| 11 | Otomatik başlangıç | `AutoStartService` |
| 12 | Kilit/giriş durumunda PII gizleme | `SessionStateService` |
| 13 | 18 yeni otomatik test (toplam 164) | `dotnet test` → 164/164 |

---

## 3. Uygulama kararları

- Hatırlatıcılar `Kind` ile ayrışır; 5 dk arama hatırlatıcısı her zaman planlanır,
  30 dk randevu hatırlatıcısı varsa 30 dk arama bildirimi oluşturulmaz (D-42).
- Motor 15 saniyede bir due tarar; batch 50.
- Tekrar: toplam 3 gösterim; aksiyon alınmazsa `Unanswered` (D-45).
- Snooze 10 dk erteler, otomatik tekrar sayılmaz.
- Geçmiş randevular için bildirim gösterilmez; hatırlatıcı `Missed` olur.
- Kanal bazlı timestamp (`WindowsNotifiedAtUtc`, `PushNotifiedAtUtc`) aynı hatırlatıcının
  iki kez gönderilmesini engeller.
- Aksiyon penceresi kimlik doğrulanmış ve kilitli olmayan oturumda açılır; aksi hâlde
  yalnızca PII'siz bildirim gösterilir (D-43).
- Windows bildirimi uygulama içi topmost toast olarak gerçekleşir; OS Action Center
  entegrasyonu sonraki spike'a bırakıldı (D-44).
- Push varsayılan kapalı ve isimsizdir; sessiz saatte gönderilmez.
- Sessiz saat varsayılanı 22:00–07:00, kullanıcı değiştirebilir (D-46).
- Otomatik başlangıç varsayılanı "giriş ekranı açık"; "tepside gizli" de seçilebilir (D-47).
- Telegram token ve chat id DPAPI ile şifreli saklanır.

---

## 4. Doğrulama

| Kontrol | Sonuç |
|---|---|
| `dotnet build` (Debug) | 0 uyarı / 0 hata |
| `dotnet test` | 164/164 başarılı |
| UI smoke (6 sayfa) | Açıldı, çökme yok |
| Uçtan uca hatırlatıcı | 6 dk sonrasına randevu → 5 dk arama bildirimi + aksiyon penceresi göründü; "Sadece kapat" çalıştı |

Uçtan uca doğrulama: giriş → yeni görüşme (6 dk sonra) → 5 dk arama hatırlatıcısı due
olduğunda aksiyon penceresi otomatik açıldı.

---

## 5. Testler

| Alan | Test sayısı |
|------|-------------|
| Domain | 41 |
| Application | 116 |
| Infrastructure | 7 |
| **Toplam** | **164** |

Yeni test dosyaları:

- `ReminderServiceTests` — due seçimi, aksiyonlar, snooze, tekrar, telafi.
- `NotificationPolicyTests` — sessiz saat, gece yarığı, kapalı.
- `ReminderScheduleCalculatorTests` — call reminder ve dedup key ekleri.

---

## 6. Bilinçli olarak ertelenenler

| Konu | Gerekçe | Hedef |
|------|---------|-------|
| OS Action Center toast (paketli bildirim) | **0.5.2'de çözüldü**; başarısız olursa uygulama içi topmost yedek çalışır | Tamamlandı |
| Telegram push manuel doğrulaması | Gerçek bot/token gerekir | UAT |
| Sessiz saatte biriken bildirimlerin toplu özeti | Kapsam genişletme | Faz 2 |
| Tepsi simgesi markalı ikon | Şimdilik sistem ikonu | Sonraki sprint |

---

## 7. Sprint 5'e devir

1. WhatsApp paylaşımı: günlük liste PNG, panoya kopyalama, WhatsApp açma.
2. Alıcı rehberi ve çoklu seçim.
3. Günlük gönderim saati ve paylaşım logu.
4. OS Action Center toast spike ve gerçek WhatsApp yapıştırma doğrulaması.
