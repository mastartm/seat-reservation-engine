import { useState } from 'react'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/authState'

interface Props {
  onDone?: () => void
  className?: string
}

/** Kayıt/parola olmadan, tek tıkla tek kullanımlık misafir hesabıyla giriş (sunucuda Demo__Enabled=true gerekir). */
export function DemoLoginButton({ onDone, className = '' }: Props) {
  const { demoLogin } = useAuth()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function go() {
    setBusy(true)
    setError(null)
    try {
      await demoLogin()
      onDone?.()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Demo girişi başarısız oldu.')
      setBusy(false)
    }
  }

  return (
    <div className={className}>
      <button
        type="button"
        onClick={go}
        disabled={busy}
        className="w-full rounded-lg border border-indigo-300 bg-indigo-50 px-4 py-2 text-sm font-semibold text-indigo-700 hover:bg-indigo-100 disabled:opacity-60"
      >
        {busy ? 'Hazırlanıyor…' : 'Demo ile dene (kayıtsız)'}
      </button>
      {error && (
        <p role="alert" className="mt-2 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}
    </div>
  )
}
