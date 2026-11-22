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
    <!-- 侧栏复用页面切换的动画：fadeIn/fadeOut + 0.95 缩放回弹 -->
    <Transition enter-active-class="animate__animated animate__fadeIn deci-page-enter"
      leave-active-class="animate__animated animate__fadeOut deci-page-leave">
      <Card v-if="sidebarVisible" hover-shadow class="flex flex-col items-end gap-2 w-50 h-107 rounded-l-none fixed">
        <NavLink to="/settings/gameSettings" :label="t('settings.instanceSettings')" size="sidebar" />
        <NavLink to="/settings/LauncherSettings" :label="t('settings.launcherSettings')" size="sidebar" />
        <NavLink to="/settings/about" :label="t('settings.about')" size="sidebar" />
      </Card>
    </Transition>
    <!-- 占位元素：fixed 脱离文档流后保持右侧内容位置；常驻渲染，
         侧栏滑出后占位仍在，右侧内容不左移跳动 -->
    <div class="w-50 h-107 left-3" aria-hidden="true" />
    <Transition mode="out-in" appear
      enter-active-class="animate__animated deci-settings-enter"
      leave-active-class="animate__animated deci-settings-leave">
      <div class="grow flex flex-col h-min" :key="$route.fullPath">
        <RouterView />
      </div>
    </Transition>
  </div>
</template>
