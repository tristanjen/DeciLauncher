<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue'
// 路由离开守卫：离开设置页前先播放侧栏滑出动画，再放行导航
import { onBeforeRouteLeave } from 'vue-router'
import Card from '../../components/Controls/Card.vue'
import NavLink from '../../components/Controls/NavLink.vue'
// 国际化翻译
import { t } from '../../stores/locale'

// 侧边栏滑入动画触发
const sidebarVisible = ref(false)

onMounted(async () => {
  await nextTick()
  sidebarVisible.value = true
})

// 离开设置页（切换到其他顶层页）时：收起侧栏触发滑出动画。
// 不阻塞导航（无延迟）：路由立即切换，滑出动画与 App.vue 的 fade-leave 同步播放
onBeforeRouteLeave(() => {
  sidebarVisible.value = false
})
</script>

<template>
  <div class="flex gap-3 -ml-3">
    <Transition name="sidebar-slide">
      <Card v-if="sidebarVisible" hover-shadow class="flex flex-col items-end gap-2 w-50 h-107 rounded-l-none fixed">
        <NavLink to="/settings/gameSettings" :label="t('settings.instanceSettings')" size="sidebar" />
        <NavLink to="/settings/LauncherSettings" :label="t('settings.launcherSettings')" size="sidebar" />
        <NavLink to="/settings/about" :label="t('settings.about')" size="sidebar" />
      </Card>
    </Transition>
    <!-- 占位元素：fixed 脱离文档流后保持右侧内容位置；常驻渲染，
         侧栏滑出后占位仍在，右侧内容不左移跳动 -->
    <div class="w-50 h-107 left-3" aria-hidden="true" />
    <Transition name="content-drop" mode="out-in" appear>
      <div class="grow flex flex-col h-min" :key="$route.fullPath">
        <RouterView />
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.sidebar-slide-enter-active {
  animation: sidebar-bounce 0.3s cubic-bezier(0, 1.15, 0.58, 1);
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

@keyframes sidebar-slide-out {
  from {
    transform: translateX(0);
  }

  to {
    transform: translateX(-100%);
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
