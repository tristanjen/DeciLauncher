// Vitest 测试配置：声明与 vite.config 相同的 Vue SFC 插件链，
// 叠加 happy-dom DOM 环境供组件测试使用。
// （不直接复用 vite.config，以避免配置加载器的扩展名解析限制；
//   Tailwind 插件只在构建样式时需要，测试不涉及 CSS 产物）

import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [vue()],
  test: {
    // happy-dom：实现 localStorage / navigator / document，满足 store 与组件测试
    environment: 'happy-dom',
    include: ['src/**/*.{test,spec}.ts'],
  },
})
