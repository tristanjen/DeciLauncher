// 国际化语言状态 — localStorage 持久化 + 系统语言检测 + 轻量翻译函数
// 与 selected-game / selected-account / max-memory 等采用相同的 localStorage 模式
import { ref, watch } from 'vue'
import { zhCN } from '../i18n/zh-CN'
import { enUS } from '../i18n/en-US'

export type Locale = 'zh-CN' | 'en-US'

const SUPPORTED_LOCALES: Locale[] = ['zh-CN', 'en-US']

const messages: Record<Locale, Record<string, string>> = {
  'zh-CN': zhCN,
  'en-US': enUS
}

/**
 * 系统语言检测：navigator.language（如 zh-CN / en-US / zh-TW）
 * zh* → zh-CN，其余（含不支持的语言）→ en-US
 */
function detectSystemLocale(): Locale {
  const lang = (navigator.language || '').toLowerCase()
  if (lang.startsWith('zh')) return 'zh-CN'
  return 'en-US'
}

function resolveInitialLocale(): Locale {
  try {
    const stored = localStorage.getItem('language')
    if (stored === 'zh-CN' || stored === 'en-US') return stored
  } catch {
    // WebView2 存储被禁用/异常时回退系统语言检测，避免 store 模块加载失败导致白屏
  }
  return detectSystemLocale()
}

// 当前语言：localStorage 恢复 → 无值/非法值时按系统语言检测
export const locale = ref<Locale>(resolveInitialLocale())

// 同步 <html lang>：模块加载即生效（覆盖 index.html 的默认 zh-CN）
document.documentElement.lang = locale.value

watch(locale, (v) => {
  try {
    localStorage.setItem('language', v)
  } catch { /* 存储不可用时静默：仅本次会话生效 */ }
  document.documentElement.lang = v
})

/**
 * 翻译函数：t('key') 或 t('key', { name: 'value' })（{name} 占位替换）
 * 缺失键时回退键名本身，避免界面出现 undefined
 */
export function t(key: string, params?: Record<string, string | number>): string {
  let text = messages[locale.value][key] ?? key
  if (params) {
    for (const [k, v] of Object.entries(params)) {
      text = text.replaceAll(`{${k}}`, String(v))
    }
  }
  return text
}

export { SUPPORTED_LOCALES }
