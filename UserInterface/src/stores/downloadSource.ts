// 下载源偏好（todolist #1 的用户可选开关）
// mirror           = 尽量使用镜像源（BMCLAPI CDN）
// official-first   = 优先使用官方源，在加载缓慢时换用镜像源（回退逻辑待下载功能落地实现，现阶段等同官方源）
// official         = 尽量使用官方源
import { ref, watch } from 'vue'
// localStorage 安全读写（WebView2 存储不可用时降级内存态）
import { safeGet, safeSet } from './storage'

export type DownloadSource = 'mirror' | 'official-first' | 'official'

const DEFAULT_SOURCE: DownloadSource = 'official-first'

function loadSource(): DownloadSource {
  const stored = safeGet('download-source')
  return stored === 'mirror' || stored === 'official-first' || stored === 'official'
    ? stored
    : DEFAULT_SOURCE
}

// 当前下载源偏好（localStorage 持久化）
export const downloadSource = ref<DownloadSource>(loadSource())

watch(downloadSource, (v) => safeSet('download-source', v))
