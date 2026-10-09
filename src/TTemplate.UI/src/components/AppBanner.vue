<script setup lang="ts">
// Банер на ролях --t-color-*: нейтральний / ok / warn / bad, з необовʼязковою дією праворуч.
defineProps<{ tone?: 'ok' | 'warn' | 'bad' | '' }>()
</script>

<template>
  <div class="banner" :class="tone">
    <span class="dot" aria-hidden="true" />
    <div class="grow body"><slot /></div>
    <div v-if="$slots.action" class="action"><slot name="action" /></div>
  </div>
</template>

<style scoped>
.banner {
  display: flex; align-items: flex-start; gap: 10px;
  padding: 10px 14px; border-radius: var(--t-radius-medium); border: 1px solid var(--t-color-border);
  background: var(--t-color-surface); font-size: var(--t-font-size-small);
}
.dot { flex: none; width: 8px; height: 8px; margin-top: 6px; border-radius: 50%; background: var(--t-color-text-muted); }
.action { flex: none; display: flex; gap: var(--t-space-2); align-self: center; }
.warn { background: var(--t-color-warning-plain-bg); border-color: color-mix(in srgb, var(--t-color-warning) 35%, var(--t-color-border)); }
.warn .dot { background: var(--t-color-warning); }
.bad { background: var(--t-color-danger-plain-bg); border-color: color-mix(in srgb, var(--t-color-danger) 35%, var(--t-color-border)); }
.bad .dot { background: var(--t-color-danger); }
.ok { background: var(--t-color-success-plain-bg); border-color: color-mix(in srgb, var(--t-color-success) 35%, var(--t-color-border)); }
.ok .dot { background: var(--t-color-success); }
</style>
