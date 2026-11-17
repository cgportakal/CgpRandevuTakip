# Başlangıç — Diğer Bilgisayar

Bu dosya, projeyi ikinci bilgisayarda açmak için tek sayfalık başlangıç rehberidir.

> **İlk sefer:** Bu dosya henüz diğer bilgisayarda yoksa bir kez `git pull --ff-only`
> çalıştırın. Sonrasında aşağıdaki tek komut yeterlidir.

## macOS (çift tık veya Terminal)

```bash
bash basla.command
```

veya Finder'da `basla.command` dosyasına çift tıklayın.

- Node.js yoksa Homebrew ile Node 22 kurmayı teklif eder.
- `git pull --ff-only` yapar.
- `node scripts/bootstrap.mjs` ile .NET restore, `npm ci`, env dosyalarını hazırlar.
- `coordination/CURRENT.md` özetini ve aktif kilidi gösterir.

Soruları atlamak için:

```bash
bash basla.command --yes
```

## Windows

```bat
basla.cmd
```

`--yes` / `--check` aynı şekilde çalışır:

```bat
basla.cmd --check
```

## Elle yapmak isterseniz

```bash
git pull --ff-only
node scripts/bootstrap.mjs --check   # eksik varsa: --install
cat coordination/CURRENT.md
node coordination/lock.mjs status
```

Node yoksa önce kurun:

```bash
# macOS
brew install node@22 && brew link --overwrite node@22

# Windows
winget install OpenJS.NodeJS.LTS
```

.NET 8 SDK gerekir:

```bash
# macOS
brew install --cask dotnet-sdk

# Windows
winget install Microsoft.DotNet.SDK.8
```

## Sonraki adım (kod değişikliği)

`master` korumalıdır; doğrudan push reddedilir.

```bash
node coordination/lock.mjs acquire --machine <makine> --task "<iş>" --branch agent/<makine>/<konu>
git switch -c agent/<makine>/<konu>
# ... çalış ...
git push -u origin agent/<makine>/<konu>
gh pr create --fill
gh pr checks --watch
gh pr merge --squash --delete-branch
git switch master && git pull --ff-only
```

## Nereden okumalı

| Konu | Dosya |
|---|---|
| Devir durumu | `coordination/CURRENT.md` |
| Ajan kuralları | `AGENTS.md` |
| Çalışma protokolü | `coordination/PROTOCOL.md` |
| Ortam kurulumu | `docs/35_Gelistirme_Ortami.md` |
| API / web / mobil | `docs/30_Api_ve_Mobil_Kurulum.md` |

**Not:** Masaüstü (WPF) yalnızca Windows'ta derlenir. macOS'ta API, web ve mobil (iOS) geliştirilir.
