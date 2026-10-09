<script setup lang="ts">
// Оболонка: бічна панель (TSidebar) на десктопі, нижня навігація (TBottomNav) на вузькому екрані, вміст сторінки,
// вікно «Налаштування» (?settings=<розділ> в URL поверх будь-якої сторінки), компоненти фіч поверх сторінок, тости.
import { modalManager } from '@vitaliysimkin/t-components'
import { useMediaQuery } from '@vueuse/core'
import { computed, markRaw, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import SidebarAccount from './components/SidebarAccount.vue'
import SettingsDialog from './components/settings/SettingsDialog.vue'
import { features } from './features'
import type { MenuItem } from './features/types'
import { icon } from './lib/icons'
import { contentWidth, navCollapsed } from './lib/prefs'
import { app, refreshInfo } from './state/app'

const menuItems = computed<MenuItem[]>(() => [
  { route: '/', title: 'Головна', icon: icon.home },
  ...features.flatMap((f) => f.menu ?? []),
  { route: '/settings', title: 'Налаштування', icon: icon.settings },
])
// Лічильник — текстом у підписі пункту (TSidebar лічильників не має).
const sidebarItems = computed(() => menuItems.value.map((m) => {
  const n = m.badge?.()
  return { route: m.route, title: n ? `${m.title} · ${n}` : m.title, icon: m.icon, activeRoutes: m.activeRoutes }
}))
// TBottomNav показує не більше 4 закріплених пунктів; решта — в аркуші «Меню».
const pinnedRoutes = computed(() => menuItems.value.slice(0, 4).map((m) => m.route))

const collapsed = computed({ get: () => navCollapsed.value === '1', set: (v: boolean) => { navCollapsed.value = v ? '1' : '0' } })
const mobile = useMediaQuery('(max-width: 768px)')
const appName = computed(() => app.info?.name ?? 'TTemplate')
// Смужку-індикатор під обраним пунктом TBottomNav прибираємо штатною змінною компонента.
const bottomNavStyle = { '--t-bottom-nav-indicator-h': '0px' }

// ---------- Вікно «Налаштування»: відкрите, доки в URL є ?settings=<розділ> (на самій сторінці #/settings — не відкривається) ----------
const route = useRoute()
const router = useRouter()
const settingsOpen = computed(() => typeof route.query.settings === 'string' && route.path !== '/settings')
let settingsModalId: string | null = null
function openSettings() {
  if (route.path === '/settings') return
  void router.push({ query: { ...route.query, settings: typeof route.query.settings === 'string' ? route.query.settings : 'general' } })
}
watch(settingsOpen, (open) => {
  if (open && !settingsModalId) {
    settingsModalId = modalManager.openModal({
      label: 'Налаштування',
      contentComponent: markRaw(SettingsDialog),
      blocking: true, blockingDismissible: true, draggable: false, resizable: false, minimizable: false,
      position: 'center', size: { width: 'min(1040px, calc(100vw - 24px))', height: 'min(780px, calc(100vh - 48px))' },
    })
  } else if (!open && settingsModalId) {
    const id = settingsModalId
    settingsModalId = null
    modalManager.closeModal(id)
  }
}, { immediate: true })
// Закрито бібліотекою (Esc, ×, фон) → прибираємо ?settings з URL.
watch(() => modalManager.modals.value.map((x) => x.id).join(','), () => {
  if (settingsModalId && !modalManager.modals.value.some((x) => x.id === settingsModalId)) {
    settingsModalId = null
    const { settings: _settings, ...query } = route.query
    void router.replace({ query })
  }
})

const overlays = features.flatMap((f) => (f.overlays ?? []).map((c, i) => ({ key: `${f.id}-${i}`, component: markRaw(c) })))

onMounted(() => {
  void refreshInfo()
  for (const f of features) f.start?.()
})
</script>

<template>
  <div class="shell" :class="{ 'is-mobile': mobile }">
    <TSidebar
      v-if="!mobile" v-model:collapsed="collapsed" :menu-items="sidebarItems" :expand-on-body-click="false" aria-label="Розділи"
      :header-icon="icon.logo" :header-label="appName" :toggle-label="{ expand: 'Розгорнути меню', collapse: 'Згорнути меню' }"
    >
      <template #footer="{ collapsed: c }">
        <SidebarAccount :collapsed="c" @settings="openSettings" />
      </template>
    </TSidebar>

    <main class="content" :class="{ 'content-wide': contentWidth === 'wide' }">
      <RouterView />
    </main>

    <TBottomNav
      v-if="mobile" :menu-items="sidebarItems" :pinned-routes="pinnedRoutes"
      :header-icon="icon.menu" header-label="Меню" menu-label="Меню" :style="bottomNavStyle"
    >
      <template #profile>
        <div class="brand">
          <TIcon :icon="icon.logo" class="logo" />
          <div class="grow">
            <div class="brand-name">{{ appName }}</div>
            <div v-if="app.info" class="xs muted">v{{ app.info.version }}</div>
          </div>
        </div>
      </template>
    </TBottomNav>

    <component :is="o.component" v-for="o in overlays" :key="o.key" />
    <TModalBoxHost />
    <TNotifications :placement="mobile ? 'top-right' : 'bottom-right'" />
  </div>
</template>

<style scoped>
.shell { display: flex; min-height: 100%; }
.brand { display: flex; align-items: center; gap: 10px; min-width: 0; }
.logo { width: 26px; height: 26px; color: var(--t-color-accent); flex: none; }
.brand-name { font-weight: var(--t-font-weight-semibold); line-height: 1.2; }
.content { flex: 1 1 auto; min-width: 0; padding: 24px 28px 48px; }
/* Мобільний: вужчі поля; знизу — місце під фіксовану TBottomNav. */
.is-mobile .content { padding: var(--t-space-4) var(--t-space-4) calc(var(--t-control-h-large) + var(--t-space-4) + env(safe-area-inset-bottom, 0px) + var(--t-space-6)); }
</style>
