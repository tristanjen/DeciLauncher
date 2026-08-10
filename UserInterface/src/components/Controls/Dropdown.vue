<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

export interface DropdownOption {
  label: string
  value: string
  disabled?: boolean
}

const props = defineProps<{
  modelValue: string
  options: DropdownOption[]
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

// 下拉列表是否展开
const open = ref(false)
// 触发按钮 DOM 引用（用于关闭面板的点击外部检测）
const triggerRef = ref<HTMLElement | null>(null)
// 下拉面板 DOM 引用
const panelRef = ref<HTMLElement | null>(null)

function toggle() {
  open.value = !open.value
}

function select(option: DropdownOption) {
  if (option.disabled) return
  emit('update:modelValue', option.value)
  open.value = false
}

function onDocumentClick(e: MouseEvent) {
  const target = e.target as Node
  if (triggerRef.value?.contains(target) || panelRef.value?.contains(target)) return
  open.value = false
}

onMounted(() => document.addEventListener('click', onDocumentClick))
onUnmounted(() => document.removeEventListener('click', onDocumentClick))

const selectedLabel = () => {
  const selected = props.options.find(o => o.value === props.modelValue)
  if (selected) return selected.label
  return props.placeholder ?? ''
}
</script>

<template>
  <div class="relative">
    <!-- 触发按钮 -->
    <div ref="triggerRef" class="h-8 rounded-lg border border-[#B7EB8F] bg-white/50 px-2 text-sm flex items-center
             cursor-pointer select-none transition ease-out duration-150
             hover:border-[#95DE64]" @click="toggle">
      <span class="grow truncate">{{ selectedLabel() }}</span>
      <svg class="ml-1 size-3 transition duration-150 shrink-0" :class="open && 'rotate-180'" viewBox="0 0 12 12">
        <path d="M3 5L6 8L9 5" stroke="#333" stroke-width="1.5" fill="none" stroke-linecap="round"
          stroke-linejoin="round" />
      </svg>
    </div>
    <!-- 下拉面板：展开/收起动画 -->
    <Transition name="dropdown">
      <div v-if="open" ref="panelRef" class="absolute z-10 mt-1 w-full rounded-lg border border-[#B7EB8F] bg-white/90
               shadow-lg text-sm overflow-hidden">
        <div v-for="o in options" :key="o.value"
          class="px-2 py-1 cursor-pointer transition ease-in-out duration-150 truncate" :class="o.disabled
            ? 'text-gray-400'
            : o.value === modelValue
              ? 'bg-[#D9F7BE] text-[#389E0D]'
              : 'hover:bg-[#B7EB8F]'" @click="select(o)">
          {{ o.label }}
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active {
  transition: opacity 0.15s ease-in-out, transform 0.15s ease-in-out;
  transform-origin: top;
}

.dropdown-enter-from,
.dropdown-leave-to {
  opacity: 0;
  transform: scaleY(0.95) translateY(-4px);
}
</style>
