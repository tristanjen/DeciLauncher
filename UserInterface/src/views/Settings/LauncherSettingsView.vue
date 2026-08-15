<script setup lang="ts">
// Vue 计算属性（下拉框选项动态构建）
import { computed } from 'vue'
// 国际化：当前语言与翻译
import { locale, t, type Locale } from '../../stores/locale'
// 自定义控件
import Dropdown from '../../components/Controls/Dropdown.vue'
// 卡片容器（设置内容分组外观）
import Card from '../../components/Controls/Card.vue'

/**
 * 语言下拉选项：原生语言名（不翻译语言名本身）
 */
const languageOptions = computed(() => [
  { label: t('settings.langZhCN'), value: 'zh-CN' },
  { label: t('settings.langEnUS'), value: 'en-US' }
])

/**
 * 切换语言：locale ref 变化即时更新全部 t() 文案并持久化到 localStorage
 */
function onLanguageChange(value: string) {
  locale.value = value as Locale
}
</script>

<template>
  <div class="grow flex flex-col">
    <Card class="flex flex-col gap-4">
      <!-- 语言行 -->
      <label class="flex flex-col gap-1 grow">
        <span class="text-sm font-medium">{{ t('settings.language') }}</span>
        <Dropdown :model-value="locale" :options="languageOptions" :placeholder="t('settings.language')"
          @update:model-value="onLanguageChange" />
      </label>
    </Card>
  </div>
</template>
