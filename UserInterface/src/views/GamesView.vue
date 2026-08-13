<script setup lang="ts">
// Vue 计算属性（原版 / 模组分组）
import { computed, onMounted, onUnmounted } from 'vue'
// 前端 ↔ C# 后端消息桥（发送扫描命令 + 监听文件夹选择结果）
import { sendNative, onNativeMessage } from '../native'
// 全局共享状态（游戏列表，App.vue 启动时已扫描）
import { games, scanningGames, gamePath, selectedGame } from '../stores/store'
// 自定义控件
import DefaultButton from '../components/Controls/DefaultButton.vue'
import RadioItem from '../components/Controls/RadioItem.vue'
import Card from '../components/Controls/Card.vue'

// 原版游戏（isVanilla = true）
const vanillaGames = computed(() => games.value.filter(g => g.isVanilla))
// 模组游戏（isVanilla = false）
const moddedGames = computed(() => games.value.filter(g => !g.isVanilla))

let unsub: (() => void) | undefined

onMounted(() => {
  unsub = onNativeMessage('game-path-selected', (payload) => {
    const path = payload.path as string
    if (path) {
      gamePath.value = path
      localStorage.setItem('game-path-pref', path)
      scanGames()
    }
  })
})

onUnmounted(() => { unsub?.() })

/**
 * 向 C# 后端发起游戏版本扫描。
 * 保留当前选中项：刷新后若该版本仍存在则继续选中（App.vue 的 game-list 处理器负责回退）
 */
function scanGames() {
  scanningGames.value = true
  games.value = []
  sendNative('scan-games', { path: gamePath.value })
}

/**
 * 打开系统文件夹选择器
 */
function pickGamePath() {
  sendNative('pick-game-path')
}

/**
 * 选中游戏版本（不可取消，必须有一个条目被选中）
 */
function toggleGame(id: string) {
  selectedGame.value = id
}
</script>

<template>
  <div class="grow flex flex-col gap-3 overflow-y-auto min-h-0 scroll-smooth">
    <!-- 标题行：游戏来源 + 浏览/刷新按钮 -->
    <div class="flex items-center justify-between">
      <span class="text-xs text-gray-500">游戏目录：{{ gamePath }}</span>
      <div class="flex gap-2">
        <DefaultButton @click="pickGamePath">浏览</DefaultButton>
        <DefaultButton
          :loading="scanningGames"
          loading-text="刷新中..."
          :disabled="scanningGames"
          @click="scanGames"
        >
          刷新
        </DefaultButton>
      </div>
    </div>
    <!-- 游戏内容区：下拉弹入动画 -->
    <Transition name="content-drop" appear>
      <div v-if="!scanningGames" key="games" class="grow flex flex-col gap-3">
        <!-- 原版分区卡片 -->
        <Card v-if="vanillaGames.length > 0" class="flex flex-col gap-2">
          <span class="text-xs text-[#333] font-medium">原版</span>
          <div v-for="g in vanillaGames" :key="g.id"
            class="flex items-center cursor-pointer"
            @click="toggleGame(g.id)">
            <RadioItem :selected="selectedGame === g.id" />
            <div class="flex flex-col">
              <span class="text-sm">{{ g.id }}</span>
              <span class="text-xs text-gray-500">Minecraft {{ g.mcVersion }}</span>
            </div>
          </div>
        </Card>
        <!-- 模组分区分区卡片 -->
        <Card v-if="moddedGames.length > 0" class="flex flex-col gap-2">
          <span class="text-xs text-[#333] font-medium">可安装模组</span>
          <div v-for="g in moddedGames" :key="g.id"
            class="flex items-center cursor-pointer"
            @click="toggleGame(g.id)">
            <RadioItem :selected="selectedGame === g.id" />
            <div class="flex flex-col">
              <span class="text-sm">{{ g.id }}</span>
              <span class="text-xs text-gray-500">Minecraft {{ g.mcVersion }} / {{ g.loader }}</span>
            </div>
          </div>
        </Card>
        <!-- 空列表提示 -->
        <p v-if="games.length === 0" class="grow flex items-center justify-center text-2xl font-medium">
          未找到已安装的游戏
        </p>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.content-drop-enter-active {
  animation: content-drop 0.3s cubic-bezier(0.42, 1.5, 0.58, 1);
}

@keyframes content-drop {
  from {
    opacity: 0;
    transform: translateY(-64px);
  }

  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
