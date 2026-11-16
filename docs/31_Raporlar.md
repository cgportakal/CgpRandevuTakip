# Raporlar ve İstatistikler

Sürüm **1.6.0** ile masaüstü uygulamaya rapor sayfası eklendi. Amaç; günlük akışın yanında
dönemsel görüşme yoğunluğunu, tamamlanma oranlarını, kategori/görüşmeci dağılımını ve saat
alışkanlıklarını tek ekranda göstermek.

## Erişim ve roller

| Rol | Görünen veri |
|---|---|
| Yönetici | Tüm görüşmeler; kategori ve görüşmeci filtresi serbest |
| Sekreter | Tüm görüşmeler; kategori ve görüşmeci filtresi serbest |
| Görüşmeci | Yalnızca kendi görüşmeleri (filtre alanı gizlenir) |

- Raporlar menüde **Raporlar** başlığıyla tüm rollere açıktır.
- Görüşmeci rolünde kapsam sunucu tarafında (`ReportService`) zorlanır; arayüz filtresi
  gizlense de başka bir görüşmecinin verisi okunamaz.
- Silinmiş (30 gün geri alma penceresindeki) görüşmeler tüm raporlardan hariçtir.

## Tarih aralığı

- **Bu ay**, **Geçen ay**, **Bu yıl**, **Son 30 gün** hazır aralıkları tek tıkla uygulanır.
- Özel aralık için başlangıç ve bitiş tarihi seçilip **Uygula**'ya basılır. Bitiş günü dahildir.
- Ters girilen aralık (bitiş < başlangıç) otomatik olarak düzeltilir.
- Sayfa açıldığında varsayılan aralık **ayın 1'i – bugün** olur.

## Özet kartları (KPI)

| Kart | Anlamı |
|---|---|
| Toplam görüşme | Aralıktaki tüm görüşme sayısı |
| Tamamlandı | `Completed` durumundaki görüşmelerin oranı ve adedi |
| Gelmedi | `NoShow` durumundaki görüşmelerin oranı ve adedi |
| İptal | `Cancelled` durumundaki görüşmelerin oranı ve adedi |
| Ortalama süre | Görüşme başına ortalama süre (dk) ve toplam süre |

Ek satırda tekil kişi, tekrar gelen kişi ve "tekrar görüşmek ister" işaretli görüşme sayısı
gösterilir. Tekrar talebi, görüşme sonucundaki **WillRepeat** alanından gelir.

## Grafikler ve tablolar

- **Dağılım grafiği:** 31 güne kadar **günlük**, daha uzun aralıkta **aylık** kovalar üretilir.
  Her kovada toplam, tamamlanan ve gelmedi sayıları yer alır; sütun genişliği en yüksek kova
  değerine göre ölçeklenir.
- **Kategori dağılımı:** kategori adı, toplam, tamamlanan ve yüzde pay.
- **Görüşmeci dağılımı:** görüşmeci adı, toplam, tamamlanan, iptal ve yüzde pay.
- **Saat dağılımı:** görüşmelerin başlangıç saatine göre yoğunluğu (kutucuklar).
- Aralıkta hiç görüşme yoksa boş durum mesajı gösterilir.

## Rapor türleri

Sayfadaki **Rapor türü** seçicisiyle aynı tarih aralığında farklı raporlar üretilir:

| Tür | İçerik |
|---|---|
| Özet | KPI'lar, günlük/aylık dağılım, kategori/görüşmeci/saat tabloları |
| Görüşmeci performansı | Görüşmeci başına toplam, tamamlanan, gelmedi, iptal, tamamlanma/gelmedi oranı, ortalama süre |
| Kategori trendi | Kategori başına toplam ve dönem kovaları |
| Katılım | Gün bazında geldi, tamamlandı, gelmedi, iptal, ertelendi, planlandı |
| Tekrar görüşme | Birden fazla gelen veya "tekrar" işaretli kişiler, son görüşme ve aralık |
| Kişi geçmişi | Ad/telefon aramasıyla kişilerin tüm görüşme geçmişi |
| Denetim / aktivite | Aksiyon bazında adetler ve son 100 denetim kaydı (yalnızca yönetici verisi) |

API karşılığı: `GET /v1/reports/summary` ve `GET /v1/reports/{kind}` (kind: `Summary`,
`HostPerformance`, `CategoryTrend`, `Attendance`, `Repeat`, `PersonHistory`, `AuditActivity`).
Görüşmeci rolü her iki uçta da yalnızca kendi verisini görür.

## Performans

- `Appointments` tablosuna `(Status, StartsAtUtc)` ve `(CategoryId, StartsAtUtc)` bileşik
  indeksleri eklendi.
- Rapor sorgusu tek geçişte hafif satırlar okur; kategori/görüşmeci/saat kırılımları bellekte
  doğrusal olarak hesaplanır (ek N+1 sorgu yok).
- 2.000 satırlık performans testi (`ReportServiceTests`) süreyi üst sınırla doğrular.
- Rapor aralığı API'de en fazla 10 yıl ile sınırlıdır.

## Dışa aktarma

| Biçim | Ayrıntı |
|---|---|
| Excel | `Özet`, `Kategori`, `Görüşmeci`, `Saat` ve rapor türüne özel sayfalar (performans, katılım, tekrar, kişi geçmişi, denetim) |
| CSV | `;` ayraçlı, UTF-8 BOM (Excel'de Türkçe karakterler doğru açılır) |
| PDF | A4 dikey, QuestPDF; özet, dağılım, kategori ve görüşmeci tabloları, saat özeti, sayfa numarası |

Dosya adları `CGP_Rapor_yyyy-MM-dd_yyyy-MM-dd` kalıbındadır. Dışa aktarma yalnızca dolu
raporlarda etkindir.

## Teknik notlar

- `IReportQueries` tek sorguda hafif `ReportRow` satırları okur; `ReportService` yerel güne
  çevrimi, kovalama ve oranları hesaplar.
- Gün sınırları yerel geceden UTC'ye çevrilir; `ToLocal`/`ToUtc` `IClock` üzerinden çalışır.
- Testler: `ReportServiceTests` (oranlar, host kapsamı, kategori/görüşmeci filtresi, günlük/aylık
  kovalar, yerel gün sınırı, boş aralık, ters aralık).

## Bilinen sınırlar

- Rapor, ilk yüklemede tüm satırları belleğe alır. On binlerce kayıtta dahi kabul edilebilir
  olsa da çok geniş aralıklarda (yıllar) yükleme birkaç saniye sürebilir.
- Para/ücret veya hedef karşılaştırma raporları bu sürümde yoktur.
- Grafikler yalnızca toplam/tamamlanan/gelmedi gösterir; renkli çok serili grafik sonraki fazdadır.
