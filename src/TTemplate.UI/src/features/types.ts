import type { Component } from 'vue'
import type { RouteRecordRaw } from 'vue-router'

export interface MenuItem {
  route: string
  title: string
  icon: string
  /** Маршрути, на яких пункт теж підсвічений (напр. ['/tickets'] для #/tickets/:id). */
  activeRoutes?: string[]
  /** Лічильник у підписі пункту («Операції · 2»); 0/null — без лічильника. */
  badge?: () => number | null
}

/**
 * Фіча — самодостатній шматок застосунку: пункти меню, маршрути, компоненти поверх сторінок (плаваюча кнопка, drawer),
 * старт (опитування тощо). Підключення — один рядок у features/index.ts; налаштування фічі приходять з бекенду ([SettingsSection]).
 */
export interface AppFeature {
  id: string
  menu?: MenuItem[]
  routes?: RouteRecordRaw[]
  /** Монтуються в App.vue поверх сторінок. */
  overlays?: Component[]
  /** Викликається один раз після монтування App. */
  start?: () => void
}
