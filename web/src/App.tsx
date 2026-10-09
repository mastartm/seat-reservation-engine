import { useEffect, useMemo, useState } from 'react'
import { api } from './api/client'
import type { EventSummary, ReservationView } from './api/types'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AuthForm } from './components/AuthForm'
import { SeatsPage } from './pages/SeatsPage'

type View = 'seats' | 'auth'

function Shell() {
  const { session, logout } = useAuth()
  const [view, setView] = useState<View>('seats')
  const [events, setEvents] = useState<EventSummary[]>([])
  const [eventsError, setEventsError] = useState<string | null>(null)

  useEffect(() => {
    api
      .listEvents()
      .then(setEvents)
      .catch((e: Error) => setEventsError(e.message))
  }, [])

  // Aşama 2 / özellik 2'de rezervasyonlar sunucudan çekilecek; şimdilik koltuk haritası salt okunur.
  const mine = useMemo(() => new Map<string, ReservationView>(), [])

  return (
    <div className="mx-auto min-h-screen max-w-4xl px-4 pb-16">
      <header className="flex items-center justify-between py-5">
        <button type="button" onClick={() => setView('seats')} className="text-lg font-bold tracking-tight text-indigo-700">
          Koltuk Rezervasyon
        </button>
        <nav className="flex items-center gap-3 text-sm">
          {session ? (
            <>
              <span className="hidden text-slate-600 sm:inline">{session.email}</span>
              <button type="button" onClick={logout} className="rounded-lg border border-slate-300 px-3 py-1.5 hover:bg-slate-100">
                Çıkış
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => setView('auth')}
              className="rounded-lg bg-indigo-600 px-3 py-1.5 font-medium text-white hover:bg-indigo-700"
            >
              Giriş / Kayıt
            </button>
          )}
        </nav>
      </header>

      <main>
        {eventsError && (
          <p role="alert" className="mb-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
            Etkinlikler yüklenemedi: {eventsError}
          </p>
        )}
        {view === 'auth' && !session ? (
          <div className="mx-auto max-w-sm rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h1 className="mb-4 text-xl font-semibold">Hesabınla devam et</h1>
            <AuthForm onDone={() => setView('seats')} />
          </div>
        ) : (
          <SeatsPage events={events} mine={mine} />
        )}
      </main>
    </div>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <Shell />
    </AuthProvider>
  )
}
