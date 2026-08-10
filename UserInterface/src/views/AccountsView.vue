<script setup lang="ts">
// Vue 响应式
import { ref } from 'vue'
// 前端 ↔ C# 后端消息桥
import { sendNative } from '../native'
// 全局共享状态（账户列表）
import { accounts, accountBusy, toast, selectedAccount } from '../stores/store'
// 自定义控件
import PrimaryButton from '../components/Controls/PrimaryButton.vue'
import DefaultButton from '../components/Controls/DefaultButton.vue'
import Modal from '../components/Controls/Modal.vue'
import RadioItem from '../components/Controls/RadioItem.vue'
import Card from '../components/Controls/Card.vue'
import TextInput from '../components/Controls/TextInput.vue'
import IconButton from '../components/Controls/IconButton.vue'

// 新账户名称输入
const newName = ref('')
// 创建离线账户弹窗显隐
const showCreateModal = ref(false)

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
 * 切换账户选中状态（再次点击取消）
 */
function toggleAccount(uuid: string) {
  selectedAccount.value = uuid
}

/**
 * 复制账户 UUID 到剪贴板
 */
async function copyUuid(uuid: string) {
  try {
    await navigator.clipboard.writeText(uuid)
    toast.value = '已复制到剪贴板'
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
    case 'offline': return '离线账户'
    case 'microsoft': return '正版账户'
    case 'yggdrasil': return '第三方账户'
    default: return type
  }
}

/**
 * 账户类型标签颜色
 */
function typeColor(type: string): string {
  switch (type) {
    case 'offline': return 'text-gray-500'
    case 'microsoft': return 'text-[#52C41A]'
    case 'yggdrasil': return 'text-blue-500'
    default: return 'text-gray-700'
  }
}
</script>

<template>
  <div class="grow flex flex-col gap-3 relative">
    <!-- 操作栏：三个账户按钮 + 刷新按钮 -->
    <div class="flex gap-2 items-center">
      <DefaultButton @click="showCreateModal = true">
        创建离线账户
      </DefaultButton>
      <DefaultButton disabled>
        登录正版账户
      </DefaultButton>
      <DefaultButton disabled>
        登录第三方账户
      </DefaultButton>
      <DefaultButton
        class="ml-auto"
        :loading="accountBusy"
        loading-text="刷新中..."
        :disabled="accountBusy"
        @click="refreshAccounts"
      >
        刷新
      </DefaultButton>
    </div>
    <!-- 账户内容区：下拉弹入动画 -->
    <Transition name="content-drop" appear>
      <div v-if="!accountBusy" key="accounts" class="grow flex flex-col gap-2">
        <!-- 账户列表 -->
        <Card v-for="a in accounts" :key="a.uuid" padding="px-3 py-2" clickable class="flex items-center"
          @click="toggleAccount(a.uuid)">
          <!-- 单选圆 -->
          <RadioItem :selected="selectedAccount === a.uuid" />
          <!-- 用户名 -->
          <span class="text-sm">{{ a.username }}</span>
          <!-- 账户类型标签 -->
          <span class="text-xs ml-2" :class="typeColor(a.type)">{{ typeLabel(a.type) }}</span>
          <!-- 复制 UUID 按钮 -->
          <IconButton class="ml-auto" variant="list" @click="copyUuid(a.uuid)">
            <svg class="size-3" viewBox="0 0 12 12">
              <rect x="3" y="2" width="7" height="9" rx="1" stroke="currentColor" stroke-width="1.2" fill="none" />
              <path d="M2 4V11H8" stroke="currentColor" stroke-width="1.2" fill="none" />
            </svg>
          </IconButton>
          <!-- 删除按钮（推到最右） -->
          <IconButton variant="list" color="red" @click="deleteAccount(a.uuid)">
            <svg class="size-3" viewBox="0 0 12 12">
              <path d="M3 3L9 9M9 3L3 9" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
            </svg>
          </IconButton>
        </Card>
        <!-- 空列表提示 -->
        <p v-if="accounts.length === 0" class="grow flex items-center justify-center text-2xl font-medium">
          还没有账户
        </p>
      </div>
    </Transition>
    <!-- 创建离线账户弹窗 -->
    <Modal v-model="showCreateModal" title="创建离线账户">
      <label class="flex flex-col gap-1">
        <span class="text-xs text-gray-700">玩家名</span>
        <TextInput v-model="newName" :maxlength="16" @submit="createAccount" />
      </label>
      <template #footer>
        <DefaultButton @click="closeCreateModal">取消</DefaultButton>
        <PrimaryButton :disabled="accountBusy" @click="createAccount">创建</PrimaryButton>
      </template>
    </Modal>
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
