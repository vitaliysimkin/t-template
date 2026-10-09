<script setup lang="ts">
// Вміст «Налаштувань» — один компонент для модального вікна (SettingsDialog) і сторінки #/settings (SettingsView).
// Ліворуч — пошук і розділи; праворуч — підрозділи з рядками «назва+опис — контрол»; унизу — липка панель «Зберегти все».
// Розділи приходять з бекенду (класи з [SettingsSection]); «Загальне» додатково має «Вигляд» (тема, ширина — localStorage).
// Неактивні розділи лише ховаються (v-show): чернетки живуть до «Зберегти все» / «Скасувати».
import { useTheme } from '@vitaliysimkin/t-components'
import { computed, onMounted, provide, ref, watch } from 'vue'
import type { SettingView } from '../../api/core'
import { errorText } from '../../api/http'
import { icon } from '../../lib/icons'
import { contentWidth } from '../../lib/prefs'
import { allDirtyValid, cancelAllDirty, dirtyCount, saveAllDirty } from '../../lib/settingsDraft'
import { matchesSearch, settingsSearchKey } from '../../lib/settingsSearch'
import { app, refreshSettings, toast } from '../../state/app'
import AppBanner from '../AppBanner.vue'
import ChoiceGroup, { type ChoiceOption } from '../ChoiceGroup.vue'
import SettingItem from './SettingItem.vue'
import SettingRow from './SettingRow.vue'
import SettingsSection from './SettingsSection.vue'

const props = defineProps<{ mode: 'modal' | 'page'; section?: string | null }>()
const emit = defineEmits<{ 'update:section': [key: string] }>()

onMounted(() => { void refreshSettings() })

// ---------- «Вигляд»: тема (useTheme бібліотеки — застосовується одразу) і ширина вмісту ----------
const { currentTheme } = useTheme()
const themeOptions: ChoiceOption[] = [
  { value: 'light', label: 'Світла', icon: icon.sun },
  { value: 'dark', label: 'Темна', icon: icon.moon },
  { value: 'auto', label: 'Як у системі', icon: icon.auto },
]
const themeModel = computed({
  get: () => currentTheme.value,
  set: (v: string) => { if (v === 'auto' || v === 'light' || v === 'dark') currentTheme.value = v },
})
const THEME_LABEL = 'Тема'
const THEME_DESC = 'Зберігається в цьому браузері й застосовується одразу'
const widthOptions: ChoiceOption[] = [
  { value: 'limited', label: 'Обмежена' },
  { value: 'wide', label: 'На всю ширину' },
]
const widthModel = computed({
  get: () => contentWidth.value as string,
  set: (v: string) => { if (v === 'limited' || v === 'wide') contentWidth.value = v },
})
const WIDTH_LABEL = 'Ширина вмісту'
const WIDTH_DESC = 'Обмежена — сторінка до 1320px по центру. Зберігається в цьому браузері'

// ---------- Розділи (з бекенду) ----------
interface NavItem { key: string; title: string; icon: string }
const navItems = computed<NavItem[]>(() => {
  const sections = app.settings.data?.sections ?? []
  const items = sections.map((s) => ({ key: s.key, title: s.caption, icon: s.icon || icon.sliders }))
  // «Загальне» є завжди (там «Вигляд»), навіть якщо бекенд недоступний.
  if (!items.some((i) => i.key === 'general')) items.unshift({ key: 'general', title: 'Загальне', icon: icon.sliders })
  return items
})
const active = computed(() => navItems.value.find((n) => n.key === props.section)?.key ?? 'general')

/** Налаштування розділу, згруповані за group (null — без підзаголовка). */
function groupsOf(key: string): { title: string | null; items: SettingView[] }[] {
  const items = (app.settings.data?.items ?? []).filter((s) => s.section === key)
  const groups = new Map<string | null, SettingView[]>()
  for (const s of items) groups.set(s.group ?? null, [...(groups.get(s.group ?? null) ?? []), s])
  return Array.from(groups, ([title, list]) => ({ title, items: list }))
}

// ---------- Пошук ----------
const query = ref('')
provide(settingsSearchKey, query)
const searching = computed(() => query.value.trim() !== '')
const q = (...texts: (string | null | undefined)[]) => matchesSearch(query.value, ...texts)
const themeMatch = computed(() => q(THEME_LABEL, THEME_DESC, 'Вигляд'))
const widthMatch = computed(() => q(WIDTH_LABEL, WIDTH_DESC, 'Вигляд'))
const groupMatch = (items: SettingView[]) => items.some((s) => q(s.caption, s.description, s.name))
function sectionMatch(key: string) {
  const own = groupsOf(key).some((g) => groupMatch(g.items))
  return key === 'general' ? own || themeMatch.value || widthMatch.value : own
}
const isShown = (key: string) => (searching.value ? sectionMatch(key) : key === active.value)
const nothingFound = computed(() => searching.value && !navItems.value.some((n) => sectionMatch(n.key)))

const body = ref<HTMLElement | null>(null)
function pick(key: string) {
  query.value = ''
  if (key !== active.value) emit('update:section', key)
}
watch(active, () => { if (props.mode === 'modal') body.value?.scrollTo({ top: 0 }) })

// ---------- «Зберегти все» ----------
const savingAll = ref(false)
async function saveAllChanges() {
  if (savingAll.value) return
  savingAll.value = true
  try {
    await saveAllDirty()
    if (dirtyCount.value === 0) toast('Усі зміни збережено', 'ok')
  } catch (e) {
    toast(errorText(e), 'bad')
  } finally {
    savingAll.value = false
  }
}
</script>

<template>
  <div class="sp" :class="`sp--${mode}`">
    <nav class="sp-nav" aria-label="Розділи налаштувань">
      <TInput v-model="query" class="sp-search" size="small" :prefix-icon="icon.search" clearable placeholder="Пошук налаштувань" aria-label="Пошук налаштувань" />
      <div class="sp-nav-list">
        <button
          v-for="n in navItems" :key="n.key" type="button" class="sp-nav-item"
          :class="{ 'is-active': !searching && n.key === active, 'is-dim': searching && !sectionMatch(n.key) }"
          :aria-current="!searching && n.key === active ? 'page' : undefined" @click="pick(n.key)"
        >
          <TIcon :icon="n.icon" class="sp-ico" />
          <span>{{ n.title }}</span>
        </button>
      </div>
    </nav>

    <div ref="body" class="sp-body">
      <AppBanner v-if="app.settings.error" tone="bad" class="sp-banner">
        Не вдалося отримати налаштування: {{ app.settings.error }}
        <template #action><TButton type="button" size="small" label="Повторити" @click="refreshSettings()" /></template>
      </AppBanner>

      <TEmpty v-if="nothingFound" :icon="icon.search" :description="`Нічого не знайдено за «${query.trim()}»`" />

      <div v-for="n in navItems" v-show="isShown(n.key)" :key="n.key" class="sp-part">
        <h2 v-if="searching" class="sp-part-title">{{ n.title }}</h2>
        <SettingsSection v-if="n.key === 'general'" v-show="themeMatch || widthMatch" title="Вигляд">
          <SettingRow v-show="themeMatch" :label="THEME_LABEL" :description="THEME_DESC">
            <ChoiceGroup v-model="themeModel" :options="themeOptions" icon-only aria-label="Тема" />
          </SettingRow>
          <SettingRow v-show="widthMatch" :label="WIDTH_LABEL" :description="WIDTH_DESC">
            <ChoiceGroup v-model="widthModel" :options="widthOptions" :aria-label="WIDTH_LABEL" />
          </SettingRow>
        </SettingsSection>
        <SettingsSection
          v-for="g in groupsOf(n.key)" v-show="groupMatch(g.items)" :key="g.title ?? ''"
          :title="g.title ?? (n.key === 'general' ? 'Застосунок' : null)"
        >
          <SettingItem v-for="s in g.items" :key="s.name" :setting="s" />
        </SettingsSection>
      </div>

      <div class="sp-foot">
        <span v-if="dirtyCount > 0 && !allDirtyValid" class="small bad-text grow">Виправте поля, позначені червоним, перш ніж зберігати</span>
        <span v-else-if="dirtyCount > 0" class="small muted grow">Незбережених змін: {{ dirtyCount }}</span>
        <span v-else class="small muted grow">Незбережених змін немає</span>
        <TButton v-if="dirtyCount > 0" type="button" size="small" label="Скасувати зміни" :disabled="savingAll" @click="cancelAllDirty()" />
        <TButton
          type="button" size="small" variant="accent" :label="savingAll ? 'Збереження…' : 'Зберегти все'"
          :disabled="dirtyCount === 0 || !allDirtyValid || savingAll" @click="saveAllChanges"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.sp { display: grid; grid-template-columns: 232px minmax(0, 1fr); min-height: 0; }
.sp-nav { display: flex; flex-direction: column; gap: var(--t-space-3); padding: var(--t-space-3); min-width: 0; }
.sp-nav-list { display: flex; flex-direction: column; gap: 2px; }
.sp-nav-item {
  display: flex; align-items: center; gap: var(--t-space-3); padding: 7px var(--t-space-3); border: 0; border-radius: var(--t-radius-default);
  background: transparent; color: var(--t-color-text); font: inherit; font-weight: var(--t-font-weight-medium); text-align: left; cursor: pointer;
}
.sp-nav-item:hover { background: var(--t-color-hover); }
.sp-nav-item:focus-visible { outline: 2px solid var(--t-color-focus-ring); outline-offset: -2px; }
.sp-nav-item.is-active { background: var(--t-color-hover); box-shadow: inset 0 0 0 1px var(--t-color-border); }
.sp-nav-item.is-dim { opacity: 0.5; }
.sp-ico { width: 18px; height: 18px; flex-shrink: 0; color: var(--t-color-text-muted); }
.sp-nav-item.is-active .sp-ico { color: var(--t-color-accent); }
.sp-body { display: flex; flex-direction: column; min-width: 0; padding: var(--t-space-6) var(--t-space-6) 0; }
.sp-banner { margin-bottom: var(--t-space-4); }
.sp-part { margin-bottom: var(--t-space-6); }
.sp-part-title { font-size: var(--t-font-size-small); font-weight: var(--t-font-weight-semibold); color: var(--t-color-text-muted); text-transform: uppercase; letter-spacing: 0.04em; margin: 0 0 var(--t-space-3); }
.sp-foot {
  position: sticky; bottom: 0; z-index: 1; margin: auto calc(-1 * var(--t-space-6)) 0; padding: var(--t-space-3) var(--t-space-6);
  background: var(--t-color-surface); border-top: 1px solid var(--t-color-border); display: flex; align-items: center; gap: var(--t-space-3);
}

/* Вікно: висота задана модалкою, прокручуються окремо навігація і вміст. */
.sp--modal { height: 100%; }
.sp--modal .sp-nav { background: var(--t-color-bg); border-right: 1px solid var(--t-color-border); overflow: auto; }
.sp--modal .sp-body { overflow: auto; }

/* Сторінка: вміст — у картці, навігація липка. */
.sp--page { border: 1px solid var(--t-color-border); border-radius: var(--t-radius-large); background: var(--t-color-surface); }
.sp--page .sp-nav { position: sticky; top: 0; align-self: start; border-right: 1px solid var(--t-color-border); }
.sp--page .sp-foot { border-radius: 0 0 var(--t-radius-large) 0; }

/* Вузько: навігація — горизонтальна смуга над вмістом. */
@media (max-width: 760px) {
  .sp { grid-template-columns: 1fr; }
  .sp-nav, .sp--modal .sp-nav, .sp--page .sp-nav { position: static; border-right: 0; border-bottom: 1px solid var(--t-color-border); }
  .sp-nav-list { flex-direction: row; overflow-x: auto; }
  .sp-nav-item { flex: none; }
  .sp--modal { display: flex; flex-direction: column; }
  .sp--modal .sp-nav { overflow: visible; }
  .sp--modal .sp-body { flex: 1 1 auto; min-height: 0; }
  .sp-body { padding: var(--t-space-4) var(--t-space-4) 0; }
  .sp-foot { margin: auto calc(-1 * var(--t-space-4)) 0; padding: var(--t-space-3) var(--t-space-4); flex-wrap: wrap; }
}
</style>
