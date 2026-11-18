<script setup lang="ts">
import { SliderRange, SliderRoot, SliderThumb, SliderTrack } from 'reka-ui'
import { t } from '../../stores/locale'

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

function onChange(value: number[] | undefined) {
  emit('update:modelValue', value?.[0] ?? props.min)
}
</script>

<template>
  <!-- Reka UI Slider：键盘方向键、Home/End、ARIA 由原语提供 -->
  <SliderRoot :model-value="[modelValue]" :min="min" :max="max" :step="step"
    class="relative flex h-8 grow touch-none select-none items-center"
    @update:model-value="onChange">
    <SliderTrack class="relative h-2 grow overflow-hidden rounded-full bg-gray-200">
      <SliderRange class="absolute h-full rounded-full bg-deci-border" />
    </SliderTrack>
    <SliderThumb :aria-label="t('settings.instanceMemory')"
      class="block size-4 rounded-full bg-deci-primary shadow outline-none
             focus-visible:ring-2 focus-visible:ring-deci-primary/40" />
  </SliderRoot>
</template>
