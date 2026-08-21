// UnoCSS 配置 — Tailwind v4 兼容预设（preset-wind4）
import { defineConfig, presetWind4 } from 'unocss'

export default defineConfig({
  presets: [
    // Tailwind v4 语法兼容预设：动态 spacing（如 w-218.5 / max-w-120）、
    // bg-linear-to-* 渐变、size-*、rounded-4xl 等，并自带 preflight
    presetWind4(),
  ],
})
