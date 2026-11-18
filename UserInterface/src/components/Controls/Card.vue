<script setup lang="ts">
withDefaults(defineProps<{
  padding?: string
  hoverShadow?: boolean
  clickable?: boolean
}>(), {
  padding: 'p-3',
  hoverShadow: false,
  clickable: false
})
</script>

<template>
  <div
    class="bg-white/25 shadow-deci border border-transparent
           hover:border-deci-primary/25 rounded-lg transition ease-out duration-150"
    :class="[
      padding,
      hoverShadow && 'hover:shadow-deci-hover',
      clickable && 'cursor-pointer group'
    ]"
  >
    <slot />
  </div>
</template>
