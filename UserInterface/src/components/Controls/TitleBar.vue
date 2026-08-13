<script setup lang="ts">
// Vue 响应式引用
import { ref } from 'vue'
// Toast 提示消息
import { toast } from '../../stores/store'
// 国际化翻译
import { t } from '../../stores/locale'
// 导入窗口拖拽 composable（封装拖拽逻辑 + Photino 通信）
import { useWindowDrag } from '../../composables/useWindowDrag'
// 自定义控件：导航链接 + 图标按钮
import NavLink from './NavLink.vue'
import IconButton from './IconButton.vue'

// 从 composable 获取 onMouseDown 处理函数和 send 消息发送函数
const { onMouseDown, send } = useWindowDrag()

/**
 * 关闭按钮点击处理
 * 向 C# 后端发送 "close" 消息，触发 Photino 窗口关闭
 */
function closeWindow() {
  send('close')
}

/**
 * 最小化按钮点击处理
 * 向 C# 后端发送 "minimize" 消息，触发 Photino 窗口最小化
 */
function minimizeWindow() {
  send('minimize')
}

// 连续点击 logo 计数器 + 超时定时器（重置 localStorage）
const logoClickCount = ref(0)
let logoClickTimer: ReturnType<typeof setTimeout> | undefined

function handleLogoClick() {
  logoClickCount.value++
  if (logoClickCount.value >= 5) {
    localStorage.clear()
    toast.value = t('titlebar.localStorageReset')
    logoClickCount.value = 0
    if (logoClickTimer) { clearTimeout(logoClickTimer); logoClickTimer = undefined }
    return
  }
  if (logoClickTimer) clearTimeout(logoClickTimer)
  logoClickTimer = setTimeout(() => { logoClickCount.value = 0 }, 1500)
}
</script>

<template>
  <!-- 标题栏容器：212.5x12 单元（实际尺寸由 Tailwind 倍数定义） -->
  <div class="border-b border-[#52C41A]/25 shadow-[0_0_4px_#52C41A3F] relative w-212.5 h-12 bg-white/50 rounded-t-lg inline-flex flex-row items-center justify-between shrink-0" @mousedown="onMouseDown">
    <!-- 应用 Logo / 标题文字（SVG 图形） -->
    <img class="ml-4" src="../../assets/images/title.svg" alt="Title" @click="handleLogoClick" @mousedown.stop>

    <!-- 导航链接：用 custom+v-slot 渲染为 <span> 规避 WebView2 状态栏 URL 显示 -->
    <div class="absolute left-1/2 -translate-x-1/2 flex items-center justify-center gap-2" @mousedown.stop>
      <NavLink to="/" :label="t('nav.home')" />
      <NavLink to="/games" :label="t('nav.games')" />
      <NavLink to="/downloads" :label="t('nav.downloads')" />
      <NavLink to="/accounts" :label="t('nav.accounts')" />
      <NavLink to="/settings" :label="t('nav.settings')" />
    </div>

    <!-- 右侧按钮组：关闭 + 最小化 -->
    <div class="mr-2 flex flex-row-reverse gap-2">
      <!-- 关闭按钮：绿色悬停 + 深绿按下效果 -->
      <IconButton variant="titlebar" @click="closeWindow">
        <img class="pointer-events-none" src="../../assets/images/close.svg" alt="Close">
      </IconButton>
      <!-- 最小化按钮：绿色悬停 + 深绿按下效果 -->
      <IconButton variant="titlebar" @click="minimizeWindow">
        <img class="pointer-events-none" src="../../assets/images/minimize.svg" alt="Minimize">
      </IconButton>
    </div>
  </div>
</template>
