<script setup lang="ts">
import { computed } from 'vue'
import { ToastProvider, ToastRoot, ToastTitle, ToastViewport } from 'reka-ui'
import { Info } from '@lucide/vue'
import { toast } from '../../stores/store'
import { t } from '../../stores/locale'

const show = computed({
  get: () => !!toast.value,
  set: (v: boolean) => { if (!v) toast.value = null }
})

function dismiss() {
  toast.value = null
}
</script>

<template>
  <!-- Reka Toast：自带 3 秒计时、悬停暂停、F8 快捷键与 ARIA live region -->
  <ToastProvider :duration="3000" :label="t('common.notification')">
    <ToastViewport
      :label="t('common.notifications')"
      class="fixed bottom-4 right-4 z-50 m-0 flex list-none flex-col gap-2 p-0 outline-none">
      <ToastRoot v-model:open="show" @click="dismiss"
        class="flex cursor-pointer items-center gap-2 rounded-lg bg-white/90 px-4 py-2 shadow-lg text-sm
               border border-deci-primary data-[state=open]:animate-deci-toast-in">
        <Info class="size-4 shrink-0 text-deci-primary" aria-hidden="true" />
        <ToastTitle>{{ toast }}</ToastTitle>
      </ToastRoot>
    </ToastViewport>
  </ToastProvider>
</template>
