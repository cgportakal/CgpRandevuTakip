# 01 — Analiz, Çelişkiler ve Eksikler

**Sürüm:** 0.1 · **Tarih:** 06.11.2026

Bu doküman, verilen özellik listesinin yazılım mühendisliği açısından analizini içerir.
Amaç; çelişkileri, eksikleri ve ilk aşamada acil geliştirilmesi gerekenleri **karar
verilebilir** hâle getirmektir. Her bulguya bir öneri ve öncelik atanmıştır.

Öncelik ölçeği: **P0** = MVP'yi bloke eder · **P1** = ilk sürümde olmalı ·
**P2** = ikinci faz · **P3** = ileri faz.

---

## 1. En kritik 3 bulgu (yönetici özeti)

### B-01 — "Mobil bildirim" ile "Windows masaüstü" hedefi çelişiyor (P0)

Gereksinim 3'te "mobil bildirim" isteniyor; ancak uygulama Windows 11 masaüstünde
çalışacak. Masaüstü uygulaması mobil bildirim gönderemez.

**Karar (sizin cevabınız):** Windows toast + ntfy/Telegram ile telefona push.
Yani bildirim hem ekranda hem telefonda görünür. Mobil uygulama Faz 3'e alındı.

**Etki:** Bildirim servisi soyutlanmalı (`INotificationChannel`); Windows ve push
kanalları birbirinden bağımsız çalışmalı. Push kanalı çevrimdışıysa kuyruğa alınmalı.

### B-02 — WhatsApp otomatik gönderim hem teknik hem hukuki risk (P0)

"Her gün seçilen saatte WhatsApp'tan mesaj atılabilmeli" ifadesi tam otomatik gönderim
gibi okunuyor. WhatsApp'ın resmî olmayan yolları (whatsapp-web.js, Baileys) hesabın
**kalıcı kapatılmasına** yol açabilir; resmî Cloud API ise işletme hesabı, şablon onayı
ve mesaj başına ücret gerektirir. Ayrıca görsel (PNG) gönderimi şablon onayına tabidir.

**Karar (sizin cevabınız):** MVP'de yarı otomatik. Uygulama PNG'yi üretir, panoya
kopyalar, WhatsApp'ı açar ve mesaj metnini hazırlar; gönderme onayı kullanıcıdadır.

**Etki:** "Otomatik gönderildi" beklentisi yönetilmeli. Zamanı gelince uygulama
**gönderim penceresi** açar; bilgisayar kapalıysa bir sonraki açılışta "kaçırılan
gönderim" uyarısı gösterilir. Resmî API adaptörü Faz 3 için arayüz olarak bırakılır.

### B-03 — Ekran görüntüsü alma yöntemi yanlış kurgulanmış (P1)

"Listenin ekran görüntüsü alınabilmeli" denince akla `Print Screen` geliyor. Bu yöntem
başka pencereleri, bildirimleri ve kişisel verileri de yakalar; çözünürlük ve tekrar
üretilebilirlik sorunludur.

**Öneri:** Liste görünümü **bellekte render edilerek** PNG üretilsin (WPF
`RenderTargetBitmap`, 2x DPI). Böylece yalnızca liste, başlık ve tarih görünür; yüzen
butonlar, imleç ve diğer pencereler dışarıda kalır. Aynı görsel hem panoya hem geçici
dosyaya yazılır. Test edilebilir ve deterministiktir.

---

## 2. Gereksinim çelişkileri

| ID | Bulgu | Etki | Öneri / karar | Öncelik |
|----|-------|------|---------------|---------|
| C-01 | "Mobil bildirim" ↔ Windows hedefi | Bildirim altyapısı belirsiz | Windows toast + ntfy/Telegram push | P0 |
| C-02 | "Her gün otomatik WhatsApp" ↔ ban riski | Hesap kapanabilir | Yarı otomatik + onay | P0 |
| C-03 | Yedek adı `yyyy/aa/gg SS/dd/ss` | `/` Windows dosya adında yasak; `dd` hem gün hem dakika | Onaylandı: `CGP_RandevuTakip_Yedek_2026-11-06_14-30-00.zip` (D-37) | P0 |
| C-04 | `gg/AA/yyyy` ekran formatı ↔ yedek formatı | Karışıklık | Ekranda TR formatı, dosyada ISO 8601 | P1 |
| C-05 | "Görüşme yöntemi" tekil mi çoklu mu? | Veri modeli belirsiz | Tek seçim (Telefon/Yüz yüze/WhatsApp/İnternet) | P0 |
| C-06 | "Sırası gelen kişi" tanımı yok | Bildirim zamanı belirsiz | Onaylandı: randevudan 30 dk ve 5 dk önce | P0 |
| C-07 | "10 dk ertelendi" neyi erteler? | Randevu mu, bildirim mi? | Yalnızca bildirim 10 dk snooze; randevu saati değişmez | P0 |
| C-08 | Görüşme süresi belirtilmemiş | Bitiş saati, çakışma, takvim yok | Varsayılan 30 dk, düzenlenebilir | P1 |
| C-09 | "Tekrar edilecek mi?" → yeni kayıt mı? | İş akışı belirsiz | "Tekrar randevu oluştur" aksiyonu; seri Faz 2 | P1 |
| C-10 | Ekran listesinde giriş/kişi rehberi yok | Güvenlik ve alıcı yönetimi eksik | Giriş ekranı + Ayarlar altında "Alıcılar" | P0 |
| C-11 | "Yedekleme yeri kalıcı seçimi" ↔ Google Drive | İki ayrı kavram | Yerel klasör + bulut sağlayıcı ayrı ayarlar | P1 |
| C-12 | Bildirim kapatma 4 seçenek, toast butonları sınırlı | Windows toast 4 aksiyonu göstermez | Toast tıklanınca uygulama içi aksiyon penceresi açılır | P0 |

## 3. Eksik gereksinimler

### 3.1 Veri ve iş kuralları

| ID | Eksik | Neden gerekli | Öneri | Öncelik |
|----|-------|---------------|-------|---------|
| E-01 | Kişi (ziyaretçi) kaydı ayrı mı? | Aynı kişi birden çok görüşmeye gelebilir | `Person` tablosu + telefonla otomatik tamamlama | P1 |
| E-02 | Telefon numarası formatı ve ülke kodu | WhatsApp/filtre/arama | E.164 normalize (`+905xx…`), TR varsayılan | P1 |
| E-03 | Mükerrer randevu kontrolü | Aynı kişi/saat iki kez girilebilir | Uyarı (engelleme değil), kullanıcı onayı | P1 |
| E-04 | Randevu durumu yaşam döngüsü | "Geldi/gelmedi" listeyi nasıl etkiler? | Planlandı → Geldi/Gelmedi/Ertelendi/İptal → Tamamlandı | P0 |
| E-05 | Silme mi, arşivleme mi? | Denetim ve geri alma | Soft delete + 30 gün geri alma; kalıcı silme yönetici | P1 |
| E-06 | Arama ve filtre | 100+ kayıtta liste kullanılamaz | Ad, telefon, kategori, görüşmeci, durum filtresi | P0 |
| E-07 | Saat dilimi ve gece yarısı | 23:50 randevusu hangi güne düşer? | Yerel saatle sakla + UTC; gün = yerel gün | P1 |
| E-08 | Çalışma saati / gece randevusu | Gece bildirimi rahatsız eder | Sessiz saat (22:00-08:00) opsiyonu | P2 |
| E-09 | Görüşme sonucu zorunluluğu | Sonuçlar girilmiyor | Görüşme saati geçince "sonuç bekliyor" rozeti | P1 |
| E-10 | Sonuç değiştirme yetkisi | Yanlış giriş düzeltilebilmeli | Görüşmeci kendi sonucunu, yönetici tümünü düzenler | P1 |
| E-11 | Veri saklama süresi | KVKK | Varsayılan 2 yıl, ayarlanabilir; anonimleştirme | P1 |
| E-12 | Toplu işlemler | İptal günü tüm randevular | Seçili kayıtları iptal/ertele (Faz 2) | P2 |
| E-13 | Ek dosya | Rapor, form, dilekçe | Faz 2; MVP'de yalnızca metin | P3 |
| E-14 | Yazdırma / PDF | Masada kâğıt liste isteyen olabilir | Faz 2, PNG'den PDF | P2 |

### 3.2 Ekran ve kullanılabilirlik

| ID | Eksik | Öneri | Öncelik |
|----|-------|-------|---------|
| E-15 | Giriş ekranı ve oturum kilidi | Kullanıcı adı/şifre, 5 hatalı denemede kilit, 30 dk hareketsizlikte kilit | P0 |
| E-16 | Boş durum (empty state) | "Bugün görüşme yok" + "Görüşme ekle" butonu | P1 |
| E-17 | Yükleniyor/hata durumları | İskelet yükleme, tekrar dene, hata günlüğü | P1 |
| E-18 | Silme onayı ve geri alma | "Görüşme silinsin mi?" + 5 sn "Geri al" | P1 |
| E-19 | Klavye kısayolları | `Ctrl+N` yeni, `Ctrl+F` ara, `←/→` gün, `Esc` kapat | P2 |
| E-20 | Yüksek DPI / %200 ölçek | 1920x1080 ve 4K testleri | P1 |
| E-21 | Erişilebilirlik | Kontrast, ekran okuyucu etiketleri, odak sırası | P2 |
| E-22 | Bildirim aksiyon ekranı | 4 seçenek + görüşme özeti + "Ara" butonu | P0 |
| E-23 | "Bugüne dön" butonu | Tarih gezinmesinde kaybolmamak için | P1 |
| E-24 | Liste üstünde özet | "12 görüşme · 3 tamamlandı · 1 gelmedi" | P2 |
| E-25 | Durum renkleri | Kategori rengi + durum rozeti; renk körü için metin | P1 |

### 3.3 Entegrasyon ve işletim

| ID | Eksik | Öneri | Öncelik |
|----|-------|-------|---------|
| E-26 | WhatsApp Desktop kurulu değilse | WhatsApp Web'e yönlendir + kullanım ipucu | P1 |
| E-27 | Çoklu alıcıya görsel gönderme | WhatsApp tek seferde çoklu seçim destekler; kullanıcıya adım adım rehber | P1 |
| E-28 | Push konu adresi güvenliği | Tahmin edilemez ntfy topic; mümkünse erişim tokenı | P1 |
| E-29 | Push'ta kişisel veri | Bildirimde yalnızca "Randevu yaklaşıyor" + saat; isim opsiyonel | P0 |
| E-30 | Bildirim kaçırma | Açılışta kaçırılanları "gecikmiş" olarak göster | P0 |
| E-31 | Otomatik başlangıç | Kurulumda "Windows açılışında başlat" seçeneği | P1 |
| E-32 | Tek örnek (single instance) | İkinci açılışta mevcut pencereye odaklan | P1 |
| E-33 | Güncelleme | MVP'de manuel kurulum; sürüm kontrolü Faz 2 | P2 |
| E-34 | Günlük yedek | Uygulama açılışında/çıkışında otomatik yerel yedek, 30 gün sakla | P1 |
| E-35 | Yedeği geri yükleme güvenliği | Önce otomatik yedek, sonra doğrula, sonra değiştir; yeniden başlat | P0 |
| E-36 | Kurulum/kaldırma | Per-user Inno Setup; veri `%LocalAppData%` altında korunur | P1 |
| E-37 | Günlük gönderim saati | Varsayılan 08:30, ayarlanabilir; kaçırılırsa uyarı | P0 |
| E-38 | Alıcı rehberi | Ad, telefon, aktiflik, etiket; WhatsApp çoklu seçim listesi | P0 |
| E-39 | Gönderim günlüğü | Ne zaman, kime, hangi tarih listesi, başarılı mı | P1 |
| E-40 | Kategori kullanımda silinemez | Pasifleştir; mevcut kayıtlar korunur | P1 |

## 4. İlk aşamada acilen geliştirilmesi gerekenler (P0)

Aşağıdaki 12 madde olmadan uygulama "çalışan ürün" sayılmaz:

1. **Çözüm iskeleti + SQLite şeması + migration** — her şeyin temeli.
2. **Giriş ve roller** — kişisel veriye erişim kontrolsüz olmamalı.
3. **Görüşme ekleme + zorunlu alan doğrulaması** — çekirdek değer.
4. **Günlük liste + tarih gezinme + saat sıralaması** — ana ekran.
5. **Görüşme detayı + düzenleme** — kaydın yaşaması için.
6. **Kategori CRUD** — görüşme konusu onsuz seçilemez.
7. **Görüşme sonucu + durum yaşam döngüsü** — "geldi/gelmedi" takibi.
8. **Hatırlatıcı motoru (kalıcı, yeniden başlatmaya dayanıklı)** — ürünün ana vaadi.
9. **Windows toast + uygulama içi aksiyon penceresi (4 seçenek)** — hatırlatmanın
   kullanıcı tarafından kapatılabilmesi.
10. **Yerel yedekleme + geri yükleme** — veri kaybı kabul edilemez.
11. **WhatsApp için PNG üretimi + pano + deep link** — günlük paylaşım.
12. **Ayarlar (tema, yedek yolu, bildirim, hakkında)** — yapılandırılabilirlik.

P0 olmayan ama ilk sürümde beklenenler: arama/filtre, alıcı rehberi, push, günlük
gönderim zamanlayıcısı, denetim kaydı, otomatik yedek, tek örnek, DPI uyumu.

## 5. MoSCoW ve MVP sınırı

| Priorite | Kapsam | Sprint |
|----------|--------|--------|
| **Must** | Giriş/rol, görüşme CRUD, günlük liste, detay/düzenleme, kategori, sonuç, hatırlatıcı + 4 aksiyon, yerel yedek/geri yükleme, temel ayarlar | 1-4 |
| **Should** | WhatsApp PNG + deep link + metin, alıcı rehberi, günlük gönderim uyarısı, push, arama/filtre, denetim kaydı, otomatik yedek | 4-6 |
| **Could** | Google Drive, raporlar, Excel, tekrar serisi, yedek şifreleme, PDF | Faz 2 |
| **Won't (bu sürüm)** | Otomatik WhatsApp API, mobil uygulama, çok kullanıcı sunucu, SMS, ses kaydı | Faz 3+ |

**MVP çıkış kriteri:** Sekreter bir günün tüm randevularını girebilir, listeyi
WhatsApp'ta paylaşabilir, hatırlatmaları alıp sonuçlandırabilir ve ertesi gün yedeği
geri yükleyebilir. Bu 4 senaryo uçtan uca geçmeden sürüm çıkmaz.

## 6. Doğrulanması gereken riskli varsayımlar (spike'lar)

| Spike | Soru | Süre | Başarısızlık durumunda |
|-------|------|------|------------------------|
| S-01 | WhatsApp Desktop, panodaki PNG'yi sohbet alanına yapıştırıyor mu? | 0,5 gün | Görseli dosya olarak kaydedip "Dosya ekle" adımı gösterilir |
| S-02 | Windows toast, uygulama tepside ve kilit ekranındayken görünüyor mu? | 0,5 gün | Uygulama içi topmost pencere + görev çubuğu yanıp sönmesi |
| S-03 | ntfy push, kurumsal ağ/proxy arkasında çalışıyor mu? | 0,5 gün | Telegram Bot API alternatifi |
| S-04 | SQLite eşzamanlı yedek (online backup) tutarlı mı? | 0,5 gün | Uygulamayı kilitle, dosyayı kopyala, doğrula |
| S-05 | 1000 kayıtta liste açılışı < 300 ms mi? | 0,5 gün | Sanallaştırma + sayfalama |

Spike'lar Sprint 0'da yapılır; sonuçları bu dokümana işlenir.

## 7. Kapsam dışı bırakma gerekçeleri

- **Otomatik WhatsApp:** Hesap banı ve resmî API maliyeti; MVP'de değer/risk oranı düşük.
- **Mobil uygulama:** Push ile bildirim ihtiyacı karşılanıyor; ayrı uygulama bakım yükü.
- **Çok kullanıcı:** Tek bilgisayar seçildi; roller yerel hesaplarla karşılanıyor.
- **Google Drive:** OAuth kurulumu ve onay süreci MVP'yi geciktirir; yerel yedek yeterli.
- **Raporlama:** Veri birikmeden anlamlı değil; Faz 2'de gerçek veriyle tasarlanmalı.

## 8. Açık uçlar (karar gerektiren)

Detaylı liste ve önerilen varsayılanlar: [06_Karar_Kaydi_ve_Sorular.md](06_Karar_Kaydi_ve_Sorular.md).
En kritik üçü: ürün adı, yedek dosya adı formatı, "sırası gelen" bildirim kuralı.
