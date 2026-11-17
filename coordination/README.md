# coordination — İki ajan / iki bilgisayar devir düzeni

Bu klasör, aynı depoda iki farklı bilgisayarda çalışan ajanların çakışmadan
ilerlemesi için tek kaynaklı devir bilgisini ve yumuşak kilidi tutar.

## İlk adım (her oturum)

1. `git pull --ff-only`
2. [CURRENT.md](CURRENT.md) oku.
3. `node coordination/lock.mjs status` — başka makinenin **8 saatten yeni** kilidi varsa
   o makinenin belirttiği dosyalara/konuya dokunma.
4. `node coordination/lock.mjs acquire --machine <makine> --task "<iş>"`
5. İş bitince: testleri çalıştır, [CURRENT.md](CURRENT.md) güncelle,
   `node coordination/lock.mjs release --machine <makine>`, commit + push.

## Klasör yapısı

| Yol | Amaç |
|---|---|
| `CURRENT.md` | Son durum, sıradaki iş, aktif kilit, dikkat edilecekler |
| `PROTOCOL.md` | Dallanma, commit, çakışma ve devir kuralları |
| `lock.mjs` | Kilit al/ver/durum (Node; Windows + macOS) |
| `locks/<makine>.json` | Aktif çalışma kilidi (commit edilir) |

## Hızlı komutlar

```bash
node coordination/lock.mjs status
node coordination/lock.mjs acquire --machine macbook --task "Icerik konsolu" --branch agent/macbook/content
node coordination/lock.mjs release --machine macbook
```

Kilit **8 saatten** eskiyse bayat sayılır; `status` bunu belirtir ve diğer ajan
`acquire --force` ile devralabilir (nedenini `CURRENT.md`'ye yazmalı).
