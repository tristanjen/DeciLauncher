// Vitest 测试配置：声明与 vite.config 相同的插件链（vue SFC / UnoCSS），
// 叠加 happy-dom DOM 环境供组件测试使用。
// （不直接复用 vite.config，以避免配置加载器的扩展名解析限制）

import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [vue(), UnoCSS()],
  test: {
    // happy-dom：实现 localStorage / navigator / document，满足 store 与组件测试
    environment: 'happy-dom',
    include: ['src/**/*.{test,spec}.ts'],
  },
})