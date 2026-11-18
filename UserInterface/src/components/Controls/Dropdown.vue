<script setup lang="ts">
import { computed } from 'vue'
import {
  SelectContent,
  SelectIcon,
  SelectItem,
  SelectItemIndicator,
  SelectItemText,
  SelectPortal,
  SelectRoot,
  SelectTrigger,
  SelectValue,
  SelectViewport,
  type AcceptableValue
} from 'reka-ui'
// Lucide 图标
import { Check, ChevronDown } from '@lucide/vue'

export interface DropdownOption {
  label: string
  value: string
  disabled?: boolean
}

const props = defineProps<{
  modelValue: string
  options: DropdownOption[]
  placeholder?: string
  /** 触发按钮的无障碍名称（可见标签在外部，需显式关联） */
  ariaLabel?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

// Reka UI 禁止 SelectItem 使用空字符串 value（空值用于清空选择并显示 placeholder）。
// 业务上用 value: '' 表示“扫描中/未找到”这类不可选状态，这里统一映射为哨兵值：
// 触发器仍显示该状态文案（effectivePlaceholder），选择时再映射回 ''。
const EMPTY_VALUE = '__deci_empty__'

const items = computed(() =>
  props.options.map(o => (o.value === '' ? { ...o, value: EMPTY_VALUE } : o))
)

const effectivePlaceholder = computed(() =>
  props.options.find(o => o.value === '')?.label ?? props.placeholder ?? ''
)

const selectValue = computed(() => (props.modelValue === '' ? undefined : props.modelValue))

function onChange(value: AcceptableValue) {
  const next = typeof value === 'string' ? value : String(value ?? '')
  emit('update:modelValue', next === EMPTY_VALUE ? '' : next)
}
</script>

<template>
  <SelectRoot :model-value="selectValue" @update:model-value="onChange">
    <!-- 触发按钮 -->
    <SelectTrigger
      :aria-label="ariaLabel"
      class="group h-8 w-full rounded-lg border border-deci-border bg-white/50 px-2 text-sm flex items-center
             cursor-pointer select-none transition ease-out duration-150
             hover:border-deci-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/25">
      <SelectValue :placeholder="effectivePlaceholder" class="grow truncate text-left" />
      <SelectIcon>
        <ChevronDown :size="12" :stroke-width="1.5" aria-hidden="true"
          class="ml-1 shrink-0 transition duration-150 group-data-[state=open]:rotate-180" />
      </SelectIcon>
    </SelectTrigger>
    <!-- 下拉面板：popper 定位，默认挂载到 body 避免被卡片 overflow 裁剪 -->
    <SelectPortal>
      <SelectContent position="popper" :side-offset="4"
        class="z-50 max-h-72 min-w-[var(--reka-select-trigger-width)] overflow-hidden rounded-lg border border-deci-border
               bg-white/95 shadow-lg text-sm data-[state=open]:animate-deci-dropdown-in">
        <SelectViewport class="overflow-y-auto p-1">
          <SelectItem v-for="o in items" :key="o.value" :value="o.value" :disabled="o.disabled"
            class="relative flex w-full items-center rounded px-2 py-1 pr-6 text-left cursor-pointer select-none
                   outline-none transition ease-in-out duration-150
                   data-[disabled]:cursor-not-allowed data-[disabled]:text-gray-400
                   data-[highlighted]:bg-deci-border
                   data-[state=checked]:bg-deci-soft data-[state=checked]:text-deci-primary-active">
            <SelectItemText class="truncate">{{ o.label }}</SelectItemText>
            <SelectItemIndicator class="absolute right-2 inline-flex items-center">
              <Check :size="12" :stroke-width="1.5" aria-hidden="true" />
            </SelectItemIndicator>
          </SelectItem>
        </SelectViewport>
      </SelectContent>
    </SelectPortal>
  </SelectRoot>
</template>
