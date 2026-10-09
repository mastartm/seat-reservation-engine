# Mimari ve Kararlar

> Sürüm: Aşama 2 (arayüz eklendi; backend çekirdeği Aşama 1). Her karar "ne" değil **"neden"** ile yazıldı.

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

### İki veritabanı, tek test paketi (SQLite yerelde, SQL Server CI'da)
Varsayılan olarak testler Docker/SQL Server gerektirmesin diye SQLite dosyası kullanır. SQLite `rowversion` üretmediği için
test bağlamı (`SqliteAppDbContext`) değeri `SaveChanges` öncesinde kendisi yeniler. **EF'in ürettiği
`UPDATE ... WHERE RowVersion = ...` ve `DbUpdateConcurrencyException` mekanizması aynıdır**, ama SQL Server'ın
kendi `rowversion` ve kilit davranışı bu yolda çalışmaz.

Bu boşluğu kapatmak için `TEST_SQLSERVER_CONNECTION` ortam değişkeni doluysa `ApiFactory` SQLite yerine gerçek SQL Server'a bağlanır
(CI'daki `sqlserver` işi: `mcr.microsoft.com/mssql/server:2022-latest` service container; yalnızca eş zamanlılık testleri,
`--filter` ile). Boşsa yerel `dotnet test` Docker istemeden SQLite'ta koşar.

* **Yalın tutuldu:** SQL Server yolunda `DbContext` kaydına dokunulmaz; üretimdeki `AddInfrastructure` (`UseSqlServer`,
  gerçek `rowversion`) yalnızca bağlantı dizesi değiştirilerek kullanılır. `SqliteAppDbContext`'in RowVersion yaması bu yolda uygulanmaz.
* **Her `ApiFactory` kendi veritabanını açar** (`seats_test_<guid>`) ve Dispose'ta `DROP DATABASE` ile siler; paralel koşan testler
  birbirinin verisini görmez, sunucuda artık kalmaz.
* **`EnsureCreated` değil `Migrate`:** `EnsureCreated` şemayı *modelden* kurar; migration'ları hiç çalıştırmaz. Üretimde (compose,
  `Database__MigrateOnStartup`) ise şemayı *migration'lar* kurar. İkisi ayrışırsa (migration üretilmedi, `rowversion` kolonu
  eksik...) `EnsureCreated` ile testler yeşil kalır ama gerçek şema bozuktur. `Migrate` üretim yolunun kendisini sınar. Bedeli:
  factory başına ~1 sn migration süresi; yalnızca SQL Server yolunda ödenir. SQLite yolunda `EnsureCreated` kalır çünkü
  migration'lar SQL Server'a özgüdür (`rowversion`, `datetimeoffset`).
* **Parola:** CI'daki SA parolası workflow dosyasında düz yazılıdır; GitHub secret değildir çünkü gerçek bir sır değildir:
  yalnızca işin ömrü boyunca yaşayan, dışarıdan erişilemeyen bir konteynerin geçici değeridir (workflow'da yorumla belirtildi).
* **Hâlâ SQLite'ta koşan:** diğer testlerin tamamı (`build-test` işi). Tüm paketin SQL Server'a karşı da geçtiği yerelde
  elle doğrulandı, ama CI'da yalnızca eş zamanlılık alt kümesi koşar: yavaş olmasın, ve SQL Server'a özgü davranışın
  asıl önemli olduğu yerler bunlar.

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

Aşama 2'de backend'e yeni paket eklenmedi (CORS ve seçenek bağlama framework'ün parçası). Arayüz paketleri §11.1'de.

**Eklenmeyenler:** ASP.NET Identity (parola özetleme + JWT için fazla), MediatR (use-case sayısı az; düz servis
sınıfı yeterli), AutoMapper (birkaç `record` eşlemesi elle), FluentValidation (DataAnnotations + domain kuralları yeterli),
Testcontainers (yerel `dotnet test` Docker şart koşardı; SQL Server'ı CI'da GitHub Actions `services:` ile veriyoruz, §3).

## 9. Yapılandırma ve sırlar

Sır commit'lenmez. `.env` yerelde kalır; `.env.example` yalnızca örnek değer içerir. Compose, zorunlu değişken
eksikse `${VAR:?}` ile açıklayıcı hatayla durur.

| Ortam değişkeni | Anlam |
|---|---|
| `ConnectionStrings__Default` | SQL Server bağlantı dizesi (compose üretir) |
| `Jwt__Key` | JWT imzalama anahtarı (≥ 32 karakter, zorunlu) |
| `Database__MigrateOnStartup` | `true` ise açılışta migration (compose'ta açık) |
| `Admin__Email`, `Admin__Password` | Verilirse ilk Admin kullanıcıyı oluşturur |
| `Demo__Enabled` | `true` ise açılışta (etkinlik yoksa) örnek veri tohumlanır ve `POST /api/auth/demo` açılır (§11.8) |
| `Demo__MaxSessionsPerMinute` | Demo girişinin dakikalık genel sınırı (varsayılan `30`; aşılınca 429) |
| `Cors__AllowedOrigins` | Arayüzün adresi (virgülle ayrılmış). Boşsa CORS kapalı (§11.7) |
| `Reservation__HoldDuration` | Tutma süresi (varsayılan `00:10:00`) |
| `Reservation__ExpirySweepInterval` | Süre dolum taraması sıklığı (varsayılan `00:00:30`) |

Swagger her ortamda açıktır (vitrin projesi; `docker compose up` Production ortamında çalışır). Gerçek bir ürün
için yalnızca Development'ta açılırdı. HTTPS yönlendirmesi yok: TLS'i API'nin önündeki proxy sonlandırır.

## 10. Bilinen sınırlar (Aşama 3 adayları)

* Eş zamanlılık dışındaki testler (ve yerel `dotnet test`) SQLite üzerinde; SQL Server'a karşı yalnızca eş zamanlılık alt kümesi CI'da koşar (§3).
* Hold sayısı kullanıcı başına sınırlı değil (bir kullanıcı tüm koltukları tutabilir).
* Refresh token, parola sıfırlama, e-posta doğrulama yok (kapsam dışı).
* `docker compose up --build` bu geliştirme ortamında uçtan uca çalıştırılamadı: sandbox'ın ağı, imaj derlemesi
  sırasında konteynerin NuGet'e erişmesine izin vermedi. Bunun yerine parçalar ayrı ayrı gerçek bileşenlerle doğrulandı:
  gerçek SQL Server 2022 konteyneri + API (migration, demo tohumu, hold/onay), gerçek `nginx:1.27` + `web/nginx.conf`
  (index, `/api` ve `/health` yönlendirmesi, önbellek başlıkları) ve Chromium ile tarayıcı akışı (§11.9).
* Arayüz için SQL Server'a karşı koşan otomatik uçtan uca (E2E) test CI'da yok; yalnızca elle çalıştırıldı (§11.9).
* Kullanıcı tutmasından vazgeçemez (iptal/serbest bırakma ucu yok); koltuk süre dolunca kendiliğinden boşalır.
* Demo hız sınırı **genel**dir (tüm istemciler toplamı), istemci başına değil: proxy arkasında gerçek IP'ye güvenmek
  `ForwardedHeaders` ve güvenilen proxy yapılandırması gerektirir; bu vitrin için gereksiz karmaşıklık sayıldı.

### Aşama 3'te giderilenler

* **Eş zamanlı aynı e-posta kaydı:** "var mı?" kontrolü ile kayıt arasında ikinci istek geçebiliyordu (kontrol-sonra-yaz
  yarışı) ve benzersiz indeks ihlali 500 olarak dönüyordu. Artık `EfUnitOfWork` ihlali tanır (SQL Server hata numarası
  2601/2627, mesaj metni değil: metin sunucu diline göre değişir) ve `UniqueConstraintViolationException`'a çevirir;
  `AuthService` bunu "zaten kayıtlı" (409) yapar. Son söz veritabanındaki unique index'tedir; uygulama kodu yarışı
  kazanmaya çalışmaz. Test: `Register_same_email_in_parallel_...` (20 paralel istek → tam 1 adet 201, 19 adet 409). Düzeltme
  kaldırılınca test başarısız olur (denendi: 19 çakışma beklenirken 9).
* **Demo ucu hız sınırı:** `Demo__MaxSessionsPerMinute` (varsayılan 30) dakikalık sabit pencere; aşılınca 429. Yalnızca
  `POST /api/auth/demo`'yu etkiler.

## 11. Arayüz (`web/`)

React 19 + TypeScript + Vite + Tailwind CSS v4. Tek sayfa; sunucu tarafı render yok (SEO gerekmiyor, vitrin uygulaması).

### 11.1 Yığın ve eklenmeyenler
| Paket | Neden |
|---|---|
| `react`, `react-dom`, `vite`, `typescript` | İstenen yığın |
| `tailwindcss`, `@tailwindcss/vite` | Stil; ayrı CSS dosyası/bileşen kütüphanesi gerektirmez, build'de kullanılmayan sınıflar atılır |
| `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom` | Bileşen testleri. Vitest, Vite'ın kendi dönüştürücüsünü kullanır (ayrı Jest/Babel kurulumu yok) |
| `oxlint` | Vite şablonuyla geldi; hızlı, sıfır ayar |

**Eklenmeyenler:** React Router (yalnızca 3 görünüm var: `useState` yeterli, URL paylaşımı gereksinim değil),
React Query/SWR (tek bir 20 satırlık `usePolled` kancası ihtiyacı karşılıyor), Redux/Zustand (paylaşılan tek durum oturum:
Context), bileşen kütüphanesi (MUI vb.; vitrin olarak Tailwind ile elle yazılmış bileşenler daha okunur ve hafif),
axios (`fetch` + 60 satırlık istemci).

### 11.2 Anlık güncelleme: polling (SignalR değil)
`usePolled` koltuk haritasını 3 sn'de, rezervasyonlarımı 5 sn'de bir yeniler; sekme görünmezken durur, geri gelince hemen yeniler.
* **Neden polling:** akış tek yönlü ve saniyeler mertebesinde tazelik yeterli. SignalR ek sunucu bileşeni, bağlantı/yeniden
  bağlanma yönetimi ve ölçeklendiğinde backplane (Redis) gerektirir; polling'de API durumsuz kalır.
* **Doğruluk polling'e bağlı değil:** harita bayat olsa bile kaybedilen yarışı sunucu 409 ile çözer (§3). Polling yalnızca
  kullanıcıya "o koltuk gitti"yi erken göstermek içindir.
* **Bayat yanıt koruması:** yavaş dönen eski bir yanıt yeni yanıtın üstüne yazmasın diye her istek bir sıra numarası taşır;
  `load` değişince (başka etkinlik, başka kullanıcı) eski veri hiç gösterilmez.
* **Maliyeti:** N açık sekme ≈ N/3 istek/sn. Gerçek ölçekte `ETag`/`If-None-Match` veya SignalR düşünülür (bkz. `INTERVIEW.md`).

### 11.3 Geri sayım ve sunucu saati
Kalan süre = sunucunun verdiği `expiresAt` − "sunucu şimdisi". Kullanıcının saati yanlışsa (saat farkı, manuel ayar) 10 dakikalık
gösterge de yanlış olurdu, bu yüzden her yanıttaki `Date` başlığından sunucu–istemci farkı ölçülür (`lib/clock.ts`).
* `Date` saniyeyi aşağı yuvarlar; ortası (+500 ms) alınır → gösterge hatası ≤ ±0,5 sn.
* Başlığın çapraz-origin'de okunabilmesi için CORS politikası `Date`'i `Access-Control-Expose-Headers` ile açar (test: `CorsTests`).
* **Geri sayım bir ipucudur, yetki değil:** süre dolumunu ve onay reddini sunucu uygular (süresi dolan onay → 409).
  Sayaç 00:00'a varınca düğme kalkar ve kart "süre doldu" der; sunucu hâlâ "Held" dese de (arka plan servisi 30 sn'de bir çalışır)
  arayüz gerçek durumu gösterir.

### 11.4 "Benim koltuğum" nasıl bilinir?
`GET /events/{id}/seats` yalnızca `Available/Held/Sold` döner: **kimin tuttuğunu söylemez** (başkalarının bilgisi sızmasın).
Arayüz "senin tuttuğun/satın aldığın" ayrımını `GET /reservations/mine` ile koltuk kimliğini eşleştirerek türetir
(`lib/seatState.ts`). Haritada koltuk "Held" ve benim aktif rezervasyonum varsa → "senin için tutuldu"; yoksa → "başkası tutuyor".

### 11.5 Tutma akışı: tıkla → hemen tut
Koltuğa tıklamak onu **hemen** tutar; "seç, sonra tut" iki adımı yok. Seçimin yerelde tutulup sonra tutulması, kullanıcıyı
kaybedeceği bir yarışa sokar (seçilen koltuk arada alınır). Hemen tutmak yarışı en kısa pencereye indirir; kaybeden 409'un
mesajını görür, harita yenilenir. İstek sürerken diğer koltuklar kilitlenir (çift tıklama koruması).
Giriş yapılmadan tıklama istek atmaz, giriş ekranına yönlendirir.

### 11.6 Oturum
JWT ve kullanıcı bilgisi `localStorage`'da tutulur; sunucu 401 dönerse (token reddedildi) oturum otomatik kapanır. Giriş denemesindeki
401 "parola yanlış" demektir ve oturumu kapatmaz.
* **Güvenlik takası:** `localStorage` XSS ile okunabilir; `HttpOnly` çerez bunu önlerdi ama CSRF koruması, çerez/CORS ayarı ve
  (Render/Vercel gibi) farklı alan adlarında `SameSite` sorunları getirir. Bu projede kullanıcı girdisi React tarafından
  kaçışlanır (`dangerouslySetInnerHTML` yok) ve token ömrü 60 dk; yine de bu bilinçli bir takastır (bkz. `INTERVIEW.md`).
* `localStorage` erişimi (gizli pencere vb.) hata verirse oturum yalnızca bellekte yaşar; uygulama çalışmaya devam eder.

### 11.7 Geliştirme ve canlıda API adresi
* **Geliştirme:** Vite dev sunucusu `/api` ve `/health`'i `http://localhost:8080`'e yönlendirir → tarayıcı için aynı origin, CORS yok.
* **`docker compose`:** `web` servisi (nginx) statik dosyaları sunar ve `/api`'yi API konteynerine iletir → yine aynı origin.
* **Canlı (ayrı alan adları):** arayüz `VITE_API_URL` ile API'yi doğrudan çağırır; API `Cors__AllowedOrigins` ile o adresi tanır.
  CORS yalnızca bu ayar doluysa açılır (varsayılan kapalı = güvenli taraf). `AllowCredentials` yok: kimlik çerezle değil
  `Authorization` başlığıyla taşınır. `VITE_API_URL` derleme anında gömülür (docs/DEPLOY.md).

### 11.8 Demo modu
Ziyaretçi 2 dakikada deneyebilsin diye (`Demo__Enabled=true`):
* **Tohum:** hiç etkinlik yokken 2 örnek etkinlik oluşur; koltukların ~%25'i "satılmış" gelir (sabit tohumla, tekrarlanabilir).
  Tohum veri de gerçek domain yolundan (`Hold` → `Confirm`) geçer: durum makinesinin kurallarına uyar. Yeniden başlatmada çoğalmaz.
* **Tek tıkla giriş = tek kullanımlık misafir hesabı** (`POST /api/auth/demo`), sabit "demo/demo" kullanıcısı değil. Sabit kimlik
  repoda herkesin bildiği bir parola olurdu (CLAUDE.md: parola commit'lenmez) ve ziyaretçiler birbirinin rezervasyonlarını görürdü.
  Misafirin parola özeti kimsenin bilmediği rastgele bir değerdir: bu hesaba parolayla girilemez, yalnızca yanıttaki token çalışır.
* **Kötüye kullanım:** demo ucu dakikada en çok `Demo__MaxSessionsPerMinute` (varsayılan 30) misafir açar, aşılınca 429. Bu
  genel bir sınırdır, istemci başına değil (§10); yine de veritabanı yavaşça dolabilir, `DEPLOY.md`'de uyarıldı.

### 11.9 Test stratejisi (frontend)
* **Bileşen/akış testleri (35, CI'da):** `SeatMap` (durumlar, sıralama, tıklanabilirlik), `AuthForm` (başarı/hata/ağ hatası/demo),
  `HoldCard` (geri sayım, uyarı rengi, süre dolumu, **yanlış istemci saati**), `MyReservationsPage` (durumlar, sıralama,
  süresi geçmiş-ama-"Held" kayıt), `App` (giriş→tut→onayla→Rezervasyonlarım akışı, 409, demo girişi), `formatRemaining`.
* **Sahte sunucu:** `test/fakeApi.ts` durumlu bir `fetch` sahtesidir; hold/confirm gerçekten koltuk durumunu değiştirir. Testler
  tek tek istekleri değil, kullanıcının gördüğü akışı doğrular. Sahte zamanlayıcıyla 10 dakika beklenmez.
* **Elle doğrulama (CI'da değil):** gerçek SQL Server + API + Vite + Chromium ile iki ayrı tarayıcı bağlamı: A koltuğu tutar
  (geri sayım işler), B aynı koltuğu ≤ 3 sn'de "başkası tutuyor" görür, A onaylar, B "satıldı" görür.
  Bu bir otomatik E2E değildir; Aşama 3 adayı.
