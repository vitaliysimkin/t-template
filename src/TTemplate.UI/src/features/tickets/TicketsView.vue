<script setup lang="ts">
// «Заявки» (#/tickets): усі заявки зі статусами «варіанти від Claude → обговорення → погодження → застосування».
// Фільтр — група «чия черга», пошук — за текстом/назвою/id/маршрутом; обидва в query (#/tickets?status=&q=). Рядок → #/tickets/:id.
import type { TTableColumn } from '@vitaliysimkin/t-components'
import { useMediaQuery } from '@vueuse/core'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { errorText } from '../../api/http'
import AppBanner from '../../components/AppBanner.vue'
import ChoiceGroup from '../../components/ChoiceGroup.vue'
import { formatDate } from '../../lib/format'
import { ticketsApi, type TicketListItem } from './api'
import { TICKETS_HOTKEY } from './lib/context'
import { GROUP_ORDER, groupLabel, groupOf, severityLabel, statusTag, subStatusLabel, typeLabel, typeTag, type TicketGroup } from './lib/status'

type Row = TicketListItem & Record<string, unknown>
type Filter = 'active' | TicketGroup | 'all'
const FILTERS: Filter[] = ['active', 'user', 'claude', 'closed', 'all']

const route = useRoute()
const router = useRouter()
const mobile = useMediaQuery('(max-width: 768px)')
const queryStr = (v: unknown) => (typeof v === 'string' ? v : '')

const initial = queryStr(route.query.status) as Filter
const statusFilter = ref<string>(FILTERS.includes(initial) ? initial : 'active')
const search = ref(queryStr(route.query.q))

const items = ref<TicketListItem[] | null>(null)
const dir = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

async function load() {
  loading.value = true
  try {
    const r = await ticketsApi.list()
    items.value = r.items
    dir.value = r.path
    error.value = null
  } catch (e) {
    error.value = errorText(e)
  } finally {
    loading.value = false
  }
}
onMounted(load)

watch([statusFilter, search], () => {
  void router.replace({ query: { ...(statusFilter.value !== 'active' ? { status: statusFilter.value } : {}), ...(search.value ? { q: search.value } : {}) } })
})

const counts = computed(() => {
  const c: Record<TicketGroup, number> = { user: 0, claude: 0, closed: 0 }
  for (const t of items.value ?? []) c[groupOf(t.status)]++
  return c
})
const withCount = (label: string, n: number) => (n ? `${label} (${n})` : label)
const filterOptions = computed(() => [
  { value: 'active', label: withCount('Активні', counts.value.user + counts.value.claude) },
  { value: 'user', label: withCount('У вас', counts.value.user) },
  { value: 'claude', label: withCount('У Claude', counts.value.claude) },
  { value: 'closed', label: withCount('Закрито', counts.value.closed) },
  { value: 'all', label: 'Усі' },
])

const rows = computed<Row[]>(() => {
  const q = search.value.trim().toLowerCase()
  const f = statusFilter.value
  return (items.value ?? [])
    .filter((t) => {
      const g = groupOf(t.status)
      if (f === 'active' && g === 'closed') return false
      if (f !== 'active' && f !== 'all' && g !== f) return false
      return !q || [t.text, t.caption, t.id, t.route].some((s) => (s ?? '').toLowerCase().includes(q))
    })
    .sort((a, b) => GROUP_ORDER.indexOf(groupOf(a.status)) - GROUP_ORDER.indexOf(groupOf(b.status))) as Row[]
})

const title = (t: TicketListItem) => t.caption || t.text || '(без опису)'
function open(t: TicketListItem) { void router.push(`/tickets/${encodeURIComponent(t.id)}`) }

const columns: TTableColumn<Row>[] = [
  { key: 'status', label: 'Статус' },
  { key: 'severity', label: 'Важливість' },
  { key: 'text', label: 'Заявка' },
  { key: 'variants', label: 'Варіанти', align: 'right' },
  { key: 'createdAt', label: 'Створено' },
]
const emptyText = computed(() => (items.value?.length
  ? 'Нічого не знайдено за фільтром.'
  : `Заявок ще нема. Додайте першу кнопкою з прапорцем у куті (${TICKETS_HOTKEY}).`))
</script>

<template>
  <section class="page">
    <header class="page-head">
      <div class="grow">
        <h1>Заявки</h1>
        <p class="small muted">Варіанти від Claude, обговорення і погодження змін</p>
      </div>
      <TButton type="button" label="Оновити" :disabled="loading" @click="load" />
    </header>

    <div class="filters">
      <ChoiceGroup v-model="statusFilter" :options="filterOptions" aria-label="Чия черга" class="groups" />
      <TInput v-model="search" placeholder="Пошук: текст, id, маршрут…" clearable aria-label="Пошук заявок" class="search" />
    </div>

    <AppBanner v-if="error && !items" tone="bad">
      Не вдалося отримати заявки: {{ error }}
      <template #action><TButton type="button" size="small" label="Повторити" @click="load" /></template>
    </AppBanner>

    <template v-else-if="mobile">
      <TCard v-if="!rows.length"><TEmpty :description="items ? emptyText : 'Завантаження…'" /></TCard>
      <ul v-else class="cards">
        <li v-for="t in rows" :key="t.id">
          <button type="button" class="card-btn" @click="open(t)">
            <span class="row wrap">
              <TTag :variant="statusTag(t.status)" size="small">{{ groupLabel(t.status) }}</TTag>
              <span class="xs muted">{{ subStatusLabel(t.status) }}</span>
              <span v-if="t.severity" class="sev xs" :class="t.severity">{{ severityLabel(t.severity) }}</span>
              <span class="grow" />
              <span class="xs muted nowrap">{{ formatDate(t.createdAt) }}</span>
            </span>
            <span class="t-text">{{ title(t) }}</span>
            <span class="xs muted mono ellipsis">{{ t.id }}<template v-if="t.route"> · {{ t.route }}</template></span>
          </button>
        </li>
      </ul>
    </template>

    <TTable v-else :columns="columns" :rows="rows" row-key="id" :loading="loading && !items">
      <template #loading><span class="muted">Завантаження…</span></template>
      <template #empty><TEmpty :description="emptyText" /></template>
      <template #cell-status="{ row: t }">
        <span class="status-cell">
          <TTag :variant="statusTag(t.status)" size="small" class="nowrap">{{ groupLabel(t.status) }}</TTag>
          <span class="xs muted nowrap">{{ subStatusLabel(t.status) }}</span>
        </span>
      </template>
      <template #cell-severity="{ row: t }"><span class="sev small" :class="t.severity ?? 'none'">{{ severityLabel(t.severity) }}</span></template>
      <template #cell-text="{ row: t }">
        <button type="button" class="t-link" @click="open(t)">
          <span class="row">
            <TTag v-if="t.type" :variant="typeTag(t.type)" size="small">{{ typeLabel(t.type) }}</TTag>
            <span class="t-text" :class="{ caption: t.caption }" :title="t.caption ? `Ваш текст: ${t.text}` : undefined">{{ title(t) }}</span>
          </span>
          <span class="xs muted mono">{{ t.id }}<template v-if="t.route"> · {{ t.route }}</template></span>
        </button>
      </template>
      <template #cell-variants="{ row: t }"><span class="small">{{ t.variants || '—' }}</span></template>
      <template #cell-createdAt="{ row: t }"><span class="small nowrap">{{ formatDate(t.createdAt) }}</span></template>
    </TTable>

    <p v-if="dir" class="xs muted mono path">{{ dir }}</p>
  </section>
</template>

<style scoped>
.filters { display: flex; flex-wrap: wrap; align-items: flex-end; gap: var(--t-space-3); }
.search { width: 280px; }
.groups { flex-wrap: wrap; }
.status-cell { display: flex; flex-direction: column; align-items: flex-start; gap: 2px; }
.t-link {
  display: flex; flex-direction: column; gap: 2px; align-items: flex-start; text-align: left;
  background: none; border: 0; padding: 0; color: inherit; cursor: pointer; max-width: 640px;
}
.t-link:hover .t-text { color: var(--t-color-accent); }
.t-text { overflow-wrap: anywhere; }
.t-text.caption { font-weight: var(--t-font-weight-semibold); }
.sev { display: inline-flex; align-items: center; gap: 5px; white-space: nowrap; }
.sev::before { content: ""; width: 8px; height: 8px; border-radius: 50%; background: var(--t-color-border-strong); flex: none; }
.sev.high::before { background: var(--t-color-danger); }
.sev.medium::before { background: var(--t-color-warning); }
.sev.low::before { background: var(--t-color-info); }
.sev.none { color: var(--t-color-text-muted); }
.path { overflow-wrap: anywhere; }
.cards { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--t-space-2); }
.card-btn {
  width: 100%; display: flex; flex-direction: column; gap: 6px; text-align: left; cursor: pointer; color: inherit; font: inherit;
  padding: var(--t-space-3); border: 1px solid var(--t-color-border); border-radius: var(--t-radius-medium); background: var(--t-color-surface);
}
@media (max-width: 480px) {
  .filters { flex-direction: column; align-items: stretch; }
  .search { width: auto; }
}
</style>
