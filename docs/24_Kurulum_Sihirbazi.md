# 24 — Kurulum Sihirbazı ve Kurum Profili

**Sürüm:** 1.4.0

## 1. Amaç

İlk kurulumda kurum bilgilerini, yönetici hesabını ve isteğe bağlı entegrasyonları tek akışta
toplamak; aynı sihirbazı daha sonra Ayarlar’dan **güncelleme modunda** yeniden çalıştırmak.

## 2. Adımlar

| # | Adım | Zorunlu | Alanlar |
|---|------|:------:|---------|
| 0 | Hoş geldiniz | – | Tanıtım, adım listesi, veri notu |
| 1 | Kurum bilgileri | ✔ | Kurum adı, kurum e-posta, telefon, adres, logo (ops.) |
| 2 | Yönetici hesabı | ✔ | Ad soyad, kullanıcı adı, e-posta, şifre, şifre tekrar |
| 3 | Görünüm ve tercihler | ✔ (varsayılanlarla geçilir) | Tema (canlı), hatırlatıcı, çalışma saatleri, günlük paylaşım saati, otomatik başlatma, tepsi |
| 4 | E-posta / SMTP | ✖ atlanabilir | Sunucu, port, SSL, kullanıcı, parola, gönderen adı/adresi, test gönderimi |
| 5 | Telefon bildirimleri | ✖ atlanabilir | Push aç/kapat, ntfy veya Telegram, test, sessiz saatler |
| 6 | Yedekleme | ✖ atlanabilir | Klasör, otomatik yedek, şifreleme, Drive for Desktop klasörü |
| 7 | Özet ve tamamla | ✔ | Tüm değerler, “atlandı” etiketleri |

### Doğrulama

- Kurum adı 1–120 karakter; kurum e-posta geçerli; telefon 10–13 rakam; adres 10–250 karakter.
- Yönetici e-postası zorunlu ve geçerli; kullanıcı adı/şifre mevcut politikalarla aynı.
- SMTP: sunucu girildiyse gönderen adresi zorunlu; port 1–65535; kullanıcı adı varsa parola zorunlu.
- Push: ntfy için topic; Telegram için bot token ve sayısal chat id.
- Yedekleme: şifreleme açıksa en az 8 karakter parola ve eşleşen doğrulama; Drive için şifreleme zorunlu.

Doğrulama kuralları `Application/Settings/SetupValidator.cs` içinde tutulur ve birim testleriyle
doğrulanır; WPF katmanı yalnızca adım akışını yönetir.

## 3. Tamamlama sırası

1. Tüm adımlar yeniden doğrulanır; hata varsa ilgili adıma dönülür.
2. Logo bekleyen dosya varsa veri klasörüne kopyalanır (`branding/institution-logo.<ext>`).
3. İlk kurulumda `CreateFirstAdminAsync` yöneticiyi oluşturur ve oturum açar.
4. Güncelleme modunda yönetici e-postası güncellenir; “Şifremi değiştir” işaretliyse
   mevcut şifre doğrulanarak yeni şifre kaydedilir ve **yeni süresiz oturum token’ı** üretilir.
5. `SettingsService.SaveAsync` tüm ayarları tek seferde yazar.
6. Tema uygulanır, otomatik başlatma yapılandırılır, arayüze değişiklik mesajları gönderilir.

## 4. Güncelleme modu

- Ayarlar > Araçlar > **Kurulum sihirbazını yeniden çalıştır**.
- Yönetici hesabı adımında ad/kullanıcı adı salt okunur; e-posta düzenlenebilir.
- Şifre değişimi isteğe bağlıdır; boş bırakılırsa değişmez.
- Tema canlı uygulanır; pencere kapatılırsa önceki temaya dönülür.
- “Atla” adımlarında mevcut değerler korunur.

## 5. Kurum logosu

- Sihirbazın kurum adımında ve Ayarlar > Görünüm ve genel bölümünde seçilebilir.
- PNG, JPG veya BMP; en fazla 4 MB.
- Veri kökünde `branding/` altında saklanır; ayarlarda göreli yol tutulur (taşınabilir mod uyumlu).
- Günlük WhatsApp liste PNG’sinde en üstte, kurum adının üzerinde ~56 px yükseklikte çizilir.
- Logo yoksa veya okunamazsa liste görseli logosuz üretilir.

## 6. 1.4.2 davranışları

- **Kurum e-postası → SMTP:** Adım 1’de girilen adres, gönderen adresi/test alıcısı boşsa otomatik doldurulur. SMTP adımı atlansa bile kaydetmede gönderen adresi boşsa kurum e-postası kullanılır.
- **Başlangıç modu:** “Windows açılışında başlat” açıksa “Gizli başlat (tepside)” veya “Giriş ekranı açık” seçilebilir; mevcut seçim korunur.
- **Atla:** Atlanan adımda girilen yarım değerler kaydedilmez, mevcut ayarlar korunur; kenar çubuğunda “Atlandı”, özette “Atlandı” görünür.
- **Şifre değişimi:** Güncelleme modunda şifre değişirse yeni süresiz oturum token’ı üretilir ve “Beni hatırla” kaydı yeni şifreyle güncellenir.
- **Kapatma onayı:** Sihirbaz X ile kapatılırken değişikliklerin kaybolacağı bildirilir ve onay istenir.
- **Ayarlar:** Kurum adı, e-posta, telefon ve adres Ayarlar kaydında da zorunludur.
- **Yetki:** Kurulum sihirbazını yeniden çalıştırma yalnızca yöneticide etkindir.
