<script setup lang="ts">
defineProps<{
  selected: boolean
}>()
</script>

<template>
  <!-- 纯视觉指示器；交互语义由外层 Reka UI RadioGroupItem 提供 -->
  <svg class="size-4 shrink-0" viewBox="0 0 12 12" aria-hidden="true" focusable="false">
    <circle cx="6" cy="6" r="5" :class="selected ? 'fill-deci-primary' : 'fill-none'"
      class="stroke-deci-border" stroke-width="1.5"/>
    <circle v-if="selected" class="radio-dot fill-white" cx="6" cy="6" r="2" />
  </svg>
</template>

<style scoped>
.radio-dot {
  animation: dot-in 0.15s ease-out;
  transform-origin: 6px 6px;
}

@keyframes dot-in {
  from { transform: scale(0); }
  to { transform: scale(1); }
}
</style>
