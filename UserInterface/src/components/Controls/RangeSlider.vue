<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  modelValue: number
  min?: number
  max?: number
  step?: number
}>(), {
  min: 512,
  max: 8192,
  step: 256
})

const emit = defineEmits<{
  'update:modelValue': [value: number]
}>()

// 滑块背景渐变（绿色填充 / 灰色剩余），百分比 clamp 到 [0, 100] 防御越界值
const fillPercent = computed(() =>
  Math.min(100, Math.max(0, ((props.modelValue - props.min) / (props.max - props.min)) * 100))
)
const sliderBg = computed(() =>
  `linear-gradient(to right, #B7EB8F ${fillPercent.value}%, #e5e5e5 ${fillPercent.value}%)`
)
</script>

<template>
  <input
    type="range"
    :min="min"
    :max="max"
    :step="step"
    :value="modelValue"
    :style="{ background: sliderBg }"
    class="grow h-2 rounded-full appearance-none cursor-pointer accent-[#52C41A]"
    @input="emit('update:modelValue', Number(($event.target as HTMLInputElement).value))"
  />
</template>

<style scoped>
input[type='range']::-webkit-slider-thumb {
  appearance: none;
  width: 16px;
  height: 16px;
  margin-top: -4px;
  border-radius: 50%;
  background: #52C41A;
  cursor: pointer;
  box-shadow: 0 0 2px rgba(0, 0, 0, 0.15);
}

input[type='range']::-webkit-slider-runnable-track {
  height: 8px;
  border-radius: 4px;
}
</style>
