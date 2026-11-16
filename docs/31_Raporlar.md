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

## Dışa aktarma

| Biçim | Ayrıntı |
|---|---|
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
