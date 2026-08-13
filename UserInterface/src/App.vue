<script setup lang="ts">
import { onMounted } from 'vue'
import { sendNative, onNativeMessage } from './native'
import { javaList, selectedJava, scanning, hasScanned, games, scanningGames, gamePath, accounts, notification, accountBusy, selectedGame, selectedAccount, launching, gameRunning } from './stores/store'
import TitleBar from './components/Controls/TitleBar.vue';
import Notification from './components/Controls/Notification.vue';
import Toast from './components/Controls/Toast.vue';

onMounted(async () => {
  onNativeMessage('java-list', (payload) => {
    javaList.value = (payload.javas as { path: string; version: string }[]) ?? []
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
    games.value = (payload.games as { id: string; isVanilla: boolean; mcVersion: string; loader: string }[]) ?? []
    scanningGames.value = false
    if (payload.path) gamePath.value = payload.path as string
    // 自动选择：优先恢复 localStorage 中的选中项，不存在则选第一个
    if (!games.value.some(g => g.id === selectedGame.value))
      selectedGame.value = games.value[0]?.id || ''
  })

  onNativeMessage('account-list', (payload) => {
    accounts.value = (payload.accounts as { username: string; uuid: string; type: string; skinModel: string }[]) ?? []
    accountBusy.value = false
    // 自动选择：优先恢复 localStorage 中的选中项，不存在则选第一个
    if (!accounts.value.some(a => a.uuid === selectedAccount.value))
      selectedAccount.value = accounts.value[0]?.uuid || ''
  })

  onNativeMessage('account-error', (payload) => {
    notification.value = payload.message as string ?? ''
    accountBusy.value = false
  })

  onNativeMessage('game-error', (payload) => {
    notification.value = payload.message as string ?? ''
    launching.value = false
  })

  onNativeMessage('game-launched', () => {
    launching.value = false
    gameRunning.value = true
  })

  onNativeMessage('game-exited', () => {
    launching.value = false
    gameRunning.value = false
  })

  scanning.value = true
  await new Promise(r => requestAnimationFrame(r))
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
