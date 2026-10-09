import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from './api/client'
import type { EventSummary } from './api/types'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AuthForm } from './components/AuthForm'
import { activeReservationsBySeat } from './lib/seatState'
import { usePolled } from './lib/usePolled'
import { MyReservationsPage } from './pages/MyReservationsPage'
import { SeatsPage } from './pages/SeatsPage'

type View = 'seats' | 'mine' | 'auth'

const MINE_POLL_MS = 5000

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

  // Rezervasyonlarım: koltuk haritasındaki "senin" renkleri ve geri sayım kartları buradan beslenir.
  // userId bağımlılığı bilinçli: kullanıcı değişince `load` değişir ve usePolled önceki kullanıcının verisini atar.
  const userId = session?.userId
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  const loadMine = useCallback(() => api.myReservations(), [userId])
  const reservations = usePolled(loadMine, MINE_POLL_MS, session !== null)
  const reservationList = useMemo(() => reservations.data ?? [], [reservations.data])
  const mine = useMemo(() => activeReservationsBySeat(reservationList), [reservationList])
  const holds = useMemo(() => reservationList.filter((r) => r.status === 'Held'), [reservationList])
  const reloadMine = reservations.reload

  return (
    <div className="mx-auto min-h-screen max-w-4xl px-4 pb-16">
      <header className="flex items-center justify-between py-5">
        <button type="button" onClick={() => setView('seats')} className="text-lg font-bold tracking-tight text-indigo-700">
          Koltuk Rezervasyon
        </button>
        <nav className="flex items-center gap-3 text-sm">
          {session ? (
            <>
              <button
                type="button"
                onClick={() => setView('mine')}
                aria-current={view === 'mine' ? 'page' : undefined}
                className={`rounded-lg px-3 py-1.5 hover:bg-slate-100 ${view === 'mine' ? 'font-semibold text-indigo-700' : ''}`}
              >
                Rezervasyonlarım
                {holds.length > 0 && (
                  <span className="ml-1.5 rounded-full bg-indigo-600 px-1.5 py-0.5 text-[10px] font-bold text-white" aria-label={`${holds.length} aktif tutma`}>
                    {holds.length}
                  </span>
                )}
              </button>
              <span className="hidden text-slate-600 md:inline">{session.email}</span>
              <button type="button" onClick={() => { logout(); setView('seats') }} className="rounded-lg border border-slate-300 px-3 py-1.5 hover:bg-slate-100">
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
        ) : view === 'mine' && session ? (
          <MyReservationsPage
            reservations={reservations.data}
            error={reservations.error}
            events={events}
            onChanged={reloadMine}
            onBrowse={() => setView('seats')}
          />
        ) : (
          <SeatsPage
            events={events}
            mine={mine}
            holds={holds}
            loggedIn={session !== null}
            onLoginRequired={() => setView('auth')}
            onReservationsChanged={reloadMine}
          />
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
