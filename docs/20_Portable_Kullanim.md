# 20 — Taşınabilir Kullanım

**Sürüm:** 1.1.0

## 1. Nasıl çalışır?

Taşınabilir mod iki yoldan açılır:

- EXE'nin yanında `portable.flag` dosyası varsa (portable ZIP içinde hazır gelir), veya
- Uygulama `--portable` parametresiyle çalıştırılırsa.

Bu modda tüm veriler EXE'nin bulunduğu klasördeki **`CGP-Data`** altında tutulur:

```
CGP-Data\
  data.db          → SQLite veritabanı
  logs\            → teknik/crash logları
  temp\            → geçici dosyalar
  Yedekler\        → varsayılan yedek klasörü
  remember.dat/.cred
  auto-backup-state.json
```

Normal kurulumda veriler `%LOCALAPPDATA%\CgpRandevuTakip` altında kalır.

## 2. Kurulum

1. `CGP-RandevuTakip-1.1.0-portable.zip` dosyasını yazılabilir bir klasöre açın.
2. `CgpRandevuTakip.Desktop.exe` dosyasını çalıştırın.
3. İlk açılışta kurulum ekranı gelir; yönetici hesabını oluşturun.

.NET kurulumu gerekmez (self-contained).

## 3. Taşıma

- Klasörü başka bir bilgisayara/klasöre kopyalarken **`CGP-Data` klasörünü de** kopyalayın.
- USB bellek gibi yazılabilir bir konum kullanın; salt okunur konumda uygulama normal moda döner.
- Yedekler `CGP-Data\Yedekler` altında oluşur; şifreli yedekler `.cgpbak` uzantılıdır.

## 4. Sınırlamalar

- Taşınabilir modda **Windows açılışında otomatik başlatma kullanılmaz**.
- Tek örnek kilidi klasöre özeldir; normal kurulum ile taşınabilir sürüm aynı anda açılabilir.
- Aynı `CGP-Data` klasörünü iki uygulama aynı anda kullanmamalıdır.
- Windows toast bildirimi uygulama kimliğine bağlıdır; taşınabilir klasör taşındığında ilk bildirimde yeniden kayıt gerekebilir.

## 5. Yedekleme

- Otomatik yedekleme aynı kurallarla çalışır (günde 2).
- Google Drive'a yükleme, şifreli yedeklerle çalışır.
- Geri yükleme yalnızca yönetici rolündedir.
