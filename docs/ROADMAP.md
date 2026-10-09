# Yol Haritası

Temel soru: **iki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?** Her aşama kendi başına çalışır durumda bitmeli.

## Aşama 1 — Backend çekirdeği (en değerli kısım)
- [x] Domain: `Event`, `Seat`, `Reservation` entity'leri; koltuk durumu (Boş / Tutuldu / Satıldı) ve geçiş kuralları domain içinde
- [x] EF Core + SQL Server, migration'lar, `Seat` üzerinde `RowVersion`
- [x] Koltuk tutma (hold): 10 dakika süreli, aynı koltuğa ikinci istek reddedilir
- [x] Satın alma/onaylama: yalnızca hold sahibi onaylayabilir
- [x] Hold süresi dolunca otomatik serbest bırakan arka plan servisi (`BackgroundService`)
- [x] JWT ile kayıt/giriş, rol (Admin / Kullanıcı)
- [x] Testler: domain birim testleri + **eş zamanlılık testi** (örn. 100 paralel istek, tek koltuk, tam 1 başarılı)
- [x] Docker: `docker compose up` ile API + SQL Server çalışır, migration otomatik uygulanır — _gerçek SQL Server konteynerinde doğrulandı (2026-10-09): `/health` 200, aynı koltuğa 100 paralel hold isteğinde 1 adet 201 + 99 adet 409_
- [x] CI: build + test yeşil — _`.github/workflows/ci.yml` ile aynı komutlar yerelde yeşil; GitHub'da henüz koşmadı_
- [x] Swagger arayüzü
- [x] `docs/ARCHITECTURE.md` ve `docs/INTERVIEW.md` ilk sürüm

## Aşama 2 — Arayüz ve canlı demo
- [x] React + Vite + Tailwind, `web/` klasöründe — _35 bileşen/akış testi, CI'da `web` işi (lint + test + build)_
- [x] Koltuk haritası, anlık durum güncellemesi (polling, 3 sn) — _hold/onay + 10 dk geri sayım dahil_
- [x] Giriş ekranı, "Rezervasyonlarım"
- [x] Demo için örnek veri (seed) ve tek tıkla demo girişi — _`Demo__Enabled=true`; gerçek SQL Server'da doğrulandı_
- [ ] Canlı deploy (ücretsiz katman) ve README'ye link — _hazırlık tamam (`docs/DEPLOY.md`, `web/Dockerfile`, CORS); hesap/anahtar adımları proje sahibinde_

## Aşama 3 — Cila
- [ ] Yük/eş zamanlılık testi sonuçları README'de (tablo)
- [ ] Mimari diyagramı, demo GIF'i
- [ ] README'de "Neden bu tasarım?" bölümü
- [ ] Kod temizliği, eksik testlerin tamamlanması

## Bilinçli olarak yapılmayanlar
Ödeme entegrasyonu, e-posta, mikroservis. Kapsam dışı, README'de "sonraki adımlar" olarak anılır.
