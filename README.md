# seat-reservation-engine

Eş zamanlılık odaklı koltuk/randevu rezervasyon motoru. Temel soru: **iki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?**

> Durum: altyapı iskeleti. Özellikler aşama aşama eklenecek.

## Teknoloji
.NET 8, ASP.NET Core Web API, EF Core, SQL Server, xUnit, Docker, GitHub Actions. React arayüzü sonraki aşamada.

## Mimari (katmanlı)
```
src/SeatReservation.Domain          iş kuralları, entity'ler (hiçbir şeye bağımlı değil)
src/SeatReservation.Application     use-case'ler, arayüzler
src/SeatReservation.Infrastructure  EF Core, veritabanı
src/SeatReservation.Api             HTTP katmanı
tests/SeatReservation.Domain.Tests  xUnit testleri
```

## Çalıştırma
```bash
cp .env.example .env      # parolayı kendin belirle
docker compose up --build
curl http://localhost:8080/health
```

## Test
```bash
dotnet test
```
