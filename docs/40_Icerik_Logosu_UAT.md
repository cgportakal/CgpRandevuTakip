# 40 — İçerik Logosu Yükleme UAT

**Tarih:** 18.11.2026 · **Kapsam:** Web içerik konsolu logo yükleme

## 1. Senaryo

1. API ve web yerel olarak başlatıldı; admin hesabıyla giriş yapıldı.
2. **İçerik Yönetimi → Kurum ve Ayarlar** açıldı.
3. 160×80 PNG logo dosyası seçildi.
4. Yükleme sonrası önizleme, bildirim ve kayıt dosyası kontrol edildi.

## 2. Sonuçlar

| Kontrol | Sonuç |
|---|---|
| Logo yükleme isteği (`POST /v1/admin/content/logo`) | ✅ |
| “Kurum logosu yüklendi.” bildirimi | ✅ |
| `branding/institution-logo.png` oluştu | ✅ |
| Sürüm sorgulu logo URL’si (`?v=...`) | ✅ |
| Logo önizlemesi | ✅ 160×80 PNG |
| Anonim `GET /v1/content/logo` | ✅ HTTP 200 |
| API testi (yükleme + anonim erişim) | ✅ |

## 3. Bulunan sorun ve düzeltme

**Logo önizlemesi kırıktı.** `/v1/content/logo` ucu `RequireAuthorization()` grubundaydı;
`<img src>` tarayıcı isteğine Bearer başlığı eklemediği için görsel 401 alıyordu.

- Logo ucu `.AllowAnonymous()` yapıldı; kurum logosu hassas veri değildir.
- API testine anonim logo erişimi eklendi.
- Önizleme yeniden yüklendiğinde 160×80 görsel doğrulandı.

## 4. Notlar

- Logo yükleme anında geçerlidir ve `contentChanged` olayı gönderilir.
- İçerik sürümü geri alma işlemi logo dosyasını geri almaz; logo ayrı bir dosya asset’idir.
- İstemciler `?v=<dosya zamanı>` sayesinde eski logoyu önbellekten göstermez.
