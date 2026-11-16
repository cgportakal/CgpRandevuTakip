# CGP - Randevu Takip · Web

Next.js (App Router) tabanlı web istemcisi. Aynı ASP.NET Core API’sini kullanır.

## Çalıştırma

```powershell
npm install
$env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5199'
npm run dev
```

- `http://127.0.0.1:3000`
- Giriş: API’de tanımlı kullanıcı (ilk kurulum desktop veya `/v1/auth/setup`).
- Üretim: `npm run build` + `npm run start`.

## Kapsam

- Günlük liste: tarih gezinme, arama, yenileme
- Yeni görüşme + tekrarlayan seri
- Detay, görüşme sonucu, silme, seri iptali
- 7 rapor türü ve özet KPI’lar
- Çevrimdışı okuma önbelleği (`localStorage`)

Ayrıntılar: [`../docs/29_Platform_Mimarisi.md`](../docs/29_Platform_Mimarisi.md),
[`../docs/30_Api_ve_Mobil_Kurulum.md`](../docs/30_Api_ve_Mobil_Kurulum.md).
