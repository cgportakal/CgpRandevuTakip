# Değişiklik Günlüğü

Tüm önemli değişiklikler bu dosyada tutulur. Biçim: [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/),
sürümleme: [SemVer](https://semver.org/lang/tr/).

## [1.4.1] — 2026-11-14 · Kurulum sihirbazı rötuşları

### Düzeltildi
- "Atla" ile geçilen adımlar kenar çubuğunda **Atlandı** olarak işaretlenir; o adımda girilen yarım değerler kaydedilmez, mevcut ayarlar korunur.
- Özet ekranı atlanan SMTP, telefon bildirimi ve yedekleme adımlarını değerlerden bağımsız olarak **Atlandı** gösterir.
- Ayarlar ekranında kurum e-posta ve telefon alanları tam genişliğe alındı; uzun e-posta adresleri kırpılmıyor.
- EXE dosya özelliklerinde sürüm bilgisi `1.0.0` yerine **1.4.1** olarak görünüyor (`Directory.Build.props`).

### Test
- 269/269 test.
- SMTP adımına değer girip "Atla" ile geçme senaryosu uçtan uca doğrulandı; değerlerin kaydedilmediği veritabanından kontrol edildi.

## [1.4.0] — 2026-11-13 · Kurulum sihirbazı ve kurum logosu

### Eklendi
- **8 adımlı kurulum sihirbazı:** Hoş geldiniz → Kurum bilgileri → Yönetici hesabı → Görünüm/tercihler → E-posta (SMTP) → Telefon bildirimleri → Yedekleme → Özet. Zorunlu alanlar tamamlanmadan ilerlenemez; SMTP, push ve yedekleme adımları atlanabilir.
- **Kurum profili:** Kurum adı, e-posta, telefon ve adres zorunlu; wizard ve Ayarlar aynı doğrulamayı kullanır.
- **Yönetici e-postası zorunlu:** Şifre sıfırlama ve bilgilendirme için kullanılır; ilk yönetici oluşturulurken kaydedilir.
- **Canlı tema:** Tema seçildiği anda uygulanır; sihirbaz iptal edilirse önceki temaya dönülür.
- **Ayarlar’dan yeniden çalıştırma:** Sihirbaz güncelleme modunda açılır; yönetici e-postası ve isteğe bağlı olarak şifresi değiştirilebilir, şifre değişiminde oturum korunur.
- **Kurum logosu:** Sihirbazda ve Ayarlar’da seçilebilir/kaldırılabilir; veri klasöründe saklanır ve günlük WhatsApp liste PNG’sinin en üstünde görünür.
- **Canlı durum ekranları:** Eski sprint notları kaldırıldı; kontrol panelinde son yedek, bildirim durumu, veri klasörü ve sürüm bilgisi gösterilir. Ana pencere, giriş ve hatırlatıcı önizlemesi güncel metinlere kavuştu.

### Test
- 269/269 test.
- Temiz veriyle ilk kurulum, opsiyonel adımların atlanması, özet ve tamamlama doğrulandı.
- Güncelleme modu, e-posta değişimi, canlı tema ve iptalde tema geri dönüşü doğrulandı.
- Logo seçimi, kalıcı kayıt, kaldırma ve günlük liste PNG’sinde logo görünümü doğrulandı.

## [1.3.0] — 2026-11-13 · Kalıcı oturum ve arka plan bildirimleri

### Eklendi
- **Süresiz oturum:** Başarılı girişten sonra oturum, açık çıkış yapılana kadar geçerli. Token süresi yok; uygulama ve bilgisayar yeniden başlasa da otomatik giriş yapılır.
- **Çıkış:** Kalıcı token iptal edilir; sonraki açılışta giriş formu gösterilir.
- **Şifre değişimi/sıfırlama:** Kalıcı oturum token'ı geçersiz olur.
- **“Beni hatırla”:** Kutu korunur ve varsayılan işaretlidir. Süresiz oturum her durumda geçerlidir; kutu yalnızca çıkış sonrası kullanıcı adı/şifre ön doldurmayı yönetir.
- **Otomatik kilit kaldırıldı:** 30 dakika hareketsizlikte şifre sorulmaz.
- **Elle kilitleme:** Kenar çubuğunda “Kilitle” düğmesi. Şifre ile açılır; kilit durumu uygulama/PC yeniden başlasa da korunur.
- **Arka plan bildirimleri:** Windows açılışında gizli (`--tray`) başlatma. Kurulu sürümde HKCU Run, taşınabilir sürümde Başlangıç klasörü kısayolu.
- **Varsayılan otomatik başlatma:** Açık ve gizli mod. Bildirimler, hatırlatıcılar, otomatik yedek ve saklama servisleri arka planda çalışır.

### Not
- Bilgisayarda Windows oturumu kapalıysa uygulama çalışmaz; toast/push üretilemez. Windows açılışında gizli başlatma bu süreyi en aza indirir.
- Kalıcı oturum, aynı Windows hesabını kullanan herkesin uygulamaya erişebilmesi anlamına gelir.

### Test
- 235/235 test.
- Otomatik başlatma kaydı `--tray` ile doğrulandı.
- Elle kilitleme, kilit durumunun yeniden başlatmada korunması ve şifreyle açılması doğrulandı.
- `--tray` ile açılışta görünür pencere açılmadı; oturum otomatik açıldı.

## [1.2.1] — 2026-11-11 · Dijital imza ve indirme sayfası

### Eklendi
- **Kod imzalama hattı:** `Imza-Sertifikasi-Olustur.ps1` geliştirme sertifikası üretir; `Kurulum-Olustur.ps1` uygulama EXE'sini ve Setup EXE'yi imzalar.
- Üretim için PFX/CA sertifikası desteği (`-PfxPath`, `-CertificateThumbprint`).
- README ana sayfasına **İndir** bölümü: Setup EXE ve taşınabilir ZIP bağlantıları.
- `docs/22_Imza_ve_Yayin.md` yayın/imza rehberi.
- GitHub deposu public; Releases üzerinden indirilebilir.

### Önemli
- Bu sürümdeki imza **geliştirme (self-signed)** sertifikasıyladır. Windows, sertifika güvenilir değilse
  “Bilinmeyen yayıncı” gösterebilir. Üretim için CA'dan alınmış kod imzalama sertifikası kullanılmalıdır.

## [1.2.0] — 2026-11-11 · Kararlar ve yeni özellikler

### Eklendi
- **Excel’den içe aktarma:** `.xlsx` şablonu, görüşmeci/kategori eşleme, doğrulama, hatalı satır raporu ve toplu ekleme. Dışa aktar menüsünden şablon indirilebilir.
- **2 yıl saklama politikası:** 2 yıldan eski görüşmeler günlük olarak anonimleştirilir; kişi adı/telefonu, açıklama ve öğrenci bilgileri temizlenir. Eski kişinin daha yeni görüşmesi varsa kişi korunur.
- **Google Drive for Desktop:** OAuth yerine senkronize Drive klasörü. Yedekler klasöre kopyalanır; yedek listesinde Drive dosyaları görünür ve geri yükleme için indirilir.
- **Otomatik güncelleme:** Public GitHub Releases kontrolü; kurulu sürümde sessiz Setup, taşınabilir sürümde ZIP ile yerinde güncelleme. Ayarlar’dan manuel kontrol.
- **Toast tıklaması:** Bildirime tıklanınca ilgili hatırlatıcı aksiyon penceresi açılır.
- **WhatsApp iptal bilgisi:** Ziyaretçi iptalinde kişiye WhatsApp mesajı hazırlanır (pano + WhatsApp aç).
- **Erteleme:** Sonuç ekranında “Ertelendi” seçilince yeni tarih/saat zorunlu, onay istenir ve hatırlatıcılar yeniden planlanır.
- **Bildirimlerde kişi adı:** Windows toast ve push bildirimlerinde kişi adı gösterilir (ürün sahibi onayı).
- **Hatırlatıcı tekrarı:** İlk gösterim + 3 tekrar (toplam 4), 10’ar dakika arayla.
- **Excel/CSV/PDF/yazdırma** önceki sürümden korunur.

### Değişti
- **Q19:** Ağ sürücüsü özelliği eklenmedi.
- **Q23:** İptal eden kişiye WhatsApp bilgilendirmesi eklendi.
- **Q24:** Excel içe aktarma eklendi.
- GitHub deposu **public** yapıldı (otomatik güncelleme için).

### Test
- 233/233 otomatik test.
- Excel ayrıştırma gerçek veritabanı host/kategori adlarıyla doğrulandı.
- Google Drive klasör kopyalama/listeleme/indirme testleri.
- Setup ve taşınabilir paketler.

## [1.1.2] — 2026-11-11 · Dışa aktar menüsü teması

### Düzeltildi
- “Dışa aktar” düğmesinin açtığı bağlam menüsü koyu temaya uyarlandı; beyaz arka plan ve okunmayan yazılar giderildi.
- Menü öğeleri için koyu arka plan, açık metin ve vurgu rengi eklendi.

## [1.1.1] — 2026-11-11 · Sistem saat dilimi

### Düzeltildi
- Tüm UTC ↔ yerel saat dönüşümleri artık **Windows sistem saat dilimini** kullanır (önceden sabit `Europe/Istanbul` idi).
- Yedek dosya adları ve manifest zamanları, yedek geçmişi, randevu saatleri, hatırlatıcılar, denetim kaydı ve günlük paylaşım saatleri sistem saat dilimine göre gösterilir/hesaplanır.
- İşletim sistemi saat dilimi değiştirilirse uygulama yeniden başlatıldığında yeni dilim geçerli olur.

## [1.1.0] — 2026-11-10 · Taşınabilir mod

### Eklendi
- **Taşınabilir mod:** `portable.flag` veya `--portable` ile veriler EXE yanındaki `CGP-Data` klasöründe tutulur.
- `CGP-Data` içinde veritabanı, loglar, geçici dosyalar ve yedekler (`Yedekler`) bulunur.
- Taşınabilir modda otomatik başlatma (HKCU Run) devre dışıdır.
- Taşınabilir ve normal kurulum aynı anda çalışabilir (ayrı tek örnek kilidi).
- Taşınabilir ZIP içinde `portable.flag` ve `PORTABLE-BENIOKU.txt` bulunur.

### Not
- Klasör yazılamıyorsa uygulama güvenli biçimde kullanıcı profiline döner.

## [1.0.0] — 2026-11-10 · MVP sürümü

### Eklendi
- Per-user kurulum paketi (Inno Setup) ve taşınabilir ZIP
- Kurulum betiği: `Kurulum-Olustur.cmd`
- Sprint 6 UAT raporu: [docs/19_UAT_Raporu.md](docs/19_UAT_Raporu.md)

### Sürüm kapsamı
- Sprint 0–5, 4.5 UAT, 4.6 kimlik/kurtarma, manuel düzeltmeler
- Sprint 6A/6B: yedekleme, geri yükleme, otomatik yedek
- Sprint 6C: denetim kaydı ekranı
- Faz 2: şifreli yedek, Google Drive, Excel/CSV/PDF/yazdırma, BitLocker göstergesi

### Not
- Google Drive gerçek uçtan uca testi, kullanıcı OAuth istemci bilgilerini girdiğinde yapılacak.
- `dist\CGP-RandevuTakip-1.0.0-Setup.exe` üretildi; sessiz kurulum, çalıştırma ve kaldırma test edildi.

## [0.10.0] — 2026-11-10 · Şifreli yedek, Drive, dışa aktarma

### Eklendi
- **Yedek şifreleme:** AES-256-GCM + PBKDF2; `.cgpbak` dosyaları. Parola DPAPI ile saklanır, otomatik yedeklerde kullanılır.
- **Google Drive:** OAuth 2.0 + PKCE ile bağlanma, `drive.file` kapsamı, klasör oluşturma ve şifreli yedeği yükleme. Bağlantı token'ı DPAPI ile saklanır.
- **Excel (`.xlsx`)** ve **CSV** dışa aktarma; Günlük Liste'deki mevcut filtreyi kullanır.
- **PDF raporu** (QuestPDF, A4 yatay) ve **yazdırma** (Windows yazdırma diyaloğu).
- **BitLocker göstergesi:** Ayarlar'da disk şifreleme durumu ve yeniden kontrol.

### Güvenlik
- Drive'a yükleme yalnızca şifreli yedeklerle yapılabilir.
- Şifreli yedeklerin parolası kaybolursa geri yükleme yapılamaz.
- Dışa aktarılan Excel/PDF/CSV dosyaları kişisel veri içerir; yalnızca kullanıcı kaydeder.

### Test
- 221/221 otomatik test.
- Excel/CSV/PDF üretimi doğrulandı (PDF `%PDF`, xlsx açıldı).
- Google Drive gerçek uçtan uca testi, kullanıcı OAuth istemci bilgileri girilince yapılacak.

## [0.9.0] — 2026-11-10 · Sprint 6C: denetim kaydı ekranı

### Eklendi
- Yöneticiye özel, salt-okunur **Denetim Kaydı** sayfası
- Kullanıcı, işlem ve tarih aralığı filtreleri
- Sayfalama (25/50/100 kayıt), toplam kayıt ve sayfa özeti
- İşlem adlarının Türkçe karşılıkları; teknik detay için tooltip
- Denetim kaydı arayüzden değiştirilemez veya silinemez

### Yetki
- Denetim kayıtlarını yalnızca yönetici görüntüleyebilir; menü diğer rollerde gizlenir ve servis yetkiyi ayrıca doğrular.

## [0.8.0] — 2026-11-10 · Sprint 6A/6B: yedekleme ve geri yükleme

### Eklendi
- ZIP yedeği: `data.db` + `settings.json` + `manifest.json`; manifestte sürüm, şema, kayıt sayıları ve SHA-256
- Yedekleme sayfası: manuel yedek (tüm roller), yedek geçmişi, doğrulama durumu, klasörü açma
- Son 30 yedeğin saklanması
- Günde 2 otomatik yedek: 12:30 öğle yedeği + öğleden sonra günlük liste paylaşımı sonrası; paylaşım yoksa 18:00
- Geri yükleme: ZIP bütünlüğü, şema uyumu, SHA-256 ve SQLite `integrity_check` doğrulaması
- Geri yükleme öncesi otomatik güvenlik yedeği ve uygulamanın yeniden başlatılması

### Değişti
- Kurum adı varsayılanı **BEK Okulları**; Ayarlar'dan değiştirilebilir
- Çalışma saatleri (varsayılan 06:00–20:45) ve "mesai dışı görüşme" izni Ayarlar'dan yönetilir
- Push sağlayıcı seçimi (ntfy/Telegram) Ayarlar'dan yapılır; seçili sağlayıcı kullanılır

### Güvenlik
- Geri yükleme yalnızca yönetici rolüne açıktır
- Yedek `settings.json` dosyasına SMTP parolası ve push token/chat id yazılmaz

## [0.7.0] — 2026-11-10 · Toplu işlemler

### Eklendi
- Günlük listede her satırın başında seçim kutusu; başlıktan tümünü seçme
- Seçili görüşmeleri toplu silme (yönetici) ve toplu taşıma (yönetici/sekretör)
- Toplu taşımada "saatleri koru" veya "tümünü aynı saate taşı" seçenekleri; çakışmada onay
- Görüşmeci rolünde toplu seçim sütunu gizlenir

### Değişti
- Yeni görüşmede varsayılan tarih/saat: bugün + 1 saat (15 dk dilime yuvarlanır, 06:00–20:45 sınırlarına uyar)
- Yeni görüşmede veya saat değiştirilerek geçmişe alınan görüşmede "geçmiş tarih" uyarısı

### Test
- 187/187 test

## [0.6.4] — 2026-11-10

### Düzeltildi
- Liste satırlarında fare üzerine gelindiğinde tüm satırlarda (1., 2., 3. …) hover rengi görünüyor;
  DataGrid seviyesindeki `RowBackground`/`AlternatingRowBackground` ayarları kaldırıldı

## [0.6.3] — 2026-11-10

### Düzeltildi
- "Beni hatırla" yalnızca uygulama açılışında otomatik giriş yapıyor; çıkış sonrası giriş formu gösteriliyor ve önceden dolduruluyor

## [0.6.2] — 2026-11-10 · Manuel hata düzeltmeleri 2

### Düzeltildi
- Saat seçenekleri 06:00–20:45 aralığına çekildi
- Günlük liste sütun sırası: Görüşmeci, Kişi, Saat, Konu, Yöntem, Durum; sütun sıralaması açık
- Görüşmeciler/Kategoriler/Kullanıcılar listelerinde tüm sütunlar sıralanabilir
- Ziyaretçi iptal aksiyonu pencereyi kapatmıyordu; `Show()` ile açılan pencerede `DialogResult` hatası giderildi
- Günlük paylaşım mesajına görüşme detayları ve özet eklendi
- Ayarlar ekranındaki iki sütunun yüksekliği dengelendi
- "Beni hatırla" çıkış sonrası kullanıcı adı/şifreyi giriş formuna geri dolduruyor

## [0.6.1] — 2026-11-09 · Manuel hata düzeltmeleri

### Düzeltildi
- Liste satırlarında hover/seçim görünürlüğü güçlendirildi; sütun başlıkları hizalandı
- Pasif kategoriler yeni görüşme ekranında görünmüyor; düzenlemede mevcut pasif kategori korunuyor
- Görüşme düzenlemede kategori değişikliği artık kaydediliyor (navigation/FK çakışması giderildi)
- Saat alanı serbest metin yerine 15 dakikalık seçim listesi oldu
- Yeni görüşme varsayılan süresi 1 saat oldu
- Özel hatırlatıcı: saat/dakika seçimi ve 1 dk–7 gün aralığı
- Üst çubuktaki tarih bugünün listesini açar; marka/kurum adı kullanıcı alanına gider
- Günlük listedeki tarih alanı takvim seçicisi oldu
- "Host" kullanıcı metinleri "Görüşmeci" olarak değiştirildi
- Tanılama ekranındaki sahte S-01/S-02 butonları gerçek paylaşım/toast araçlarıyla değiştirildi
- Ziyaretçi iptali servis testi ve aksiyon akışı doğrulandı

## [0.6.0] — 2026-11-09 · Sprint 5

### Eklendi
- Günlük liste PNG'si gerçek randevu verilerinden, 2x çözünürlükte üretiliyor
- Paylaşım penceresi: görsel önizleme, düzenlenebilir mesaj, alıcı seçimi
- Panoya kopyalama ve `whatsapp://send?text=...` ile WhatsApp'ı açma
- Alıcı rehberi: ekleme, düzenleme, pasifleştirme
- Görüşme detayından WhatsApp metni paylaşımı
- Günlük paylaşım saati hatırlatıcısı ve kaçırılan paylaşım uyarısı
- `WhatsAppShareLog` kaydı

## [0.5.4] — 2026-11-09 · Kimlik ve kurtarma

### Eklendi
- "Beni hatırla": 30 gün geçerli, DPAPI korumalı oturum token'ı ile otomatik giriş
- Kullanıcı e-posta adresi alanı (şifre sıfırlama için)
- SMTP ayarları ve "Test e-postası gönder"
- "Şifremi unuttum": e-posta ile 6 haneli kod, 15 dk geçerlilik, 5 deneme sınırı
- Yönetici şifre sıfırlama fallback'i korunuyor
- `Sprint4_6_AuthRecovery` migration

### Not
- E-posta ile sıfırlama için yöneticinin SMTP ayarlarını ve kullanıcı e-postalarını bir kez tanımlaması gerekir.

## [Yayınlanmamış]

### Eklendi
- `Calistir-Debug.cmd`: tek tıkla Debug derleme + uygulamayı başlatma
- `Derle.cmd`: tek tıkla Release derleme + test çalıştırma

## [0.5.3] — 2026-11-08

### Düzeltildi
- Sessiz saatlerde Windows Action Center toast sessiz ses ile gönderiliyor

## [0.5.2] — 2026-11-08 · Sprint 4.5 UAT

### Eklendi
- Windows Action Center toast entegrasyonu; başarısız olursa uygulama içi toast'a düşer
- Ayarlarda "Test bildirimi gönder" (ntfy/Telegram) ve "Windows toast testi"
- Push kurulum rehberi

### Düzeltildi
- Sessiz saat başlangıç/bitiş değerleri doğru ayar anahtarlarından okunuyor
- "Kapatınca tepside çalış" ayarı anında uygulanıyor
- İkinci açılış mevcut pencereyi öne getiriyor; yeni süreç sessizce kapanıyor
- Ana pencere modal olmayan `Show()` ile yönetiliyor; tepsiye küçültme güvenilir
- Hatırlatıcı aksiyonları `IClock` kullanıyor

## [0.5.1] — 2026-11-08

### Eklendi
- Uygulama, pencere ve tepsi simgesi olarak `CGPRandevuTakip.ico` kullanılıyor

## [0.5.0] — 2026-11-08 · Sprint 4

### Eklendi
- Hatırlatıcı motoru: zamanı gelen hatırlatıcılar 15 saniyede bir işlenir
- Windows bildirimi (uygulama içi topmost toast) ve 4 seçenekli aksiyon penceresi
- "Sırası gelen" arama hatırlatıcıları: 30 dk ve 5 dk önce
- Push bildirimi: ntfy ve Telegram; varsayılan kapalı ve isimsiz
- Tepsi simgesi ve "kapatınca tepside çalış"
- Windows açılışında başlatma (giriş ekranı veya tepside gizli)
- Sessiz saatler: kullanıcı seçimli, varsayılan 22:00–07:00
- Sırların DPAPI ile şifreli saklanması
- Hatırlatıcı aksiyonları için denetim kaydı

### Değiştirildi
- `ReminderDispatch` şemasına `Kind` (Appointment / Call) eklendi

### Düzeltildi
- Uygulama kapalıyken kaçırılan hatırlatıcılar açılışta telafi edilir; geçmiş randevular `Missed` olur
- Aynı hatırlatıcının iki kez gönderilmesi kanal timestamp'leri ile engellenir

## [0.4.1] — 2026-11-08 · Sprint 3.5

### Eklendi
- Silinen görüşmeler ekranı; yönetici 30 gün içindeki kayıtları görüp geri alabilir
- Süresi dolan silinmiş görüşmeler için otomatik kalıcı temizlik (açılışta ve 6 saatte bir)
- Günlük listede host filtresi
- GitHub Actions CI: Windows üzerinde Release build ve testler; test sonuçları artifact olarak yüklenir
- Dependabot ile NuGet ve GitHub Actions güncelleme takibi

### Düzeltildi
- Geri alınan randevunun iptal edilmiş gelecek hatırlatıcıları yeniden etkinleştirilir
- Silme ve geri alma işlemleri `IClock` üzerinden tutarlı UTC zamanı kullanır
- Görüşme detayındaki silme onayı 30 günlük geri alma penceresini belirtir
- Henüz etkinleşmemiş günlük paylaşım düğmesi kullanıcıdan gizlendi
- Bildirim ayarlarına Sprint 4 etkinleştirme notu eklendi

### Değiştirildi
- NuGet bağımlılıkları güncellendi: EF Core 9.0.22, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.14, xUnit 2.9.3
- Teknik ad, çözüm/proje/klasör ve assembly adları `CgpRandevuTakip` oldu; yedek ve kullanıcıya görünen metinlerdeki marka CGP olarak kaldı
- Kurum adı ayarlardan kaydedildiğinde üst çubuk ve kontrol paneli yeniden başlatmadan güncellenir; boşsa `CGP` gösterilir
- Eski yerel veri klasörü ilk açılışta yeni `CgpRandevuTakip` klasörüne tek seferlik taşınır; mevcut veritabanı ve kullanıcılar korunur

### İyileştirildi
- Liste yenilemeleri `RangeObservableCollection.ReplaceWith` ile tek `Reset` bildirimi kullanır; `Clear + foreach` tekrarları kaldırıldı
- Kontrol paneli kategori sayılarını tek sorgudan üretir; kategori, ayar ve kullanıcı sayısı paralel yüklenir
- Görüşme düzenleyici kategori/görüşmeci/ayar aramalarını paralel yükler; günlük liste özeti tek geçişte hesaplanır
- Kullanılmayan görüşmeci servisi bağımlılığı günlük liste modelinden kaldırıldı

## [0.4.0] — 2026-11-07 · Sprint 3

### Eklendi
- Görüşme sonucu: katılım (Geldi/Gelmedi/Ertelendi/İptal), değerlendirme, tekrar ve kapanış
- Durum makinesi: `Appointment.ApplyOutcome` ve kapanmış kaydı yeniden açma (`Reopen`)
- Kapanış bilgisi: kapanış zamanı ve kapatan kullanıcı; detayda sonuç kartı
- Detaydan "Sonuç gir" ve "Tekrar randevu oluştur" akışları
- Tekrar randevu taslağı: kişi/telefon/host/kategori/yöntem/hatırlatıcı devralınır, tarih +7 gün önerilir
- Düzenleme ekranında sonuç kartı ve kapanmış görüşme uyarısı
- Günlük listede "Sıradaki görüşme" şeridi
- Kullanıcı yönetimi: ekleme, rol güncelleme, aktif/pasif, giriş izni, şifre sıfırlama
- Son aktif yönetici koruması ve kendini pasifleştirme engeli
- 30 dakika hareketsizlik kilidi, kilit ekranı ve şifreyle açma
- Tüm pencerelerden (modal diyaloglar dahil) etkinlik izleme
- `20261107040550_Sprint3_OutcomeAndLock` migration'ı
- 32 yeni test (toplam 135)

### Değişti
- Kapanışta bekleyen hatırlatıcılar `Cancelled` durumuna alınır; gönderilmişler korunur
- Ana kabuğa yalnızca yöneticinin gördüğü "Kullanıcılar" gezinmesi eklendi
- "Sprint 3 kapsamı" bilgisi ana kabuk alt bilgisine işlendi
- Sürüm `0.3.0` → `0.4.0`

### Düzeltildi
- Kilit penceresinin alt düğmelerinin kırpılması (`SizeToContent="Height"`)
- Modal diyalog açıkken oturum etkinliğinin izlenmemesi
- Kullanıcı listesinde ham DTO metninin görünmesi

### Notlar
- 30 dk kilit UI testi geçici olarak 15 sn / 2 sn ile yapıldı; test sonrası
  üretim değerleri **30 dakika / 30 saniye** olarak geri alındı.
- Windows OS toast/push Sprint 4'e, WhatsApp paylaşımı Sprint 5'e, yedekleme Sprint 6'ya ertelendi.

## [0.3.0] — 2026-11-07 · Sprint 2

### Eklendi
- Görüşme oluşturma, düzenleme ve silme (soft delete, 30 gün geri alma)
- Günlük liste: tarih gezinme, bugün, kategori/durum filtreleri, özet satırı
- Arama: ad, soyad, telefon, kategori ve host; arama doluyken tarih filtresi kaldırılır
- Görüşme detay penceresi ve listeden açık "Detay" düğmesi
- Telefon E.164 normalizasyonu ve aynı kişinin tekrar kullanımı
- Host çakışma uyarısı; onaylanırsa `IgnoreConflicts` ile kayıt
- Çoklu hatırlatıcı (1 gün / 1 saat / 30 dk) ve güncellemede yeniden planlama
- Görüşmeci rolü yalnızca kendi görüşmelerini görür; randevu oluşturamaz
- Host (görüşmeci) yönetimi: girişsiz host, otomatik kullanıcı adı, aktif/pasif
- Günlük Liste varsayılan sayfa; "Görüşmeciler" yalnızca yönetici/sekreter
- PerMonitorV2 DPI farkındalığı (`app.manifest`)
- 24 yeni test (toplam 103)

### Değişti
- Ana kabuk gezinmesi: Günlük Liste / Görüşmeciler / Kategoriler / Kontrol Paneli / Ayarlar
- Tarih biçimi tüm ekranlarda `gg/AA/yyyy` olarak sabitlendi
- Sürüm `0.2.0` → `0.3.0`

### Düzeltildi
- Karanlık temada ComboBox ve DatePicker beyaz görünümü
- Görüşmeciler listesinde "Ad soyad" sütununun sıfıra sıkışması
- Detay penceresinin salt okunur `Run.Text` bağlaması nedeniyle çökmesi
- Karanlık temada kaydırma çubuğunun beyaz şerit olarak görünmesi
- 150% DPI ölçeğinde pencere taşması

### Notlar
- Windows OS toast/push Sprint 4'e, 30 dk hareketsizlik kilidi ve kullanıcı yönetimi Sprint 3'e ertelendi.

## [0.2.0] — 2026-11-06 · Sprint 1

### Eklendi
- İlk kurulum sihirbazı; varsayılan şifre olmadan yönetici hesabı oluşturma
- Kullanıcı adı/şifre ile giriş, rol bazlı oturum (Yönetici, Sekreter, Görüşmeci)
- 5 hatalı denemede 15 dakika hesap kilidi ve kalan süre bilgisi
- Şifre değiştirme ekranı ve PBKDF2-SHA256 doğrulaması
- Koyu / açık / otomatik tema; otomatik modda Windows temasını izleme
- Kategori yönetimi: ekleme, düzenleme, sıralama, pasifleştirme, kullanım kontrolü
- Ayarlar: kurum adı, yedek klasörü (yazılabilirlik testi), bildirim/push tercihleri,
  varsayılan hatırlatıcı ve günlük paylaşım saati
- Hakkında bölümü: ürün adı, sürüm, telif, veritabanı ve günlük yolları
- Denetim kaydı altyapısı (giriş, kategori, ayar, tema olayları)
- Tek örnek (single-instance) koruması ve çökme günlüğü
- S-01: günlük liste PNG üretimi (`RenderTargetBitmap`), panoya kopyalama, WhatsApp açma
- S-02: hatırlatıcı aksiyon penceresi ve toast önizleme
- 79 birim/entegrasyon testi (tümü geçiyor)

### Değişti
- Sprint 0 durum ekranı yerine gezinmeli ana kabuk (Kontrol Paneli, Kategoriler, Ayarlar)
- Sürüm `0.1.0` → `0.2.0`

### Notlar
- Windows OS toast ve telefon push Sprint 4'e ertelendi.
- 30 dakika hareketsizlik kilidi (FR-AUTH-05) ve kullanıcı yönetimi Sprint 2/3'e ertelendi.

## [0.1.0] — 2026-11-06 · Sprint 0

### Eklendi
- `CgpRandevuTakip.sln` ve 5 proje: Domain, Application, Infrastructure, Desktop (WPF), Tests
- Domain modeli: Appointment, Person, Category, User, ReminderDispatch, AppointmentOutcome,
  Recipient, Setting, AuditLog, WhatsAppShareLog, BackupRecord
- Telefon E.164 normalizasyonu, hatırlatıcı hesaplama, yedek dosya adı kuralları
- EF Core 8 + SQLite, ilk migration ve eğitim kurumu varsayılan kategorileri
- PBKDF2-SHA256 parola özetleme ve Türkiye saat dilimi
- WPF açılış kabuğu (Sprint 0 durum ekranı)
- 36 birim/entegrasyon testi (tümü geçiyor)

## [Yayınlanmadı] — Analiz ve tasarım

### Eklendi
- Proje vizyonu, kapsam ve paydaş analizi (`docs/00`)
- Gap analizi, çelişkiler, eksik gereksinimler ve MVP tanımı (`docs/01`)
- Fonksiyonel/fonksiyonel olmayan gereksinimler, iş kuralları, kabul kriterleri (`docs/02`)
- Katmanlı mimari, veri modeli, entegrasyon akışları (`docs/03`)
- Sprint planı, DoR/DoD, test stratejisi, risk kaydı (`docs/04`)
- KVKK ve veri güvenliği çerçevesi (`docs/05`)
- Karar kaydı ve açık sorular (`docs/06`)

### Karar
- Teknoloji: .NET 8 + WPF + SQLite
- WhatsApp: MVP'de yarı otomatik
- Bildirim: Windows toast + ntfy/Telegram push
- Tek bilgisayar, yerel kullanıcı/rol
