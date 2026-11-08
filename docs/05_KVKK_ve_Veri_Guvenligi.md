# 05 — KVKK ve Veri Güvenliği

**Sürüm:** 0.1 · **Tarih:** 06.11.2026
**Not:** Bu doküman teknik bir uyum çerçevesidir; hukuki görüş yerine geçmez.
Kurumun KVKK sorumlusu/hukukçu onayı alınmalıdır.

---

## 1. Veri envanteri

| Veri | Kategori | Kaynak | Amaç | Saklama |
|------|----------|--------|------|---------|
| Ad, soyad | Kişisel | Sekreter girişi | Randevu tanımlama | 2 yıl (varsayılan) |
| Telefon | Kişisel / iletişim | Sekreter girişi | Arama, WhatsApp, hatırlatma | 2 yıl |
| E-posta (opsiyonel) | Kişisel / iletişim | Sekreter girişi | İletişim | 2 yıl |
| Kurum/birim (opsiyonel) | Kişisel / mesleki | Sekreter girişi | Bağlam | 2 yıl |
| Öğrenci ad-soyadı (opsiyonel) | Kişisel (çocuk olabilir) | Sekreter girişi | Görüşme bağlamı | 2 yıl |
| Görüşme açıklaması | Kişisel / hassas olabilir | Sekreter/görüşmeci | Görüşmenin içeriği | 2 yıl |
| Görüşme sonucu ve notları | Kişisel / hassas olabilir | Görüşmeci | Takip, kalite | 2 yıl |
| Görüşme saati, host, kategori | Kişisel / işlem | Sistem | Planlama | 2 yıl |
| Kullanıcı adı, şifre özeti | Kimlik | Yönetici | Erişim kontrolü | Hesap aktif oldukça |
| Denetim kaydı | İşlem güvenliği | Sistem | İzlenebilirlik | 2 yıl |
| Push topic/token | Teknik | Ayarlar | Bildirim | Ayar silinene kadar |
| Yedek dosyaları | Türetilmiş kişisel veri | Sistem | Felaket kurtarma | 30 yedek / 2 yıl |

> **Hassas veri uyarısı:** Görüşme açıklaması sağlık, din, etnik köken gibi özel
> nitelikli veri içerebilir. Arayüzde "hassas bilgi yazmayın" uyarısı ve
> kullanım politikası bulunmalı; gerekiyorsa açıklama alanı şifrelenmeli (Faz 2).

---

## 2. İşleme amaçları ve hukuki dayanak

| Amaç | Dayanak (KVKK m.5) | Not |
|------|--------------------|-----|
| Randevu oluşturma ve yürütme | Sözleşmenin kurulması/ifası veya meşru menfaat | Kurum türüne göre hukukçu değerlendirmeli |
| Görüşme sonucu takibi | Meşru menfaat / hukuki yükümlülük | Kalite ve savunma hakkı |
| Hatırlatma bildirimi (WhatsApp/push) | Açık rıza veya meşru menfaat | Pazarlama değil, işlem bildirimi |
| Yedekleme | Veri güvenliği (m.12) | Teknik zorunluluk |
| Denetim kaydı | Hukuki yükümlülük / güvenlik | Erişim izleme |

**Aydınlatma yükümlülüğü:** Görüşme kaydı alınmadan önce kişiye; verinin ne için
işlendiği, kimlere aktarılabileceği, saklama süresi ve hakları yazılı/sözlü
bildirilmelidir. Uygulamada "Aydınlatma metni" bağlantısı (Ayarlar → Hakkında)
bulunur; kurum kendi metnini ekler.

---

## 3. Üçüncü taraflar ve aktarımlar

| Taraf | Veri | Aktarım | Önlem |
|-------|------|---------|-------|
| WhatsApp (Meta) | Ad, telefon, görüşme özeti, liste görseli | Kullanıcı manuel gönderir | Yalnızca kurum içi alıcılar; hassas not gönderilmez |
| ntfy.sh | Bildirim başlığı/gövdesi (varsayılan isimsiz) | HTTPS | Tahmin edilemez topic; isim varsayılan kapalı |
| Telegram (opsiyonel) | Aynı | HTTPS | Bot token DPAPI; özel sohbet |
| Google Drive (Faz 2) | Şifreli/şifresiz yedek | OAuth 2.0 | `drive.file` kapsamı; kullanıcı onayı; mümkünse parola şifreleme |
| Microsoft (Windows bildirim) | Bildirim metni | Yerel | Kilit ekranında gizle |

**Yurt dışı aktarım:** ntfy/Telegram/Google sunucuları yurt dışında olabilir.
Kurum, KVKK m.9 kapsamında değerlendirme yapmalı; gerekirse bu özellikler
kapatılmalı ve yalnızca yerel bildirim kullanılmalıdır. Uygulamada push
**varsayılan kapalı** sunulmalı, açılırken açık bilgilendirme gösterilmelidir.

---

## 4. Saklama ve imha

- Varsayılan saklama: **2 yıl** (ayarlanabilir).
- Süre sonunda: kişi kimliği anonimleştirilir (ad/telefon silinir), istatistik
  kaydı kalabilir; veya kayıt tamamen silinir (kurum politikası).
- Silme işi arka planda, denetim kaydı üreterek çalışır.
- Yedeklerdeki eski kayıtlar, yedek saklama süresi (30 yedek) sonunda imha edilir.
- Uygulama kaldırılırken kullanıcıya "verileri de sil" seçeneği sunulur.

---

## 5. Veri sahibi hakları (KVKK m.11)

| Hak | Uygulamada karşılığı |
|-----|----------------------|
| Bilgi talep etme | Kişi arama → detay → dışa aktarma (Faz 2) |
| Düzeltme | Görüşme düzenleme |
| Silme | Yönetici kalıcı silme / anonimleştirme |
| İtiraz | Kayıt kapatma, iletişim tercihi |
| Şikâyet | Kurum KVKK başvuru kanalı |

Başvuru süreci: kimlik doğrulama → 30 gün içinde yanıt → işlem denetim kaydı.
Uygulama, kişi bazlı "veri paketi" dışa aktarma özelliğini Faz 2'de alacaktır.

---

## 6. Teknik ve idari tedbirler

### 6.1 Teknik

- Erişim kontrolü: kullanıcı adı/şifre, rol, oturum kilidi.
- Şifreler PBKDF2-SHA256 ile geri döndürülemez saklanır.
- Loglarda PII redaksiyonu; hata mesajlarında kişi bilgisi yok.
- Yedeklerde SHA-256 bütünlük; Faz 2'de AES-256-GCM şifreleme.
- Denetim kaydı append-only.
- Windows DPAPI ile sır koruma.
- Tek örnek + oturum kilidi ile yetkisiz erişim azaltılır.
- Güncelleme paketleri imzalanabilir yapıda.

### 6.2 İdari

- Kullanıcı yetkileri en az yetki ilkesiyle verilir.
- Kurum bilgisayarında ayrı Windows hesabı; ekran kilidi zorunlu.
- BitLocker/disk şifreleme önerilir (uygulama zorlayamaz).
- Yedeklerin erişimi kısıtlı klasörde; fiziksel ortamda kilitli.
- Yıllık erişim gözden geçirmesi ve yedek geri yükleme tatbikatı.
- Alt işleyen sözleşmeleri (varsa) ve aydınlatma metni güncel tutulur.

---

## 7. Veri ihlali müdahale planı

1. **Tespit:** Anormal erişim, kayıp cihaz, yedek sızıntısı, log anomalisi.
2. **Sınırlama:** Hesapları kilitle, ağ bağlantısını kes, etkilenen yedeği izole et.
3. **Değerlendirme:** Hangi veri, kaç kişi, hassas mı, aktarım var mı?
4. **Bildirim:** Kurum KVKK sorumlusu → 72 saat içinde Kurul'a (gerekiyorsa).
5. **Veri sahiplerine bildirim:** Gerekiyorsa makul sürede.
6. **Kök neden:** Düzeltici aksiyon, test, doküman güncellemesi.
7. **Kayıt:** Olay dosyası, alınan kararlar, süreler.

---

## 8. Yapılacaklar (uygulama öncesi)

- [ ] Kurumun aydınlatma metni ve saklama politikası yazılı hâle getirilecek
- [ ] Hangi hukuki dayanağın geçerli olduğu hukukçuya doğrulatılacak
- [ ] Push/Google Drive kullanılıp kullanılmayacağına karar verilecek
- [ ] Veri işleyen/aktarım envanteri onaylanacak
- [ ] Kullanıcı yetki matrisi kurum yöneticisine imzalatılacak
- [ ] Yedeklerin fiziksel/şifreli saklama yöntemi belirlenecek
- [ ] Yıllık tatbikat ve eğitim planı takvime alınacak

---

## 9. Tasarım kararlarına yansıması

| Karar | KVKK etkisi |
|-------|-------------|
| Push varsayılan kapalı, isimsiz | Veri minimizasyonu, yurt dışı aktarım riski azalır |
| WhatsApp yarı otomatik | Uygulama otomatik aktarım yapmaz; kullanıcı kontrolü |
| Yedek şifreleme Faz 2 | Faz 1'de NTFS + fiziksel önlem; risk kabulü belgelenmeli |
| Denetim kaydı | Hesap verebilirlik |
| 2 yıl saklama | Amaçla sınırlılık |
| Kalıcı silme yöneticide | Yetki suistimalini azaltır |
