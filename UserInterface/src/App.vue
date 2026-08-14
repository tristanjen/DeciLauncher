<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { sendNative, onNativeMessage } from './native'
import { javaList, selectedJava, scanning, hasScanned, games, scanningGames, gamePath, accounts, notification, accountBusy, selectedGame, selectedAccount, launching, gameRunning, launchStage, downloadSource } from './stores/store'
// 国际化：当前语言（后端错误消息随语言同步）
import { locale } from './stores/locale'
import TitleBar from './components/Controls/TitleBar.vue';
import Notification from './components/Controls/Notification.vue';
import Toast from './components/Controls/Toast.vue';

// 语言变化时同步给 C# 后端（错误/提示消息语言即时一致）
watch(locale, (v) => sendNative('set-language', { language: v }))

// 下载源偏好变化时同步给 C# 后端（控制 DownloadManager.IsEnableMirror 镜像开关）
watch(downloadSource, (v) => sendNative('set-download-source', { source: v }))

onMounted(async () => {
  onNativeMessage('java-list', (payload) => {
    javaList.value = payload.javas ?? []
    scanning.value = false
    hasScanned.value = true
    // 仅在当前选择失效时回退：持久化的路径仍存在则保留，否则回退自动选择/清空
    if (javaList.value.length === 0) {
      selectedJava.value = ''
    } else if (!javaList.value.some(j => j.path === selectedJava.value)) {
      selectedJava.value = '__auto__'
    }
  })

  onNativeMessage('java-error', () => {
    scanning.value = false
    hasScanned.value = true
  })

  onNativeMessage('game-list', (payload) => {
    games.value = payload.games ?? []
    scanningGames.value = false
    if (payload.path) gamePath.value = payload.path
    // 自动选择：优先恢复 localStorage 中的选中项，不存在则选第一个
    if (!games.value.some(g => g.id === selectedGame.value))
      selectedGame.value = games.value[0]?.id || ''
  })

  onNativeMessage('account-list', (payload) => {
    accounts.value = payload.accounts ?? []
    accountBusy.value = false
    // 自动选择：优先恢复 localStorage 中的选中项，不存在则选第一个
    if (!accounts.value.some(a => a.uuid === selectedAccount.value))
      selectedAccount.value = accounts.value[0]?.uuid || ''
  })

  onNativeMessage('account-error', (payload) => {
    notification.value = payload.message ?? ''
    accountBusy.value = false
  })

  onNativeMessage('game-error', (payload) => {
    notification.value = payload.message ?? ''
    launching.value = false
    launchStage.value = null
  })

  onNativeMessage('launch-progress', (payload) => {
    launchStage.value = payload.stage
  })

  onNativeMessage('launch-warning', (payload) => {
    // 非阻断警告（如 Java 版本过低）：仅提示，不影响启动流程
    notification.value = payload.message ?? ''
  })

  onNativeMessage('crash-analysis', (payload) => {
    notification.value = payload.message ?? ''
  })

  onNativeMessage('game-launched', () => {
    launching.value = false
    launchStage.value = null
    gameRunning.value = true
  })

  onNativeMessage('game-exited', () => {
    launching.value = false
    launchStage.value = null
    gameRunning.value = false
  })

  scanning.value = true
  await new Promise(r => requestAnimationFrame(r))
  // 先把当前语言同步给后端，再发起扫描（保证后端错误消息语言一致）
  sendNative('set-language', { language: locale.value })
  // 同步下载源偏好（镜像/官方开关）
  sendNative('set-download-source', { source: downloadSource.value })
  sendNative('scan-java')
  sendNative('scan-games', { path: gamePath.value })
  sendNative('list-accounts')
})
</script>

<template>
  <div class="w-218.5 h-131 flex justify-center items-center">
    <div
      class="flex flex-col w-212.5 h-125 bg-linear-to-br from-[#F4FFB8] to-[#D9F7BE] rounded-lg shadow-[0_0_10px_#0000003F] overflow-hidden relative"
      id="main-card">
      <TitleBar />
      <Transition name="fade" mode="out-in">
        <RouterView :key="$route.matched[0]?.name" class="grow p-3 rounded-b-lg overflow-y-auto min-h-0 scroll-smooth" />
      </Transition>
      <Notification />
      <Toast />
      <!-- <NavigationBar /> -->
    </div>
  </div>
</template>

<style scoped>
.fade-enter-active {
  transition: opacity 0.15s ease-out, transform 0.2s cubic-bezier(0.42, 1.5, 0.58, 1);
}


.fade-leave-active {
  transition: opacity 0.1s ease-out, transform 0.1s cubic-bezier(0.42, 1.5, 0.58, 1);
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: scale(0.95);
}
</style>
