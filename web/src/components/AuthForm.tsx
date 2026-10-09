import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'

type Mode = 'login' | 'register'

export function AuthForm({ onDone }: { onDone: () => void }) {
  const { login, register } = useAuth()
  const [mode, setMode] = useState<Mode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await (mode === 'login' ? login : register)(email, password)
      onDone()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Beklenmeyen bir hata oluştu.')
    } finally {
      setSubmitting(false)
    }
  }

  const isLogin = mode === 'login'
  return (
    <form onSubmit={submit} className="space-y-4" aria-label={isLogin ? 'Giriş formu' : 'Kayıt formu'}>
      <div>
        <label htmlFor="email" className="mb-1 block text-sm font-medium">
          E-posta
        </label>
        <input
          id="email"
          type="email"
          required
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200"
        />
      </div>
      <div>
        <label htmlFor="password" className="mb-1 block text-sm font-medium">
          Parola
        </label>
        <input
          id="password"
          type="password"
          required
          minLength={isLogin ? undefined : 8}
          autoComplete={isLogin ? 'current-password' : 'new-password'}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200"
        />
        {!isLogin && <p className="mt-1 text-xs text-slate-500">En az 8 karakter.</p>}
      </div>

      {error && (
        <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={submitting}
        className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60"
      >
        {submitting ? 'Bekleyin…' : isLogin ? 'Giriş yap' : 'Kayıt ol'}
      </button>

      <p className="text-center text-sm text-slate-600">
        {isLogin ? 'Hesabın yok mu?' : 'Zaten hesabın var mı?'}{' '}
        <button
          type="button"
          onClick={() => {
            setMode(isLogin ? 'register' : 'login')
            setError(null)
          }}
          className="font-medium text-indigo-600 hover:underline"
        >
          {isLogin ? 'Kayıt ol' : 'Giriş yap'}
        </button>
      </p>
    </form>
  )
}
