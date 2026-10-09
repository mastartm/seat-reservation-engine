# Yol Haritası

Temel soru: **iki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?** Her aşama kendi başına çalışır durumda bitmeli.

## Aşama 1 — Backend çekirdeği (en değerli kısım)
- [ ] Domain: `Event`, `Seat`, `Reservation` entity'leri; koltuk durumu (Boş / Tutuldu / Satıldı) ve geçiş kuralları domain içinde
- [ ] EF Core + SQL Server, migration'lar, `Seat` üzerinde `RowVersion`
- [ ] Koltuk tutma (hold): 10 dakika süreli, aynı koltuğa ikinci istek reddedilir
- [ ] Satın alma/onaylama: yalnızca hold sahibi onaylayabilir
- [ ] Hold süresi dolunca otomatik serbest bırakan arka plan servisi (`BackgroundService`)
- [ ] JWT ile kayıt/giriş, rol (Admin / Kullanıcı)
- [ ] Testler: domain birim testleri + **eş zamanlılık testi** (örn. 100 paralel istek, tek koltuk, tam 1 başarılı)
- [ ] Docker: `docker compose up` ile API + SQL Server çalışır, migration otomatik uygulanır
- [ ] CI: build + test yeşil
- [ ] Swagger arayüzü
- [ ] `docs/ARCHITECTURE.md` ve `docs/INTERVIEW.md` ilk sürüm

## Aşama 2 — Arayüz ve canlı demo
- [ ] React + Vite + Tailwind, `web/` klasöründe
- [ ] Koltuk haritası, anlık durum güncellemesi (polling veya SignalR)
- [ ] Giriş ekranı, "Rezervasyonlarım"
- [ ] Demo için örnek veri (seed) ve tek tıkla demo girişi
- [ ] Canlı deploy (ücretsiz katman) ve README'ye link

## Aşama 3 — Cila
- [ ] Yük/eş zamanlılık testi sonuçları README'de (tablo)
- [ ] Mimari diyagramı, demo GIF'i
- [ ] README'de "Neden bu tasarım?" bölümü
- [ ] Kod temizliği, eksik testlerin tamamlanması

## Bilinçli olarak yapılmayanlar
Ödeme entegrasyonu, e-posta, mikroservis. Kapsam dışı, README'de "sonraki adımlar" olarak anılır.
