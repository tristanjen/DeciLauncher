<script setup lang="ts">
defineProps<{
  selected: boolean
}>()
</script>

<template>
  <svg class="size-4 shrink-0" viewBox="0 0 12 12">
    <circle cx="6" cy="6" r="5" :fill="selected ? '#52C41A' : 'none'"
      stroke="#B7EB8F" stroke-width="1.5"/>
    <circle v-if="selected" class="radio-dot" cx="6" cy="6" r="2" fill="#fff"/>
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
