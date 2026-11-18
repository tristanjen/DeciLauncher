<script setup lang="ts">
import { DialogContent, DialogOverlay, DialogPortal, DialogRoot, DialogTitle, VisuallyHidden } from 'reka-ui'
import { t } from '../../stores/locale'

withDefaults(defineProps<{
  title?: string
  centered?: boolean
}>(), {
  title: '',
  centered: false
})

// v-model：外部控制显隐（Reka DialogRoot 的 open 状态）
const model = defineModel<boolean>({ default: false })
</script>

<template>
  <!-- 挂载到 #main-card：保持圆角卡片裁剪，覆盖整个窗口内容区；
       defer 等 App.vue 的卡片节点进入 document 后再解析目标 -->
  <DialogRoot v-model:open="model">
    <DialogPortal to="#main-card" defer>
      <DialogOverlay
        class="absolute inset-0 z-50 bg-black/30 data-[state=open]:animate-deci-overlay-in" />
      <DialogContent
        class="absolute left-1/2 top-1/2 z-50 -translate-x-1/2 -translate-y-1/2
               rounded-lg bg-white/95 p-6 shadow-lg border border-deci-border flex flex-col gap-2
               data-[state=open]:animate-deci-content-in data-[state=closed]:animate-deci-content-out"
        :class="centered && 'items-center'"
        @pointer-down-outside.prevent>
        <DialogTitle v-if="title" class="text-base font-medium">{{ title }}</DialogTitle>
        <VisuallyHidden v-else>
          <DialogTitle>{{ t('common.dialog') }}</DialogTitle>
        </VisuallyHidden>
        <slot />
        <div v-if="$slots.footer" class="flex gap-2"
          :class="centered ? 'justify-center' : 'justify-end'">
          <slot name="footer" />
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
