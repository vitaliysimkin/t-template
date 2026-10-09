<script setup lang="ts">
// Сторінка «Налаштування» — той самий SettingsPanel, що й у вікні; розділ — у URL (#/settings?tab=<розділ>).
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import SettingsPanel from '../components/settings/SettingsPanel.vue'

const route = useRoute()
const router = useRouter()
const tab = computed(() => (typeof route.query.tab === 'string' ? route.query.tab : null))
function setTab(key: string) {
  void router.replace({ query: { ...route.query, tab: key } })
}
</script>

<template>
  <section class="page">
    <header class="page-head">
      <div class="grow"><h1>Налаштування</h1></div>
    </header>
    <SettingsPanel mode="page" :section="tab" @update:section="setTab" />
  </section>
</template>
