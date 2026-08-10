<script setup lang="ts">
withDefaults(defineProps<{
  title?: string
  centered?: boolean
}>(), {
  title: '',
  centered: false
})

// v-model：外部控制显隐
const model = defineModel<boolean>({ default: false })
</script>

<template>
  <Teleport to="#main-card">
    <Transition name="notify">
      <div v-if="model" class="absolute inset-0 z-50 flex items-center justify-center bg-black/30">
        <Transition name="card" appear>
          <div v-if="model"
            class="rounded-lg bg-white/95 p-6 shadow-lg border border-[#B7EB8F] flex flex-col gap-2"
            :class="centered && 'items-center'"
            @click.stop>
            <span v-if="title" class="text-base font-medium">{{ title }}</span>
            <slot />
            <div v-if="$slots.footer" class="flex gap-2"
              :class="centered ? 'justify-center' : 'justify-end'">
              <slot name="footer" />
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.notify-enter-active {
  transition: opacity 0.15s ease-out;
}
.notify-leave-active {
  transition: opacity 0.15s ease-out;
}
.notify-enter-from,
.notify-leave-to {
  opacity: 0;
}

.card-enter-active {
  transition: opacity 0.15s ease-out, transform 0.15s ease-out;
}
.card-leave-active {
  transition: opacity 0.1s ease-out, transform 0.1s ease-out;
}
.card-enter-from,
.card-leave-to {
  opacity: 0;
  transform: scale(0.95);
}
</style>
