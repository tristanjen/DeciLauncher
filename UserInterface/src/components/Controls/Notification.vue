<script setup lang="ts">
import { computed } from 'vue'
import {
  AlertDialogAction,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogOverlay,
  AlertDialogPortal,
  AlertDialogRoot,
  AlertDialogTitle,
  VisuallyHidden
} from 'reka-ui'
// 国际化翻译
import { t } from '../../stores/locale'
import { notification } from '../../stores/store'

// Modal 需要布尔值，通知内容为字符串
const show = computed({
  get: () => !!notification.value,
  set: (v: boolean) => { if (!v) notification.value = null }
})
</script>

<template>
  <!-- AlertDialog：必须显式确认，不允许点击遮罩或 Esc 静默关闭 -->
  <AlertDialogRoot v-model:open="show">
    <AlertDialogPortal to="#main-card" defer>
      <AlertDialogOverlay
        class="absolute inset-0 z-50 bg-black/30 data-[state=open]:animate-deci-overlay-in" />
      <AlertDialogContent
        class="absolute left-1/2 top-1/2 z-50 -translate-x-1/2 -translate-y-1/2
               rounded-lg bg-white/95 p-6 shadow-lg border border-deci-border flex flex-col items-center gap-2
               data-[state=open]:animate-deci-content-in data-[state=closed]:animate-deci-content-out">
        <VisuallyHidden>
          <AlertDialogTitle>{{ t('common.notice') }}</AlertDialogTitle>
        </VisuallyHidden>
        <AlertDialogDescription class="text-sm">{{ notification }}</AlertDialogDescription>
        <AlertDialogAction
          class="h-8 px-4 rounded-lg text-sm bg-white/80 border border-deci-border transition duration-150
                 hover:bg-deci-border active:bg-deci-hover active:scale-95 shrink-0
                 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/40">
          {{ t('common.ok') }}
        </AlertDialogAction>
      </AlertDialogContent>
    </AlertDialogPortal>
  </AlertDialogRoot>
</template>
