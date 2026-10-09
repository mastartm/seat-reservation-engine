import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * `load`'u hemen ve ardından her `intervalMs`'de çağırır (sekme görünmezken durur, geri gelince hemen yeniler).
 * Neden polling, SignalR değil: tek yönlü, saniyeler mertebesinde tazelik yeterli; ek sunucu bileşeni ve
 * bağlantı yönetimi gerektirmez. Kaybedilen yarış zaten sunucuda 409 ile çözülür (docs/ARCHITECTURE.md §11).
 *
 * `load` değişince (örn. başka etkinlik seçilince) eski veri gösterilmez. Çağıran `load`'u useCallback ile sabitlemeli.
 */
export function usePolled<T>(load: () => Promise<T>, intervalMs: number, enabled = true) {
  // Verinin hangi `load`'a ait olduğunu da tutarız: başka etkinliğe geçince eski etkinliğin koltukları bir an bile görünmesin.
  const [result, setResult] = useState<{ source: () => Promise<T>; value: T } | null>(null)
  const [error, setError] = useState<Error | null>(null)
  const [settled, setSettled] = useState<(() => Promise<T>) | null>(null)
  // Yavaş dönen eski yanıt, daha yeni bir yanıtın üstüne yazmasın.
  const latest = useRef(0)

  const reload = useCallback(async () => {
    const id = ++latest.current
    try {
      const value = await load()
      if (id !== latest.current) return
      setResult({ source: load, value })
      setError(null)
    } catch (e) {
      if (id === latest.current) setError(e as Error)
    } finally {
      if (id === latest.current) setSettled(() => load)
    }
  }, [load])

  useEffect(() => {
    if (!enabled) return
    void reload()
    const timer = setInterval(() => {
      if (!document.hidden) void reload()
    }, intervalMs)
    const onVisible = () => {
      if (!document.hidden) void reload()
    }
    document.addEventListener('visibilitychange', onVisible)
    const counter = latest
    return () => {
      counter.current++ // kurulum değişince/unmount'tan sonra gelen yanıtlar yok sayılsın
      clearInterval(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [enabled, intervalMs, reload])

  const current = enabled && result?.source === load
  return {
    data: current ? result.value : undefined,
    // Eski `load`'dan kalan hata da yeni bağlamda gösterilmesin.
    error: enabled && settled === load ? error : null,
    loading: enabled && settled !== load,
    reload,
  }
}
