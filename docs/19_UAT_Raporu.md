# 19 — Sprint 6D / MVP UAT Raporu

**Sürüm:** 1.0.0 · **Tarih:** 10.11.2026
**Kapsam:** Sprint 6D paketleme + MVP kabul testleri.

---

## 1. Paketleme

| Çıktı | Durum |
|-------|-------|
| `dist\CGP-RandevuTakip-1.0.0-win-x64\` (self-contained) | ✔ Üretildi, çalıştırıldı |
| `dist\CGP-RandevuTakip-1.0.0-portable.zip` | ✔ Üretildi (~92 MB) |
| `installer\CGP-RandevuTakip.iss` per-user kurulum betiği | ✔ Hazır |
| `Kurulum-Olustur.cmd` | ✔ Çalışıyor |
| Kurulum EXE'si | ⚠ Bu makinede Inno Setup 6 yok; kurulunca aynı betikle üretilir |

Kurulum özellikleri: per-user (`PrivilegesRequired=lowest`), yönetici hakkı istemez, masaüstü kısayolu opsiyonel, kullanıcı verisi kaldırmada silinmez.

## 2. Fonksiyonel UAT

| # | Senaryo | Sonuç |
|---|---------|-------|
| 1 | Admin girişi / çıkışı / beni hatırla | ✔ |
| 2 | Görüşme ekleme, düzenleme, silme, geri alma | ✔ |
| 3 | Günlük liste, tarih gezinme, arama/filtre | ✔ |
| 4 | Sıradaki görüşme, sonuç/durum geçişleri | ✔ |
| 5 | Hatırlatıcı toast/aksiyon, ziyaretçi iptali | ✔ |
| 6 | Günlük liste PNG, pano, WhatsApp, alıcı rehberi | ✔ |
| 7 | Ayarlar: tema, kurum, çalışma saatleri, push, sessiz saat | ✔ |
| 8 | Kullanıcı/görüşmeci/kategori yönetimi | ✔ |
| 9 | Toplu seçim, taşıma, silme | ✔ |
| 10 | Yedekleme: manuel, geçmiş, doğrulama | ✔ |
| 11 | Şifreli yedek (`.cgpbak`) oluşturma/doğrulama/geri yükleme | ✔ |
| 12 | Geri yükleme: güvenlik yedeği + yeniden başlatma | ✔ |
| 13 | Otomatik yedek: günde 2, üçüncü yok | ✔ |
| 14 | Denetim kaydı: listeleme, filtre, sayfalama | ✔ |
| 15 | Excel / CSV / PDF dışa aktarma | ✔ |
| 16 | BitLocker durum göstergesi | ✔ (yönetici hakkına göre “Kontrol edilemedi” olabilir) |
| 17 | Google Drive bağlantısı | ⚠ OAuth istemci bilgileri girilince test edilecek |
| 18 | Kurulum/taşınabilir paket açılışı | ✔ |

## 3. Otomasyon

- **221/221 test** geçiyor.
- Release build: 0 uyarı / 0 hata.
- CI: GitHub Actions yeşil.

## 4. Test edilemeyen / sonraki doğrulamalar

- **Google Drive uçtan uca:** kullanıcı OAuth istemci kimliği/gizli anahtarı girip “Bağlan” demeli.
- **%150 DPI:** bu ortamda sistem ölçeği değiştirilmeden görsel kontrol yapılmadı.
- **24 saat açık kalma / bellek:** ayrı dayanıklılık testi gerekir.
- **1000 kayıt performansı:** üretim verisiyle ölçülmeli.
- **Kurulum EXE'si:** Inno Setup 6 kurulduktan sonra `Kurulum-Olustur.cmd` ile üretilmeli.

## 5. Go/No-Go

- Çekirdek MVP ve veri güvenliği senaryoları **Go**.
- Google Drive gerçek testi ve kurulum EXE'si için **koşullu Go**.
