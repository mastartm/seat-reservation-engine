# Mülakat Soruları ve Cevapları

Bu projeden sorulabilecek sorular. Cevaplar kısa tutuldu; ayrıntı için `ARCHITECTURE.md` bölüm numaraları verildi.

## Eş zamanlılık

**1. İki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?**
Tam biri başarılı olur (201), diğeri 409 alır. Mekanizma: `Seat` üzerinde SQL Server `rowversion`. EF, UPDATE'i
`WHERE RowVersion = <okuduğum>` ile üretir; yarışı kaybeden 0 satır etkiler ve `DbUpdateConcurrencyException` alır.
Aynı `SaveChanges` transaction'ında yazılan rezervasyon kaydı da geri alınır. (§3)

**2. Neden pessimistic lock değil, optimistic concurrency?**
Çakışma nadirdir ve kaybedenin cevabı zaten "koltuk alındı"dır. Kilit, bağlantıyı tutarak beklemeye yol açar.
Optimistic yaklaşım kilitsiz okur, çakışmayı yazarken yakalar. Çok sıcak bir tek satır (konser açılış anı) olsaydı
ve kaybedenlerin yeniden denemesi gerekseydi pessimistic ya da kuyruk düşünülebilirdi.

**3. Bunu nasıl kanıtladın?**
`ConcurrencyTests`: 100 ayrı kullanıcı, tek koltuk, `TaskCompletionSource` kapısıyla aynı anda salınan gerçek HTTP
istekleri; her turda tam 1 `201`, 99 `409`, veritabanında tam 1 rezervasyon; 5 tur. `RowVersion` korumasını
kapatınca testlerin kırıldığını da denedim (1 yerine 3–4 "kazanan" çıktı), yani test gerçekten race yakalıyor.

**4. Testlerin SQLite'ta çalışması bir zayıflık değil mi?**
Kısmen evet, ve bunu dokümanda açıkça yazdım. SQLite `rowversion` üretmez; test bağlamı değeri kendisi yeniler. EF'in
ürettiği `UPDATE ... WHERE` ve istisna mekanizması aynıdır, ama SQL Server'ın kendi davranışı kanıtlanmış olmaz.
Çözüm: CI'da SQL Server servis konteynerine karşı koşan bir iş (Aşama 3 adayı).

**5. Kaybeden isteği neden yeniden denemiyorsun?**
Doğru cevap "bu koltuk artık müsait değil"dir; yeniden denemek aynı sonucu verirdi. Retry, çakışmanın "başka birinin
değiştirdiği veriyi yeniden değerlendirip yine de başarılı olabileceğin" durumlarda anlamlıdır (örn. bir sayaç artırma).

**6. Onay ile süre dolumu servisi aynı anda çalışırsa?**
İkisi de aynı `Seat` satırını günceller; `RowVersion` biri geçirir diğerini reddeder. Onaylanmış koltuk yanlışlıkla
serbest kalamaz. `Confirm_racing_with_sweep_cannot_both_win` testi bunu deterministik olarak sınar.

**7. Aynı kullanıcı butona iki kez basarsa?**
İkinci istek aynı koltuğu zaten tutulu bulur veya yarışı kaybeder: 409. Rezervasyon bir tane kalır (test var).

## Tasarım

**8. İş kuralları neden entity içinde?**
Kural tek yerde, veritabanı/HTTP olmadan birim test edilebilir (25 domain testi milisaniyeler içinde koşar). Controller
sadece kimliği alıp servisi çağırır. `Seat.Status` `private set`: dışarıdan kural atlanarak değiştirilemez. (§1–2)

**9. Süresi dolan tutma neden hemen "boş" sayılıyor, arka plan servisi ne işe yarıyor?**
Doğruluğu periyodik bir işe bağlamamak için. `StatusAt(now)` ve `Hold()` süreyi kendileri kontrol eder; servis çökse
bile sistem yanlış davranmaz. Servis yalnızca veri hijyeni: `Reservation`'ı `Expired` yapar, koltuğu fiilen serbest bırakır.

**10. `ActiveReservationId` ne işe yarıyor?**
Süresi dolmuş eski bir rezervasyon, koltuk başkasına geçtikten sonra `Expire()` ya da `Confirm()` çağırırsa yeni
sahibin tutmasını bozmamalı. Koltuk "benim rezervasyonumu mu tutuyor?" diye kontrol eder.

**11. Neden MediatR/AutoMapper/Identity kullanmadın?**
Use-case sayısı az; düz servis sınıfı yeterli ve akış okunabilir. Her kütüphane ek bağımlılık ve öğrenme yükü.
Gerekçeleri `ARCHITECTURE.md` §8'de. Ölçek büyüyüp cross-cutting davranışlar (loglama, doğrulama pipeline'ı) çoğalsaydı
MediatR'ı yeniden düşünürdüm.

**12. Application katmanı neden EF Core'u bilmiyor?**
Bağımlılık yönü içe doğru. Application, ihtiyacı kadar repository arayüzü tanımlar (genel `IRepository<T>` değil);
Infrastructure EF ile gerçekler. `DbUpdateConcurrencyException` bile `EfUnitOfWork` içinde Application'ın
`ConcurrencyConflictException`'ına çevrilir.

**13. Hata yönetimi nasıl?**
İstisna **tipi** anlamı taşır, `ApiExceptionHandler` tek yerde HTTP'ye çevirir. Domain HTTP bilmez; controller'da
try/catch yok. Bilinmeyen hata 500 olur ve ayrıntısı istemciye sızmaz.

## Güvenlik

**14. Parolaları nasıl saklıyorsun?**
PBKDF2-HMAC-SHA256, 600 000 iterasyon, kullanıcı başına 16 bayt rastgele tuz, sabit zamanlı karşılaştırma. Özet
sürümlü biçimde (`v1.iter.tuz.özet`) saklanır, ileride iterasyon artırılabilir. Argon2/bcrypt yerine BCL'deki PBKDF2'yi
seçtim: ek kütüphane yok; üretimde Argon2id de makul bir tercih.

**15. Herkes kayıt olup Admin olabilir mi?**
Hayır. Herkese açık kayıt her zaman `User` üretir. Admin yalnızca `Admin__Email/Admin__Password` ortam değişkenleriyle
açılışta tohumlanır.

**16. Giriş hatasında neden tek tip mesaj?**
Yanlış parola ve bilinmeyen e-posta aynı 401'i döner; bilinmeyen e-postada da sahte özet doğrulanır ki yanıt süresi
hesabın varlığını sızdırmasın (kullanıcı numaralandırma).

**17. JWT anahtarı nerede?**
Yalnızca `Jwt__Key` ortam değişkeninde. Eksik ya da 32 karakterden kısa ise uygulama açılışta düşer. Repoda yalnızca
`.env.example` içinde örnek değer var.

**18. Başkasının rezervasyonunu onaylamaya çalışırsam?**
403. Sahiplik kontrolü `Reservation.Confirm` içinde ve süre/durum kontrollerinden önce: yabancıya rezervasyonun
durumu hakkında bilgi sızmaz.

## Operasyon ve test

**19. Zamanla ilgili testleri nasıl yazdın (10 dakika beklemeden)?**
Kod `TimeProvider` kullanır; testler elle ilerletilen `FakeTimeProvider` enjekte eder. (Ek not: sahte saat ilerleyince
sonradan üretilen JWT'nin `nbf` değeri gerçek saatin ilerisinde kalıp 401 verir; bu yüzden saati ilerleten test
sınıfları test başına izole factory kullanır. Bu, bir yan etki olarak öğrendiğim bir tuzak.)

**20. Test piramidi nasıl?**
25 saf domain birim testi (hızlı, kural odaklı) + 42 entegrasyon testi (gerçek HTTP hattı, JWT, EF, SQLite): auth,
etkinlik, hold, onay, süre dolumu (servis + worker), paralel istek ve Swagger.

**21. `docker compose up` ne yapıyor?**
SQL Server ve API'yi ayağa kaldırır; API, SQL Server healthcheck'i geçene kadar başlamaz; açılışta migration'lar
otomatik uygulanır; sırlar `.env`'den gelir, eksikse compose açıklayıcı hatayla durur. (Not: bu komutu geliştirme
ortamında Docker daemon olmadığı için uçtan uca çalıştıramadım; yapılandırma ve yayımlanmış API ayrı ayrı doğrulandı.)

**22. Ölçeklendirmek istesen ne yaparsın?**
API durumsuz (JWT), yatay ölçeklenir; eş zamanlılık doğruluğu veritabanındaki `RowVersion`'a dayandığı için birden çok
API örneği yine doğrudur. Süre dolum worker'ı birden çok örnekte çalışırsa aynı satırlara dokunabilir; `RowVersion`
bunu zararsız kılar ama gereksiz iş yapar: tek örneğe sabitlemek ya da dağıtık kilit düşünülebilir.

## Bilinen eksikler (sorulursa dürüst cevap)

* Testler SQL Server yerine SQLite'ta koşar (soru 4).
* Aynı e-postayla tam eşzamanlı iki kayıt isteği 500 döner (benzersiz indeks ihlali); 409'a çevrilmeli.
* Kullanıcı başına hold limiti yok.
