# 00 — Vizyon ve Kapsam

**Proje:** CGP - Randevu Takip
**Sürüm:** 0.1 · **Tarih:** 06.11.2026 · **Durum:** Onay bekliyor

---

## 1. Yönetici özeti

Kurumda randevuyla görüşmeye gelen kişilerin kaydı, günlük listesinin paylaşımı ve
zamanında hatırlatılması bugün elle yapılıyor. Bu iş akışı; kayıt kaybı, günlük liste
hazırlama yükü, aranması unutulan kişiler ve sonuçların kayda geçmemesi risklerini taşıyor.

CGP - Randevu Takip, Windows 11 masaüstünde çalışan, çevrimdışı öncelikli, tek kullanıcılı bir
uygulamadır. Randevuyu oluşturur, günlük listeyi tarih bazında gösterir, listeden PNG
üretip WhatsApp'ta paylaşılabilir hâle getirir, seçilen aralıklarda hatırlatma üretir ve
görüşme sonucunu kaydeder.

## 2. Problem tanımı

| Problem | Bugünkü durum | Etki |
|---------|---------------|------|
| Kayıtların dağınıklığı | Defter/Excel/WhatsApp mesajları | Kayıp, mükerrer kayıt, arama zorluğu |
| Günlük liste hazırlığı | Elle yazma / ekran görüntüsü | Günde 10-20 dk kayıp, hata |
| Zamanında hatırlatma | Kişisel hafıza, alarm | Kişi aranmıyor, görüşme kaçıyor |
| Görüşme sonucu | Çoğu zaman kaydedilmiyor | Geriye dönük izleme yok, raporlama yok |
| Yedekleme | Yok veya manuel kopya | Bilgisayar arızasında tüm veri kaybı |

## 3. Vizyon cümlesi

> "Görüşme trafiğinin tek ekrandan yönetildiği, günlük paylaşımın 20 saniyede tamamlandığı
> ve hiçbir görüşmenin hatırlatma olmadan geçmediği bir masaüstü deneyimi."

## 4. Hedefler ve başarı ölçütleri

| ID | Hedef | Ölçüt | Hedef değer |
|----|-------|-------|-------------|
| G-01 | Kayıt hızı | Yeni görüşme kaydı süresi | < 60 sn |
| G-02 | Günlük paylaşım | Listenin WhatsApp'a hazırlanma süresi | < 30 sn |
| G-03 | Hatırlatma güvenilirliği | Zamanında üretilen hatırlatma oranı | ≥ %98 |
| G-04 | Veri kaybı | Başarısız yedek/geri dönüş sayısı | 0 |
| G-05 | Sonuç kaydı | Sonucu girilmiş görüşme oranı | ≥ %90 |
| G-06 | Kullanılabilirlik | Kritik akışta tıklama sayısı | Liste → paylaşım ≤ 3 |
| G-07 | Kararlılık | 8 saatlik vardiyada çökme | 0 |
| G-08 | Memnuniyet | Kullanıcı anketi (1-5) | ≥ 4 |

## 5. Paydaşlar

| Paydaş | Rol | İlgi | Etki |
|--------|-----|------|------|
| Kurum yöneticisi | Sponsor, satın alma/onay | Verimlilik, KVKK uyumu | Yüksek |
| Sekreter / karşılama | Birincil kullanıcı | Hızlı kayıt, kolay paylaşım | Yüksek |
| Görüşmeci (uzman/öğretmen/danışman) | "Kiminle görüşülecek" | Kendi randevusunu görme, sonuç girme | Orta |
| Görüşmeye gelen kişi / veli | Veri sahibi | Randevu doğruluğu, mahremiyet | Düşük (dolaylı) |
| BT / bakım | Kurulum, yedek | Kurulum kolaylığı, desteklenebilirlik | Orta |
| KVKK / hukuk | Uyum | Aydınlatma, saklama, aktarım | Orta |

## 6. Personas

**P-01 — Sekreter (birincil).** Günde 20-40 randevu girer, sabah listeyi WhatsApp'tan
yöneticiye atar, telefonu açar, görüşme sonuçlarını gün içinde işler. Teknik bilgisi orta;
hız ve az tıklama önceliği.

**P-02 — Görüşmeci.** Kendi randevularını ve kimin geleceğini görmek ister; sonucu
("geldi/gelmedi", not) girer. Uygulamayı sürekli açık tutmaz.

**P-03 — Yönetici.** Günlük listeyi alır, kategori ve kullanıcıları yönetir, yedeği alır.
Rapor ve denetim kaydı ister.

**P-04 — Ziyaretçi.** Uygulamayı kullanmaz; yalnızca verisi işlenir. Bilgilendirilme ve
silme hakkı vardır.

## 7. Kapsam

### 7.1 MVP (Faz 1) — zorunlu

- Kullanıcı adı/şifre ile giriş, rol bazlı yetki (Yönetici, Sekreter, Görüşmeci)
- Görüşme ekleme: kişi, telefon, tarih/saat, görüşülecek kişi, kategori, açıklama,
  yöntem, çoklu hatırlatıcı, opsiyonel öğrenci adı
- Günlük görüşme listesi: `gg/AA/yyyy` başlık, önceki/sonraki gün, saat sırası
- Görüşme detayları ve düzenleme
- Görüşme sonucu: geldi/gelmedi/ertelendi/iptal, değerlendirme, tekrar, kapanış
- Kategori yönetimi (renk, sıra, pasifleştirme)
- Hatırlatıcı motoru: 1 gün / 1 saat / 30 dk, çoklu seçim, varsayılan 30 dk
- Windows toast + uygulama içi hatırlatma penceresi + 4 kapatma aksiyonu
- Telefona push (ntfy veya Telegram — yapılandırılabilir)
- Günlük liste PNG üretimi, panoya kopyalama, WhatsApp'ı açma, metin şablonu
- Alıcı rehberi (kurum içi kişiler) ve çoklu seçim
- Görüşme detaylarını WhatsApp'ta paylaşma
- Yerel yedekleme/geri yükleme (ZIP + manifest + SHA-256)
- Ayarlar: tema (koyu/açık/otomatik), kategoriler, yedek konumu, bildirim, hakkında
- Denetim kaydı (kim, ne zaman, ne yaptı)
- Kurulum paketi (x64), otomatik başlangıç seçeneği

### 7.2 Faz 2 — önemli

- Google Drive'a yedekleme (OAuth)
- Raporlar: günlük/haftalık sayılar, gelmedi oranı, kategori dağılımı
- Tekrarlayan görüşme serisi (haftalık/aylık)
- Excel/CSV içe ve dışa aktarma
- Yedek şifreleme (parola tabanlı AES-256-GCM)
- Bildirim eskalasyonu ve sessiz saatler
- Arama geçmişi, kayıtlı filtreler

### 7.3 Faz 3 — ileri

- Mobil istemci (MAUI) ve senkronizasyon
- Resmî WhatsApp Cloud API ile otomatik gönderim
- Çok kullanıcı/çok şube sunucu mimarisi
- Takvim (Outlook/Google) entegrasyonu
- Otomatik güncelleme

### 7.4 Kapsam dışı

- Tam CRM / faturalama / muhasebe
- SMS gönderimi (maliyet ve mevzuat nedeniyle)
- Görüşme sesi/video kaydı
- Yapay zekâ özetleme (şimdilik)
- Web tabanlı yönetim paneli

## 8. Varsayımlar

| ID | Varsayım | Doğrulanacak mı? |
|----|----------|------------------|
| A-01 | Kurum tek lokasyon, tek bilgisayar | Evet |
| A-02 | Görüşmeler 5-60 dk arasında, varsayılan 30 dk | Onaylandı |
| A-03 | Bilgisayar mesai saatlerinde açık, uygulama tepside çalışır | Evet |
| A-04 | WhatsApp Desktop veya WhatsApp Web kurulu | Evet |
| A-05 | Alıcılar kurum içi, telefon numaraları biliniyor | Evet |
| A-06 | İnternet kesintisi olabilir; çekirdek işlevler çevrimdışı çalışmalı | Hayır |
| A-07 | Arayüz yalnızca Türkçe | Evet |
| A-08 | Saat dilimi Europe/Istanbul (UTC+3, DST yok) | Evet |

## 9. Kısıtlar

- **Platform:** Windows 11 x64 (Windows 10 desteği hedeflenmez)
- **Dağıtım:** Yerel kurulum, yönetici hakkı gerektirmeyen per-user kurulum tercih edilir
- **Veri:** Yerel SQLite; bulut yalnızca yedek ve push için
- **WhatsApp:** MVP'de otomatik gönderim yok; kullanıcı onayı zorunlu (hesap banı riski)
- **KVKK:** Kişisel veri işlenir; aydınlatma, saklama ve aktarım kuralları uygulanır
- **Bütçe:** Üçüncü taraf ücretli servis kullanılmayacak (ntfy ücretsiz katman, yerel yedek)
- **Ekip:** Tek geliştirici + ürün sahibi; 2 haftalık sprintler

## 10. Sözlük

| Terim | Anlam |
|-------|-------|
| Görüşme / Randevu | Belirli kişiyle, belirli tarih-saatte yapılacak planlı görüşme |
| Görüşmeci (host) | Kurumda görüşmeyi yapacak kişi |
| Ziyaretçi | Görüşmeye gelecek kişi (veli, öğrenci, başvuru sahibi) |
| Kategori | Görüşme konusu (danışmanlık, şikâyet, bilgi talebi vb.) |
| Hatırlatıcı | Randevudan belirli süre önce üretilen bildirim |
| Sonuç | Görüşmenin geldi/gelmedi/ertelendi/iptal durumu ve değerlendirmesi |
| Yarı otomatik paylaşım | Uygulamanın mesajı ve görseli hazırlaması, göndermeyi kullanıcının yapması |
| Push | Telefona ntfy/Telegram üzerinden gönderilen bildirim |
