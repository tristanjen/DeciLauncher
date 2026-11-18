<script setup lang="ts">
withDefaults(defineProps<{
  to: string
  label: string
  size?: 'nav' | 'sidebar'
}>(), {
  size: 'nav'
})

const linkClass = 'transition ease-out duration-150 hover:bg-deci-border inline-flex items-center justify-center align-middle text-sm rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-deci-primary/40'
const activeClass = 'bg-deci-soft text-deci-primary-active! font-medium'
</script>

<template>
  <RouterLink :to="to" custom v-slot="{ navigate, isActive }">
    <!-- 侧边栏模式：带左侧激活指示条 -->
    <div v-if="size === 'sidebar'" class="relative flex items-center active:scale-95 transition ease-out duration-150">
      <div v-if="isActive"
        class="absolute left-0 w-1 h-5 rounded-full bg-deci-primary transition ease-in-out duration-150" />
      <span @click="navigate()" @keydown.enter="navigate()" @keydown.space.prevent="navigate()"
        role="link" tabindex="0" :aria-current="isActive ? 'page' : undefined"
        :class="[linkClass, 'w-44 h-9', isActive && activeClass]">{{ label }}</span>
    </div>
    <!-- 标题栏模式：纯文本链接 -->
    <span v-else @click="navigate()" @keydown.enter="navigate()" @keydown.space.prevent="navigate()"
      role="link" tabindex="0" :aria-current="isActive ? 'page' : undefined"
      :class="[linkClass, 'w-18 h-8 active:scale-95', isActive && activeClass]">{{ label }}</span>
  </RouterLink>
</template>
