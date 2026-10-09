// Geri sayım sunucunun verdiği son kullanma anına göre işler; kullanıcının saati yanlışsa 10 dakikalık
// gösterge de yanlış olurdu. Bu yüzden her yanıttaki `Date` başlığından sunucu ile istemci arasındaki
// fark ölçülür ve "şimdi" bu farkla düzeltilir. Başlık saniyeyi aşağı yuvarlar (gerçek an [başlık, başlık+1sn)
// aralığındadır); ortasını (+500 ms) alırız, böylece hata en çok ±0,5 sn olur. Asıl yetki zaten sunucudadır:
// geri sayım bir ipucudur, süre dolumunu sunucu uygular.
const HEADER_PRECISION_MIDPOINT_MS = 500
let offsetMs = 0

export function syncServerTime(dateHeader: string | null): void {
  if (!dateHeader) return
  const server = Date.parse(dateHeader)
  if (Number.isNaN(server)) return
  offsetMs = server + HEADER_PRECISION_MIDPOINT_MS - Date.now()
}

export function serverNow(): number {
  return Date.now() + offsetMs
}

export function resetServerTimeForTests(): void {
  offsetMs = 0
}
