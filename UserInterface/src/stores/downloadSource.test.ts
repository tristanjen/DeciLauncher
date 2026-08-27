// downloadSource store 测试：合法值恢复、非法值回退 official-first、
// 与后端默认值一致性（official-first，见 AGENTS.md 镜像开关约定）。

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'

beforeEach(() => {
  vi.resetModules()
  localStorage.clear()
})

describe('downloadSource 初始化', () => {
  it.each(['mirror', 'official-first', 'official'] as const)(
    '存储的合法值 %s 被原样恢复',
    async (stored) => {
      localStorage.setItem('download-source', stored)
      const { downloadSource } = await import('../stores/downloadSource')
      expect(downloadSource.value).toBe(stored)
    },
  )

  it('非法值回退默认 official-first', async () => {
    localStorage.setItem('download-source', 'pirate-bay')
    const { downloadSource } = await import('../stores/downloadSource')
    expect(downloadSource.value).toBe('official-first')
  })

  it('未设置时为默认 official-first', async () => {
    const { downloadSource } = await import('../stores/downloadSource')
    expect(downloadSource.value).toBe('official-first')
  })
})

describe('downloadSource 持久化', () => {
  it('修改后经 watch 写回 localStorage', async () => {
    const { downloadSource } = await import('../stores/downloadSource')
    downloadSource.value = 'mirror'
    await nextTick()
    expect(localStorage.getItem('download-source')).toBe('mirror')
  })
})
