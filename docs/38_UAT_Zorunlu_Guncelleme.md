# 38 — Zorunlu Güncelleme UAT (Web + Masaüstü)

**Tarih:** 17.11.2026 · **Sürüm:** 1.8.1 · düzeltmeler **1.8.2** (Setup-only) · **Kapsam:** web ve masaüstü zorunlu güncelleme akışı

## 1. Amaç

`version-policy` minimum sürümün altında kalan istemcilerde:

- Web’de bloklayıcı “Güncelleme gerekli” ekranının doğru sürümlerle çizilmesi,
- Masaüstünde giriş öncesi `ForcedUpdateWindow` penceresinin açılması, sürüm notlarının
  yüklenmesi ve “Çıkış” ile uygulamanın kapanması,
- API `VersionPolicy` ile kök `version-policy.json` değerlerinin tutarlı olması.

## 2. Test düzeni

| Bileşen | Değer |
|---|---|
| API | `http://127.0.0.1:5199`, geçici veri dizini `%TEMP%\opencode\cgp-force-uat` |
| API politikası (web testi) | `minWebVersion=9.9.9`, `latestWebVersion=9.9.9` |
| Web | `npm run dev`, `NEXT_PUBLIC_APP_VERSION=1.0.0` |
| Masaüstü | mevcut koddan geçici **1.0.0** Debug derlemesi |
| Masaüstü politikası | GitHub `master` → `minDesktopVersion=1.5.0`, `latestDesktopVersion=1.8.1` |

Masaüstü istemcisi sürüm politikasını GitHub `master` üzerindeki `version-policy.json` ve
`/releases/latest` uçlarından okur; yerel API bu akışta kullanılmaz.

## 3. Web sonucu

| Kontrol | Sonuç |
|---|---|
| Bloklayıcı ekran | ✅ “Güncelleme gerekli” |
| Mevcut sürüm | ✅ `1.0.0` |
| Gerekli sürüm | ✅ `9.9.9` (`minWebVersion`) |
| Son sürüm | ✅ `9.9.9` (`latestWebVersion`) |
| Eylemler | ✅ “Sayfayı yenile” düğmesi ve sürüm notları bağlantısı |
| Giriş ekranı | ✅ Gösterilmiyor (bloklayıcı ekran öncelikli) |

Ekran metni tarayıcı erişilebilirlik ağacından doğrulandı; başsız pencere nedeniyle ekran
görüntüsü alınamadı.

## 4. Masaüstü sonucu

| Kontrol | Sonuç |
|---|---|
| Pencere | ✅ “Güncelleme gerekli · CGP - Randevu Takip” |
| Mevcut sürüm | ✅ `1.0.0` |
| Gerekli sürüm | ✅ `1.5.0` |
| Son sürüm | ✅ `v1.8.1` |
| Sürüm notları | ✅ GitHub `v1.8.1` notları yüklendi (ağ hatasında “yükleniyor” metni kalmaz) |
| Düğmeler | ✅ “Şimdi güncelle”, “Çıkış” |
| Çıkış davranışı | ✅ Pencere kapanınca uygulama sonlanıyor (`APP_RUNNING=False`) |

Ekran görüntüsü: `%TEMP%\opencode\cgp-shots\forced-update-current.png` (commit edilmez).

## 5. Bulunan sorunlar ve düzeltmeler

**1) Sürüm notları ham Markdown görünüyordu.** GitHub sürüm gövdesi `##`, `**`, `-` ve
`[metin](url)` işaretleriyle geliyor; masaüstü pencereleri düz metin gösterdiği için notlar
okunaksızdı.

- `ReleaseNotesFormatter` eklendi: başlık, liste, kalın, satır içi kod ve bağlantı işaretlerini
  sadeleştirir; HTML yorumlarını ve yatay çizgileri kaldırır, boş satırları toparlar.
- `ForcedUpdateWindow` ve `ReleaseNotesWindow` bu biçimlendiriciyi kullanıyor.
- 6 birim testi eklendi (`ReleaseNotesFormatterTests`).

**2) API `appsettings.json` sürümü gerideydi.** `latestDesktopVersion` **1.8.0** kalmıştı;
kök `version-policy.json` ve GitHub Release **1.8.1** idi. **1.8.1** olarak düzeltildi.
`latestWebVersion` 1.8.0 kalıyor: web istemcisinin varsayılan `NEXT_PUBLIC_APP_VERSION`
değeri 1.8.0.

## 6. Sınırlar

- **Mobil cihaz UAT’i yapılamadı:** bu makinede Android SDK/emülatör, Xcode ve Expo CLI yok;
  fiziksel cihaz veya EAS dev build gerekiyor. Mobil zorunlu güncelleme ekranı
  (`ForcedUpdateScreen`) kod incelemesiyle doğrulandı, cihazda çalıştırılmadı.
- Web testi yerel API üzerinde yapıldı; üretim dağıtımında `version-policy` kaynağı aynı
  uçtan okunur.
- Masaüstü testi geçici 1.0.0 derlemesiyle yapıldı; test sonrası sürüm dosyaları 1.8.1’e
  geri alındı.
