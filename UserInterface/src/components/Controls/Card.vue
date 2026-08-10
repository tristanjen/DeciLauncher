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
    class="bg-white/25 hover:bg-white/50 shadow-[0_0_4px_#52C41A3F] border border-transparent
           hover:border-[#52C41A]/25 rounded-lg transition ease-out duration-150"
    :class="[
      padding,
      hoverShadow && 'hover:shadow-[0_0_8px_#52C41A3F]',
      clickable && 'cursor-pointer group'
    ]"
  >
    <slot />
  </div>
</template>
