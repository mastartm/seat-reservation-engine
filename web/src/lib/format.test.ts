import { describe, expect, it } from 'vitest'
import { formatRemaining } from './format'

describe('formatRemaining', () => {
  it.each([
    [600_000, '10:00'],
    [599_001, '10:00'], // yukarı yuvarlar: kullanıcı 00:00'ı süre bittiğinde görür, bir saniye erken değil
    [61_000, '01:01'],
    [9_000, '00:09'],
    [1, '00:01'],
    [0, '00:00'],
    [-5_000, '00:00'],
  ])('%i ms → %s', (ms, expected) => {
    expect(formatRemaining(ms)).toBe(expected)
  })
})
