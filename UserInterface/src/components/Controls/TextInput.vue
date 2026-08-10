<script setup lang="ts">
withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  maxlength?: number
}>(), {
  placeholder: '',
  maxlength: undefined
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  submit: []
}>()
</script>

<template>
  <input
    type="text"
    :value="modelValue"
    :placeholder="placeholder"
    :maxlength="maxlength"
    class="h-8 rounded-lg border border-[#B7EB8F] bg-white/50 px-2 text-sm outline-none
           hover:border-[#95DE64] transition ease-out duration-150 w-48"
    @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    @keyup.enter="emit('submit')"
  />
</template>
