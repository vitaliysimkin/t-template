<script setup lang="ts">
// Модальне вікно «Налаштування» (App.vue): відкрите, доки в URL є ?settings=<розділ>; розділ — той самий query.
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import SettingsPanel from './SettingsPanel.vue'

const route = useRoute()
const router = useRouter()
const section = computed(() => (typeof route.query.settings === 'string' ? route.query.settings : null))
function setSection(key: string) {
  void router.replace({ query: { ...route.query, settings: key } })
}
</script>

<template>
  <SettingsPanel class="settings-dialog" mode="modal" :section="section" @update:section="setSection" />
</template>

<style scoped>
.settings-dialog { height: 100%; min-height: 0; }
</style>
