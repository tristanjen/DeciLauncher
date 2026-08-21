<script setup lang="ts">
withDefaults(defineProps<{
  to: string
  label: string
  size?: 'nav' | 'sidebar'
}>(), {
  size: 'nav'
})

const linkClass = 'transition ease-out duration-150 hover:bg-[#B7EB8F] inline-flex items-center justify-center align-middle text-sm rounded-lg'
const activeClass = 'bg-[#D9F7BE] !text-[#389E0D] font-medium'
</script>

<template>
  <RouterLink :to="to" custom v-slot="{ navigate, isActive }">
    <!-- 侧边栏模式：带左侧激活指示条 -->
    <div v-if="size === 'sidebar'" class="relative flex items-center active:scale-95 transition ease-out duration-150">
      <div v-if="isActive"
        class="absolute left-0 w-1 h-5 rounded-full bg-[#52C41A] transition ease-in-out duration-150" />
      <span @click="navigate" :class="[linkClass, 'w-44 h-9', isActive && activeClass]">{{ label }}</span>
    </div>
    <!-- 标题栏模式：纯文本链接 -->
    <span v-else @click="navigate" :class="[linkClass, 'w-18 h-8 active:scale-95', isActive && activeClass]">{{ label }}</span>
  </RouterLink>
</template>
