<script setup lang="ts">
// Vue 响应式
import { ref, watch } from 'vue'
// Reka UI 单选组：为账户列表提供键盘导航与 ARIA 语义
import { RadioGroupItem, RadioGroupRoot, type AcceptableValue } from 'reka-ui'
// Lucide 图标
import { Copy, Globe, LogIn, Plus, RefreshCw, Trash2 } from '@lucide/vue'
// 前端 ↔ C# 后端消息桥
import { sendNative } from '../native'
// 全局共享状态（账户列表）
import { accounts, accountBusy, toast, selectedAccount } from '../stores/store'
// 国际化翻译
import { t } from '../stores/locale'
// 自定义控件
import PrimaryButton from '../components/Controls/PrimaryButton.vue'
import DefaultButton from '../components/Controls/DefaultButton.vue'
import Modal from '../components/Controls/Modal.vue'
import RadioItem from '../components/Controls/RadioItem.vue'
import TextInput from '../components/Controls/TextInput.vue'
import IconButton from '../components/Controls/IconButton.vue'
// 账户头像（Steve，对所有账户类型统一展示）
import steveAvatar from '../assets/images/steve.png'

// 新账户名称输入
const newName = ref('')
// 创建离线账户弹窗显隐
const showCreateModal = ref(false)

// 弹窗通过 Esc 关闭时同样清空输入（取消/创建按钮已有各自清理）
watch(showCreateModal, (open) => {
  if (!open) newName.value = ''
})

/**
 * 向 C# 后端发起离线账户创建
 */
function createAccount() {
  const name = newName.value.trim()
  if (!name) return
  accountBusy.value = true
  sendNative('create-offline-account', { name })
  newName.value = ''
  showCreateModal.value = false
}

/**
 * 关闭创建弹窗并清空输入
 */
function closeCreateModal() {
  showCreateModal.value = false
  newName.value = ''
}

/**
 * 删除指定 UUID 的账户
 */
function deleteAccount(uuid: string) {
  if (selectedAccount.value === uuid) selectedAccount.value = ''
  sendNative('delete-offline-account', { uuid })
}

/**
 * 切换账户选中状态（RadioGroup 受控更新）
 */
function onAccountSelect(value: AcceptableValue) {
  selectedAccount.value = typeof value === 'string' ? value : String(value ?? '')
}

/**
 * 复制账户 UUID 到剪贴板
 */
async function copyUuid(uuid: string) {
  try {
    await navigator.clipboard.writeText(uuid)
    toast.value = t('accounts.copied')
  } catch { /* 忽略剪贴板错误 */ }
}

/**
 * 刷新账户列表
 */
function refreshAccounts() {
  accountBusy.value = true
  sendNative('list-accounts')
}

/**
 * 账户类型显示名称
 */
function typeLabel(type: string): string {
  switch (type) {
    case 'offline': return t('accounts.typeOffline')
    case 'microsoft': return t('accounts.typeMicrosoft')
    case 'yggdrasil': return t('accounts.typeYggdrasil')
    default: return type
  }
}

/**
 * 账户类型标签颜色
 */
function typeColor(type: string): string {
  switch (type) {
    case 'offline': return 'text-gray-500'
    case 'microsoft': return 'text-deci-primary'
    case 'yggdrasil': return 'text-blue-500'
    default: return 'text-gray-700'
  }
}
</script>

<template>
  <div class="grow flex flex-col gap-3 relative">
    <!-- 操作栏：三个账户按钮 + 刷新按钮（英文文案较长时允许换行） -->
    <div class="flex flex-wrap gap-2 items-center">
      <DefaultButton @click="showCreateModal = true">
        <Plus class="size-4" aria-hidden="true" />
        {{ t('accounts.createOffline') }}
      </DefaultButton>
      <DefaultButton disabled>
        <LogIn class="size-4" aria-hidden="true" />
        {{ t('accounts.loginMicrosoft') }}
      </DefaultButton>
      <DefaultButton disabled>
        <Globe class="size-4" aria-hidden="true" />
        {{ t('accounts.loginThirdParty') }}
      </DefaultButton>
      <DefaultButton class="ml-auto" :loading="accountBusy" :loading-text="t('common.refreshing')"
        :disabled="accountBusy" @click="refreshAccounts">
        <RefreshCw class="size-4" :class="accountBusy && 'animate-spin'" aria-hidden="true" />
        {{ t('common.refresh') }}
      </DefaultButton>
    </div>
    <!-- 账户内容区：下拉弹入动画 -->
    <Transition appear
      enter-active-class="animate__animated animate__fadeInDown"
      leave-active-class="animate__animated animate__fadeOutUp">
      <div v-if="!accountBusy" key="accounts" class="grow flex flex-col gap-2">
        <!-- 账户列表：Reka RadioGroup（as="div" 允许行内嵌套复制/删除按钮） -->
        <RadioGroupRoot v-if="accounts.length > 0" :model-value="selectedAccount" orientation="vertical"
          class="flex grow flex-col gap-2" @update:model-value="onAccountSelect">
          <RadioGroupItem v-for="a in accounts" :key="a.uuid" :value="a.uuid" as="div"
            class="group flex w-full cursor-pointer items-center gap-2 rounded-lg border border-transparent
                   bg-white/25 px-3 py-2 text-left shadow-deci transition ease-out duration-150
                   hover:border-deci-primary/25
                   focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/40">
            <!-- 单选圆 -->
            <RadioItem :selected="selectedAccount === a.uuid" />
            <!-- Steve 头像 -->
            <img class="w-8 h-8 shrink-0 image-pixelated shadow rounded-sm" :src="steveAvatar" alt="" />
            <div class="flex flex-col">
              <!-- 用户名 -->
              <span class="text-sm">{{ a.username }}</span>
              <!-- 账户类型标签 -->
              <span class="text-xs" :class="typeColor(a.type)">{{ typeLabel(a.type) }}</span>
            </div>
            <!-- 复制 UUID 按钮 -->
            <IconButton class="ml-auto" variant="list" :label="t('accounts.copyUuid')"
              @click="copyUuid(a.uuid)" @keydown.stop>
              <Copy :size="12" :stroke-width="1.5" aria-hidden="true" />
            </IconButton>
            <!-- 删除按钮（推到最右） -->
            <IconButton variant="list" color="red" :label="t('accounts.delete')"
              @click="deleteAccount(a.uuid)" @keydown.stop>
              <Trash2 :size="12" :stroke-width="1.5" aria-hidden="true" />
            </IconButton>
          </RadioGroupItem>
        </RadioGroupRoot>
        <!-- 空列表提示 -->
        <p v-else class="grow flex items-center justify-center text-2xl font-medium">
          {{ t('accounts.noneFound') }}
        </p>
      </div>
    </Transition>
    <!-- 创建离线账户弹窗 -->
    <Modal v-model="showCreateModal" :title="t('accounts.createOffline')">
      <label class="flex flex-col gap-1">
        <span class="text-xs text-gray-700">{{ t('accounts.playerName') }}</span>
        <TextInput v-model="newName" :maxlength="16" @submit="createAccount" />
      </label>
      <template #footer>
        <DefaultButton @click="closeCreateModal">{{ t('common.cancel') }}</DefaultButton>
        <PrimaryButton :disabled="accountBusy" @click="createAccount">{{ t('accounts.create') }}</PrimaryButton>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.image-pixelated {
  image-rendering: pixelated;
}
</style>
