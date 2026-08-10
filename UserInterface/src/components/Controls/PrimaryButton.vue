<script setup lang="ts">
withDefaults(defineProps<{
  variant?: 'primary' | 'danger'
  size?: 'sm' | 'lg'
  disabled?: boolean
}>(), {
  variant: 'primary',
  size: 'sm',
  disabled: false
})
</script>

<template>
  <button
    class="rounded-lg text-sm transition duration-150 text-white border active:scale-95 shrink-0
           disabled:opacity-50 disabled:cursor-not-allowed"
    :class="[
      size === 'sm' ? 'h-8 px-3' : 'h-12 px-8',
      variant === 'danger'
        ? 'border-[#ffa39e] bg-[#f5222d] hover:bg-[#ff4d4f] active:bg-[#cf1322]'
        : 'border-[#B7EB8F] bg-[#52c41a] hover:bg-[#73d13d] active:bg-[#389e0d]'
    ]"
    :disabled="disabled"
  >
    <slot />
  </button>
</template>
