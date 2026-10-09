<script setup lang="ts">
// Бічна панель праворуч (drawer): surface + hairline, фон --t-color-backdrop. Esc — закрити (якщо поверх нема модалки).
import { modalManager } from '@vitaliysimkin/t-components'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { icon } from '../lib/icons'

defineProps<{ title: string; subtitle?: string }>()
const emit = defineEmits<{ close: [] }>()
const panel = ref<HTMLElement | null>(null)

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape' && modalManager.modalCount.value === 0) emit('close')
}
onMounted(() => {
  document.addEventListener('keydown', onKey)
  panel.value?.focus()
})
onBeforeUnmount(() => document.removeEventListener('keydown', onKey))
</script>

<template>
  <div class="drawer-backdrop" @click.self="emit('close')">
    <aside ref="panel" class="drawer" role="dialog" aria-modal="true" :aria-label="title" tabindex="-1">
      <header class="drawer-head">
        <div class="grow">
          <h2>{{ title }}</h2>
          <div v-if="subtitle" class="muted small mono ellipsis">{{ subtitle }}</div>
        </div>
        <slot name="head" />
        <TButton type="button" :icon="icon.close" mode="ghost" size="small" aria-label="Закрити" title="Закрити (Esc)" @click="emit('close')" />
      </header>
      <div class="drawer-body"><slot /></div>
      <footer v-if="$slots.footer" class="drawer-foot"><slot name="footer" /></footer>
    </aside>
  </div>
</template>

<style scoped>
.drawer-backdrop { position: fixed; inset: 0; background: var(--t-color-backdrop); z-index: 1000; display: flex; justify-content: flex-end; }
.drawer {
  width: var(--app-drawer-w); max-width: 100%; height: 100%; display: flex; flex-direction: column;
  background: var(--t-color-surface); border-left: 1px solid var(--t-color-border); box-shadow: var(--t-shadow-3); outline: none;
}
.drawer-head { display: flex; align-items: flex-start; gap: 10px; padding: var(--t-space-4) 18px var(--t-space-3); border-bottom: 1px solid var(--t-color-border); }
.drawer-body { flex: 1 1 auto; overflow: auto; padding: var(--t-space-4) 18px; display: flex; flex-direction: column; gap: var(--t-space-4); min-height: 0; }
.drawer-body > :deep(*) { flex-shrink: 0; }
.drawer-foot { display: flex; gap: var(--t-space-2); justify-content: flex-end; align-items: center; padding: var(--t-space-3) 18px; border-top: 1px solid var(--t-color-border); }
</style>
