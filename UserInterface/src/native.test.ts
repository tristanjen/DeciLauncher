// native.ts 消息桥测试：sendNative 序列化、dispatch 路由/卸载、异常输入容错。
// 模块顶层会读取 window.external / chrome.webview 并注册接收通道，
// 每个用例通过 vi.resetModules + 动态 import 重建独立的 handlers 注册表。

import { beforeEach, describe, expect, it, vi } from 'vitest'

type NativeModule = typeof import('./native')

function fakeExternal() {
  return { sendMessage: vi.fn(), receiveMessage: undefined as ((m: string) => void) | undefined }
}

let external: ReturnType<typeof fakeExternal>
let native: NativeModule

beforeEach(async () => {
  vi.resetModules()
  external = fakeExternal()
  window.external = external as unknown as Window['external']
  // 模块加载时会把 dispatch 写入 receiveMessage
  native = await import('./native')
})

describe('sendNative', () => {
  it('发送 type + data 的 JSON', () => {
    native.sendNative('scan-games', { path: 'C:/mc' })
    expect(external.sendMessage).toHaveBeenCalledOnce()
    expect(external.sendMessage).toHaveBeenCalledWith(
      JSON.stringify({ type: 'scan-games', path: 'C:/mc' }),
    )
  })

  it('未提供 data 时仅发送 type', () => {
    native.sendNative('list-accounts')
    expect(external.sendMessage).toHaveBeenCalledWith('{"type":"list-accounts"}')
  })
})

describe('onNativeMessage 分发', () => {
  const msg = (type: string, payload: Record<string, unknown> = {}) =>
    JSON.stringify({ type, ...payload })

  it('把 payload（剥离 type）投递给已注册的监听器', () => {
    const handler = vi.fn()
    native.onNativeMessage('game-list', handler)

    window.external.receiveMessage!('{"type":"game-list","path":"/mc","games":[]}')

    expect(handler).toHaveBeenCalledOnce()
    expect(handler).toHaveBeenCalledWith({ path: '/mc', games: [] })
  })

  it('多个监听器全部收到消息', () => {
    const a = vi.fn()
    const b = vi.fn()
    native.onNativeMessage('account-error', a)
    native.onNativeMessage('account-error', b)

    window.external.receiveMessage!(msg('account-error', { message: 'x' }))

    expect(a).toHaveBeenCalledWith({ message: 'x' })
    expect(b).toHaveBeenCalledWith({ message: 'x' })
  })

  it('返回的取消函数可移除监听', () => {
    const handler = vi.fn()
    const off = native.onNativeMessage('game-exited', handler)
    off()

    window.external.receiveMessage!(msg('game-exited'))

    expect(handler).not.toHaveBeenCalled()
  })

  it('未注册的消息类型与损坏 JSON 均静默忽略', () => {
    expect(() => {
      window.external.receiveMessage!(msg('__unknown__'))
      window.external.receiveMessage!('{ not json')
      window.external.receiveMessage!('')
    }).not.toThrow()
  })

  it('payload 缺失 type 字段时不分发', () => {
    const handler = vi.fn()
    native.onNativeMessage('game-exited', handler)

    window.external.receiveMessage!(JSON.stringify({ foo: 1 }))

    expect(handler).not.toHaveBeenCalled()
  })
})
