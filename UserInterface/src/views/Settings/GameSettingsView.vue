<script setup lang="ts">
// Vue 计算属性（下拉框选项动态构建）
import { computed, watch } from 'vue'
// 前端 ↔ C# 后端消息桥（发送扫描命令）
import { sendNative } from '../../native'
// 全局共享状态（Java 列表、选中项、扫描标记、内存上限）
import { javaList, selectedJava, scanning, hasScanned, maxMemory } from '../../stores/store'
// 国际化翻译
import { t } from '../../stores/locale'
// 自定义控件（下拉/按钮/滑块）
import Dropdown from '../../components/Controls/Dropdown.vue'
import DefaultButton from '../../components/Controls/DefaultButton.vue'
import RangeSlider from '../../components/Controls/RangeSlider.vue'

/**
 * 向 C# 后端发起 Java 运行时扫描
 * requestAnimationFrame 确保 "扫描中..." DOM 先渲染到屏幕再执行
 */
async function scanJava() {
  scanning.value = true          // 标记扫描中，下拉框显示 "扫描中..."
  javaList.value = []            // 清空旧结果
  selectedJava.value = ''        // 重置选中项
  await new Promise(r => requestAnimationFrame(r))  // 等待浏览器下一帧渲染
  sendNative('scan-java')        // 通过 Photino 消息桥发送扫描命令
}

/**
 * 下拉框选项：根据扫描状态动态构建
 */
const dropdownOptions = computed(() => {
  if (scanning.value) return [{ label: t('settings.scanning'), value: '', disabled: true }]
  if (javaList.value.length === 0) return [{ label: hasScanned.value ? t('settings.noJavaFound') : t('settings.notScannedYet'), value: '', disabled: true }]
  return [
    { label: t('settings.autoSelect'), value: '__auto__' },
    ...javaList.value.map(j => ({
      label: `${j.version ? `Java ${j.version}` : t('settings.unknown')} — ${j.path}`,
      value: j.path
    }))
  ]
})

// 内存变化时持久化到 localStorage
watch(maxMemory, (val) => {
  localStorage.setItem('max-memory', String(val))
})
</script>

<template>
  <div class="grow flex flex-col gap-4">
    <!-- Java 运行时行 -->
    <div class="flex items-end gap-2">
      <label class="flex flex-col gap-1 grow">
        <span class="text-sm font-medium">{{ t('settings.javaRuntime') }}</span>
        <Dropdown v-model="selectedJava" :options="dropdownOptions" :placeholder="t('settings.notScannedYet')" />
      </label>
      <DefaultButton
        :loading="scanning"
        :loading-text="t('settings.scanning')"
        :disabled="scanning"
        @click="scanJava"
      >
        {{ t('settings.scan') }}
      </DefaultButton>
    </div>
    <!-- 游戏内存行 -->
    <label class="flex flex-col gap-1">
      <span class="text-sm font-medium">{{ t('settings.gameMemory') }}</span>
      <div class="flex items-center gap-3">
        <RangeSlider v-model="maxMemory" />
        <span class="text-sm text-gray-700 w-16 text-right shrink-0">
          {{ maxMemory >= 1024 ? (maxMemory / 1024).toFixed(1) + ' ' + t('settings.gb') : maxMemory + ' ' + t('settings.mb') }}
        </span>
      </div>
    </label>
  </div>
</template>
