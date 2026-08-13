<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue'
import Card from '../../components/Controls/Card.vue'
import NavLink from '../../components/Controls/NavLink.vue'

// 侧边栏滑入动画触发
const sidebarVisible = ref(false)

onMounted(async () => {
  await nextTick()
  sidebarVisible.value = true
})
</script>

<template>
  <div class="flex gap-3">
    <Transition name="sidebar-slide">
      <Card v-if="sidebarVisible" hover-shadow class="flex flex-col gap-2 w-50">
        <NavLink to="/settings/gameSettings" label="游戏设置" size="sidebar" />
        <NavLink to="/settings/LauncherSettings" label="启动器设置" size="sidebar" />
        <NavLink to="/settings/about" label="关于" size="sidebar" />
      </Card>
    </Transition>
    <Transition name="content-drop" mode="out-in" appear>
      <Card hover-shadow class="grow flex flex-col" :key="$route.fullPath">
        <RouterView />
      </Card>
    </Transition>
  </div>
</template>

<style scoped>
.sidebar-slide-enter-active {
  animation: sidebar-bounce 0.3s cubic-bezier(0, 1.2, 0.58, 1);
}

.sidebar-slide-enter-from {
  transform: translateX(-100%);
}

@keyframes sidebar-bounce {
  from {
    transform: translateX(-100%);
  }

  to {
    transform: translateX(0);
  }
}

.content-drop-enter-active {
  transition: opacity 0.15s ease-out, transform 0.2s cubic-bezier(0.42, 1.5, 0.58, 1);
}

.content-drop-leave-active {
  transition: opacity 0.1s ease-out, transform 0.1s cubic-bezier(0.42, 1.5, 0.58, 1);
}

.content-drop-enter-from {
  opacity: 0;
  transform: translateY(-32px);
}

.content-drop-leave-to {
  opacity: 0;
  transform: translateY(8px);
}
</style>
