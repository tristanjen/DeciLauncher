<script setup lang="ts">
// Vue 计算属性
import { computed } from 'vue'
// 前端 ↔ C# 后端消息桥
import { sendNative } from '../native'
// 全局共享状态
import { selectedGame, games } from '../stores/games'
import { selectedAccount } from '../stores/accounts'
import { selectedJava } from '../stores/java'
import { maxMemory, gamePath } from '../stores/games'
import { launching, gameRunning, launchStage } from '../stores/launch'
// 自定义控件
import PrimaryButton from '../components/Controls/PrimaryButton.vue'
import DefaultButton from '../components/Controls/DefaultButton.vue'
// 国际化翻译
import { t } from '../stores/locale'

// 当前选中的游戏版本信息
const currentGame = computed(() => games.value.find(g => g.id === selectedGame.value))

/**
 * 向 C# 后端发起游戏启动
 */
function launch() {
  if (!selectedGame.value || !selectedAccount.value) return
  launching.value = true
  sendNative('launch-game', {
    gameId: selectedGame.value,
    accountUuid: selectedAccount.value,
    javaPath: selectedJava.value,
    maxMemory: maxMemory.value,
    minecraftPath: gamePath.value
  })
}

/**
 * 关闭正在运行的游戏
 */
function closeGame() {
  sendNative('close-game')
}

/**
 * 取消正在进行的游戏启动
 */
function cancelLaunch() {
  sendNative('cancel-launch')
}

/**
 * 按钮文字：启动中时按阶段显示进度文案（launch-progress 消息驱动）
 */
const buttonText = computed(() => {
  if (launching.value) {
    switch (launchStage.value) {
      case 'parse': return t('home.stage.parse')
      case 'java': return t('home.stage.java')
      case 'run': return t('home.stage.run')
      case 'waiting': return t('home.stage.waiting')
      default: return t('home.launching')
    }
  }
  if (gameRunning.value) return t('home.closeInstance')
  return t('home.launchInstance')
})
</script>

<template>
  <div class="grow flex flex-col">
    <!-- 中间内容区（留空） -->
    <div class="grow" />
    <!-- 底部栏：左下选中游戏 / 右下启动按钮 -->
    <div class="flex items-end justify-between">
      <!-- 左下：当前选中的游戏版本 -->
      <span class="text-3xl font-medium">
        {{ currentGame ? currentGame.id : t('home.noInstanceSelected') }}
      </span>
      <!-- 右下：取消/启动/关闭游戏按钮 -->
      <div class="flex items-center gap-2 ml-auto">
        <Transition name="cancel-btn">
          <DefaultButton v-if="launching" size="lg" @click="cancelLaunch">{{ t('common.cancel') }}</DefaultButton>
        </Transition>
        <PrimaryButton
          size="lg"
          :variant="gameRunning ? 'danger' : 'primary'"
          :disabled="launching || (!gameRunning && (!selectedGame || !selectedAccount))"
          @click="gameRunning ? closeGame() : launch()"
        >
          {{ buttonText }}
        </PrimaryButton>
      </div>
    </div>
  </div>
</template>

<style scoped>
.cancel-btn-leave-active,
.cancel-btn-enter-active {
  transition: transform 0.15s ease-out, opacity 0.15s ease-out;
}

.cancel-btn-enter-from,
.cancel-btn-leave-to {
  transform: scale(0);
  opacity: 0;
}
</style>
