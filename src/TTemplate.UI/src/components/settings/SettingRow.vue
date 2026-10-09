<script setup lang="ts">
// Рядок налаштування: назва й опис ліворуч, елемент керування праворуч (слот), помилка — під рядком.
defineProps<{ label: string; description?: string | null; error?: string | null; labelFor?: string }>()
</script>

<template>
  <div class="s-row">
    <div class="s-main">
      <div class="s-text">
        <span class="s-label">
          <label v-if="labelFor" :for="labelFor">{{ label }}</label>
          <span v-else>{{ label }}</span>
          <slot name="tag" />
        </span>
        <span v-if="description" class="s-desc">{{ description }}</span>
      </div>
      <div class="s-ctl"><slot /></div>
    </div>
    <span v-if="error" class="small bad-text" role="alert">{{ error }}</span>
  </div>
</template>

<style scoped>
.s-row { display: flex; flex-direction: column; gap: var(--t-space-1); padding: var(--t-space-3) 0; border-bottom: 1px solid var(--t-color-border); }
.s-row:last-child { border-bottom: 0; }
.s-main { display: flex; align-items: center; justify-content: space-between; gap: var(--t-space-5); }
.s-text { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.s-label { display: inline-flex; align-items: center; gap: var(--t-space-2); flex-wrap: wrap; }
.s-desc { color: var(--t-color-text-muted); font-size: var(--t-font-size-small); }
.s-ctl { display: flex; align-items: center; gap: var(--t-space-1); flex-shrink: 0; }
@media (max-width: 720px) {
  .s-main { flex-direction: column; align-items: stretch; gap: var(--t-space-2); }
  .s-ctl { flex-shrink: 1; }
}
</style>
