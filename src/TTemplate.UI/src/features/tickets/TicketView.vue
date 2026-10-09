<script setup lang="ts">
// Сторінка заявки (#/tickets/:id): ліворуч — обговорення й дії, праворуч — перегляд (знімок сторінки й варіанти від Claude у sandbox-iframe).
// Кожна дія = коментар + рішення: «Ще ітерація» (Ctrl+Enter), «Погодити» відкритий варіант, «Дрібне — просто виправ», «Зроблено», «Відхилити»;
// або лише коментар. Коментар привʼязується до варіанта, відкритого в перегляді.
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { errorText } from '../../api/http'
import AppBanner from '../../components/AppBanner.vue'
import { formatDate } from '../../lib/format'
import { toast } from '../../state/app'
import { ticketsApi, type TicketAction, type TicketDetail } from './api'
import { groupOf, severityLabel, statusTag, groupLabel, subStatusLabel, typeLabel, typeTag } from './lib/status'

const route = useRoute()
const router = useRouter()
const id = computed(() => String(route.params.id))

const detail = ref<TicketDetail | null>(null)
const error = ref<string | null>(null)
const busy = ref(false)
const message = ref('')
const threadEl = ref<HTMLElement | null>(null)

async function load() {
  try {
    detail.value = await ticketsApi.get(id.value)
    error.value = null
  } catch (e) {
    error.value = errorText(e)
  }
}
// Claude пише у файли напряму — поки сторінка відкрита, підтягуємо зміни раз на 5 с.
let timer: ReturnType<typeof setInterval> | null = null
onMounted(() => { void load(); timer = setInterval(() => { if (!busy.value) void load() }, 5000) })
onBeforeUnmount(() => { if (timer) clearInterval(timer) })
watch(id, () => { detail.value = null; tab.value = 'snapshot'; void load() })
watch(() => detail.value?.messages.length, () => void nextTick(() => threadEl.value?.scrollTo({ top: threadEl.value.scrollHeight })))

const t = computed(() => detail.value?.ticket ?? null)
const variants = computed(() => detail.value?.variants ?? [])
const closed = computed(() => !!detail.value && groupOf(detail.value.status) === 'closed')

// ---------- перегляд ----------
const tab = ref<string>('snapshot')
const tabs = computed(() => [
  ...(detail.value?.hasSnapshot ? [{ value: 'snapshot', label: 'Оригінал' }] : []),
  ...variants.value.map((v) => ({ value: v.slug, label: v.title })),
])
// Нові варіанти — показуємо останній.
watch(() => variants.value.length, (n, prev) => { if (n > (prev ?? 0) && prev !== undefined) tab.value = variants.value[n - 1].slug })
watch(tabs, (list) => { if (list.length && !list.some((x) => x.value === tab.value)) tab.value = String(list[0].value) }, { immediate: true })
const openVariant = computed(() => variants.value.find((v) => v.slug === tab.value) ?? null)
const frameUrl = computed(() => (openVariant.value ? ticketsApi.variantUrl(id.value, openVariant.value.slug) : ticketsApi.snapshotUrl(id.value)))
const variantTitle = (slug?: string | null) => variants.value.find((v) => v.slug === slug)?.title ?? slug

// ---------- дії ----------
async function act(action: TicketAction) {
  if (busy.value) return
  busy.value = true
  try {
    await ticketsApi.action(id.value, action, openVariant.value?.slug, message.value)
    message.value = ''
    await load()
  } catch (e) {
    toast(errorText(e), 'bad')
  } finally {
    busy.value = false
  }
}

async function commentOnly() {
  if (busy.value || !message.value.trim()) return
  busy.value = true
  try {
    await ticketsApi.comment(id.value, message.value, openVariant.value?.slug)
    message.value = ''
    await load()
  } catch (e) {
    toast(errorText(e), 'bad')
  } finally {
    busy.value = false
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); void act('iterate') }
}

async function setSeverity(v: string) {
  try {
    await ticketsApi.meta(id.value, { severity: v as 'low' })
    await load()
  } catch (e) {
    toast(errorText(e), 'bad')
  }
}

const context = computed(() => {
  const c = t.value
  if (!c) return []
  return [
    ['Маршрут', c.route], ['Селектор', c.selector], ['Компоненти', c.components], ['Файл', c.componentFile],
    ['Текст елемента', c.elementText], ['Вікно', c.viewport], ['Тема', c.theme],
  ].filter((x): x is [string, string] => !!x[1])
})
</script>

<template>
  <section class="page">
    <header class="page-head">
      <div class="grow">
        <h1>{{ detail?.caption || t?.text || 'Заявка' }}</h1>
        <p class="row wrap small muted">
          <template v-if="detail">
            <TTag :variant="statusTag(detail.status)" size="small">{{ groupLabel(detail.status) }} · {{ subStatusLabel(detail.status) }}</TTag>
            <TTag v-if="detail.type" :variant="typeTag(detail.type)" size="small">{{ typeLabel(detail.type) }}</TTag>
            <span>запитано: {{ t?.scope === 'variants' ? 'варіанти' : t?.scope === 'fix' ? 'фікс' : 'на розсуд Claude' }}</span>
            <span>{{ formatDate(t?.createdAt) }}</span>
          </template>
          <span class="mono">{{ id }}</span>
        </p>
      </div>
      <TButton type="button" label="До списку" @click="router.push('/tickets')" />
    </header>

    <AppBanner v-if="error && !detail" tone="bad">
      Не вдалося відкрити заявку: {{ error }}
      <template #action><TButton type="button" size="small" label="Повторити" @click="load" /></template>
    </AppBanner>
    <p v-else-if="!detail" class="muted">Завантаження…</p>

    <template v-else>
      <AppBanner v-if="detail.status === 'variants-ready'" tone="warn">Варіанти готові — перегляньте праворуч і погодьте один або попросіть ще ітерацію.</AppBanner>
      <AppBanner v-else-if="detail.status === 'simple-proposed'" tone="warn">Claude пропонує фікс — гляньте обговорення й натисніть «Дрібне — просто виправ», якщо згодні.</AppBanner>
      <AppBanner v-else-if="detail.status === 'applied'" tone="warn">Зміни внесено — перевірте й закрийте заявку («Зроблено») або напишіть, що не так.</AppBanner>
      <AppBanner v-else-if="groupOf(detail.status) === 'claude'">Чекає на Claude: попросіть його «опрацюй заявки» (скіл /tickets).</AppBanner>

      <div class="split" :class="{ 'with-preview': tabs.length }">
        <div class="col">
          <TCard header="Обговорення">
            <div class="stack">
              <ol ref="threadEl" class="thread">
                <li class="msg user">
                  <div class="row wrap xs muted">
                    <TTag variant="blue" size="small">{{ t?.author || 'користувач' }}</TTag>
                    <span>{{ formatDate(t?.createdAt) }}</span><span>· текст заявки</span>
                  </div>
                  <div class="msg-text small">{{ t?.text || '(без опису)' }}</div>
                </li>
                <li v-for="(m, i) in detail.messages" :key="i" class="msg" :class="m.role">
                  <div class="row wrap xs muted">
                    <TTag :variant="m.role === 'claude' ? 'teal' : 'blue'" size="small">{{ m.role === 'claude' ? 'Claude' : m.author || 'користувач' }}</TTag>
                    <span>{{ formatDate(m.ts) }}</span>
                    <span v-if="m.variant">до «{{ variantTitle(m.variant) }}»</span>
                  </div>
                  <div class="msg-text small">{{ m.text }}</div>
                </li>
              </ol>

              <div v-if="!closed" class="compose">
                <TTextarea v-model="message" :rows="2" aria-label="Коментар" placeholder="Коментар — піде разом із дією (Ctrl+Enter — «Ще ітерація»)" @keydown="onKeydown" />
                <div class="row wrap">
                  <span v-if="openVariant" class="xs muted">Коментар до «{{ openVariant.title }}»</span>
                  <span class="grow" />
                  <TButton v-if="openVariant" type="button" size="small" variant="success" mode="plain" :label="`Погодити «${openVariant.title}»`" :disabled="busy" @click="act('approve')" />
                  <TButton type="button" size="small" variant="accent" label="Ще ітерація" :disabled="busy" @click="act('iterate')" />
                  <TDropdown trigger="click" placement="top-end" :close-on-panel-click="true">
                    <template #trigger="{ triggerProps }">
                      <TButton v-bind="triggerProps" type="button" size="small" icon="system-uicons:menu-horizontal" aria-label="Інші дії" title="Інші дії" :disabled="busy" />
                    </template>
                    <template #default>
                      <div class="menu" role="menu">
                        <button type="button" class="menu-item" role="menuitem" @click="act('simple')">Дрібне — просто виправ</button>
                        <button type="button" class="menu-item" role="menuitem" @click="act('done')">Зроблено — закрити</button>
                        <button type="button" class="menu-item danger" role="menuitem" @click="act('reject')">Відхилити</button>
                        <div class="sep" />
                        <button type="button" class="menu-item" role="menuitem" :disabled="!message.trim()" @click="commentOnly">Лише коментар, без рішення</button>
                      </div>
                    </template>
                  </TDropdown>
                </div>
              </div>
            </div>
          </TCard>

          <TCard header="Деталі">
            <div class="stack tight small">
              <div class="row">
                <span class="muted key">Важливість</span>
                <TSelect
                  :model-value="detail.severity ?? 'none'" :options="[{ value: 'none', label: 'Не вказано' }, { value: 'low', label: 'Низька' }, { value: 'medium', label: 'Середня' }, { value: 'high', label: 'Висока' }]"
                  value-key="value" label-key="label" value-mode="value" :clearable="false" :searchable="false" size="small" style="width: 160px"
                  aria-label="Важливість" @update:model-value="setSeverity(String($event))"
                />
                <span class="muted xs">{{ severityLabel(detail.severity) }}</span>
              </div>
              <div v-for="[k, v] in context" :key="k" class="row ctx-row">
                <span class="muted key">{{ k }}</span><span class="mono grow ctx-val">{{ v }}</span>
              </div>
              <div class="row ctx-row"><span class="muted key">Тека</span><span class="mono grow ctx-val">{{ detail.dir }}</span></div>
            </div>
          </TCard>
        </div>

        <div v-if="tabs.length" class="col">
          <TCard header="Перегляд">
            <div class="stack">
              <div class="row wrap">
                <TTabs v-model="tab" :tabs="tabs" size="small" aria-label="Оригінал і варіанти" class="grow" />
                <a class="small" :href="frameUrl" target="_blank" rel="noopener noreferrer">Відкрити окремо</a>
              </div>
              <p v-if="openVariant?.summary" class="small muted">{{ openVariant.summary }}</p>
              <!-- sandbox без allow-scripts: HTML знімка/варіанта не виконує JS. allow-same-origin потрібен, бо з порожнім sandbox деякі браузери
                   (вбудований Chromium) не малюють сторінку; походження й так непрозоре через заголовок CSP sandbox від бекенду. -->
              <iframe :key="frameUrl" :src="frameUrl" sandbox="allow-same-origin" referrerpolicy="no-referrer" class="frame" title="Перегляд" />
            </div>
          </TCard>
        </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.split { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--t-space-4); }
.split.with-preview { grid-template-columns: minmax(320px, 2fr) minmax(0, 3fr); }
.col { display: flex; flex-direction: column; gap: var(--t-space-4); min-width: 0; }
.thread { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--t-space-3); max-height: 56vh; overflow: auto; }
.msg { display: flex; flex-direction: column; gap: 4px; padding: var(--t-space-2) var(--t-space-3); border-radius: var(--t-radius-medium); background: var(--t-color-surface-2); }
.msg.claude { background: color-mix(in srgb, var(--t-color-accent) 7%, var(--t-color-surface)); }
.msg-text { white-space: pre-wrap; overflow-wrap: anywhere; }
.compose { display: flex; flex-direction: column; gap: var(--t-space-2); }
.key { width: 110px; flex: none; }
.ctx-row { align-items: flex-start; }
.ctx-val { overflow-wrap: anywhere; white-space: pre-wrap; }
.frame { width: 100%; height: 70vh; border: 1px solid var(--t-color-border); border-radius: var(--t-radius-medium); background: #fff; }
.menu { display: flex; flex-direction: column; min-width: 220px; padding: var(--t-space-1); background: var(--t-color-surface); border: 1px solid var(--t-color-border); border-radius: var(--t-radius-medium); box-shadow: var(--t-shadow-3); }
.menu-item {
  display: flex; align-items: center; padding: 6px var(--t-space-3); border: none; background: none; text-align: left;
  font: inherit; font-size: var(--t-font-size-small); color: var(--t-color-text); border-radius: var(--t-radius-default); cursor: pointer;
}
.menu-item:hover:not(:disabled) { background: var(--t-color-surface-2); }
.menu-item:disabled { color: var(--t-color-text-muted); cursor: not-allowed; }
.menu-item.danger { color: var(--t-color-danger); }
.sep { height: 1px; margin: 4px 2px; background: var(--t-color-border); }
@media (max-width: 1000px) {
  .split.with-preview { grid-template-columns: minmax(0, 1fr); }
}
</style>
