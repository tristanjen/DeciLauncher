// 游戏启动状态
import { ref } from 'vue'

// 启动进行中标记（true 时前端显示进度与取消按钮）
export const launching = ref(false)

// 游戏正在运行标记（true 时按钮切换为"关闭游戏"）
export const gameRunning = ref(false)

// 启动阶段（后端 launch-progress 消息：parse/java/run/waiting；非启动期间为 null）
export const launchStage = ref<string | null>(null)
