// 游戏清单 + 内存设置相关状态
import { ref, watch } from 'vue'

export interface GameEntry {
  id: string
  isVanilla: boolean
  mcVersion: string
  loader: string
}

// 已安装的游戏版本列表
export const games = ref<GameEntry[]>([])
// 游戏扫描进行中标记
export const scanningGames = ref(true)
// 游戏来源路径（由后端传入，初始化从 localStorage 恢复）
const DEFAULT_GAME_PATH = ''

export const gamePath = ref(
  localStorage.getItem('game-path-pref') || DEFAULT_GAME_PATH
)

// 游戏内存上限（MB），从 localStorage 恢复并 clamp 到滑块范围 [512, 8192]，默认 2048
const MIN_MEMORY = 512
const MAX_MEMORY = 8192
const DEFAULT_MEMORY = 2048

export const maxMemory = ref(
  clampMemory(parseInt(localStorage.getItem('max-memory') || '') || DEFAULT_MEMORY)
)

function clampMemory(v: number): number {
  return Math.min(MAX_MEMORY, Math.max(MIN_MEMORY, v))
}

// 当前选中的游戏版本 ID（localStorage 持久化）
export const selectedGame = ref(localStorage.getItem('selected-game') || '')

// 选中项变化时自动保存
watch(selectedGame, (v) => localStorage.setItem('selected-game', v))
