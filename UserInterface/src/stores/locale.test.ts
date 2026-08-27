// locale store 测试：语言检测优先级（localStorage > navigator 检测），
// t() 翻译/参数替换/缺失键回退，切换语言即时生效并同步 <html lang>。

import { beforeEach, describe, expect, it, vi } from 'vitest'

function stubNavigatorLanguage(lang: string) {
  Object.defineProperty(navigator, 'language', {
    value: lang,
    configurable: true,
  })
}

beforeEach(() => {
  vi.resetModules()
  localStorage.clear()
})

describe('初始语言解析', () => {
  it('localStorage 存储值优先于系统语言', async () => {
    stubNavigatorLanguage('en-US')
    localStorage.setItem('language', 'zh-CN')
    const { locale } = await import('../stores/locale')
    expect(locale.value).toBe('zh-CN')
  })

  it('未存储且系统 zh-TW → 检测为 zh-CN', async () => {
    stubNavigatorLanguage('zh-TW')
    const { locale } = await import('../stores/locale')
    expect(locale.value).toBe('zh-CN')
  })

  it('未存储且系统 ja-JP → 回退 en-US', async () => {
    stubNavigatorLanguage('ja-JP')
    const { locale } = await import('../stores/locale')
    expect(locale.value).toBe('en-US')
  })

  it('存储的非法值 → 回退系统语言检测', async () => {
    stubNavigatorLanguage('en-GB')
    localStorage.setItem('language', 'fr-FR')
    const { locale } = await import('../stores/locale')
    expect(locale.value).toBe('en-US')
  })
})

describe('t() 翻译函数', () => {
  it('返回当前语言文案', async () => {
    localStorage.setItem('language', 'zh-CN')
    const { t } = await import('../stores/locale')
    expect(t('common.cancel')).toBe('取消')
  })

  it('params 占位符被替换', async () => {
    localStorage.setItem('language', 'zh-CN')
    const { t } = await import('../stores/locale')
    expect(t('games.minecraft', { version: '1.20.4' })).toBe('Minecraft 1.20.4')
    expect(t('games.directory', { path: 'D:/mc' })).toBe('实例目录：D:/mc')
  })

  it('缺失键回退键名本身', async () => {
    const { t } = await import('../stores/locale')
    expect(t('__missing_key__')).toBe('__missing_key__')
  })

  it('切换语言后文案即时切换并同步 html lang', async () => {
    const { locale, t } = await import('../stores/locale')
    locale.value = 'zh-CN'
    expect(t('common.cancel')).toBe('取消')

    locale.value = 'en-US'
    expect(t('common.cancel')).toBe('Cancel')
    expect(document.documentElement.lang).toBe('en-US')
  })
})
