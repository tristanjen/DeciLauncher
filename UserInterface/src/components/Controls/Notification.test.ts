// Notification 组件测试：随 notification store 显隐、按钮文案随语言切换、
// 点击「知道了」清除消息。Modal 的 Teleport 使用 stub 内联渲染以便断言。

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import Notification from './Notification.vue'
import { notification } from '../../stores/store'
import { locale } from '../../stores/locale'

beforeEach(() => {
  vi.resetModules()
  localStorage.clear()
  notification.value = null
})

const mountNotification = () =>
  mount(Notification, {
    global: {
      // Teleport to="#main-card" 目标在 App 布局中，单测用 stub 内联渲染
      stubs: { teleport: true },
    },
  })

describe('Notification 组件', () => {
  it('notification 为 null 时隐藏', () => {
    const w = mountNotification()
    expect(w.text()).not.toContain('我知道了')
    expect(w.text()).not.toContain('Got it')
    w.unmount()
  })

  it('设置通知后显示消息内容', async () => {
    const w = mountNotification()
    notification.value = '游戏启动失败'
    await nextTick()
    expect(w.text()).toContain('游戏启动失败')
    w.unmount()
  })

  it('确认按钮文案跟随语言（zh-CN / en-US）', async () => {
    const w = mountNotification()
    notification.value = 'msg'

    locale.value = 'zh-CN'
    await nextTick()
    expect(w.text()).toContain('我知道了')

    locale.value = 'en-US'
    await nextTick()
    expect(w.text()).toContain('Got it')
    w.unmount()
  })

  it('点击确认按钮清除通知并复位显隐', async () => {
    const w = mountNotification()
    notification.value = '待清除的消息'
    await nextTick()

    // 找到 footer 中的确认按钮（含当前语言的文案）
    const btn = w.findAll('button').find(b => b.text() !== '')
    expect(btn).toBeDefined()
    await btn!.trigger('click')

    expect(notification.value).toBeNull()
    await nextTick()
    expect(w.text()).not.toContain('待清除的消息')
    w.unmount()
  })
})
