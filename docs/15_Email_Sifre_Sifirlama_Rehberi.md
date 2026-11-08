# 15 — E-posta ile Şifre Sıfırlama ve "Beni Hatırla"

Bu rehber, kullanıcının yöneticiye ihtiyaç duymadan e-posta ile şifresini
sıfırlamasını ve "beni hatırla" ile otomatik giriş yapmasını anlatır.

---

## 1. "Beni Hatırla"

Giriş ekranındaki **Beni hatırla (30 gün)** kutusu işaretlenirse:

- Parola **saklanmaz**.
- 30 gün geçerli, rastgele bir oturum token'ı üretilir.
- Token yerel dosyada **Windows DPAPI** ile şifrelenir.
- Veritabanında yalnızca token'ın SHA-256 özeti tutulur.
- Sonraki açılışta uygulama otomatik giriş yapar.
- Çıkış yapılırsa, parola değiştirilirse veya yönetici parola sıfırlarsa token geçersiz olur.

Dosya: `%LocalAppData%\CgpRandevuTakip\remember.dat`

---

## 2. Kullanıcı e-postası

Şifre sıfırlama için kullanıcıda kayıtlı bir e-posta olmalıdır.

- Yönetici: **Kullanıcılar** → kullanıcıyı seç → **E-posta** alanını doldur → Kaydet.
- E-posta adresi benzersizdir.
- E-posta yoksa kullanıcı self-servis sıfırlama yapamaz; yönetici şifre sıfırlama fallback'i kullanılır.

---

## 3. SMTP ayarları (bir kez)

**Ayarlar → E-posta (şifre sıfırlama)**:

| Alan | Örnek |
|------|-------|
| SMTP sunucusu | `smtp.gmail.com` |
| Port | `587` |
| SSL/TLS | Açık |
| Kullanıcı adı | `kurum@gmail.com` |
| Şifre | Gmail **uygulama şifresi** |
| Gönderen adresi | `kurum@gmail.com` |
| Gönderen adı | `CGP - Randevu Takip` |

- Gmail kullanıyorsan normal parola değil, **uygulama şifresi** üret.
- SMTP parolası **DPAPI** ile şifreli saklanır.
- **Kaydet** → **Test e-postası gönder**.
- Test, kayıtlı ayarları kullanır; önce kaydetmen gerekir.

Kurumsal SMTP sunucunuz varsa host/port/kullanıcı bilgilerini BT biriminden alın.

---

## 4. Kullanıcı akışı

1. Giriş ekranı → **Şifremi unuttum**.
2. Kayıtlı e-posta adresini gir → **Kod gönder**.
3. E-postaya gelen **6 haneli kodu** gir.
4. Yeni şifreyi iki kez gir → **Şifreyi sıfırla**.
5. Başarılıysa yeni şifreyle giriş yap.

Kurallar:

- Kod **15 dakika** geçerlidir.
- Aynı kod için en fazla **5 deneme**.
- Yeni kod için **1 dakika** bekleme.
- Bilinmeyen e-posta adreslerinde hesap varlığı açıklanmaz.
- Parola değişince "beni hatırla" token'ı geçersiz olur.

---

## 5. Yönetici fallback

Kullanıcı e-postasını kaybetmiş, SMTP çalışmıyor veya kod gelmiyorsa:

- **Kullanıcılar** → kullanıcıyı seç → **Şifre sıfırla**.
- Yönetici yeni şifreyi belirler; kullanıcı ilk girişte değiştirebilir.

Bu nedenle yönetici hesabı ve e-posta erişimi güvenli tutulmalıdır.

---

## 6. KVKK / güvenlik notları

- E-posta gönderimi üçüncü taraf SMTP sunucusundan geçer; kurum değerlendirmesi yapılmalıdır.
- E-posta içeriğinde telefon/ad gibi gereksiz kişisel veri bulunmaz.
- SMTP parolası ve oturum token'ı düz metin saklanmaz.
- Şüpheli giriş denemeleri denetim kaydına yazılır.
