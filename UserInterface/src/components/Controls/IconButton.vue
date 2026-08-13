<script setup lang="ts">
withDefaults(defineProps<{
  variant?: 'titlebar' | 'list'
  color?: 'green' | 'red'
}>(), {
  variant: 'titlebar',
  color: 'green'
})
</script>

<template>
  <button
    class="flex items-center justify-center rounded transition duration-150"
    :class="[
      variant === 'titlebar'
        ? 'size-8 ease-out hover:bg-[#B7EB8F] active:bg-[#95DE64] active:scale-90'
        : 'size-6 opacity-0 group-hover:opacity-100',
      variant === 'list' && (color === 'red'
        ? 'text-gray-400 hover:text-red-400'
        : 'text-gray-400 hover:text-[#52C41A]')
    ]"
    @click.stop
  >
    <slot />
  </button>
</template>
