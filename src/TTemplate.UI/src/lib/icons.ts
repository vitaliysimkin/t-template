// Іконки: офлайн-набір system-uicons (@vitaliysimkin/t-components/icons) + власні гліфи (префікс app:*).
import { addCollection } from '@iconify/vue'

addCollection({
  prefix: 'app',
  width: 16,
  height: 16,
  icons: {
    // Логотип (header-icon TSidebar): квадрат з розімкненим кільцем; колір — currentColor.
    logo: { body: '<path fill="currentColor" fill-rule="evenodd" d="M3 0h10a3 3 0 0 1 3 3v10a3 3 0 0 1-3 3H3a3 3 0 0 1-3-3V3a3 3 0 0 1 3-3zM11.37 5.17A4.4 4.4 0 1 0 11.37 10.83L10.38 9.99A3.1 3.1 0 1 1 10.38 6.01z"/>' },
  },
})

/** Наші назви → iconify-імена. */
export const icon = {
  logo: 'app:logo',
  home: 'system-uicons:home',
  settings: 'system-uicons:settings',
  menu: 'system-uicons:menu-hamburger',
  close: 'system-uicons:close',
  check: 'system-uicons:check',
  plus: 'system-uicons:plus',
  refresh: 'system-uicons:refresh',
  sun: 'system-uicons:sun',
  moon: 'system-uicons:moon',
  auto: 'system-uicons:circle-split',
  search: 'system-uicons:search',
  sliders: 'system-uicons:sliders',
  appearance: 'system-uicons:display',
} as const
