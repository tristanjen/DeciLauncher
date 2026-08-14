// 窗口拖拽 Composable — 封装与 Photino C# 后端的拖拽通信逻辑
// 使用 requestAnimationFrame 节流，限制消息频率为 60fps
import { onMounted, onUnmounted } from 'vue'
// 前端 ↔ 后端消息桥（类型契约见 messages.ts）
import { sendNative } from '../native'

/**
 * 窗口拖拽逻辑 Hook
 * @returns {{ onMouseDown, send }} 鼠标按下处理函数和发送消息函数
 */
export function useWindowDrag() {
  // 拖拽状态：是否正在拖拽中
  let dragging = false
  // 上一帧鼠标屏幕坐标 X（用于计算增量）
  let prevScreenX = 0
  // 上一帧鼠标屏幕坐标 Y
  let prevScreenY = 0
  // 本帧内累计的 X 方向位移（rAF 批量合并）
  let pendingDx = 0
  // 本帧内累计的 Y 方向位移
  let pendingDy = 0
  // 是否已有待处理的 rAF 请求（防止重复排队）
  let pending = false
  // rAF 返回的 ID（用于组件卸载时取消未完成的动画帧）
  let rafId = 0

  /**
   * 鼠标按下事件处理 — 开始窗口拖拽
   * @param e 鼠标事件对象
   */
  function onMouseDown(e: MouseEvent) {
    // 如果点击的是标题栏中的按钮（关闭/最小化），不触发拖拽
    if ((e.target as HTMLElement).closest('button')) return
    // 进入拖拽状态
    dragging = true
    // 记录起始屏幕坐标
    prevScreenX = e.screenX
    prevScreenY = e.screenY
    // 通知 C# 后端开始拖拽（Windows 启动原生拖拽，其他平台记录初始位置）
    sendNative('drag-start')
  }

  /**
   * 鼠标移动事件处理 — 窗口位置追踪
   * @param e 鼠标事件对象
   */
  function onMouseMove(e: MouseEvent) {
    // 非拖拽状态则忽略
    if (!dragging) return
    // 累加本帧的鼠标位移增量
    pendingDx += e.screenX - prevScreenX
    pendingDy += e.screenY - prevScreenY
    // 更新参考点屏幕坐标
    prevScreenX = e.screenX
    prevScreenY = e.screenY

    // 如果没有待处理的 rAF，排队一个
    if (!pending) {
      pending = true
      // 在下一次屏幕刷新时批量发送位移数据（最多 60fps）
      rafId = requestAnimationFrame(() => {
        pending = false
        // 如果在等待期间已经停止拖拽，丢弃累积数据
        if (!dragging) { pendingDx = 0; pendingDy = 0; return }
        // 如果没有有效位移，跳过发送
        if (pendingDx === 0 && pendingDy === 0) return
        // 发送本帧累积的位移增量到 C# 后端
        sendNative('drag', { dx: pendingDx, dy: pendingDy })
        // 重置累积器，为下一帧准备
        pendingDx = 0
        pendingDy = 0
      })
    }
  }

  /**
   * 鼠标释放事件处理 — 结束窗口拖拽
   */
  function onMouseUp() {
    // 退出拖拽状态
    dragging = false
  }

  // 组件挂载时注册全局鼠标事件监听
  onMounted(() => {
    // 监听 document 级别的 mousemove 以追踪窗口外的鼠标移动
    document.addEventListener('mousemove', onMouseMove)
    // 监听 document 级别的 mouseup 以确保任何位置的释放都正确结束
    document.addEventListener('mouseup', onMouseUp)
  })

  // 组件卸载时清理事件监听和未完成的动画帧
  onUnmounted(() => {
    // 取消未完成的 rAF 回调，防止内存泄漏
    cancelAnimationFrame(rafId)
    // 移除 mousemove 事件监听
    document.removeEventListener('mousemove', onMouseMove)
    // 移除 mouseup 事件监听
    document.removeEventListener('mouseup', onMouseUp)
  })

  // 返回需要暴露给 TitleBar 组件的方法（send 复用消息桥的 sendNative，类型化）
  return { onMouseDown, send: sendNative }
}
