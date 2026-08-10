<script setup lang="ts">
import { watch } from 'vue'
import { toast } from '../../stores/store'

let timer: ReturnType<typeof setTimeout> | undefined

watch(toast, (val) => {
  if (timer) { clearTimeout(timer); timer = undefined }
  if (val) {
    timer = setTimeout(() => { toast.value = null }, 3000)
  }
})

function dismiss() {
  if (timer) { clearTimeout(timer); timer = undefined }
  toast.value = null
}
</script>

<template>
  <Transition name="toast">
    <div v-if="toast"
      class="fixed bottom-4 right-4 z-50 rounded-lg bg-white/90 px-4 py-2 shadow-lg text-sm
             border border-[#52C41A] cursor-pointer"
      @click="dismiss">
      {{ toast }}
    </div>
  </Transition>
</template>

<style scoped>
.toast-enter-active {
  transition: opacity 0.2s ease-out, transform 0.2s ease-out;
}
.toast-leave-active {
  transition: opacity 0.15s ease-out, transform 0.15s ease-out;
}
.toast-enter-from {
  opacity: 0;
  transform: translateX(32px);
}
.toast-leave-to {
  opacity: 0;
  transform: translateX(32px);
}
</style>
