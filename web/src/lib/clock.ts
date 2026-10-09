// Geri sayım sunucunun verdiği son kullanma anına göre işler; kullanıcının saati yanlışsa 10 dakikalık
// gösterge de yanlış olurdu. Bu yüzden her yanıttaki `Date` başlığından sunucu ile istemci arasındaki
// fark ölçülür ve "şimdi" bu farkla düzeltilir. (Başlık saniye çözünürlüklüdür; bu bir geri sayım için yeterli.)
let offsetMs = 0

export function syncServerTime(dateHeader: string | null): void {
  if (!dateHeader) return
  const server = Date.parse(dateHeader)
  if (Number.isNaN(server)) return
  offsetMs = server - Date.now()
}

export function serverNow(): number {
  return Date.now() + offsetMs
}

export function resetServerTimeForTests(): void {
  offsetMs = 0
}
