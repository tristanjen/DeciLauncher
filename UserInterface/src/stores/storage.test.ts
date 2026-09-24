// safeGet/safeSet 单测：正常读写 + 存储抛异常时不崩溃（WebView2 存储被禁用场景）。
// 此前各 store 模块顶层裸调 localStorage，存储不可用时会导致整包加载失败白屏。

import { afterEach, describe, expect, it, vi } from 'vitest'
import { safeClear, safeGet, safeSet } from './storage'

afterEach(() => {
  vi.restoreAllMocks()
  localStorage.clear()
})

describe('safeGet/safeSet', () => {
  it('正常读写 localStorage', () => {
    safeSet('test-key', 'hello')
    expect(safeGet('test-key')).toBe('hello')
  })

  it('读取抛异常时返回 null 而不抛出', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage disabled')
    })
    expect(safeGet('test-key')).toBeNull()
  })

  it('写入抛异常时静默不抛出', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage disabled')
    })
    expect(() => safeSet('test-key', 'v')).not.toThrow()
  })

  it('safeClear 正常清空', () => {
    safeSet('test-key', 'hello')
    safeClear()
    expect(safeGet('test-key')).toBeNull()
  })

  it('清空抛异常时静默不抛出', () => {
    vi.spyOn(Storage.prototype, 'clear').mockImplementation(() => {
      throw new Error('storage disabled')
    })
    expect(() => safeClear()).not.toThrow()
  })
})
