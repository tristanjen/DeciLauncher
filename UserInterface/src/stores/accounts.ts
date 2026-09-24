// 账户列表相关状态
import { ref, watch } from 'vue'
// localStorage 安全读写（WebView2 存储不可用时降级内存态）
import { safeGet, safeSet } from './storage'

export interface AccountEntry {
  username: string
  uuid: string
  type: string
  skinModel: string
}

// 已创建的离线账户列表
export const accounts = ref<AccountEntry[]>([])

// 账户操作进行中标记（创建/刷新）
export const accountBusy = ref(false)

// 当前选中的账户 UUID（localStorage 持久化）
export const selectedAccount = ref(safeGet('selected-account') || '')

watch(selectedAccount, (v) => safeSet('selected-account', v))
