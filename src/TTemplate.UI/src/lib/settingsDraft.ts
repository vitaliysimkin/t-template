// Спільний реєстр «брудних» рядків налаштувань: кожен SettingItem реєструється при монтуванні й знімається при демонтажі.
// SettingsPanel показує одну липку панель «Зберегти все» / «Скасувати зміни».
import { computed, reactive } from 'vue'

export interface SettingsDraftSection {
  id: string
  dirty: () => boolean
  /** Можна зберігати; відсутнє — так. */
  valid?: () => boolean
  /** Зберегти (кидає при помилці — секція лишається «брудною»). */
  save: () => Promise<void>
  /** Скинути чернетку до серверних даних. */
  cancel: () => void
}

const sections = reactive(new Map<string, SettingsDraftSection>())

export function registerDraftSection(section: SettingsDraftSection): () => void {
  sections.set(section.id, section)
  return () => { sections.delete(section.id) }
}

const all = computed(() => Array.from(sections.values()))
export const dirtySections = computed(() => all.value.filter((s) => s.dirty()))
export const dirtyCount = computed(() => dirtySections.value.length)
export const allDirtyValid = computed(() => dirtySections.value.every((s) => s.valid?.() ?? true))

export async function saveAllDirty(): Promise<void> {
  for (const s of dirtySections.value) await s.save()
}

export function cancelAllDirty(): void {
  for (const s of dirtySections.value) s.cancel()
}
