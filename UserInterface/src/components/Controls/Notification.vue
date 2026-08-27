<script setup lang="ts">
import { computed } from 'vue'
// 国际化翻译
import { t } from '../../stores/locale'
import { notification } from '../../stores/store'
import Modal from './Modal.vue'
import DefaultButton from './DefaultButton.vue'

// Modal 需要布尔值，通知内容为字符串
const show = computed({
  get: () => !!notification.value,
  set: (v: boolean) => { if (!v) notification.value = null }
})

function dismiss() {
  notification.value = null
}
</script>

<template>
  <Modal v-model="show" centered>
    <span class="text-sm">{{ notification }}</span>
    <template #footer>
      <DefaultButton class="!px-4 !bg-white/80" @click="dismiss">{{ t('common.ok') }}</DefaultButton>
    </template>
  </Modal>
</template>
