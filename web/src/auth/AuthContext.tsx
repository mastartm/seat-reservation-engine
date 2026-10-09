import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAuthToken, setUnauthorizedHandler } from '../api/client'
import type { AuthResult } from '../api/types'

const STORAGE_KEY = 'seat-reservation.session'

interface AuthState {
  session: AuthResult | null
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)

// localStorage erişimi gizli pencerede / engelli sitelerde hata verebilir; oturum o zaman yalnızca bellekte yaşar.
function loadSession(): AuthResult | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const saved = JSON.parse(raw) as AuthResult
    // Süresi geçmiş token'ı sunucuya göndermeden at: boşuna 401 turu olmasın.
    return Date.parse(saved.expiresAt) > Date.now() ? saved : null
  } catch {
    return null
  }
}

function persist(session: AuthResult | null): void {
  try {
    if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    /* saklanamıyorsa oturum sayfa yenilenene kadar bellekte kalır */
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthResult | null>(() => {
    const initial = loadSession()
    // İlk render'dan önce ayarlanmalı: çocuk bileşenlerin ilk istekleri token'ı görsün.
    setAuthToken(initial?.token ?? null)
    return initial
  })

  const apply = useCallback((next: AuthResult | null) => {
    setAuthToken(next?.token ?? null)
    persist(next)
    setSession(next)
  }, [])

  const logout = useCallback(() => apply(null), [apply])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  const value = useMemo<AuthState>(
    () => ({
      session,
      login: async (email, password) => apply(await api.login(email, password)),
      register: async (email, password) => apply(await api.register(email, password)),
      logout,
    }),
    [session, apply, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth, AuthProvider içinde kullanılmalı.')
  return ctx
}
