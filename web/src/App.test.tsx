import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { resetServerTimeForTests } from './lib/clock'
import { installFakeApi, signIn } from './test/fakeApi'

afterEach(() => {
  vi.unstubAllGlobals()
  resetServerTimeForTests()
})

describe('koltuk tutma ve onaylama akışı', () => {
  it('giriş yapmadan koltuğa tıklayınca giriş ekranına yönlendirir, istek atmaz', async () => {
    const { fetchMock } = installFakeApi()
    render(<App />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'A1, boş' }))

    expect(await screen.findByRole('form', { name: 'Giriş formu' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/hold'))).toBe(false)
  })

  it('koltuğu tutar, geri sayımı gösterir, onaylayınca koltuk "senin koltuğun" olur', async () => {
    signIn()
    installFakeApi()
    render(<App />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'A2, boş' }))

    const timer = await screen.findByRole('timer', { name: 'A2 için kalan süre' })
    // Date başlığı saniye çözünürlüklü olduğundan başlangıç değeri ±1 sn oynayabilir.
    expect(timer).toHaveTextContent(/^(10:01|10:00|09:59)$/)
    expect(screen.getByRole('button', { name: 'A2, senin için tutuldu' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'A2 için satın almayı onayla' }))

    expect(await screen.findByRole('button', { name: 'A2, senin koltuğun' })).toBeInTheDocument()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('A2 satın alındı')
  })

  it('aynı anda başkası kazandıysa (409) sunucunun mesajını gösterir ve haritayı yeniler', async () => {
    signIn()
    const { seats } = installFakeApi({ failHoldWith: { status: 409, detail: 'Koltuk artık müsait değil.' } })
    render(<App />)
    const user = userEvent.setup()

    const seat = await screen.findByRole('button', { name: 'A1, boş' })
    // Biz tıklamadan hemen önce başkası tutmuş gibi: sunucu durumu değişir ama harita henüz eski.
    seats[0].status = 'Held'
    await user.click(seat)

    expect(await screen.findByRole('alert')).toHaveTextContent('Koltuk artık müsait değil.')
    expect(await screen.findByRole('button', { name: 'A1, başkası tutuyor' })).toBeDisabled()
  })

  it('oturum açıkken kullanıcının e-postası ve çıkış düğmesi görünür; çıkınca tutma paneli kapanır', async () => {
    signIn()
    installFakeApi()
    render(<App />)
    const user = userEvent.setup()

    const header = await screen.findByRole('banner')
    expect(within(header).getByText('ali@example.com')).toBeInTheDocument()
    await user.click(within(header).getByRole('button', { name: 'Çıkış' }))
    expect(within(header).getByRole('button', { name: 'Giriş / Kayıt' })).toBeInTheDocument()
  })
})
