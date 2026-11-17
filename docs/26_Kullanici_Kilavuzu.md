# 26 — Kullanım Kılavuzu (Son Kullanıcı)

**Sürüm:** 1.6.0

## 1. İlk kurulum

1. Setup EXE’yi çalıştırın (yönetici hakkı gerekmez) veya taşınabilir ZIP’i bir klasöre açın.
2. Kurulum sihirbazı açılır:
   - **Kurum bilgileri:** ad, e-posta, telefon, adres zorunlu; logo isteğe bağlı.
   - **Yönetici hesabı:** ad soyad, kullanıcı adı, e-posta, şifre.
   - **Görünüm ve tercihler:** tema, hatırlatıcı, çalışma saatleri, başlangıç modu.
   - **E-posta (SMTP), telefon bildirimleri, yedekleme:** isteğe bağlı; “Atla” ile geçilebilir.
3. “Kurulumu tamamla” ile ayarlar kaydedilir ve oturum açılır.

> Sihirbazı daha sonra **Ayarlar > Araçlar > Kurulum sihirbazını yeniden çalıştır** ile açabilirsiniz.

## 2. Günlük akış

- Açılışta **Günlük Liste** gelir. Tarih okları veya takvimle gün seçilir.
- **Yeni görüşme:** kişi, telefon, görüşmeci, kategori, saat ve yöntem girilir.
- Görüşme sonucu satır detayından girilir: tamamlandı, gelmedi, ertelendi, iptal.
- **Paylaş:** günlük liste PNG olarak üretilir; panoya kopyalanır ve WhatsApp açılır.
- **Alıcı rehberi** ile günlük listeyi kurum içi kişilere hazırlayabilirsiniz.
- Arama kutusu ad, telefon, kategori veya görüşmeciye göre arar.

### 2.1 Raporlar

- Soldaki menüden **Raporlar** açılır; tüm roller erişebilir.
- **Görüşmeci** rolü yalnızca kendi görüşmelerini görür; yönetici ve sekreter tümünü görür.
- **Bu ay / Geçen ay / Bu yıl / Son 30 gün** hazır aralıkları veya başlangıç-bitiş tarihi seçilir; **Uygula**'ya basılır (bitiş günü dahil).
- Üstteki kartlar toplam görüşme, tamamlanma, gelmedi ve iptal oranları ile ortalama/toplam süreyi gösterir.
- Grafik 31 güne kadar günlük, daha uzun aralıkta aylık dağılım verir.
- Kategori ve görüşmeci tabloları ile saat yoğunluğu alt bölümlerde yer alır.
- **Rapor türü** seçicisiyle görüşmeci performansı, kategori trendi, katılım, tekrar görüşme, kişi geçmişi ve denetim raporları açılır.
- **Kişi geçmişi** raporunda ad veya telefonla arama yapılır.
- **Excel**, **CSV** ve **PDF** düğmeleriyle rapor kaydedilir. Excel'de rapor türüne özel sayfalar bulunur; CSV, Excel'de Türkçe karakterler bozulmadan açılır.
- Silinmiş görüşmeler rapora girmez.

### 2.2 Tekrarlayan görüşme

- **Yeni görüşme** penceresinde "Bu görüşmeyi seri olarak planla" işaretlenir.
- Sıklık (her hafta / 2 haftada bir / her ay) ve kayıt sayısı (2–12) seçilir.
- **Önizle** her tarih için `Oluşturulacak`, `Çakışıyor` veya `Geçmiş` durumunu gösterir.
- Kaydet'te çakışan/geçmiş tarihler atlanır; kaç görüşme oluşturulacağı onaylatılır.
- Aylık seride gün korunur; ay kısa ise ay sonuna kırpılır (31 Ocak → 28 Şubat → 31 Mart).
- Detay penceresinde seri `Seri 2/6` rozetiyle görünür; **Seri yönetimi** ile seriye yeni kayıt eklenir veya görüşmeci/kategori/süre/saat gibi ortak alanlar güncellenir. Düzenleme yalnızca gelecek tarihli planlanmış kayıtları etkiler.
- **Seriyi iptal et** yalnızca gelecek tarihli ve hâlâ planlanmış kayıtları iptal eder. Geçmiş ve kapanmış kayıtlar korunur.
- Seri yalnızca **yeni** görüşme eklerken önerilir; mevcut bir görüşmeyi düzenlerken seri paneli görünmez.

## 3. Bildirimler

- Windows bildirimi her zaman çalışır; uygulama tepside olsa da hatırlatır.
- Telefon bildirimi için Ayarlar > Bildirimler:
  - **ntfy:** uygulamayı kurun, topic’e abone olun, aynı topic’i girin.
  - **Telegram:** @BotFather ile bot oluşturun, token ve sayısal chat id girin, bota `/start` yazın.
- “Test bildirimi gönder” ile doğrulayın.
- **Sessiz saatlerde** push gönderilmez.

## 4. Yedekleme

- Otomatik yedek günde iki kez alınır.
- Ayarlar > Yedekleme: klasör, şifreleme ve Google Drive for Desktop klasörü.
- Şifreleme açıksa parola en az 8 karakterdir ve bu bilgisayara bağlı saklanır.
- **Yedekleme** sayfasından yedekleri listeleyip geri yükleyebilirsiniz.
- Şifreli yedeğin parolasını kaybetmeyin; geri yüklenemez.

## 5. Oturum ve güvenlik

- Oturum, siz **Çıkış** yapana kadar açık kalır.
- **Kilitle** düğmesi şifre isteyen kilit ekranını açar; kilit uygulama yeniden başlasa da korunur.
- “Beni hatırla” yalnızca çıkış sonrası giriş formunun ön doldurulmasını yönetir; şifre değişince güncellenir.
- Aynı Windows hesabını kullanan herkes uygulamaya erişebilir; Windows şifresi ve BitLocker önerilir.

## 6. Güncelleme

- **Ayarlar > Güncelleme > Kontrol et** yeni sürümü denetler.
- "Güncelle" paketi indirir, **SHA-256** ile doğrular, önceki sürümün kopyasını saklar ve kurar.
- İndirme sırasında yüzdelik ilerleme penceresi açılır; indirilen/toplam boyut, doğrulama,
  paket açma ve kurulum başlatma adımları gösterilir.
- Zorunlu güncellemede aynı ilerleme penceresi giriş öncesinde açılır; kullanıcı güncellemeden
  devam edemez.
- Kurulum tamamlanamazsa uygulama açılışta otomatik olarak önceki sürüme döner.
- **Önceki sürüme dön** düğmesiyle elle geri dönülebilir.
- **Güncelleme günlüğü** düğmesi adımları ve hataları gösterir; **Sürüm notları** yenilikleri gösterir.
- Ara sürümlerde yalnızca Setup EXE yayınlanır; taşınabilir ZIP kararlı sürümlerde çıkar.

## 6.1 Kaldırma

- Windows **Ayarlar > Uygulamalar** üzerinden veya Başlat menüsünden kaldırın.
- Program dosyaları, kısayollar, başlangıç kaydı ve bildirim kayıtları temizlenir.
- Kaldırma sırasında **“Uygulama verileri de silinsin mi?”** sorulur; varsayılan **Hayır**’dır.
- **Yedekleriniz (`Belgeler\CgpRandevuTakip`) hiçbir durumda silinmez.**
- Sessiz kaldırmada veriler korunur.

## 7. Sorun giderme

1. Ayarlar > Araçlar > **Tanılama paketi oluştur**: masaüstüne kişisel veri içermeyen ZIP kaydeder.
2. **Güncelleme günlüğü** ve **Veri klasörünü aç** ile dosyaları inceleyin.
3. Destek alırken tanılama ZIP’ini ve hatanın ekran görüntüsünü paylaşın.

## 8. Kısayollar ve ipuçları

- `Enter` sihirbazda ilerler; `Esc` yerine pencereyi kapatmadan önce onay istenir.
- Görüşme listesinde çoklu seçimle **Taşı** ve **Sil** işlemleri yapılabilir.
- Silinen görüşmeler 30 gün içinde geri alınabilir.
- Excel’den içe aktarma ve dışa aktarma Araçlar menüsündedir.
