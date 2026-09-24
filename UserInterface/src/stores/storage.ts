// localStorage 安全读写封装。
// WebView2 存储被禁用/异常时 localStorage.getItem/setItem 会直接抛异常，
// 模块顶层裸调用会导致 store 模块加载失败 → 整个应用白屏。
// 统一在此 try/catch：存储不可用时静默降级为内存态（仅本次会话生效）。

/** 读取 localStorage；存储不可用时返回 null */
export function safeGet(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

/** 写入 localStorage；存储不可用时静默跳过 */
export function safeSet(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch { /* 存储不可用时静默：仅本次会话生效 */ }
}

/** 清空 localStorage；存储不可用时静默跳过（TitleBar 连点 logo 的调试手势使用） */
export function safeClear(): void {
  try {
    localStorage.clear()
  } catch { /* 存储不可用时静默 */ }
}
