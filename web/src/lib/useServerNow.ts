import { useEffect, useState } from 'react'
import { serverNow } from './clock'

/** Sunucu saatine göre "şimdi"yi (ms) her `intervalMs`'de günceller; geri sayımlar bunu okur. */
export function useServerNow(intervalMs = 1000): number {
  const [now, setNow] = useState(serverNow)
  useEffect(() => {
    const timer = setInterval(() => setNow(serverNow()), intervalMs)
    return () => clearInterval(timer)
  }, [intervalMs])
  return now
}
