# Mimari ve Kararlar

> Sürüm: Aşama 1 (backend çekirdeği). Her karar "ne" değil **"neden"** ile yazıldı.

## 1. Katmanlar

```
Domain  ←  Application  ←  Infrastructure  ←  Api
(kurallar)  (use-case'ler)   (EF, JWT, hash)    (HTTP, DI, worker)
```

| Katman | Sorumluluk | Neye bağımlı |
|---|---|---|
| `Domain` | Entity'ler, durum makinesi, iş kuralı istisnaları | Hiçbir şeye |
| `Application` | Use-case servisleri (`ReservationService`, `AuthService`...), repository/güvenlik arayüzleri | Domain |
| `Infrastructure` | EF Core + SQL Server, repository'ler, PBKDF2, JWT üretimi, migration | Application |
| `Api` | İnce controller'lar, JWT doğrulama, hata→HTTP eşlemesi, arka plan servisi, Swagger | Application, Infrastructure (yalnızca DI kaydı için) |

**Neden böyle?** İş kuralları (bir koltuk ne zaman tutulabilir?) veritabanı ve HTTP'den bağımsız test edilebilsin diye
Domain'de. Controller'da tek satır iş mantığı yok: kimlik al → servisi çağır → sonucu dön.

## 2. Domain modeli

```
Event 1───* Seat 1───* Reservation *───1 User
```

* **Seat** koltuğun durum makinesidir: `Available → Held → Sold`, `Held → Available` (süre dolumu).
  Geçişlerin tamamı `Seat` üzerinden geçer; dışarıdan `Status` set edilemez (`private set`).
* **Reservation** bir tutma/satın alma kaydıdır: `Held → Confirmed` veya `Held → Expired`.
  "Kim onaylayabilir, süre dolmuş mu" kuralları `Reservation.Confirm` içindedir.
* `Seat.ActiveReservationId` koltuğu şu an hangi rezervasyonun tuttuğunu söyler.

### Neden `ActiveReservationId`?
Süresi dolan bir tutma temizlenmeden başkası koltuğu devralabilir. Eski rezervasyon "Held" kalmış olabilir;
eski rezervasyonun `Expire()` veya `Confirm()` çağrısı yeni sahibin koltuğunu bozmamalı. `ActiveReservationId`
karşılaştırması bunu garanti eder (testleri: `Expire_does_not_release_a_seat_already_taken_by_someone_else`,
`Stale_reservation_cannot_confirm_after_seat_was_taken_over`).

### Neden "tembel" süre dolumu? (`StatusAt(now)`)
Süresi dolmuş tutma, arka plan servisi henüz çalışmamış olsa bile **okuma ve tutma anında boş sayılır**.
Böylece doğruluk periyodik bir işe bağlı değil; arka plan servisi yalnızca veri hijyeni (`Reservation` durumlarını
`Expired` yapmak, koltuğu fiilen `Available`'a çekmek) içindir. Servis bir tur çalışmasa bile sistem yanlış davranmaz.

## 3. Eş zamanlılık (projenin kalbi)

**Sorun:** İki istek aynı anda boş görünen koltuğu okur, ikisi de "tutabilirim" der.

**Çözüm: Optimistic concurrency (`RowVersion`) + transaction.**

1. `Seat.RowVersion` SQL Server `rowversion` kolonudur; her UPDATE'te veritabanı değeri kendisi artırır.
2. EF, UPDATE'i `WHERE Id = @id AND RowVersion = @okuduğumuz` olarak üretir.
3. Aynı anda iki istek koltuğu boşken okursa ikisi de `Hold()` der; ama UPDATE'te **yalnızca biri** eşleşen
   `RowVersion`'ı bulur. Diğerinin UPDATE'i 0 satır etkiler → `DbUpdateConcurrencyException`
   → `ConcurrencyConflictException` → HTTP 409.
4. `SaveChanges` tek transaction açar: `Reservation` INSERT'ü ile `Seat` UPDATE'i birlikte yazılır ya da
   birlikte geri alınır. Kaybeden isteğin yarım kalmış rezervasyonu veritabanında iz bırakmaz.

İki kaybetme yolu da aynı sonucu (409) verir:
* İstek koltuğu okuduğunda zaten tutuluydu → domain `SeatNotAvailableException`.
* Okuduğunda boştu ama yazarken yarışı kaybetti → `ConcurrencyConflictException`.

**Neden pessimistic lock (`SELECT ... WITH (UPDLOCK)`) değil?** Yarış nadir ve kazanan belli olduğunda kaybedene
verilecek cevap zaten "koltuk alındı". Kilit tutmak, veritabanı bağlantısını kullanıcı beklerken meşgul eder ve
ölçeklenmeyi zorlaştırır. Optimistic yaklaşım kilitsiz okur, çakışmayı yazarken yakalar.

**Neden yeniden deneme (retry) yok?** Kaybeden için doğru cevap "koltuk artık sizin değil"dir; yeniden denemek
yalnızca aynı hatayı üretirdi.

**Onay ↔ süre dolumu yarışı:** `Confirm` ve `HoldExpirationService` aynı `Seat` satırını günceller; aynı
`RowVersion` mekanizması biri geçirir diğerini reddeder. Onaylanmış koltuk yanlışlıkla serbest kalamaz
(test: `Confirm_racing_with_sweep_cannot_both_win`).

### Kanıt: testler
`ConcurrencyTests`: gerçek HTTP hattı + gerçek EF Core + gerçek veritabanı, `TaskCompletionSource` kapısıyla
aynı anda salınan 100 istek, 5 tur, her turda tam 1 `201` ve 99 `409`; ayrıca çok koltuklu çekişme, aynı
kullanıcının çift tıklaması ve paralel onay. `RowVersion` koruması geçici olarak kapatıldığında bu testlerin
kırıldığı elle doğrulandı (yani testler gerçekten race yakalıyor).

### Dürüst sınır: testler SQLite üzerinde koşar
Testler Docker/SQL Server gerektirmesin diye SQLite dosyası kullanır. SQLite `rowversion` üretmediği için
test bağlamı (`SqliteAppDbContext`) değeri `SaveChanges` öncesinde kendisi yeniler. **EF'in ürettiği
`UPDATE ... WHERE RowVersion = ...` ve `DbUpdateConcurrencyException` mekanizması aynıdır**, ama SQL Server'ın
kendi `rowversion` ve kilit davranışı bu testlerde çalışmaz. Aşama 3'te SQL Server konteynerine karşı koşan bir
CI işi eklenmesi planlanıyor (bkz. "Bilinen sınırlar").

## 4. Kimlik doğrulama

* **JWT (HS256)**, kısa claim adları (`sub`, `email`, `role`); `MapInboundClaims = false` ile ASP.NET'in
  sessiz claim yeniden adlandırması kapalı.
* **Rol:** `User` / `Admin`. Herkese açık kayıt **her zaman `User`** üretir; Admin yalnızca sunucu tarafında
  `Admin__Email`/`Admin__Password` ortam değişkenleriyle tohumlanır. Aksi halde herkes kendini admin yapabilirdi.
* **Parola:** PBKDF2-HMAC-SHA256, 600 000 iterasyon, kullanıcı başına rastgele tuz; BCL'de hazır olduğu için ek
  kütüphane yok. Özet `v1.iterasyon.tuz.özet` biçiminde saklanır: iterasyon sayısı ileride artırılabilir.
* **Giriş hatası tek tip:** yanlış parola ve bilinmeyen e-posta aynı 401'i döner; bilinmeyen e-postada da sahte
  bir özet doğrulanır, böylece yanıt süresi "bu e-posta kayıtlı mı?" bilgisini sızdırmaz.
* **JWT anahtarı** yalnızca `Jwt__Key` ortam değişkeninden gelir; eksik/32 karakterden kısa ise uygulama açılışta
  düşer (zayıf anahtarla sessizce çalışmaktansa hızlı hata).

## 5. Hata yönetimi

Domain ve Application istisnalarının **tipi** neyin yanlış gittiğini söyler; HTTP'ye çeviri tek yerde
(`ApiExceptionHandler`):

| İstisna | HTTP |
|---|---|
| `DomainValidationException`, `RequestValidationException` | 400 |
| `InvalidCredentialsException` | 401 |
| `NotHoldOwnerException` | 403 |
| `NotFoundException` | 404 |
| `SeatNotAvailable`, `HoldExpired`, `InvalidStateTransition`, `EmailAlreadyRegistered`, `ConcurrencyConflict` | 409 |
| diğer | 500 (ayrıntı istemciye sızmaz, loglanır) |

Domain HTTP'yi bilmez. 4xx durumları ASP.NET'in `ExceptionHandlerMiddleware` log kategorisinde hata
seviyesinde basılmasın diye o kategori kapatıldı; gerçek 500'leri handler'ımız kendisi loglar.

## 6. Zaman

Her yerde `TimeProvider` (.NET 8 yerleşik) kullanılır. Testler 10 dakikayı beklemez; sahte saati ilerletir.
(Ek kütüphane `Microsoft.Extensions.TimeProvider.Testing` yerine 10 satırlık kendi `FakeTimeProvider`'ımız var.)

## 7. Arka plan servisi

`HoldExpirationWorker` (`BackgroundService` + `PeriodicTimer`, varsayılan 30 sn) her turda yeni DI scope'u açar ve
`HoldExpirationService`'i çağırır. Mantık serviste, zamanlama worker'da: mantık zamanlayıcı olmadan test edilir.
Bir tur başarısız olursa (ör. onayla yarıştı) servis ölmez, loglayıp sonraki turu bekler.

## 8. Eklenen kütüphaneler ve nedenleri

| Paket | Proje | Neden |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | Infrastructure | Zorunlu: EF Core + SQL Server, `rowversion` |
| `Microsoft.EntityFrameworkCore.Design` | Api (PrivateAssets) | Yalnızca `dotnet ef migrations` için; çalışma zamanına dağıtılmaz |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Api | JWT doğrulama |
| `Microsoft.IdentityModel.JsonWebTokens` | Infrastructure | JWT üretimi (`JsonWebTokenHandler`). JwtBearer'ın zaten bağımlı olduğu paket; sürümü onunla aynı |
| `Microsoft.Extensions.Options` / `...DependencyInjection.Abstractions` | Application | `ReservationOptions` ve `AddApplication()`; sadece soyutlamalar |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | Infrastructure | `JwtOptions`'ı yapılandırmadan bağlamak ve açılışta doğrulamak |
| `Swashbuckle.AspNetCore` | Api | Swagger arayüzü (iskelette vardı) |
| `Microsoft.AspNetCore.Mvc.Testing` | Test | Gerçek HTTP hattını bellekte ayağa kaldırır |
| `Microsoft.EntityFrameworkCore.Sqlite` | Test | Docker'sız, dosya tabanlı gerçek veritabanı |

**Eklenmeyenler:** ASP.NET Identity (parola özetleme + JWT için fazla), MediatR (use-case sayısı az; düz servis
sınıfı yeterli), AutoMapper (birkaç `record` eşlemesi elle), FluentValidation (DataAnnotations + domain kuralları yeterli),
Testcontainers (CI'da Docker şart koşardı; bkz. sınırlar).

## 9. Yapılandırma ve sırlar

Sır commit'lenmez. `.env` yerelde kalır; `.env.example` yalnızca örnek değer içerir. Compose, zorunlu değişken
eksikse `${VAR:?}` ile açıklayıcı hatayla durur.

| Ortam değişkeni | Anlam |
|---|---|
| `ConnectionStrings__Default` | SQL Server bağlantı dizesi (compose üretir) |
| `Jwt__Key` | JWT imzalama anahtarı (≥ 32 karakter, zorunlu) |
| `Database__MigrateOnStartup` | `true` ise açılışta migration (compose'ta açık) |
| `Admin__Email`, `Admin__Password` | Verilirse ilk Admin kullanıcıyı oluşturur |
| `Reservation__HoldDuration` | Tutma süresi (varsayılan `00:10:00`) |
| `Reservation__ExpirySweepInterval` | Süre dolum taraması sıklığı (varsayılan `00:00:30`) |

Swagger her ortamda açıktır (vitrin projesi; `docker compose up` Production ortamında çalışır). Gerçek bir ürün
için yalnızca Development'ta açılırdı. HTTPS yönlendirmesi yok: TLS'i API'nin önündeki proxy sonlandırır.

## 10. Bilinen sınırlar (Aşama 3 adayları)

* Testler SQLite üzerinde; SQL Server konteynerine karşı koşan CI işi yok.
* Aynı e-postayla **tam aynı anda** iki kayıt isteği benzersiz indeksi ihlal eder ve 500 döner (kontrol-sonra-yaz
  yarışı); 409'a çevrilmesi gerek.
* Hold sayısı kullanıcı başına sınırlı değil (bir kullanıcı tüm koltukları tutabilir).
* Refresh token, parola sıfırlama, e-posta doğrulama yok (kapsam dışı).
* `docker compose up` akışı bu geliştirme ortamında (Docker daemon yok) uçtan uca çalıştırılamadı; compose
  yapılandırması `docker compose config` ile, API ise yayımlanmış çıktıyla (başlangıç, `/health`, Swagger,
  eksik `Jwt__Key` hatası) elle doğrulandı.
