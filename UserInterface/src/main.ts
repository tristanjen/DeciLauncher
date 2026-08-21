// 禁用 WebView2 浏览器快捷键（Ctrl+R 刷新、F5、F12 等）
document.addEventListener('keydown', (e: KeyboardEvent) => {
  // F5 / Ctrl+R / Ctrl+Shift+R — 页面刷新
  if (e.key === 'F5' || (e.ctrlKey && e.key === 'r')) {
    e.preventDefault()
    return
  }
  // F12 — 开发者工具
  if (e.key === 'F12') {
    e.preventDefault()
    return
  }
  // Ctrl+U — 查看源代码
  if (e.ctrlKey && e.key === 'u') {
    e.preventDefault()
    return
  }
  // Ctrl+P — 打印
  if (e.ctrlKey && e.key === 'p') {
    e.preventDefault()
    return
  }
  // Ctrl+S — 保存页面
  if (e.ctrlKey && e.key === 's') {
    e.preventDefault()
    return
  }
  // Ctrl+F / Ctrl+G — 页面内搜索
  if (e.ctrlKey && (e.key === 'f' || e.key === 'g')) {
    e.preventDefault()
    return
  }
})

// 禁用 Ctrl+滚轮 缩放（浏览器默认行为）
document.addEventListener('wheel', (e: WheelEvent) => {
  if (e.ctrlKey || e.metaKey) {
    e.preventDefault()
  }
}, { passive: false })

// 禁用鼠标右键菜单
document.addEventListener('contextmenu', (e: MouseEvent) => {
  e.preventDefault()
})

// Vue 应用入口 — 初始化并挂载到 DOM
// 导入 UnoCSS 按需生成的样式（含 preflight，须先于自定义样式）
import 'virtual:uno.css'
// 导入全局自定义样式（body/滚动条等）
import './assets/main.css'

// 从 Vue 库导入应用创建函数
import { createApp } from 'vue'

// 导入根组件
import App from './App.vue'
// 导入路由配置
import router from './router'

// 创建 Vue 应用实例
const app = createApp(App)

// 注册 Vue Router 插件
app.use(router)

// 将应用挂载到 index.html 中的 #app 元素
app.mount('#app')
