# 23 — Kalıcı Oturum, Elle Kilit ve Arka Plan Bildirimleri

**Sürüm:** 1.3.0

## 1. Kalıcı oturum

- Başarılı girişte DPAPI ile korunan kalıcı bir oturum token'ı oluşturulur.
- Token'ın **süre sınırı yoktur**; yalnızca açık çıkış, şifre değişimi veya şifre sıfırlama ile geçersiz olur.
- Uygulama kapansa, bilgisayar yeniden başlasa da oturum otomatik açılır.
- Tüm roller (Admin, Sekreter, Görüşmeci) için geçerlidir.
- Çıkış yapıldığında token iptal edilir; bir sonraki açılışta giriş formu gösterilir.

### “Beni hatırla”
- Kutu korunur ve **varsayılan işaretlidir**.
- Süresiz oturum her durumda geçerlidir; kutu yalnızca çıkış sonrası giriş formunda
  kullanıcı adı/şifre ön doldurulmasını yönetir.

## 2. Otomatik kilit ve elle kilitleme

- 30 dakika hareketsizlikte otomatik kilit **kaldırıldı**.
- Kenar çubuğundaki **Kilitle** düğmesi oturumu kilitler.
- Kilit ekranı şifre ister; “Kilidi aç” ile devam edilir, “Çıkış yap” ile oturum kapatılır.
- Kilit durumu `%LOCALAPPDATA%\CgpRandevuTakip\session.lock` (veya taşınabilirde `CGP-Data\session.lock`)
  dosyasında tutulur; uygulama/PC yeniden başlasa da korunur.
- Kilitliyken uygulama kapatılamaz (çıkış veya kilit açma gerekir).

## 3. Arka plan bildirimleri

- Windows açılışında uygulama **`--tray`** ile gizli başlar.
- Kurulu sürüm: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` kaydı.
- Taşınabilir sürüm: Başlangıç klasöründe `CGP - Randevu Takip.vbs` başlatıcısı.
- Varsayılan olarak açıktır; Ayarlar > Uygulama’dan kapatılabilir.
- Tepside çalışırken `ReminderDispatcher`, toast/push, otomatik yedek ve saklama servisleri çalışmaya devam eder.
- Pencereyi kapatmak uygulamayı kapatmaz; tepsiye küçültür.
- Uygulama tamamen kapatılırsa veya Windows oturumu kapalıysa toast/push üretilemez.

## 4. Güvenlik notları

- Kalıcı oturum, aynı Windows hesabını kullanan herkesin uygulamaya erişebilmesi demektir.
- Windows oturum şifresi, BitLocker ve fiziksel güvenlik önlemleri yine gereklidir.
- Şifre değişimi/sıfırlama tüm kalıcı oturumları geçersiz kılar.
- Şüpheli durumda **Çıkış** yapılması token'ı iptal eder.
