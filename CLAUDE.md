# seat-reservation-engine

Eş zamanlılık odaklı koltuk/randevu rezervasyon motoru. Bir portföy vitrin projesi: kodu inceleyen biri 2 dakikada kaliteyi görebilmeli. Önce `docs/ROADMAP.md`'yi oku, aşama sırasına uy.

## Mimari kuralları
- Katmanlı: `Domain` → `Application` → `Infrastructure` → `Api`. Bağımlılık yönü içe doğru; `Domain` hiçbir projeye bağımlı olmaz.
- İş kuralları `Domain`'de (entity içinde), controller'da iş mantığı yok.
- .NET 8, EF Core, SQL Server. Eş zamanlılık: `RowVersion` (optimistic concurrency) + transaction.
- Controller'lar ince; use-case'ler `Application`'da.

## Çalışma kuralları
- Her özellik **ayrı commit**, anlamlı mesajla (`feat: koltuk tutma`, `test: ...`). Tek dev commit yok.
- Her davranış için test yaz (xUnit). Eş zamanlılık için gerçek paralel istek testi şart.
- Her aşama sonunda `dotnet build` ve `dotnet test` geçmeli. CI kırık bırakılmaz.
- Karmaşık yerlere "neden böyle" yorumu yaz (ne yaptığını değil, neden yaptığını).
- Proje sahibi bu kodu mülakatta açıklayacak: `docs/ARCHITECTURE.md` (kararlar ve gerekçeler) ve `docs/INTERVIEW.md` (bu projeden sorulabilecek sorular ve cevapları) dosyalarını güncel tut.
- Gereksiz kütüphane ekleme; eklersen nedenini `ARCHITECTURE.md`'ye yaz.

## Güvenlik
- Parola, anahtar, bağlantı dizesi **commit'lenmez**. `.env` yerelde kalır, `.env.example` sadece örnek değer içerir.
- `appsettings.json`'a gerçek sır yazma; ortam değişkeni kullan.
- SQL'i string birleştirerek kurma; EF Core parametreli sorgu kullan.

## Komutlar
```bash
dotnet build
dotnet test
docker compose up --build
```
