<script setup lang="ts">
// Одне налаштування з бекенду ([Setting] на властивості): поле за типом — string → TInput, int → TInput type=number, bool → TSwitch,
// list → TTextarea («рядок = елемент»), secret → пароль (порожнє поле = не змінювати; значення сервер не віддає).
// «Зберегти» — лише при зміні, «Скинути» — лише якщо значення змінене. Чернетка реєструється в lib/settingsDraft.ts.
import { computed, onUnmounted, ref, useId, watch } from 'vue'
import { coreApi, type SettingValue, type SettingView } from '../../api/core'
import { ApiError, errorText } from '../../api/http'
import { registerDraftSection } from '../../lib/settingsDraft'
import { matchesSearch, useSettingsSearch } from '../../lib/settingsSearch'
import { refreshSettings, toast } from '../../state/app'
import SettingRow from './SettingRow.vue'

const props = defineProps<{ setting: SettingView }>()

const draft = ref<string | boolean>('')
const listText = (v: SettingValue) => (Array.isArray(v) ? v.join('\n') : '')
const serverText = computed(() => {
  const s = props.setting
  if (s.secret) return ''
  if (s.type === 'list') return listText(s.value)
  return s.value == null ? '' : String(s.value)
})

function loadFromServer() {
  draft.value = props.setting.type === 'bool' ? props.setting.value === true : serverText.value
}
// Джерело — JSON (примітив): refreshSettings() підміняє обʼєкти, а чернетку треба скидати лише при реальній зміні цього налаштування.
watch(() => JSON.stringify([props.setting.name, props.setting.value, props.setting.isCustomValue]), loadFromServer, { immediate: true })

const saving = ref(false)
const fieldError = ref<string | null>(null)

const dirty = computed(() => {
  if (props.setting.secret) return draft.value !== ''
  if (props.setting.type === 'bool') return draft.value !== props.setting.value
  return draft.value !== serverText.value
})
const valid = () => props.setting.type !== 'int' || /^-?\d+$/.test(String(draft.value).trim())

function valueForSend(): SettingValue {
  if (props.setting.type === 'bool') return draft.value as boolean
  if (props.setting.type === 'int') return Number(String(draft.value).trim())
  if (props.setting.type === 'list') return String(draft.value).split('\n').map((s) => s.trim()).filter(Boolean)
  return String(draft.value)
}

async function save() {
  if (!dirty.value || saving.value) return
  if (!valid()) { fieldError.value = 'Очікується ціле число'; throw new Error(fieldError.value) }
  saving.value = true
  fieldError.value = null
  try {
    await coreApi.setSetting(props.setting.name, valueForSend())
    await refreshSettings()
  } catch (e) {
    fieldError.value = e instanceof ApiError ? (e.fields.value ?? e.message) : errorText(e)
    throw e
  } finally {
    saving.value = false
  }
}

async function saveOne() {
  try {
    await save()
    toast('Налаштування збережено', 'ok')
  } catch { /* помилка — під полем */ }
}

async function resetToDefault() {
  if (!props.setting.isCustomValue || saving.value) return
  saving.value = true
  fieldError.value = null
  try {
    await coreApi.resetSetting(props.setting.name)
    toast('Скинуто до типового значення', 'ok')
    await refreshSettings()
  } catch (e) {
    fieldError.value = errorText(e)
  } finally {
    saving.value = false
  }
}

const unregister = registerDraftSection({
  id: `setting:${props.setting.name}`,
  dirty: () => dirty.value,
  valid,
  save,
  cancel: () => { loadFromServer(); fieldError.value = null },
})
onUnmounted(unregister)

const boolModel = computed(() => draft.value === true)
const strModel = computed(() => (typeof draft.value === 'string' ? draft.value : ''))
function setBool(v: boolean) { draft.value = v }
function setStr(v: string | number) { draft.value = String(v) }

const id = useId()
const search = useSettingsSearch()
const visible = computed(() => matchesSearch(search.value, props.setting.caption, props.setting.description, props.setting.name))
const secretPlaceholder = computed(() => (props.setting.hasValue ? '········ (порожньо — не змінювати)' : 'не задано'))
</script>

<template>
  <SettingRow v-show="visible" :label="setting.caption" :description="setting.description" :error="fieldError" :label-for="setting.type === 'bool' ? undefined : id">
    <template #tag><TTag v-if="setting.isCustomValue" variant="blue" size="small">змінено</TTag></template>
    <TSwitch v-if="setting.type === 'bool'" :model-value="boolModel" :aria-label="setting.caption" @update:model-value="setBool" />
    <TTextarea
      v-else-if="setting.type === 'list'" :id="id" :model-value="strModel" :rows="3" class="field mono"
      placeholder="рядок = один елемент" :aria-label="setting.caption" @update:model-value="setStr"
    />
    <TInput
      v-else-if="setting.secret" :id="id" :model-value="strModel" type="password" class="field mono" size="small"
      :placeholder="secretPlaceholder" autocomplete="new-password" :aria-label="setting.caption" @update:model-value="setStr"
    />
    <TInput
      v-else :id="id" :model-value="strModel" :type="setting.type === 'int' ? 'number' : 'text'" class="field" size="small"
      spellcheck="false" :aria-label="setting.caption" @update:model-value="setStr"
    />
    <span class="actions">
      <TButton type="button" size="small" mode="text" variant="success" icon="system-uicons:check" :disabled="!dirty || saving" title="Зберегти" aria-label="Зберегти" @click="saveOne" />
      <TButton type="button" size="small" mode="text" variant="neutral" icon="system-uicons:refresh-alt" :disabled="!setting.isCustomValue || saving" title="Скинути до типового" aria-label="Скинути до типового" @click="resetToDefault" />
    </span>
  </SettingRow>
</template>

<style scoped>
.field { width: 260px; max-width: 100%; }
.actions { display: inline-flex; flex: none; gap: 2px; }
@media (max-width: 720px) { .field { width: auto; flex: 1 1 auto; } }
</style>
