# Canlı Deploy Rehberi

Bu rehber, projeyi **ücretsiz katmanlarla** yayına almanın adım adımıdır. Hesap açma, anahtar üretme ve ödeme
yöntemi gerektiren adımlar sende; repoda hiçbir sır yok, hepsi sağlayıcının panelinde ortam değişkeni olarak girilir.

> Not: Ücretsiz katman limitleri ve panel menüleri sık değişir. Aşağıdaki adımlar mantığı verir; bir ekran
> farklıysa sağlayıcının güncel belgesine bak. "Doğrula" işaretli yerler yazıldığı sırada kontrol edilemeyenlerdir.

## 1. Büyük resim

```
Tarayıcı ──► Statik arayüz (web/dist)           Vercel / Netlify / Cloudflare Pages
   │            VITE_API_URL ile ▼
   └───────► API (Docker, .NET 8)  ──────────►  SQL Server uyumlu veritabanı
              Render (veya Azure Container Apps)   Azure SQL Database ücretsiz teklifi
```

| Parça | Önerilen ücretsiz seçenek | Neden |
|---|---|---|
| Veritabanı | **Azure SQL Database — ücretsiz teklif** | SQL Server'dır: kod ve migration'lar değişmeden çalışır (PostgreSQL'e geçmek EF sağlayıcısını ve migration'ları değiştirmek demek). |
| API | **Render — Docker web servisi (Free)** | Repodaki `Dockerfile`'ı doğrudan kullanır, GitHub'dan otomatik deploy eder. Boşta uyur (ilk istek ~1 dk geç gelir) — doğrula. |
| Arayüz | **Vercel** (Netlify / Cloudflare Pages da olur) | Statik Vite çıktısı; sunucu gerekmez. |

Sıra önemlidir: **veritabanı → API → arayüz → CORS'u arayüz adresiyle güncelle → doğrula.**

## 2. Veritabanı (Azure SQL Database, ücretsiz teklif)

1. https://portal.azure.com → hesap aç (kredi kartı doğrulaması isteyebilir; ücretsiz teklif kullanım limitini
   aşmadıkça ücret çıkmaz — limit aşılınca ne olacağını seçeneklerde "durdur" olarak ayarla).
2. **Create a resource → SQL Database**. Sayfada **"Apply free offer"** (ücretsiz teklif) seçeneğini işaretle. Doğrula.
3. Yeni bir **SQL server** oluştur:
   - Authentication: **SQL authentication**; yönetici kullanıcı adı ve **güçlü bir parola** belirle. Parolayı bir parola yöneticisinde sakla.
   - Bölge: API'ye yakın bir bölge (Render'da seçtiğin bölgeye yakın; örn. Frankfurt / West Europe).
4. Free offer "limit aşılınca" davranışı: **"Auto-pause the database until next month"** seç (sürpriz fatura olmasın).
5. Veritabanı adı: `SeatReservation`.
6. **Networking** sekmesi:
   - **Public endpoint** açık olmalı.
   - **"Allow Azure services and resources to access this server"** → Yes. (Render Azure dışı bir yerde, bu yüzden bunu da ekle:)
   - **Firewall rule**: Render'ın çıkış IP'leri sabit değildir. Pratik çözüm: `0.0.0.0 – 255.255.255.255` aralığı ekle.
     Bu, güvenliği tamamen parolaya bırakır; yalnızca demo verisi tuttuğun için kabul edilebilir ama **gerçek veri koyma**. Parola uzun/rastgele olsun.
7. Bağlantı dizesi (panelde *Connection strings → ADO.NET (SQL authentication)*) şu biçimdedir; parolayı kendi parolanla değiştir:
   ```
   Server=tcp:<sunucu-adı>.database.windows.net,1433;Database=SeatReservation;User ID=<kullanıcı>;Password=<PAROLA>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60
   ```
   Bu dizeyi **sadece** Render'daki ortam değişkenine yapıştır (adım 3). Repoya, README'ye, sohbete yazma.

Serverless veritabanı boşta duraklar; ilk bağlantı ~1 dk sürebilir. Kod bunu kaldırır: EF Core `EnableRetryOnFailure`
açık, `Connection Timeout=60` uzun tutulmuştur. API ilk açılışta yine de düşerse Render onu yeniden başlatır.

## 3. API (Render)

1. https://render.com → GitHub hesabınla giriş yap, repoya erişim ver.
2. **New → Web Service** → bu repoyu seç.
   - Language/Runtime: **Docker** (kökteki `Dockerfile`'ı bulur).
   - Branch: `main`. Region: veritabanına yakın.
   - Instance type: **Free**.
   - **Health Check Path**: `/health`
3. **Environment** bölümüne şunları gir (hepsi *Environment Variables*; "Secret" olarak işaretle):

   | Anahtar | Değer | Not |
   |---|---|---|
   | `ConnectionStrings__Default` | Adım 2.7'deki dize | **Sır** |
   | `Jwt__Key` | `openssl rand -base64 48` çıktısı | **Sır**, ≥ 32 karakter. Değişirse tüm oturumlar düşer. |
   | `Database__MigrateOnStartup` | `true` | Açılışta migration'lar uygulanır. |
   | `Demo__Enabled` | `true` | Örnek etkinlikler + "Demo ile dene" düğmesi. |
   | `Cors__AllowedOrigins` | *(şimdilik boş; adım 5'te doldurulur)* | Arayüzün adresi. |
   | `PORT` | `8080` | Dockerfile 8080'de dinler; Render'a bunu söyler. Loglarda "no open ports detected" görürsen bu eksiktir. Doğrula. |
   | `Admin__Email`, `Admin__Password` | *(isteğe bağlı)* | Etkinlik oluşturmak istersen. Güçlü parola kullan. |

4. **Create Web Service**. İlk build birkaç dakika sürer. Log'da şunları ara:
   `Migration'lar uygulanıyor...` → `Demo verisi oluşturuldu.` → `Application started`.
5. Verilen adresi not et: `https://<ad>.onrender.com`. Kontrol:
   ```bash
   curl https://<ad>.onrender.com/health        # {"status":"ok"}
   curl https://<ad>.onrender.com/api/events    # 2 örnek etkinlik
   ```
   Swagger: `https://<ad>.onrender.com/swagger`.

## 4. Arayüz (Vercel)

1. https://vercel.com → GitHub ile giriş → **Add New → Project** → repoyu seç.
2. Ayarlar:
   - **Root Directory**: `web`
   - Framework Preset: **Vite** (otomatik algılanır). Build: `npm run build`, Output: `dist`.
   - **Environment Variables**: `VITE_API_URL` = `https://<ad>.onrender.com` (sonunda `/` olmasın).
     Bu değer **derleme sırasında** gömülür; değiştirirsen yeniden deploy et.
3. **Deploy**. Verilen adresi not et: `https://<proje>.vercel.app`.

(Netlify: Base directory `web`, build `npm run build`, publish `web/dist`, aynı `VITE_API_URL`. Cloudflare Pages: root `web`, output `dist`.)

## 5. CORS'u kapat

Arayüz API'den farklı bir alan adında olduğu için API, onu açıkça tanımalı. Render'da `Cors__AllowedOrigins`
değerini arayüzün adresi yap (şema dahil, sonda `/` yok, birden fazlaysa virgülle):

```
https://<proje>.vercel.app
```

Kaydedince servis yeniden başlar. Bu ayar boş kalırsa tarayıcı API'ye istek atamaz ve arayüzde
"Sunucuya ulaşılamadı" görürsün.

## 6. Doğrulama listesi

- [ ] Arayüz açılıyor, koltuk haritası görünüyor (ilk yüklemede API uyanana kadar ~1 dk bekleyebilir).
- [ ] **Demo ile dene** → giriş yapılıyor.
- [ ] Bir koltuk tut → 10:00 geri sayım başlıyor → **onayla** → koltuk koyu mor oluyor.
- [ ] İkinci bir tarayıcı/gizli pencerede aynı koltuk **"başkası tutuyor"/"satıldı"** görünüyor (≤ 3 sn).
- [ ] **Rezervasyonlarım** satın alınanı gösteriyor.
- [ ] Tarayıcı konsolunda CORS hatası yok.

Sorun giderme:

| Belirti | Neden / çözüm |
|---|---|
| Arayüzde "Sunucuya ulaşılamadı" | `Cors__AllowedOrigins` yanlış/boş, ya da `VITE_API_URL` yanlış (derleme sırasında gömülür → yeniden deploy). |
| API açılışta düşüyor, log'da SQL bağlantı hatası | Firewall kuralı (adım 2.6) veya bağlantı dizesi; serverless veritabanı uyanıyor olabilir, bir dakika sonra yeniden dene. |
| `Jwt:Key en az 32 karakter olmalı` | `Jwt__Key` tanımsız/kısa. |
| "Demo girişi bu sunucuda kapalı." | `Demo__Enabled=true` eksik. |
| Demo etkinlikleri yok | Veritabanında zaten etkinlik varsa tohumlama atlanır (bilinçli). Boş bir veritabanıyla yeniden başla ya da Admin ile etkinlik oluştur. |
| İlk istek çok yavaş | Render Free boşta uyur; normaldir. README'ye not düş. |

## 7. Bitirince

1. README'nin üstüne canlı demo bağlantısını ekle (`Canlı demo: https://<proje>.vercel.app`) ve `docs/ROADMAP.md`'de
   "Canlı deploy" maddesini işaretle.
2. Canlıda gördüğün gerçek davranışı (örn. uyanma süresi) README'ye dürüstçe yaz.

## 8. Güvenlik notları

- **Demo modu herkese açık hesap üretir.** `POST /api/auth/demo` hız sınırsızdır: biri döngüyle çağırırsa veritabanı
  misafir kullanıcıyla dolar. Demo için kabul edilebilir, gerçek ürün için değil; hız sınırlama Aşama 3 / "bilinen sınırlar" listesinde.
  Demo işin bitince `Demo__Enabled`'ı kapatabilirsin (var olan veri kalır, yeni misafir açılmaz).
- Veritabanına **gerçek kişisel veri koyma**. Misafir e-postaları `misafir-…@demo.local`'dır.
- Veritabanı parolası ve `Jwt__Key` yalnızca Render panelinde durur. Sızdığından şüphelenirsen: Azure'da parolayı sıfırla,
  `Jwt__Key`'i yenile (tüm oturumlar düşer), Render'da ortam değişkenlerini güncelle.
- Render ve Vercel panellerinde iki adımlı doğrulamayı (2FA) aç.

## 9. Alternatif: tek makinede `docker compose`

Bir sanal makinen (örn. ücretsiz bir VM) varsa her şey tek komutla çalışır; arayüz `web` servisinden (nginx) gelir,
`/api` aynı origin'e iletildiği için CORS gerekmez:

```bash
cp .env.example .env     # JWT_KEY, MSSQL_SA_PASSWORD, ADMIN_PASSWORD'ü değiştir; DEMO_ENABLED=true
docker compose up --build -d
# http://<makine>:3000
```

TLS için önüne bir ters proxy (Caddy / Cloudflare Tunnel) koy; API konteyneri yalnızca HTTP dinler.
Bu yol, ücretsiz bir SQL Server barındırma sorununu da çözer (SQL Server konteyneri aynı makinede çalışır), ama makine ve bakım sende.
