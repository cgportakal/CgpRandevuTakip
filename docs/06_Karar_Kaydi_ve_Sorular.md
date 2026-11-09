# 06 — Karar Kaydı ve Açık Sorular

**Sürüm:** 0.3 · **Tarih:** 07.11.2026

---

## 1. Karar kaydı (Decision Log)

| ID | Karar | Alternatifler | Gerekçe | Tarih | Durum |
|----|-------|---------------|---------|-------|-------|
| D-01 | .NET 8 + WPF + SQLite | Avalonia, Electron, Tauri | Windows 11 hedefi, olgunluk, tek geliştirici, bu makinede SDK kurulu | 06.11.2026 | Kabul |
| D-02 | Çekirdek katmanlar mobilde yeniden kullanılacak | UI dahil paylaşım | İş kuralları taşınabilir; UI maliyeti ayrı | 06.11.2026 | Kabul |
| D-03 | WhatsApp MVP'de yarı otomatik | Cloud API, whatsapp-web.js | Ban riski ve maliyet yok; kullanıcı onayı | 06.11.2026 | Kabul |
| D-04 | Tek bilgisayar, tek oturum | Ağda çok kullanıcı, bulut | Kapsam ve maliyet; roller yerel | 06.11.2026 | Kabul |
| D-05 | Windows toast + ntfy/Telegram push | Yalnız Windows, SMS, mobil uygulama | Telefonda bildirim ihtiyacı | 06.11.2026 | Kabul |
| D-06 | Günlük liste alıcıları kurum içi personel | Ziyaretçiler, ikisi | KVKK ve iş akışı | 06.11.2026 | Kabul |
| D-07 | Kullanıcı adı/şifre + roller | Windows kimliği, PIN | İstenen güvenlik seviyesi | 06.11.2026 | Kabul |
| D-08 | Görüntü `RenderTargetBitmap` ile üretilecek | PrintScreen | PII sızıntısı ve tekrarlanabilirlik | 06.11.2026 | Kabul |
| D-09 | Hatırlatıcılar veritabanında kalıcı | Bellek içi zamanlayıcı | Yeniden başlatma/dayanıklılık | 06.11.2026 | Kabul |
| D-10 | Yedek ZIP + manifest + SHA-256 | Tek DB kopyası | Doğrulanabilirlik | 06.11.2026 | Kabul |
| D-11 | Google Drive Faz 2 | MVP'de | OAuth ve onay yükü | 06.11.2026 | Kabul |
| D-12 | Push varsayılan kapalı, isimsiz | Açık ve isimli | KVKK veri minimizasyonu | 06.11.2026 | Kabul |
| D-13 | Ürün adı: **CGP - Randevu Takip** (teknik ad `CgpRandevuTakip`) | — | Ürün sahibi onayı; marka 07.11.2026'da CGP olarak kesinleşti | 06.11.2026 | Kabul |
| D-14 | Yedek adı: `CGP_RandevuTakip_Yedek_2026-11-06_14-30-00.zip` | `yyyy/aa/gg` | Windows'ta `/` yasak; ISO sıralanabilir | 06.11.2026 | Kabul |
| D-15 | "Sırası gelen" = randevudan **30 dk ve 5 dk önce** | Sadece saatinde, 5 dk | Ürün sahibi onayı; hazırlık + son çağrı | 06.11.2026 | Kabul |
| D-16 | İlk yönetici hesabı **kurulum sihirbazı** ile oluşturulur | Sabit hesap, Windows kimliği | Varsayılan şifre riskini ortadan kaldırır | 06.11.2026 | Kabul |
| D-17 | Host'lar **isim listesi + opsiyonel giriş** | Zorunlu kullanıcı, serbest metin | Kurulum yükü ile esneklik dengesi | 06.11.2026 | Kabul |
| D-18 | Varsayılan görüşme süresi **30 dk** | 20/45/60 | Eğitim kurumu akışı | 06.11.2026 | Kabul |
| D-19 | Spike ortamı hazır: WhatsApp Desktop + test telefonu | — | S-01/S-02 Sprint 1'de yapılabilir | 06.11.2026 | Kabul |
| D-20 | Kategori, yalnızca hiç görüşme referansı yoksa silinir; kullanımdaysa pasifleştirilir | Her durumda silme, soft delete | Geçmiş kayıtların bütünlüğü ve raporlama | 06.11.2026 | Kabul |
| D-21 | Tema, `DynamicResource` sözlük değişimiyle anında uygulanır | Yeniden başlatma, pencere yeniden açma | Kesintisiz kullanıcı deneyimi | 06.11.2026 | Kabul |
| D-22 | Ayarları yalnızca yönetici değiştirir; tema tüm roller için açıktır | Tüm ayarlar yönetici, tema da yönetici | Tema düşük riskli kişisel tercih | 06.11.2026 | Kabul |
| D-23 | Windows OS toast ve telefon push Sprint 4'e ertelendi | Sprint 1'de | Bildirim paketi ve kanal yapılandırması Sprint 4 kapsamında | 06.11.2026 | Kabul |
| D-24 | 30 dk hareketsizlik kilidi ve kullanıcı yönetimi Sprint 2/3'e ertelendi | Sprint 1'de | Görüşme CRUD ve kabuk akışlarıyla birlikte ele alınacak | 06.11.2026 | Kabul |
| D-25 | Denetim kaydı best-effort yazılır; hata iş akışını durdurmaz | Denetim hatasında işlemi geri al | Denetim altyapısı sorunu kullanıcıyı kilitlememeli | 06.11.2026 | Kabul |
| D-26 | Host listesi `User` tablosunda tutulur; girişsiz host yer tutucu parola özeti ile oluşturulur | Ayrı Host tablosu, serbest metin | Tek kullanıcı/rol kaynağı; girişsiz kişiler de atanabilir | 07.11.2026 | Kabul |
| D-27 | Arama metni doluyken tarih filtresi kaldırılır; boşken seçili gün kullanılır | Her zaman tarih + arama | Kullanıcı geçmiş kaydı tek aramayla bulabilmeli | 07.11.2026 | Kabul |
| D-28 | Çakışmada onay alınırsa `IgnoreConflicts=true` ile kayıt tekrar denenir | Sert engel, sessiz birleştirme | Esneklik; aynı kişi çakışması ayrıca işaretlenir | 07.11.2026 | Kabul |
| D-29 | Güncellemede bekleyen hatırlatıcılar iptal edilip yeniden planlanır; bildirilmiş offsetler tekrar planlanmaz | Tümünü yeniden oluştur | Mükerrer bildirimi önler | 07.11.2026 | Kabul |
| D-30 | Günlük Liste varsayılan sayfadır; "Görüşmeciler" yalnızca yönetici/sekreter görür | Kontrol Paneli varsayılan | Günlük akış ilk ekranda; yetki matrisine uyum | 07.11.2026 | Kabul |
| D-31 | Arayüz PerMonitorV2 DPI farkındasızdır; tarih kültürü `dd/MM/yyyy` olarak sabitlenir | Sistem DPI/kültürüne bırakma | 150% ölçekte taşma ve tarih biçimi tutarlılığı | 07.11.2026 | Kabul |
| D-32 | Sonuç, `Appointment.ApplyOutcome` durum makinesi üzerinden uygulanır; kapalı Gelmedi/Ertelendi/İptal kayıtları yeni sonuçta yeniden açılır, Tamamlandı'nın katılımı değiştirilemez | Sonucu ayrı tabloda bağımsız tutmak | Durum ve sonucun tek kaynaktan tutarlı yönetimi | 07.11.2026 | Kabul |
| D-33 | Görüşme kapanınca bekleyen hatırlatıcılar iptal edilir; gönderilmiş olanlar korunur | Tüm hatırlatıcıları silmek | Mükerrer bildirimi önler, geçmişi bozmaz | 07.11.2026 | Kabul |
| D-34 | Oturum kilidi 30 dk hareketsizlikte tetiklenir; sayaç 30 sn'de bir kontrol edilir ve etkinlik tüm pencerelerden `InputManager.PreProcessInput` ile izlenir | Yalnız ana pencere olayları | Modal diyalog açıkken yanlış kilitlenmeyi önler | 07.11.2026 | Kabul |
| D-35 | Kullanıcı yönetimi yalnızca yöneticidedir; son aktif yönetici korunur, kullanıcı kendi hesabını pasifleştiremez, host girişsiz olabilir | Serbest rol değişimi | Sistemin yöneticisiz kalmasını ve kendini kilitlemeyi önler | 07.11.2026 | Kabul |
| D-36 | "Tekrar randevu" kaynak kaydı şablon alır; tarih +7 gün, saat sonraki yarım saat önerilir; kaynak görüşme değiştirilmez | Kaynağı güncellemek, seri üretmek | Hızlı tekrar kayıt; yanlışlıkla geçmişi değiştirmeyi önler | 07.11.2026 | Kabul |
| D-37 | Marka adı **CGP - Randevu Takip** olarak güncellendi; yedek öneki `CGP_RandevuTakip_Yedek_` olur | Teknik adı da değiştirmek (D-38) | Kullanıcıya görünen tüm marka metinleri CGP olmalı | 07.11.2026 | Kabul · D-38 ile teknik ad da CGP oldu |
| D-38 | Teknik ad, çözüm/proje/klasör ve assembly adları `CgpRandevuTakip` olarak yeniden adlandırıldı; eski `%LocalAppData%` veri klasörü ilk açılışta tek seferlik yeni klasöre taşınır | Eski teknik adı korumak, veriyi taşımamak | Dosya ve teknik isimlendirmede tutarlılık; mevcut kullanıcı verisi korunur | 07.11.2026 | Kabul |
| D-39 | Silinen görüşmeler 30 gün soft delete olarak saklanır; yönetici "Silinenler" ekranından geri alabilir. Geri almada gelecekteki iptal edilmiş hatırlatıcılar yeniden etkinleştirilir. | Kalıcı silme, süresiz saklama | Yanlış silmeyi geri alma; BR-12 ile uyum | 08.11.2026 | Kabul |
| D-40 | Günlük liste ve arama host filtresi içerir; host rolü yalnızca kendi kayıtlarıyla sınırlıdır. | Filtresiz liste | US-10 kabul kriteri ve operasyonel hız | 08.11.2026 | Kabul |
| D-41 | 30 günü dolan silinmiş görüşmeler açılışta ve 6 saatlik aralıkla kalıcı silinir; denetim kaydı yazılır. | Manuel temizlik | Veri minimizasyonu ve disk temizliği | 08.11.2026 | Kabul |
| D-42 | Hatırlatıcılar `Kind` (Appointment / Call) ile ayrıştırılır; "sırası gelen" 30/5 dk arama hatırlatıcısı ayrı kaydedilir. 30 dk randevu hatırlatıcısı varsa 30 dk arama bildirimi oluşturulmaz. | Tek tür hatırlatıcı | FR-REM-16 ve mükerrer bildirimi önleme | 08.11.2026 | Kabul |
| D-43 | Bildirim motoru uygulama açılışında başlar; PII'li aksiyon penceresi yalnızca giriş sonrası ve kilitli değilken açılır. | Yalnızca giriş sonrası | Açılışta telafi; kilit ekranında PII gizliliği | 08.11.2026 | Kabul |
| D-44 | Windows bildirimi uygulama içi topmost toast olarak gösterilir; OS Action Center entegrasyonu sonraki spike'ta. | OS toast | Ortam bağımsız çalışma ve güvenilirlik | 08.11.2026 | Kabul |
| D-45 | Tekrar: toplam 3 gösterim; aksiyon alınmazsa `Unanswered`. "Ulaşılamadı" snooze 10 dk erteler ve otomatik tekrar sayılmaz. | Farklı tekrar sayıları | Ürün sahibi onayı | 08.11.2026 | Kabul |
| D-46 | Sessiz saatler kullanıcı seçimli; varsayılan 22:00–07:00. Sessiz saatte push gönderilmez, Windows bildirimi sessizdir. | Sabit aralık, yalnızca push | Ürün sahibi onayı | 08.11.2026 | Kabul |
| D-47 | Otomatik başlangıç kullanıcı seçimli; varsayılan "giriş ekranı açık". "Tepside gizli" modunda `--tray` ile başlar. | Sabit mod | Ürün sahibi onayı | 08.11.2026 | Kabul |
| D-48 | Windows bildirimleri `Microsoft.Toolkit.Uwp.Notifications` ile Action Center'a gönderilir; başarısız olursa uygulama içi topmost toast'a düşülür. | Yalnızca uygulama içi | Gerçek Windows bildirimi + güvenli yedek | 08.11.2026 | Kabul |
| D-49 | İkinci açılış named event ile mevcut örneğe "göster" sinyali gönderir; yeni süreç sessizce kapanır. | Yalnızca mesaj göstermek | Tek örnek ve hızlı geri dönüş | 08.11.2026 | Kabul |
| D-50 | Push kurulumu kullanıcı tarafından yapılır; ntfy topic veya Telegram bot token/chat id ayarlara girilir. Token/chat id DPAPI ile saklanır. | Uygulama içi hesap | Kurulum basitliği ve sır koruma | 08.11.2026 | Kabul |
| D-51 | "Beni hatırla" parola saklamaz; 30 günlük rastgele oturum token'ı DPAPI ile yerel dosyada tutulur, DB'de yalnızca SHA-256 özeti bulunur. | Parolayı şifreli saklamak | Daha düşük risk, iptal edilebilir oturum | 09.11.2026 | Kabul |
| D-52 | Şifre sıfırlama kullanıcı e-postasına gönderilen 6 haneli kodla yapılır; kod 15 dk geçerli, 5 deneme sınırlı, 1 dk yeniden gönderme bekleme süresi vardır. | Yönetici sıfırlaması, bağlantı | Yöneticiye ihtiyaç duymadan kurtarma | 09.11.2026 | Kabul |
| D-53 | Kullanıcı e-postası ve SMTP ayarları yönetici tarafından bir kez tanımlanır; SMTP parolası DPAPI ile saklanır. Sorun çözülemezse yönetici şifre sıfırlama fallback'i kullanılır. | Yalnızca yönetici sıfırlaması | Self-servis + güvenli fallback | 09.11.2026 | Kabul |
| D-54 | Günlük liste PNG'si gerçek randevu satırlarından 2x çözünürlükte üretilir; buton, imleç ve uygulama chrome'u yer almaz. | Ekran görüntüsü | PII sızıntısı ve tekrarlanabilirlik | 09.11.2026 | Kabul |
| D-55 | WhatsApp paylaşımı yarı otomatiktir: görsel panoya kopyalanır, `whatsapp://send?text=...` açılır; otomatik gönderim yapılmaz. | Cloud API | Ban riski ve kullanıcı onayı | 09.11.2026 | Kabul |
| D-56 | Alıcı rehberi yalnızca yönetici/sekreter tarafından yönetilir; geçmiş paylaşımlar alıcı silinse bile logda korunur. | Serbest silme | İzlenebilirlik | 09.11.2026 | Kabul |

---

## 2. Açık sorular

Her sorunun bir **varsayılan önerisi** vardır. Cevap gelmezse bu varsayımla
ilerlenir; cevap gelirse dokümanlar ve kod güncellenir.

### 2.1 Ürün ve kapsam

| ID | Soru | Neden önemli | Önerilen varsayılan | Durum |
|----|------|--------------|---------------------|-------|
| Q-01 | Ürünün resmî adı ne olacak? | Yedek adı, kurulum, Hakkında | **CGP - Randevu Takip** (D-37 ile güncellendi) | Kapandı |
| Q-02 | Kurum türü ne? (okul, danışmanlık, hukuk, sağlık, kamu) | Terminoloji ve KVKK hassasiyeti | **Eğitim kurumu** (onaylandı) | Kapandı |
| Q-03 | Kurum adı (paylaşım başlığında) | Görsel ve mesaj | Ayarlardan girilir, boş başlar | Bekliyor |
| Q-04 | Görüşme süresi ve iki randevu arası tampon? | Çakışma, takvim | **30 dk** (onaylandı), tampon yok | Kapandı |
| Q-05 | Görüşmeler mesai dışına taşar mı? | Sessiz saat, gece bildirimi | 08:00-20:00 arası; dışında sessiz | Bekliyor |
| Q-06 | Aynı kişiye aynı gün birden çok görüşme olur mu? | Listeleme, çakışma | Evet, izin ver | Bekliyor |

### 2.2 WhatsApp ve bildirim

| ID | Soru | Önerilen varsayılan | Durum |
|----|------|---------------------|-------|
| Q-07 | Günlük liste kaç alıcıya gidiyor, isimleri? | Ayarlardan yönetilir; MVP'de boş rehber | Bekliyor |
| Q-08 | Gönderim saati kaç? | 08:30 | Bekliyor |
| Q-09 | WhatsApp Desktop mı, Web mi kullanılıyor? | **Desktop + test telefonu var** (onaylandı) | Kapandı |
| Q-10 | Push için ntfy mi Telegram mı? | ntfy (kurulumu kolay) | Bekliyor |
| Q-11 | Bildirimde kişi adı görünsün mü? | Hayır (KVKK) | Bekliyor |
| Q-12 | "Sırası gelen" bildirimi ne zaman? | 30 dk + 5 dk önce (onaylandı) | Kapandı |
| Q-13 | Hatırlatıcı 3 kez tekrar etsin mi? | Evet, 10 dk arayla | Bekliyor |

### 2.3 Yedekleme ve güvenlik

| ID | Soru | Önerilen varsayılan | Durum |
|----|------|---------------------|-------|
| Q-14 | Yedek dosyası şifrelensin mi? | Faz 2'de parola ile; MVP'de NTFS | Bekliyor |
| Q-15 | Google Drive hangi hesap? | Kurum hesabı, Faz 2 | Bekliyor |
| Q-16 | Veri saklama süresi? | 2 yıl | Bekliyor |
| Q-17 | Kaç kullanıcı/rol tanımlanacak? | **1 yönetici + host isim listesi, opsiyonel giriş** (onaylandı) | Kapandı |
| Q-18 | Bilgisayarda BitLocker açık mı? | Önerilir, doğrulanmalı | Bekliyor |
| Q-19 | Yedekler ağ sürücüsüne mi gidecek? | Yerel klasör + opsiyonel ağ | Bekliyor |

### 2.4 İşleyiş

| ID | Soru | Önerilen varsayılan | Durum |
|----|------|---------------------|-------|
| Q-20 | Görüşme sonucunu kim giriyor? | Host; sekreter de girebilir | Bekliyor |
| Q-21 | "Tekrar edilecek" seçilince ne olacak? | Yeni randevu taslağı açılır | Bekliyor |
| Q-22 | Ertelenen görüşme yeni tarih ister mi? | Evet, opsiyonel; boşsa takipsiz | Bekliyor |
| Q-23 | İptal eden kişiye WhatsApp'tan bilgi verilecek mi? | Manuel, şablonla | Bekliyor |
| Q-24 | Excel'den mevcut veri aktarılacak mı? | Faz 2; örnek dosya gerekli | Bekliyor |
| Q-25 | Yazıcıdan kâğıt liste isteniyor mu? | Faz 2 (PNG/PDF) | Bekliyor |

---

## 3. Varsayılanlarla ilerleme kuralı

- Q-01, Q-02, Q-14, Q-15, Q-24 için cevap **Sprint 0 bitmeden** gerekir.
- Diğer sorular için önerilen varsayılan geçerlidir; değişiklik "CR" olarak işlenir.
- Her cevap bu dokümana tarih ve gerekçe ile işlenir; eski karar silinmez,
  "geçersiz" olarak işaretlenir.

---

## 4. Onay listesi

| # | Konu | Onaylayan | Tarih | İmza/Durum |
|---|------|-----------|-------|------------|
| 1 | Vizyon ve kapsam | Ürün sahibi | | Bekliyor |
| 2 | Gereksinimler (P0/P1) | Ürün sahibi | | Bekliyor |
| 3 | Mimari ve teknoloji | Ürün sahibi + geliştirici | | Bekliyor |
| 4 | KVKK çerçevesi | Kurum KVKK/hukuk | | Bekliyor |
| 5 | Sprint planı ve bütçe | Sponsor | | Bekliyor |
| 6 | Ürün adı ve yedek formatı | Ürün sahibi | | Bekliyor |

---

## 5. Sonraki adım

Sprint 0, Sprint 1, Sprint 2, Sprint 3, Sprint 3.5 ve Sprint 4 tamamlandı. Sıradaki adım:

1. Sprint 5: WhatsApp paylaşımı (PNG, pano, WhatsApp açma), alıcı rehberi, günlük gönderim saati.
2. OS Action Center toast entegrasyonu ve gerçek WhatsApp yapıştırma doğrulaması.
2. Kilit ekranında hassas bildirim içeriğinin gizlenmesi (BR-21, NFR-SEC-08).
3. S-01'in gerçek WhatsApp yapıştırma doğrulaması ve sonucun `docs/08`'e işlenmesi.
4. Açık sorulardan Q-03, Q-05, Q-10, Q-14, Q-15 ve Q-17 için ürün sahibi cevabı bekleniyor;
   cevap gelmezse dokümandaki varsayılanlarla ilerlenir.

> Bu noktadan sonra her sprint sonunda bu doküman, gereksinimler ve backlog
> güncellenir; kararlar **döngüsel** olarak yeniden gözden geçirilir.
