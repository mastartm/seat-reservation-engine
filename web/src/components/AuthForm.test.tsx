import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth/AuthContext'
import { AuthForm } from './AuthForm'

const session = {
  token: 'jwt', expiresAt: new Date(Date.now() + 3600_000).toISOString(),
  userId: 'u1', email: 'ali@example.com', role: 'User',
}

function mockFetch(status: number, body: unknown) {
  const fn = vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fn)
  return fn
}

afterEach(() => vi.unstubAllGlobals())

function setup(onDone = vi.fn()) {
  render(<AuthProvider><AuthForm onDone={onDone} /></AuthProvider>)
  return { onDone, user: userEvent.setup() }
}

describe('AuthForm', () => {
  it('giriş başarılıysa istek doğru gövdeyle gider ve onDone çağrılır', async () => {
    const fetchMock = mockFetch(200, session)
    const { onDone, user } = setup()

    await user.type(screen.getByLabelText('E-posta'), 'ali@example.com')
    await user.type(screen.getByLabelText('Parola'), 'gizli-parola')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(fetchMock).toHaveBeenCalledOnce()
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/auth/login')
    expect(JSON.parse(init.body)).toEqual({ email: 'ali@example.com', password: 'gizli-parola' })
    expect(onDone).toHaveBeenCalledOnce()
  })

  it('sunucu hata verirse mesajı gösterir ve onDone çağrılmaz', async () => {
    mockFetch(401, { title: 'Kimlik doğrulanamadı', detail: 'E-posta veya parola hatalı.' })
    const { onDone, user } = setup()

    await user.type(screen.getByLabelText('E-posta'), 'ali@example.com')
    await user.type(screen.getByLabelText('Parola'), 'yanlis')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya parola hatalı.')
    expect(onDone).not.toHaveBeenCalled()
  })

  it('kayıt moduna geçince register ucunu çağırır', async () => {
    const fetchMock = mockFetch(201, session)
    const { user } = setup()

    await user.click(screen.getByRole('button', { name: 'Kayıt ol' }))
    await user.type(screen.getByLabelText('E-posta'), 'yeni@example.com')
    await user.type(screen.getByLabelText('Parola'), 'en-az-8-karakter')
    await user.click(screen.getByRole('button', { name: 'Kayıt ol' }))

    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/register')
  })

  it('sunucuya hiç ulaşılamazsa anlaşılır bir mesaj gösterir', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    const { user } = setup()

    await user.type(screen.getByLabelText('E-posta'), 'ali@example.com')
    await user.type(screen.getByLabelText('Parola'), 'gizli-parola')
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Sunucuya ulaşılamadı')
  })
})
