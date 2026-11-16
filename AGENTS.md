# CGP - Randevu Takip · Ajan Kuralları

Bu depo **iki bilgisayarda iki ajan** tarafından geliştiriliyor. Çakışmayı önlemek için
her oturumun başında ve sonunda bu akış zorunludur.

## Başlangıç (sırayla)

1. `git pull --ff-only`
2. Ortam eksikse: `node scripts/bootstrap.mjs --check` (kurulum: `--install`, ayrıntı: `docs/35_Gelistirme_Ortami.md`).
3. [`coordination/CURRENT.md`](coordination/CURRENT.md) dosyasını oku.
4. `node coordination/lock.mjs status`
   - Başka makinenin **8 saatten yeni** kilidi varsa o makinenin konusuna/dosyalarına dokunma.
   - Bayat kilit varsa `--force` ile devral ve nedenini `CURRENT.md`'ye yaz.
5. Kendi kilidini al:
   ```bash
   node coordination/lock.mjs acquire --machine <makine> --task "<iş>" --branch <dal>
   ```

## Bitiş (handoff)

1. `dotnet test CgpRandevuTakip.sln -c Release`
2. `cd web && npm run build`
3. `cd mobile && npx tsc --noEmit`
4. [`coordination/CURRENT.md`](coordination/CURRENT.md) güncelle: son commit, yapılanlar,
   sıradaki iş, tuzaklar.
5. `node coordination/lock.mjs release --machine <makine>`
6. `git add -A && git commit && git push`
7. CI yeşil mi: `gh run list --limit 1`

## Kurallar

- `master`'a force-push yok; geçmişi geri yazma.
- Tek küçük düzeltme doğrudan `master`; çok dosyalı iş `agent/<makine>/<konu>` + PR.
- Push öncesi `git pull --rebase`.
- Sır commit etme: `src/CgpRandevuTakip.Api/data/`, `*.key`, `vapid.json`, `*.pfx`, `.env*`.
- `node_modules` / `bin` / `obj` taşınmaz; `npm ci` ve `dotnet restore` ile yeniden üretilir.
- Masaüstü (WPF) yalnızca **Windows**’ta derlenir; macOS’ta API/web/mobil geliştirilir.
- `web/AGENTS.md` Next.js tarafından üretilir; silme.
- Mobil `App.tsx` ekran navigasyonunu koru (today/detail/add/reports); basitleştirme.

## Nereden okumalı

| Konu | Dosya |
|---|---|
| Devir durumu | `coordination/CURRENT.md` |
| Çalışma protokolü | `coordination/PROTOCOL.md` |
| Ortam kurulumu | `docs/35_Gelistirme_Ortami.md` |
| Platform mimarisi | `docs/29_Platform_Mimarisi.md` |
| API/mobil/web çalıştırma | `docs/30_Api_ve_Mobil_Kurulum.md` |
| Sürüm geçmişi | `CHANGELOG.md` |
