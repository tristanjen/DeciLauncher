// 前端 ↔ C# 后端消息桥
// sendNative: 向 Photino 后端发送消息（消息类型契约见 messages.ts）
// onNativeMessage: 注册特定消息类型的监听器（payload 类型自动推导）

import type { RequestMap, ResponseMap } from './messages'

type AnyHandler = (payload: unknown) => void
const handlers = new Map<string, AnyHandler[]>()

export function sendNative<K extends keyof RequestMap>(type: K, data?: RequestMap[K]) {
  window.external.sendMessage(JSON.stringify({ type, ...data }))
}

export function onNativeMessage<K extends keyof ResponseMap>(type: K, handler: (payload: ResponseMap[K]) => void) {
  if (!handlers.has(type)) handlers.set(type, [])
  handlers.get(type)!.push(handler as AnyHandler)
  return () => {
    const list = handlers.get(type)
    if (list) {
      const idx = list.indexOf(handler as AnyHandler)
      if (idx !== -1) list.splice(idx, 1)
    }
  }
}

function dispatch(message: string) {
  // 仅在开发模式打印入站消息，生产构建不产生控制台噪音
  if (import.meta.env.DEV) {
    console.log('[native] incoming:', message.length > 200 ? message.substring(0, 200) + '...' : message)
  }
  try {
    const json = JSON.parse(message)
    const type = json.type as string | undefined
    if (type && handlers.has(type)) {
      const { type: _, ...payload } = json
      handlers.get(type)!.forEach(h => h(payload))
    }
  } catch { /* 忽略解析错误 */ }
}

// Windows: WebView2 — C# SendWebMessage → PostWebMessageAsString → chrome.webview message 事件
// 兜底：仅注册 receiveMessage（非 Windows 平台）。避免同时绑定两种机制导致双重分发。
const w: Record<string, unknown> = window as unknown as Record<string, unknown>
const chromeObj = (w.chrome && typeof w.chrome === 'object') ? w.chrome as Record<string, unknown> : null
const externalObj = (w.external && typeof w.external === 'object') ? w.external as Record<string, unknown> : null

if (chromeObj?.webview) {
  ;(chromeObj.webview as EventTarget).addEventListener('message', (e: Event) => {
    dispatch((e as MessageEvent).data as string)
  })
} else if (externalObj) {
  // 运行时防御：window.external 可能不存在（旧版 WebView2 运行时）
  externalObj.receiveMessage = dispatch
} else {
  console.warn('[native] 无法注册消息接收通道：window.external 与 chrome.webview 均不存在')
}
