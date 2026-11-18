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
    type="button"
    class="inline-flex items-center justify-center gap-1.5 rounded-lg text-sm transition duration-150
           bg-white/50 border border-deci-border hover:bg-deci-border active:bg-deci-hover active:scale-95 shrink-0
           disabled:opacity-50 disabled:cursor-not-allowed
           focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/40"
    :class="size === 'sm' ? 'h-8 px-3' : 'h-12 px-6'"
    :disabled="disabled || loading"
  >
    <slot>{{ loading ? loadingText : label }}</slot>
  </button>
</template>
