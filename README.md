# seat-reservation-engine

Eş zamanlılık odaklı koltuk/randevu rezervasyon motoru. Temel soru: **iki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?**

> Cevap: tam biri başarılı olur, diğeri `409 Conflict` alır. 100 paralel istekle test edilir
> ([`ConcurrencyTests`](tests/SeatReservation.Api.Tests/ConcurrencyTests.cs)); mekanizma `docs/ARCHITECTURE.md` §3'te.

> Durum: **Aşama 1 (backend) tamam; Aşama 2 (arayüz) kodu tamam, canlı deploy bekliyor** ([`docs/DEPLOY.md`](docs/DEPLOY.md)). Yol haritası: [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Teknoloji
.NET 8, ASP.NET Core Web API, EF Core, SQL Server, JWT, xUnit, Docker, GitHub Actions. Arayüz: React 19, TypeScript, Vite, Tailwind CSS, Vitest.

## Mimari (katmanlı)
```
src/SeatReservation.Domain          iş kuralları, entity'ler (hiçbir şeye bağımlı değil)
src/SeatReservation.Application     use-case'ler, arayüzler
src/SeatReservation.Infrastructure  EF Core, JWT, parola özetleme
src/SeatReservation.Api             HTTP katmanı, arka plan servisi
tests/SeatReservation.Domain.Tests  domain birim testleri
tests/SeatReservation.Api.Tests     gerçek HTTP hattı üzerinden entegrasyon + eş zamanlılık testleri
web/                                React arayüzü (koltuk haritası, tut/onayla + geri sayım, Rezervasyonlarım)
```
Kararlar ve gerekçeleri: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Mülakat soruları: [`docs/INTERVIEW.md`](docs/INTERVIEW.md).

## Çalıştırma
```bash
cp .env.example .env      # örnek değerleri kendine göre değiştir (JWT_KEY, parolalar)
docker compose up --build
```
* **Arayüz: http://localhost:3000** (nginx, `/api`'yi API'ye iletir). `.env.example`'daki `DEMO_ENABLED=true` ile örnek
  etkinlikler gelir ve **"Demo ile dene"** düğmesi kayıtsız giriş yaptırır.
* Swagger: http://localhost:8080/swagger
* Sağlık: http://localhost:8080/health
* `.env.example` içindeki `ADMIN_EMAIL`/`ADMIN_PASSWORD` ile giriş yapınca (Swagger → `POST /api/auth/login` → **Authorize**) etkinlik oluşturabilirsin.

### Arayüzü geliştirme modunda çalıştırma
```bash
docker compose up -d db api          # API + SQL Server (DEMO_ENABLED=true önerilir)
cd web && npm ci && npm run dev      # http://localhost:5173, /api isteklerini localhost:8080'e yönlendirir
```

## API özeti
| Uç | Kim | Ne yapar |
|---|---|---|
| `POST /api/auth/register`, `POST /api/auth/login` | herkes | Kayıt (rol: User) / giriş, JWT döner |
| `POST /api/auth/demo` | herkes (yalnızca `Demo__Enabled=true`) | Tek kullanımlık misafir hesabı açar, JWT döner |
| `GET /api/auth/me` | giriş yapmış | Token'daki kimlik |
| `POST /api/events` | Admin | Etkinlik + koltuk ızgarası oluşturur |
| `GET /api/events`, `GET /api/events/{id}/seats` | herkes | Etkinlikler, koltuk haritası |
| `POST /api/seats/{id}/hold` | giriş yapmış | Koltuğu 10 dk tutar (başkasındaysa 409) |
| `POST /api/reservations/{id}/confirm` | hold sahibi | Satın almayı onaylar |
| `GET /api/reservations/mine` | giriş yapmış | Rezervasyonlarım |

## Test
```bash
dotnet build
dotnet test
cd web && npm ci && npm test        # arayüz bileşen testleri (lint: npm run lint, derleme: npm run build)
```
Testler Docker gerektirmez (entegrasyon testleri SQLite dosyası kullanır; sınırı `ARCHITECTURE.md` §3'te).
