// Навігація — hash-адреси (#/settings …), щоб бекенду не треба було знати маршрути UI.
import { createRouter, createWebHashHistory, type RouteRecordRaw } from 'vue-router'
import { features } from './features'
import HomeView from './views/HomeView.vue'
import SettingsView from './views/SettingsView.vue'

const routes: RouteRecordRaw[] = [
  { path: '/', name: 'home', component: HomeView },
  { path: '/settings', name: 'settings', component: SettingsView },
  ...features.flatMap((f) => f.routes ?? []),
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

export const router = createRouter({ history: createWebHashHistory(), routes })
