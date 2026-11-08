# 14 — Push Kurulum Rehberi (ntfy / Telegram)

Bu rehber, CGP - Randevu Takip uygulamasının telefona bildirim göndermesi için
ntfy veya Telegram kurulumunu anlatır. Push **varsayılan olarak kapalıdır** ve
**isimsiz** çalışır (saat + “randevu” bilgisi).

> Token, topic ve chat id gizli bilgilerdir. Paylaşmayın; yalnızca uygulama
> ayarlarına girin. Telegram token/chat id DPAPI ile şifreli saklanır.

---

## 1. ntfy (önerilen)

### Telefon
1. Android: Google Play → **ntfy**; iPhone: App Store → **ntfy**.
2. Uygulamayı aç → **+** → **Subscribe to topic**.
3. Uzun ve tahmin edilmesi zor bir topic gir (ör. `cgp-randevu-7f3k9m2q`).

### Bilgisayarda test
```powershell
$topic = "SENIN_TOPIC"
Invoke-RestMethod -Method Post `
  -Uri "https://ntfy.sh/$topic" `
  -Headers @{ Title = "CGP Test" } `
  -Body "Test bildirimi"
```

### Uygulama ayarları
Ayarlar → Bildirimler:
- Telefon push bildirimi açık
- Push sağlayıcısı: `ntfy`
- Push konusu / topic: aynı topic
- **Test bildirimi gönder** ile doğrula
- Kaydet

---

## 2. Telegram

### Bot oluşturma
1. Telegram → **@BotFather**
2. `/newbot`
3. Bot adı ve `_bot` ile biten kullanıcı adı seç.
4. Verilen **token**’ı not al. Kimseyle paylaşma.

### Chat ID
1. Kendi botunu aç → **Start** / `/start` gönder.
2. Tarayıcıda:
   ```text
   https://api.telegram.org/bot<TOKEN>/getUpdates
   ```
3. JSON içinde `message.chat.id` sayısını bul. Örnek: `339378900`.
   - Grup kullanıyorsan genelde negatif sayıdır.

### Bilgisayarda test
```powershell
$token = "BOT_TOKEN"
$chat  = "CHAT_ID"
Invoke-RestMethod -Uri "https://api.telegram.org/bot$token/sendMessage?chat_id=$chat&text=CGP%20test"
```

### Uygulama ayarları
Ayarlar → Bildirimler:
- Telefon push bildirimi açık
- Push sağlayıcısı: `telegram`
- Bot token: token
- Sohbet kimliği: sayısal chat id
- **Test bildirimi gönder**
- Kaydet

> Chat id bot kullanıcı adı değildir; sayısal olmalıdır. Bota `/start`
> gönderilmeden bot size mesaj atamaz.

---

## 3. Sessiz saatler

Ayarlar → Bildirimler → Sessiz saatler:
- Varsayılan: **22:00 – 07:00**
- Bu aralıkta push gönderilmez; Windows bildirimi sessiz gösterilir.
- Gece yarığını aşan aralıklar desteklenir.

---

## 4. Sorun giderme

| Belirti | Kontrol |
|---|---|
| ntfy gelmiyor | Topic aynı mı, telefon abone mi, internet var mı? |
| Telegram gelmiyor | Chat id sayısal mı, bota `/start` gönderildi mi, token doğru mu? |
| Bildirim hiç gelmiyor | Push açık mı, sessiz saatte miyiz, uygulama tepside çalışıyor mu? |
| Windows toast yok | Bildirim izni ve Odak Yardımı; uygulama içi toast yedek olarak çalışır |
| Test başarısız | Ayarları kaydetmeden test, formdaki güncel değerleri kullanır |

---

## 5. KVKK notu

- Push varsayılan olarak isimsizdir; kişi adı gönderilmez.
- ntfy/Telegram sunucuları yurt dışında olabilir; kurum KVKK değerlendirmesi yapmalıdır.
- Gerekmiyorsa push kapalı bırakılabilir; yerel Windows bildirimi yeterlidir.
