// Java 运行时扫描相关状态
import { ref, watch } from 'vue'

export interface JavaEntry {
  path: string
  version: string
}

export const javaList = ref<JavaEntry[]>([])
// 选中的 Java 路径（'__auto__' = 自动选择），localStorage 持久化
export const selectedJava = ref(localStorage.getItem('selected-java') || '')
export const scanning = ref(false)
export const hasScanned = ref(false)

watch(selectedJava, (v) => localStorage.setItem('selected-java', v))
