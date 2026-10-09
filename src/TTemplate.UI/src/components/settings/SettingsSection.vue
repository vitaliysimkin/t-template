<script setup lang="ts">
// Підрозділ налаштувань: заголовок h3 + рядки.
defineProps<{ title?: string | null }>()
</script>

<template>
  <section class="s-section">
    <h3 v-if="title || $slots.title" class="s-h"><slot name="title">{{ title }}</slot></h3>
    <slot />
  </section>
</template>

<style scoped>
.s-section { margin: 0 0 var(--t-space-6); }
.s-section:last-child { margin-bottom: 0; }
.s-h { font-size: var(--t-font-size-medium); font-weight: var(--t-font-weight-semibold); margin: 0 0 var(--t-space-2); }
</style>
