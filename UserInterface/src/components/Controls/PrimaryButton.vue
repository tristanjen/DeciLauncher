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
    type="button"
    class="rounded-lg text-sm transition duration-150 text-white border active:scale-95 shrink-0
           disabled:opacity-50 disabled:cursor-not-allowed
           focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/40"
    :class="[
      size === 'sm' ? 'h-8 px-3' : 'h-12 px-8',
      variant === 'danger'
        ? 'border-deci-danger-border bg-deci-danger hover:bg-deci-danger-hover active:bg-deci-danger-active'
        : 'border-deci-border bg-deci-primary hover:bg-deci-primary-hover active:bg-deci-primary-active'
    ]"
    :disabled="disabled"
  >
    <slot />
  </button>
</template>
