<script setup lang="ts">
// Vue 生命周期与响应式（版本号填充）
import { onMounted, onUnmounted, ref } from 'vue'
// 消息桥：请求应用信息、注册响应监听
import { sendNative, onNativeMessage } from '../../native'
// 国际化翻译
import { t } from '../../stores/locale'
// 应用 Logo（与打包图标一致的绿色 D 图标）
import logo from '../../assets/images/favicon.svg'
// 卡片容器（关于页内容外观）
import Card from '../../components/Controls/Card.vue'

// 版本号（后端程序集版本，响应前为空不显示）
const version = ref('')

// 开源项目致谢（项目名为专有名词，不参与翻译）
const projects = [
  '.NET', 'ASP.NET Core', 'Photino.NET', 'MinecraftLaunch', 'Vue',
  'Vite', 'UnoCSS', 'TypeScript', 'xUnit', 'pnpm'
]

// 注册 app-info 响应监听（返回卸载函数）
const offAppInfo = onNativeMessage('app-info', (payload) => {
  version.value = payload.version
})

onMounted(() => {
  // 请求应用信息（后端回发 app-info → 版本号）
  sendNative('get-app-info')
})

onUnmounted(() => {
  offAppInfo()
})
</script>

<template>
  <div class="grow flex flex-col gap-3">
    <Card class="flex flex-col items-center gap-4 h-107">
      <!-- Logo + 名称 + 版本：grow 占满剩余空间，内部居中 -->
      <div class="grow flex flex-col items-center justify-center gap-2">
        <img :src="logo" alt="Deci Launcher" class="w-32 h-32 rounded-4xl shadow-lg" />
        <span class="text-2xl font-bold">Deci Launcher</span>
        <span v-if="version" class="text-sm leading-[0.25rem] text-[#52c41a]">{{ t('about.version', { version }) }}</span>
      </div>

      <!-- Copyright：贴卡片底部居中 -->
      <div class="text-sm text-gray-600">
        <span>Copyright © 2026 Tristan Jen</span>
      </div>
    </Card>

    <Card>
      <!-- 开源项目致谢 -->
      <div class="flex flex-col items-center gap-2">
        <span class="text-sm font-medium">{{ t('about.thanksTitle') }}</span>
        <div class="flex flex-wrap justify-center gap-1 max-w-120">
          <span v-for="p in projects" :key="p" class="px-2 py-0.5 rounded-full bg-[#73D13D26] text-xs">{{ p }}</span>
        </div>
      </div>
    </Card>
  </div>
</template>
