// Vite 前端构建配置

// 导入 Vite 配置函数
import { defineConfig } from 'vite'
// 导入 Vue SFC 编译插件 (.vue 单文件组件)
import vue from '@vitejs/plugin-vue'
// 导入 UnoCSS Vite 插件
import UnoCSS from 'unocss/vite'

export default defineConfig({
  // 构建插件列表
  plugins: [
    // 编译 .vue 单文件组件
    vue(),
    // 扫描模板/脚本中的 class 并按需生成 CSS
    UnoCSS(),
  ],
  // 生产构建配置
  build: {
    // 每次构建前先清空输出目录
    emptyOutDir: true,
    // 构建输出到 C# 项目的 Resources/wwwroot/（作为嵌入式资源发布）
    outDir: '../Resources/wwwroot'
  }
})
