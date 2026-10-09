// Персональні налаштування вигляду в localStorage цього браузера (може бути недоступний — тоді типові значення).
import { ref, watch, type Ref } from 'vue'

function stored<T extends string>(key: string, fallback: T, allowed: readonly T[]): Ref<T> {
  let initial = fallback
  try {
    const v = localStorage.getItem(key) as T | null
    if (v && allowed.includes(v)) initial = v
  } catch { /* немає сховища */ }
  const r = ref(initial) as Ref<T>
  watch(r, (v) => {
    try { localStorage.setItem(key, v) } catch { /* лише до перезавантаження */ }
  })
  return r
}

/** Ширина вмісту: обмежена (до 1320px по центру) або на всю ширину. */
export type ContentWidth = 'limited' | 'wide'
export const contentWidth = stored<ContentWidth>('app.content.width', 'limited', ['limited', 'wide'])

/** Бічна панель згорнута. */
export const navCollapsed = stored<'0' | '1'>('app.nav.collapsed', '0', ['0', '1'])
