<script setup lang="ts">
withDefaults(defineProps<{
  variant?: 'titlebar' | 'list'
  color?: 'green' | 'red'
  /** 无可见文字的图标按钮必须提供无障碍名称 */
  label?: string
}>(), {
  variant: 'titlebar',
  color: 'green',
  label: ''
})
</script>

<template>
  <button
    type="button"
    class="flex items-center justify-center rounded-lg transition duration-150
           focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/40"
    :class="[
      variant === 'titlebar'
        ? 'size-8 ease-out hover:bg-deci-border active:bg-deci-hover active:scale-90'
        : 'size-6 opacity-0 group-hover:opacity-100',
      variant === 'list' && (color === 'red'
        ? 'text-gray-400 hover:text-red-400'
        : 'text-gray-400 hover:text-deci-primary')
    ]"
    :aria-label="label || undefined"
    @click.stop
  >
    <slot />
  </button>
</template>
