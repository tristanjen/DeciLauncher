<script setup lang="ts">
withDefaults(defineProps<{
  size?: 'sm' | 'lg'
  disabled?: boolean
  loading?: boolean
  loadingText?: string
  label?: string
}>(), {
  size: 'sm',
  disabled: false,
  loading: false,
  loadingText: '',
  label: ''
})
</script>

<template>
  <button
    class="rounded-lg text-sm transition duration-150 bg-white/50 border border-[#B7EB8F]
           hover:bg-[#B7EB8F] active:bg-[#95DE64] active:scale-95 shrink-0
           disabled:opacity-50 disabled:cursor-not-allowed"
    :class="size === 'sm' ? 'h-8 px-3' : 'h-12 px-6'"
    :disabled="disabled || loading"
  >
    <slot>{{ loading ? loadingText : label }}</slot>
  </button>
</template>
