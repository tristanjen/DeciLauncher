// games store 测试：maxMemory 从 localStorage 恢复时被 clamp 到 [512, 8192]、
// 非法值回退默认 2048；selectedGame 经 watch 持久化。

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'

beforeEach(() => {
  vi.resetModules()
  localStorage.clear()
})

describe('games store 初始化', () => {
  it('localStorage 超上限（99999）→ clamp 到 8192', async () => {
    localStorage.setItem('max-memory', '99999')
    const { maxMemory } = await import('../stores/games')
    expect(maxMemory.value).toBe(8192)
  })

  it('localStorage 低于下限（100）→ clamp 到 512', async () => {
    localStorage.setItem('max-memory', '100')
    const { maxMemory } = await import('../stores/games')
    expect(maxMemory.value).toBe(512)
  })

  it('非法字符串 → 默认 2048', async () => {
    localStorage.setItem('max-memory', 'abc')
    const { maxMemory } = await import('../stores/games')
    expect(maxMemory.value).toBe(2048)
  })

  it('缺省 → 默认 2048', async () => {
    const { maxMemory } = await import('../stores/games')
    expect(maxMemory.value).toBe(2048)
  })
})

describe('selectedGame 持久化', () => {
  it('初始从 localStorage 恢复', async () => {
    localStorage.setItem('selected-game', '1.20.4-fabric')
    const { selectedGame } = await import('../stores/games')
    expect(selectedGame.value).toBe('1.20.4-fabric')
  })

  it('修改后经 watch 写回 localStorage', async () => {
    const { selectedGame } = await import('../stores/games')
    selectedGame.value = '26.3-snapshot-8'
    await nextTick()
    expect(localStorage.getItem('selected-game')).toBe('26.3-snapshot-8')
  })
})
