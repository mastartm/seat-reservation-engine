import { createContext, useContext } from 'react'
import type { AuthResult } from '../api/types'

export interface AuthState {
  session: AuthResult | null
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  /** Tek tıkla misafir hesabı (sunucuda Demo modu açıksa). */
  demoLogin: () => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth, AuthProvider içinde kullanılmalı.')
  return ctx
}
