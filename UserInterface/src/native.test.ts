// native.ts 消息桥测试：sendNative 序列化、dispatch 路由/卸载、异常输入容错。
// 模块顶层会读取 window.external / chrome.webview 并注册接收通道，
// 每个用例通过 vi.resetModules + 动态 import 重建独立的 handlers 注册表。

import { beforeEach, describe, expect, it, vi } from 'vitest'

type NativeModule = typeof import('./native')

function fakeExternal() {
  // Photino macOS/Linux 的约定：receiveMessage 是注册函数，调用它以订阅消息；
  // 直接赋值会覆盖注册函数（macOS 实测会导致所有后端消息静默丢失）
  let receiver: ((message: string) => void) | undefined
  return {
    sendMessage: vi.fn(),
    receiveMessage: vi.fn((callback: (message: string) => void) => { receiver = callback }),
    deliver(message: string) { receiver?.(message) },
  }
}

let external: ReturnType<typeof fakeExternal>
let native: NativeModule

beforeEach(async () => {
  vi.resetModules()
  external = fakeExternal()
  window.external = external as unknown as Window['external']
  // 模块加载时应调用 receiveMessage(dispatch) 注册回调
  native = await import('./native')
})

it('通过 receiveMessage(callback) 注册接收通道，而非直接赋值', () => {
  expect(external.receiveMessage).toHaveBeenCalledOnce()
  expect(external.receiveMessage).toHaveBeenCalledWith(expect.any(Function))
  // 若被赋值覆盖，注册函数本身会丢失，deliver 也拿不到回调
  external.deliver('{"type":"__probe__"}')
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

    external.deliver('{"type":"game-list","path":"/mc","games":[]}')

    expect(handler).toHaveBeenCalledOnce()
    expect(handler).toHaveBeenCalledWith({ path: '/mc', games: [] })
  })

  it('多个监听器全部收到消息', () => {
    const a = vi.fn()
    const b = vi.fn()
    native.onNativeMessage('account-error', a)
    native.onNativeMessage('account-error', b)

    external.deliver(msg('account-error', { message: 'x' }))

    expect(a).toHaveBeenCalledWith({ message: 'x' })
    expect(b).toHaveBeenCalledWith({ message: 'x' })
  })

  it('返回的取消函数可移除监听', () => {
    const handler = vi.fn()
    const off = native.onNativeMessage('game-exited', handler)
    off()

    external.deliver(msg('game-exited'))

    expect(handler).not.toHaveBeenCalled()
  })

  it('未注册的消息类型与损坏 JSON 均静默忽略', () => {
    expect(() => {
      external.deliver(msg('__unknown__'))
      external.deliver('{ not json')
      external.deliver('')
    }).not.toThrow()
  })

  it('payload 缺失 type 字段时不分发', () => {
    const handler = vi.fn()
    native.onNativeMessage('game-exited', handler)

    external.deliver(JSON.stringify({ foo: 1 }))

    expect(handler).not.toHaveBeenCalled()
  })
})
