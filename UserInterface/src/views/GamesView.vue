<script setup lang="ts">
// Vue 计算属性（原版 / 模组分组）
import { computed, onMounted, onUnmounted } from 'vue'
// 前端 ↔ C# 后端消息桥（发送扫描命令 + 监听文件夹选择结果）
import { sendNative, onNativeMessage } from '../native'
// 全局共享状态（游戏列表，App.vue 启动时已扫描）
import { games, scanningGames, gamePath, selectedGame } from '../stores/store'
// 启动状态（锁定切换）与提示
import { launching, gameRunning, toast } from '../stores/store'
// 自定义控件
import DefaultButton from '../components/Controls/DefaultButton.vue'
import RadioItem from '../components/Controls/RadioItem.vue'
import Card from '../components/Controls/Card.vue'
// 国际化翻译
import { t } from '../stores/locale'
// 内置加载器 logo（官方品牌图标，许可见 loaders/README.md）
import fabricLogo from '../assets/images/loaders/fabric.png'
import quiltLogo from '../assets/images/loaders/quilt.svg'
import forgeIcon from '../assets/images/loaders/forge.webp'
import neoforgeLogo from '../assets/images/loaders/neoforge.png'
// 内置方块图标（成品像素风，HMCL/PCL 同款做法）
import grassIcon from '../assets/images/blocks/grass.png'
import commandIcon from '../assets/images/blocks/command.gif'
// 图标回退（未知图标类型时的应用图标）
import fallbackIcon from '../assets/images/favicon.svg'

// loaderIcon → 内置图标资源映射（键需与后端 InstanceIconResolver 返回的标识一致）
const loaderLogos: Record<string, string> = {
  // 加载器
  Fabric: fabricLogo,
  Quilt: quiltLogo,
  Forge: forgeIcon,
  NeoForge: neoforgeLogo,
  // 方块
  grass: grassIcon,
  command: commandIcon,
}

/**
 * 根据内置图标标识取图片；未知标识回退应用图标。
 * 方块图标为像素风（image-rendering: pixelated），加载器 logo 平滑缩放。
 */
function gameIcon(loaderIcon: string) {
  const src = (loaderIcon && loaderLogos[loaderIcon]) || fallbackIcon
  // grass/command/anvil 方块图标像素风渲染，其余（加载器 logo / 回退）平滑缩放
  const pixelated = loaderIcon === 'grass' || loaderIcon === 'command' || loaderIcon === 'anvil'
  return { src, pixelated }
}

// 原版游戏（isVanilla = true）
const vanillaGames = computed(() => games.value.filter(g => g.isVanilla))
// 模组游戏（isVanilla = false）
const moddedGames = computed(() => games.value.filter(g => !g.isVanilla))

// 游戏启动或运行期间锁定版本切换（启动中的目标版本不可中途变更）
const switchLocked = computed(() => launching.value || gameRunning.value)

let unsub: (() => void) | undefined

onMounted(() => {
  unsub = onNativeMessage('game-path-selected', (payload) => {
    const path = payload.path
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
 * 选中游戏版本（不可取消，必须有一个条目被选中）。
 * 游戏启动或运行期间锁定：拒绝切换并提示原因
 */
function toggleGame(id: string) {
  if (switchLocked.value) {
    toast.value = t('games.switchLocked')
    return
  }
  selectedGame.value = id
}
</script>

<template>
  <div class="grow flex flex-col gap-3 overflow-y-auto min-h-0 scroll-smooth">
    <!-- 标题行：游戏来源 + 浏览/刷新按钮 -->
    <div class="flex items-center justify-between">
      <span class="text-xs text-gray-500">{{ t('games.directory', { path: gamePath }) }}</span>
      <div class="flex gap-2">
        <DefaultButton @click="pickGamePath">{{ t('games.browse') }}</DefaultButton>
        <DefaultButton
          :loading="scanningGames"
          :loading-text="t('common.refreshing')"
          :disabled="scanningGames"
          @click="scanGames"
        >
          {{ t('common.refresh') }}
        </DefaultButton>
      </div>
    </div>
    <!-- 游戏内容区：下拉弹入动画 -->
    <Transition name="content-drop" appear>
      <div v-if="!scanningGames" key="games" class="grow flex flex-col gap-3">
        <!-- 原版分区卡片 -->
        <Card v-if="vanillaGames.length > 0" class="flex flex-col gap-2">
          <span class="text-xs text-[#333] font-medium">{{ t('games.vanilla') }}</span>
          <div v-for="g in vanillaGames" :key="g.id"
            class="flex items-center gap-2 cursor-pointer"
            :class="switchLocked ? 'opacity-40 cursor-not-allowed' : ''"
            @click="toggleGame(g.id)">
            <RadioItem :selected="selectedGame === g.id" />
            <img
              class="w-8 h-8 shrink-0 rounded-sm"
              :class="gameIcon(g.loaderIcon).pixelated ? 'image-pixelated' : ''"
              :src="gameIcon(g.loaderIcon).src"
              alt="" />
            <div class="flex flex-col">
              <span class="text-sm">{{ g.id }}</span>
              <span class="text-xs text-gray-500">{{ t('games.minecraft', { version: g.mcVersion }) }}</span>
            </div>
          </div>
        </Card>
        <!-- 模组分区分区卡片 -->
        <Card v-if="moddedGames.length > 0" class="flex flex-col gap-2">
          <span class="text-xs text-[#333] font-medium">{{ t('games.modded') }}</span>
          <div v-for="g in moddedGames" :key="g.id"
            class="flex items-center gap-2 cursor-pointer"
            :class="switchLocked ? 'opacity-40 cursor-not-allowed' : ''"
            @click="toggleGame(g.id)">
            <RadioItem :selected="selectedGame === g.id" />
            <img
              class="w-8 h-8 shrink-0 rounded-sm"
              :class="gameIcon(g.loaderIcon).pixelated ? 'image-pixelated' : ''"
              :src="gameIcon(g.loaderIcon).src"
              alt="" />
            <div class="flex flex-col">
              <span class="text-sm">{{ g.id }}</span>
              <span class="text-xs text-gray-500">{{ t('games.minecraft', { version: g.mcVersion }) }} / {{ g.loader }}</span>
            </div>
          </div>
        </Card>
        <!-- 空列表提示 -->
        <p v-if="games.length === 0" class="grow flex items-center justify-center text-2xl font-medium">
          {{ t('games.noneFound') }}
        </p>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.image-pixelated {
  image-rendering: pixelated;
}

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
