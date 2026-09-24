// Java 运行时扫描相关状态
import { ref, watch } from 'vue'
// localStorage 安全读写（WebView2 存储不可用时降级内存态，避免模块加载失败白屏）
import { safeGet, safeSet } from './storage'

export interface JavaEntry {
  path: string
  version: string
}

export const javaList = ref<JavaEntry[]>([])
// 选中的 Java 路径（'__auto__' = 自动选择），localStorage 持久化
export const selectedJava = ref(safeGet('selected-java') || '')
export const scanning = ref(false)
export const hasScanned = ref(false)

watch(selectedJava, (v) => safeSet('selected-java', v))
