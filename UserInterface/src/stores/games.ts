// 游戏清单 + 内存设置相关状态
import { ref, watch } from 'vue'
// localStorage 安全读写（WebView2 存储不可用时降级内存态，避免模块加载失败白屏）
import { safeGet, safeSet } from './storage'

export interface GameEntry {
  id: string
  isVanilla: boolean
  mcVersion: string
  loader: string
  /** 内置图标标识：grass / command / anvil / Fabric / Quilt / NeoForge */
  loaderIcon: string
}

// 已安装的游戏版本列表
export const games = ref<GameEntry[]>([])
// 游戏扫描进行中标记
export const scanningGames = ref(true)
// 游戏来源路径（由后端传入，初始化从 localStorage 恢复）
const DEFAULT_GAME_PATH = ''

export const gamePath = ref(
  safeGet('game-path-pref') || DEFAULT_GAME_PATH
)

// 游戏内存上限（MB），从 localStorage 恢复并 clamp 到滑块范围 [512, 8192]，默认 2048
const MIN_MEMORY = 512
const MAX_MEMORY = 8192
const DEFAULT_MEMORY = 2048

export const maxMemory = ref(
  clampMemory(parseInt(safeGet('max-memory') || '') || DEFAULT_MEMORY)
)

function clampMemory(v: number): number {
  return Math.min(MAX_MEMORY, Math.max(MIN_MEMORY, v))
}

// 当前选中的游戏版本 ID（localStorage 持久化）
export const selectedGame = ref(safeGet('selected-game') || '')

// 选中项/来源路径/内存变化时自动保存（安全写入：存储不可用时静默降级内存态）。
// 持久化统一收敛到 store 模块：game-path 此前散落在 App.vue、max-memory 散落在 GameSettingsView
watch(gamePath, (v) => safeSet('game-path-pref', v))
watch(selectedGame, (v) => safeSet('selected-game', v))
watch(maxMemory, (v) => safeSet('max-memory', String(v)))
