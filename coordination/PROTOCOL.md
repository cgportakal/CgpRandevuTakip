# PROTOCOL — Çalışma ve devir kuralları

Bu depo iki bilgisayarda iki ajan tarafından geliştiriliyor. Amaç: aynı dosyaya
aynı anda yazmamak, kaybolan iş bırakmamak ve `master`'ı her zaman yeşil tutmak.

## 0. Ortam

- İlk kurulum: `node scripts/bootstrap.mjs` (eksikler: `--check`, kurulum: `--install`).
- Ayrıntılar: `docs/35_Gelistirme_Ortami.md`.
- `node_modules`, `bin`, `obj`, veritabanı ve anahtarlar git ile taşınmaz; `npm ci` /
  `dotnet restore` ile yeniden üretilir.
- Masaüstü (WPF) yalnızca Windows’ta derlenir; macOS’ta API, web ve mobil geliştirilir.

## 1. Altın kurallar

1. **Başlamadan önce çek:** `git pull --ff-only`. Uyuşmazsa çözmeden işe başlama.
2. **Kilitle:** `node coordination/lock.mjs acquire ...`; başka makinenin 8 saatten yeni
   kilidi varsa o konuya/dosyalara dokunma.
3. **Küçük commit:** tek konu, anlaşılır mesaj, sır yok.
4. **Push öncesi:** `git pull --rebase`, testler, `CURRENT.md` güncellemesi, kilit bırakma.
5. **`master` korumalı:** doğrudan push reddedilir (GH006). Tüm değişiklikler dal + PR + yeşil CI.
   Force-push ve dal silme kapalıdır.
6. **Başkasının dosyasını silme:** çakışmada iki tarafı oku, birleştir; emin değilsen dur ve not bırak.

## 2. Dal stratejisi

| Durum | Dal |
|---|---|
| Her değişiklik (küçük düzeltme dahil) | `agent/<makine>/<konu>` + PR |
| Deneysel/riskli iş | `experiment/<konu>` (merge edilmezse silinir) |

`master` korumalı olduğu için doğrudan push `GH006` ile reddedilir; PR zorunludur.

PR akışı:

```bash
git switch -c agent/macbook/content-admin
# ... çalış ...
git push -u origin agent/macbook/content-admin
gh pr create --fill
gh pr checks --watch
gh pr merge --squash --delete-branch
```

CI üç işi de çalıştırır: .NET, Web (Next.js), Mobil (Expo/TS). Kırmızıysa merge yok.
`master` korumalıdır: force-push kapalı, silme kapalı, üç kontrol zorunlu.

## 3. Kilit yaşam döngüsü

```bash
node coordination/lock.mjs status
node coordination/lock.mjs acquire --machine cafer-desktop --task "Icerik konsolu" --branch agent/cafer-desktop/content
node coordination/lock.mjs release --machine cafer-desktop
```

- Kilit `coordination/locks/<makine>.json` olarak commit edilir; diğer makine `git pull` ile görür.
- **8 saat** sonra bayat sayılır. Bayat kilidi devralacaksan `--force` kullan ve nedeni
  `CURRENT.md`'ye yaz.
- İş bitmeden kilit bırakılmaz; acil çıkışta `CURRENT.md`'ye “yarım kaldı, dosyalar şunlar” notu düş.
- Aynı makinede yeni oturum, eski kilidi günceller (`acquire` aynı makineyi engellemez).

## 4. Devir (handoff) kontrol listesi

- [ ] Testler yeşil: `dotnet test CgpRandevuTakip.sln -c Release`
- [ ] `cd web && npm run build`
- [ ] `cd mobile && npx tsc --noEmit`
- [ ] `coordination/CURRENT.md` güncel: son commit, yapılanlar, sıradaki iş, aktif kilit, tuzaklar
- [ ] `node coordination/lock.mjs release --machine <makine>`
- [ ] Dalı push et, PR aç, kontrolleri bekle, squash merge:
  ```bash
  git push -u origin agent/<makine>/<konu>
  gh pr create --fill
  gh pr checks --watch
  gh pr merge --squash --delete-branch
  git switch master && git pull --ff-only
  ```
- [ ] CI yeşil (`gh run list --limit 1`)

## 5. Çakışma çözümü

1. `git pull --rebase`; çakışan dosyaları listele.
2. İki tarafın amacını anla; körlemesine “ours/theirs” seçme.
3. Ortak çözümü uygula; ilgili testleri çalıştır.
4. `git rebase --continue`; gerekirse `CURRENT.md`'ye not düş.
5. Anlaşılamayan çakışmada push etme; `CURRENT.md`'ye “çakışma: dosya X, sebep Y” yaz.

## 6. Sırlar ve üretim

- Commit edilmez: `src/CgpRandevuTakip.Api/data/`, `*.key`, `vapid.json`,
  `signing.thumbprint`, `*.pfx`, `.env*`.
- Push anahtarları: VAPID otomatik üretilir; `Expo__AccessToken` yalnızca ortam değişkeni.
- Üretimde `Cors:AllowedOrigins`, `Cgp JWT` ve `CGP_API_SECRET_KEY` mutlaka ayarlanmalı.

## 7. Sık kullanılan komutlar

```bash
git status -sb
git pull --rebase
node coordination/lock.mjs status
dotnet test CgpRandevuTakip.sln -c Release
cd web && npm run build
cd mobile && npx tsc --noEmit
gh run list --limit 3
```
