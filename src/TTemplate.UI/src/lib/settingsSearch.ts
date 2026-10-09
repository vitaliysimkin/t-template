// Пошук у налаштуваннях: SettingsPanel кладе рядок пошуку в provide, рядки ховають себе через v-show (не v-if — чернетка не губиться).
import { inject, ref, type InjectionKey, type Ref } from 'vue'

export const settingsSearchKey: InjectionKey<Ref<string>> = Symbol('settingsSearch')

export function useSettingsSearch(): Ref<string> {
  return inject(settingsSearchKey, ref(''))
}

/** Порожній запит — збіг; інакше кожне слово запиту є хоча б в одному з текстів (без регістру). */
export function matchesSearch(query: string, ...texts: (string | null | undefined)[]): boolean {
  const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean)
  if (!words.length) return true
  const hay = texts.filter(Boolean).join(' ').toLowerCase()
  return words.every((w) => hay.includes(w))
}
