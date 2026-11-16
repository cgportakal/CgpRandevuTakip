# Tekrarlayan Görüşme Serisi

Sürüm **1.6.0** ile yeni görüşme oluştururken aynı görüşme haftalık, 2 haftada bir veya aylık
olarak seri hâlinde planlanabilir. Amaç, düzenli takip gerektiren veli/öğrenci görüşmelerini
tek tek girmek yerine tek işlemde oluşturmak.

## Kurallar

| Kural | Değer |
|---|---|
| Sıklıklar | Her hafta (7 gün), 2 haftada bir (14 gün), her ay |
| Kayıt sayısı | İlk görüşme dahil **2–12** |
| Uygun olmayan tarih | Geçmiş tarih veya çakışan saat → **atlanır**, seri kısmi oluşur |
| Tüm tarihler uygun değilse | Seri oluşturulmaz, kullanıcıya hata gösterilir |
| Seri yalnızca | **Yeni** görüşme eklerken önerilir; düzenlemede seri paneli görünmez |

### Aylık seride ay sonu kırpması

Aylık seride gün numarası korunur; hedef ayda o gün yoksa ayın sonuna kırpılır. Hesap her zaman
ilk randevunun gününden yapılır:

```
31 Ocak → 28 Şubat → 31 Mart → 30 Nisan ...
29 Şubat (artık yıl) → 29 Mart → 29 Nisan → ... → 28 Şubat (artık olmayan yıl)
```

Bu davranış `SeriesScheduleCalculator.AddMonthsClamped` içinde tek yerde uygulanır.

## Akış

1. **Yeni görüşme** penceresinde "Bu görüşmeyi seri olarak planla" işaretlenir.
2. Sıklık ve kayıt sayısı seçilir.
3. **Önizle** ile her tarih için durum görüntülenir:
   - `Oluşturulacak` (yeşil) — tarih uygun.
   - `Çakışıyor` (kırmızı) — aynı görüşmeci veya aynı kişiyle çakışma var; açıklamada hangi
     görüşmeyle çakıştığı yazar.
   - `Geçmiş` — tarih/saat geçmişte kaldığı için oluşturulmaz.
4. Özet satırı "X görüşme oluşturulacak · Y tarih atlanacak" biçiminde bilgi verir.
5. **Kaydet**'te en az bir uygun tarih varsa seri oluşturulur. Atlanan tarih varsa onay istenir.
6. Seri oluşturulunca bilgi penceresi sonucu (oluşturulan/atlanan) gösterir.

> Önizleme zorunlu değildir; kaydet sırasında plan yeniden hesaplanır ve aynı kurallar uygulanır.

## Veri modeli

Seri için ayrı tablo yoktur; her randevu kendi satırında şu alanları taşır:

| Alan | Anlamı |
|---|---|
| `SeriesId` | Seriyi birbirine bağlayan kimlik (tekil randevularda `null`) |
| `SeriesIndex` | Serideki sıra (1 tabanlı; atlanan tarihler sıra numarasını tüketir) |
| `SeriesCount` | Talep edilen toplam kayıt sayısı |

`SeriesId` üzerinde indeks vardır. Detay penceresinde seri kaydı `Seri 2/6` rozetiyle gösterilir.

## Seriyi düzenleme ve kayıt ekleme

Görüşme detayındaki **Seri yönetimi** penceresi iki işlem sunar:

### Kayıt ekleme
- İşlem başına **1–12** yeni kayıt eklenir; toplam seri büyüyebilir.
- Sonraki tarihler serinin **çapasından** hesaplanır; aylık kırpma korunur.
- Çakışan/geçmiş tarihler önizlemede işaretlenir, onay sonrası atlanır.
- `SeriesCount` tüm seri kayıtlarında yeni toplamla güncellenir; `SeriesIndex` sırası korunur.
- Denetim: `SeriesExtended`.

### Düzenleme
- Görüşmeci, kategori, süre, yöntem, açıklama, öğrenci bilgisi ve **saat** değiştirilebilir.
- Yalnızca **gelecekteki, planlanmış ve açık** kayıtlar etkilenir.
- Saat değişiminde çakışan kayıtlar atlanır ve kullanıcıya uyarı listesi gösterilir.
- Hatırlatıcılar yeni saate göre yeniden planlanır; gönderilmiş hatırlatıcılar korunur.
- Denetim: `SeriesUpdated`.

> Frekans ve toplam sayı bu pencerede değiştirilmez; sayıyı büyütmek için "kayıt ekle" kullanılır.

## Çapa ve sıklık alanları

| Alan | Anlamı |
|---|---|
| `SeriesAnchorLocal` | Serinin ilk yerel başlangıcı; aylık kırpma ve ekleme buradan hesaplanır |
| `SeriesFrequency` | `Weekly`, `Biweekly`, `Monthly` |

Bu alanlar `SeriesAnchorFrequency` migration'ıyla eklenir. Eski seri kayıtlarında boşsa
tarih farkından sıklık çıkarılır ve ilk kayıt çapa kabul edilerek geri doldurulur.

## Seriyi iptal etme

Görüşme detayındaki **Seriyi iptal et** düğmesi yalnızca şu koşullarda görünür:

- Seriye ait bir kayıt (`SeriesId` dolu),
- Durum `Scheduled`,
- Başlangıç zamanı gelecekte,
- Kullanıcı görüşme yönetme yetkisine sahip (yönetici veya sekreter).

İptal işlemi serideki **gelecekteki ve hâlâ planlanmış** kayıtları iptal eder. Geçmiş tarihli,
kapanmış, tamamlanmış veya zaten iptal edilmiş kayıtlar **değiştirilmez**; sonuç penceresinde
"X görüşme iptal edildi · Y kayıt değişmedi" özeti gösterilir.

## Denetim kayıtları

| Aksiyon | Ne zaman |
|---|---|
| `SeriesCreated` | Seri oluşturulduğunda; sıklık, istenen/oluşturulan/atlanan sayı ve görüşmeci bilgisi JSON olarak yazılır |
| `SeriesCancelled` | Seri iptal edildiğinde; iptal edilen ve değişmeyen kayıt sayısı yazılır |
| `SeriesUpdated` | Seri düzenlendiğinde; güncellenen/atlanan sayı ve yeni alanlar yazılır |
| `SeriesExtended` | Seriye kayıt eklendiğinde; istenen/eklenen/atlanan sayı ve yeni toplam yazılır |

## Testler

- `SeriesScheduleCalculatorTests`: haftalık/2 haftalık artışlar, ay sonu kırpması, artık yıl
  (29 Şubat), yıl geçişi, geçersiz sıklık ve sayı sınırları.
- `AppointmentSeriesTests`: tüm tarihlerin oluşturulması, çakışan tarihin atlanması, hiç uygun
  tarih yoksa hata, kısmi oluşturma, iptal kapsamı (tamamlanmış/geçmiş kayıtlar korunur),
  yetki kontrolü.
