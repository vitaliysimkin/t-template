<script setup lang="ts">
// Стартова сторінка — заміни своїм вмістом.
import { app } from '../state/app'
</script>

<template>
  <section class="page">
    <header class="page-head">
      <div class="grow">
        <h1>{{ app.info?.name ?? 'Головна' }}</h1>
        <p class="small muted">Стартова сторінка шаблону — заміни її своїм вмістом (src/views/HomeView.vue).</p>
      </div>
    </header>
    <TCard>
      <TEmpty icon="system-uicons:box" description="Тут буде твій застосунок" />
    </TCard>
  </section>
</template>
