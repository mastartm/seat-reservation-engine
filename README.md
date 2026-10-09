# seat-reservation-engine

Eş zamanlılık odaklı koltuk/randevu rezervasyon motoru. Temel soru: **iki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?**

> Cevap: tam biri başarılı olur, diğeri `409 Conflict` alır. 100 paralel istekle test edilir
> ([`ConcurrencyTests`](tests/SeatReservation.Api.Tests/ConcurrencyTests.cs)); mekanizma `docs/ARCHITECTURE.md` §3'te.

> Durum: **Aşama 1 (backend çekirdeği) tamam.** Arayüz Aşama 2'de. Yol haritası: [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Teknoloji
.NET 8, ASP.NET Core Web API, EF Core, SQL Server, JWT, xUnit, Docker, GitHub Actions. React arayüzü sonraki aşamada.

## Mimari (katmanlı)
```
src/SeatReservation.Domain          iş kuralları, entity'ler (hiçbir şeye bağımlı değil)
src/SeatReservation.Application     use-case'ler, arayüzler
src/SeatReservation.Infrastructure  EF Core, JWT, parola özetleme
src/SeatReservation.Api             HTTP katmanı, arka plan servisi
tests/SeatReservation.Domain.Tests  domain birim testleri
tests/SeatReservation.Api.Tests     gerçek HTTP hattı üzerinden entegrasyon + eş zamanlılık testleri
```
Kararlar ve gerekçeleri: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Mülakat soruları: [`docs/INTERVIEW.md`](docs/INTERVIEW.md).

## Çalıştırma
```bash
cp .env.example .env      # örnek değerleri kendine göre değiştir (JWT_KEY, parolalar)
docker compose up --build
```
* Swagger: http://localhost:8080/swagger
* Sağlık: http://localhost:8080/health
* `.env.example` içindeki `ADMIN_EMAIL`/`ADMIN_PASSWORD` ile giriş yapınca (Swagger → `POST /api/auth/login` → **Authorize**) etkinlik oluşturabilirsin.

## API özeti
| Uç | Kim | Ne yapar |
|---|---|---|
| `POST /api/auth/register`, `POST /api/auth/login` | herkes | Kayıt (rol: User) / giriş, JWT döner |
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
```
Testler Docker gerektirmez (entegrasyon testleri SQLite dosyası kullanır; sınırı `ARCHITECTURE.md` §3'te).
