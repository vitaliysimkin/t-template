<script setup lang="ts">
// Діалог заявки після вибору елемента (або «Без елемента»): «Що не так» (обовʼязково), «Що потрібно від Claude» (мінімум: фікс / варіанти),
// «Важливість». Breadcrumbs — предки вибраного елемента (клік міняє вибір; TicketsTool підсвічує новий вибір під діалогом).
// Знімок сторінки іде полем snapshotHtml з міткою вибраного елемента; завеликий — не надсилається.
import { computed, nextTick, onMounted, ref } from 'vue'
import { errorText } from '../../api/http'
import AppBanner from '../../components/AppBanner.vue'
import ChoiceGroup from '../../components/ChoiceGroup.vue'
import { formatBytes } from '../../lib/format'
import { toast } from '../../state/app'
import { ticketsApi, type TicketContext, type TicketScope, type TicketSeverity } from './api'
import { elementLabel, elementParts, elementTag, ownerComponent, ticketSelection } from './lib/context'
import { SNAPSHOT_MAX_BYTES, snapshotHtml } from './lib/pageSnapshot'
import { SCOPE_OPTIONS, SEVERITY_OPTIONS } from './lib/status'

const props = defineProps<{ context: TicketContext; hasElement: boolean; onSaved?: () => void }>()
const emit = defineEmits<{ close: [] }>()
const root = ref<HTMLElement | null>(null)

const text = ref('')
const severity = ref<string>('low')
const scope = ref<string>('fix')
const busy = ref(false)
const error = ref<string | null>(null)
const validation = ref<string | null>(null)

// ---------- вибір (breadcrumbs) ----------
const CRUMBS = 6
const selection = ticketSelection
const allCrumbs = ref(false)
const current = computed(() => {
  const sel = selection.value
  return props.hasElement && sel ? sel.chain[sel.index] ?? null : null
})
const ctx = computed<TicketContext>(() => (current.value?.isConnected ? { ...props.context, ...elementParts(current.value) } : props.context))

const crumbs = computed(() => {
  const sel = selection.value
  if (!props.hasElement || !sel) return []
  let prevComp: string | undefined
  return sel.chain.map((el, index) => {
    const comp = ownerComponent(el)
    // Імʼя компонента — лише там, де воно змінюється вздовж ланцюжка.
    const label = comp && comp !== prevComp ? `${comp} · ${elementTag(el)}` : elementTag(el)
    prevComp = comp
    return { index, text: label, title: elementLabel(el) }
  })
})
const hiddenCrumbs = computed(() => (allCrumbs.value ? 0 : Math.max(0, crumbs.value.length - CRUMBS)))
function select(index: number) {
  const sel = selection.value
  if (sel && index !== sel.index) selection.value = { ...sel, index }
}

const target = computed(() => (props.hasElement
  ? ctx.value.components?.split(' > ').slice(-2).join(' > ') || ctx.value.selector || 'елемент'
  : 'уся сторінка'))
const snapshot = computed(() => selection.value?.snapshot ?? null)

onMounted(() => void nextTick(() => root.value?.querySelector<HTMLTextAreaElement>('textarea')?.focus()))

async function submit() {
  if (busy.value) return
  if (!text.value.trim()) {
    validation.value = 'Опишіть, що не так — поле не може бути порожнім.'
    return
  }
  validation.value = null
  error.value = null
  busy.value = true
  try {
    const sel = selection.value
    const { id } = await ticketsApi.create({
      ...ctx.value,
      text: text.value.trim(),
      severity: severity.value as TicketSeverity,
      scope: scope.value as TicketScope,
      snapshotHtml: snapshotHtml(sel?.snapshot, current.value && sel ? sel.index : null),
    })
    toast(`Заявку ${id} записано`, 'ok')
    props.onSaved?.()
    emit('close')
  } catch (e) {
    error.value = `Не записано: ${errorText(e)}`
  } finally {
    busy.value = false
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); void submit() }
}
</script>

<template>
  <div ref="root" class="dlg">
    <form id="ticket-form" class="dlg-body" novalidate @submit.prevent="submit">
      <p class="small muted ctx">
        <span>Про: <b>{{ target }}</b></span>
        <span class="ellipsis">Сторінка: <code>{{ ctx.route || '/' }}</code></span>
        <span v-if="ctx.selector" class="ellipsis" :title="ctx.selector">Селектор: <code>{{ ctx.selector }}</code></span>
      </p>
      <nav v-if="crumbs.length" class="crumbs xs" aria-label="Вибраний елемент і його предки">
        <button v-if="hiddenCrumbs" type="button" class="crumb" :title="`Показати ще ${hiddenCrumbs} предків`" @click="allCrumbs = true">…</button>
        <template v-for="c in crumbs.slice(hiddenCrumbs)" :key="c.index">
          <span v-if="c.index > hiddenCrumbs" class="sep" aria-hidden="true">›</span>
          <button
            type="button" class="crumb mono" :class="{ active: c.index === selection?.index }" :title="c.title"
            :aria-current="c.index === selection?.index ? 'true' : undefined" @click="select(c.index)"
          >{{ c.text }}</button>
        </template>
      </nav>
      <p v-if="snapshot?.tooLarge" class="xs warn-text">
        Знімок сторінки завеликий ({{ formatBytes(snapshot.bytes) }}, ліміт {{ formatBytes(SNAPSHOT_MAX_BYTES) }}) — не надсилається.
      </p>
      <p v-else-if="snapshot" class="xs muted">Разом із заявкою піде знімок сторінки ({{ formatBytes(snapshot.bytes) }}).</p>
      <TFormField label="Що не так">
        <TTextarea v-model="text" :rows="5" placeholder="Що саме не так і як мало б бути (Ctrl+Enter — зберегти)" aria-label="Що не так" @keydown="onKeydown" />
      </TFormField>
      <div class="choice-row">
        <TFormField label="Що потрібно від Claude (мінімум)">
          <ChoiceGroup v-model="scope" :options="SCOPE_OPTIONS" aria-label="Що потрібно від Claude" />
        </TFormField>
        <TFormField label="Важливість">
          <ChoiceGroup v-model="severity" :options="SEVERITY_OPTIONS" aria-label="Важливість" />
        </TFormField>
      </div>
      <p v-if="validation" class="small bad-text">{{ validation }}</p>
      <AppBanner v-if="error" tone="bad" role="alert">{{ error }}</AppBanner>
    </form>
    <footer class="dlg-foot">
      <TButton type="button" label="Скасувати" @click="emit('close')" />
      <TButton type="submit" form="ticket-form" variant="success" :disabled="busy" :label="busy ? 'Запис…' : 'Зберегти'" />
    </footer>
  </div>
</template>

<style scoped>
.ctx { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.ctx code { font-size: inherit; }
.crumbs { display: flex; flex-wrap: wrap; align-items: center; gap: 2px 4px; }
.crumb {
  border: 1px solid transparent; border-radius: 3px; background: none; padding: 1px 4px; font: inherit; color: var(--t-color-text-muted);
  cursor: pointer; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.crumb:hover { color: var(--t-color-text); border-color: var(--t-color-border); }
.crumb.active { color: var(--t-color-accent); border-color: var(--t-color-accent); }
.sep { color: var(--t-color-text-muted); }
.choice-row { display: flex; flex-wrap: wrap; gap: 0 var(--t-space-5); }
.choice-row > * { flex: 1 1 max-content; width: auto; min-width: 0; }
</style>
