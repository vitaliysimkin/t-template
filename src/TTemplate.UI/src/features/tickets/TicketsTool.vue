<script setup lang="ts">
// Плаваюча кнопка «Заявки» в куті (+ Ctrl+Shift+E) → режим вибору елемента → TicketDialog → POST /api/tickets.
// Панель кнопки — останні заявки і перехід до списку. Режим вибору: підсвітка елемента під курсором, клік — вибір, Esc — скасування,
// ↑ / Alt+колесо вгору — батьківський, ↓ — назад; «Без елемента» — заявка до сторінки. Події миші/клавіатури перехоплюються на window
// у capture-фазі — клік не доходить до самого елемента. Поки діалог відкритий, вибраний елемент лишається підсвіченим під діалогом.
import { modalManager, useTheme } from '@vitaliysimkin/t-components'
import { useMediaQuery } from '@vueuse/core'
import { computed, markRaw, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { errorText } from '../../api/http'
import { ticketsApi, type TicketListItem } from './api'
import {
  ancestorChain, elementContext, elementLabel, isHotkey, isToolElement, pageContext, selectableParent, TICKETS_ATTR, TICKETS_HOTKEY,
  ticketDialogOpen, ticketSelection,
} from './lib/context'
import { capturePageSnapshot, type PageSnapshot } from './lib/pageSnapshot'
import { groupOf, statusTag, subStatusLabel } from './lib/status'
import TicketDialog from './TicketDialog.vue'

const RECENT = 5
const toolAttr = { [TICKETS_ATTR]: '' }

const mobile = useMediaQuery('(max-width: 768px)')
const { currentTheme } = useTheme()

const panelOpen = ref(false)
const picking = ref(false)
const hovered = ref<Element | null>(null)
let pointed: Element | null = null
/** Шлях підйому: елементи, з яких піднялись до батька (↓ повертає назад). */
let climbed: Element[] = []
const box = ref<{ top: number; left: number; width: number; height: number } | null>(null)
const hoverLabel = ref('')

const items = ref<TicketListItem[] | null>(null)
const loadError = ref<string | null>(null)
const openCount = computed(() => items.value?.filter((t) => groupOf(t.status) === 'user').length ?? 0)

async function loadRecent() {
  try {
    items.value = (await ticketsApi.list()).items
    loadError.value = null
  } catch (e) {
    loadError.value = errorText(e)
  }
}

function togglePanel() {
  panelOpen.value = !panelOpen.value
  if (panelOpen.value) void loadRecent()
}

function snapshot(chain: Element[]): PageSnapshot | null {
  try {
    return capturePageSnapshot(chain, `[${TICKETS_ATTR}]`)
  } catch {
    return null // знімок — додатковий контекст; без нього заявка все одно записується
  }
}

function openDialog(el: Element | null) {
  const theme = String(currentTheme.value)
  // Ланцюжок — від найглибшого елемента шляху підйому, щоб у breadcrumbs можна було повернутись і донизу.
  const deepest = el ? (climbed[0] ?? el) : null
  const chain = deepest ? ancestorChain(deepest) : []
  ticketSelection.value = { chain, index: el ? chain.indexOf(el) : -1, snapshot: snapshot(chain) }
  ticketDialogOpen.value = true
  let modalId = ''
  const close = () => modalManager.closeModal(modalId)
  modalId = modalManager.openModal({
    label: el ? 'Заявка до елемента' : 'Заявка до сторінки',
    contentComponent: markRaw(TicketDialog),
    componentProps: { context: el ? elementContext(el, theme) : pageContext(theme), hasElement: !!el, onClose: close, onSaved: loadRecent },
    blocking: true, blockingDismissible: true, draggable: false, resizable: false, minimizable: false,
    position: 'center', size: { width: 'min(560px, calc(100vw - 24px))', height: 'min-content' },
  })
  const stop = watch(() => modalManager.modals.value.some((m) => m.id === modalId), (open) => {
    if (open) return
    stop()
    ticketDialogOpen.value = false
    if (!picking.value) ticketSelection.value = null
  })
}

// ---------- режим вибору ----------
const BLOCKED = ['pointerdown', 'pointerup', 'mousedown', 'mouseup', 'click', 'dblclick', 'auxclick', 'contextmenu', 'touchstart', 'touchend', 'submit'] as const

function startPicking() {
  if (picking.value) return
  panelOpen.value = false
  picking.value = true
  hovered.value = null
  pointed = null
  climbed = []
  box.value = null
  ticketSelection.value = null
  document.documentElement.classList.add('tkt-picking')
  window.addEventListener('pointermove', onMove, true)
  window.addEventListener('wheel', onWheel, { capture: true, passive: false })
  listenBox()
  for (const t of BLOCKED) window.addEventListener(t, onBlocked, true)
}

function stopPicking() {
  if (!picking.value) return
  picking.value = false
  hovered.value = null
  pointed = null
  document.documentElement.classList.remove('tkt-picking')
  window.removeEventListener('pointermove', onMove, true)
  window.removeEventListener('wheel', onWheel, true)
  if (!ticketSelection.value) unlistenBox()
  for (const t of BLOCKED) window.removeEventListener(t, onBlocked, true)
}

function pickPage() {
  stopPicking()
  panelOpen.value = false
  climbed = []
  openDialog(null)
}

function targetAt(x: number, y: number): Element | null {
  const el = document.elementFromPoint(x, y)
  return el && !isToolElement(el) && el !== document.documentElement && el !== document.body ? el : null
}

function onMove(e: PointerEvent) {
  const el = targetAt(e.clientX, e.clientY)
  if (el === pointed) return
  pointed = el
  climbed = []
  hovered.value = el
}

function climb(up: boolean) {
  const cur = hovered.value
  if (!cur) return
  if (up) {
    const parent = selectableParent(cur)
    if (!parent) return
    climbed.push(cur)
    hovered.value = parent
    return
  }
  const back = climbed.pop()
  if (back) { hovered.value = back; return }
  const child = Array.from(cur.children).find((c) => !isToolElement(c))
  if (child) hovered.value = child
}

function onWheel(e: WheelEvent) {
  if (!e.altKey || !e.deltaY) return
  e.preventDefault()
  e.stopPropagation()
  climb(e.deltaY < 0)
}

/** Що підсвічено: у режимі вибору — елемент під курсором, поки відкритий діалог — chain[index]. */
const shown = computed<Element | null>(() => {
  if (picking.value) return hovered.value
  const sel = ticketSelection.value
  return sel && ticketDialogOpen.value ? sel.chain[sel.index] ?? null : null
})

function updateBox() {
  const el = shown.value
  if (!el || !el.isConnected) { box.value = null; return }
  const r = el.getBoundingClientRect()
  box.value = { top: r.top, left: r.left, width: r.width, height: r.height }
  hoverLabel.value = elementLabel(el)
}

let boxListening = false
function listenBox() {
  if (boxListening) return
  boxListening = true
  window.addEventListener('scroll', updateBox, true)
  window.addEventListener('resize', updateBox, true)
}
function unlistenBox() {
  if (!boxListening) return
  boxListening = false
  window.removeEventListener('scroll', updateBox, true)
  window.removeEventListener('resize', updateBox, true)
}

watch(shown, (el) => {
  if (el) listenBox()
  else if (!picking.value) unlistenBox()
  updateBox()
})

function onBlocked(e: Event) {
  if (isToolElement(e.target as Element | null)) return
  e.preventDefault()
  e.stopPropagation()
  e.stopImmediatePropagation()
  if (e.type !== 'click') return
  const me = e as MouseEvent
  const el = hovered.value ?? targetAt(me.clientX, me.clientY) ?? (e.target instanceof Element && !isToolElement(e.target) ? e.target : null)
  if (!el) return
  stopPicking()
  openDialog(el)
}

function onKeydown(e: KeyboardEvent) {
  if (isHotkey(e)) {
    e.preventDefault()
    e.stopPropagation()
    if (picking.value) stopPicking()
    else startPicking()
    return
  }
  if (!picking.value) return
  if (e.key === 'Escape' || e.key === 'ArrowUp' || e.key === 'ArrowDown') {
    e.preventDefault()
    e.stopPropagation()
    e.stopImmediatePropagation()
    if (e.key === 'Escape') stopPicking()
    else climb(e.key === 'ArrowUp')
  }
}

// Alt для Alt+колеса: у режимі вибору відпускання не має активувати меню вікна.
function onKeyup(e: KeyboardEvent) {
  if (picking.value && e.key === 'Alt') e.preventDefault()
}

onMounted(() => {
  window.addEventListener('keydown', onKeydown, true)
  window.addEventListener('keyup', onKeyup, true)
  void loadRecent()
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown, true)
  window.removeEventListener('keyup', onKeyup, true)
  stopPicking()
  unlistenBox()
  ticketSelection.value = null
})

const boxStyle = computed(() => box.value && {
  top: `${box.value.top}px`, left: `${box.value.left}px`, width: `${box.value.width}px`, height: `${box.value.height}px`,
})
const labelBelow = computed(() => (box.value?.top ?? 0) < 28)
</script>

<template>
  <div v-bind="toolAttr" class="tkt" :class="{ 'is-mobile': mobile }">
    <div v-if="panelOpen" class="tkt-panel" role="dialog" aria-label="Заявки">
      <div class="tkt-head">
        <span class="grow tkt-title">Заявки</span>
        <TButton type="button" mode="ghost" size="small" icon="system-uicons:close" aria-label="Закрити" @click="panelOpen = false" />
      </div>
      <div class="tkt-actions">
        <TButton type="button" size="small" variant="accent" icon="system-uicons:crosshair" label="Позначити елемент" :title="TICKETS_HOTKEY" @click="startPicking" />
        <TButton type="button" size="small" label="Без елемента" title="Загальна заявка до сторінки" @click="pickPage" />
      </div>
      <p class="xs muted">{{ TICKETS_HOTKEY }} — режим вибору; ↑/↓ або Alt+колесо — батьківський/дочірній елемент; Esc — скасувати.</p>
      <div class="tkt-list">
        <p v-if="loadError" class="small bad-text">{{ loadError }}</p>
        <p v-else-if="items && !items.length" class="small muted">Заявок ще немає.</p>
        <ul v-else-if="items">
          <li v-for="t in items.slice(0, RECENT)" :key="t.id">
            <RouterLink :to="`/tickets/${t.id}`" class="tkt-item" @click="panelOpen = false">
              <span class="small ellipsis">{{ t.caption || t.text }}</span>
              <TTag :variant="statusTag(t.status)" size="small" class="nowrap">{{ subStatusLabel(t.status) }}</TTag>
            </RouterLink>
          </li>
        </ul>
      </div>
      <RouterLink to="/tickets" class="small" @click="panelOpen = false">Усі заявки →</RouterLink>
    </div>
    <button
      type="button" class="tkt-fab" :class="{ active: panelOpen || picking }" :aria-expanded="panelOpen ? 'true' : 'false'"
      :title="`Заявка (${TICKETS_HOTKEY})`" aria-label="Заявки" @click="picking ? stopPicking() : togglePanel()"
    >
      <TIcon icon="system-uicons:flag" />
      <span v-if="openCount" class="tkt-badge" title="Чекають на вас">{{ openCount }}</span>
    </button>
  </div>

  <div v-if="picking" v-bind="toolAttr" class="tkt-bar" role="status">
    <span class="small">Клікніть елемент, до якого заявка · <b>↑</b>/<b>↓</b> або <b>Alt</b>+колесо — батьківський/дочірній · <b>Esc</b> — скасувати</span>
    <TButton type="button" size="small" label="Без елемента" @click="pickPage" />
    <TButton type="button" size="small" mode="ghost" label="Скасувати" @click="stopPicking" />
  </div>
  <div v-if="boxStyle && shown" v-bind="toolAttr" class="tkt-hl" :class="{ 'is-selected': !picking }" :style="boxStyle">
    <span class="tkt-hl-label" :class="{ below: labelBelow }">{{ hoverLabel }}</span>
  </div>
</template>

<style scoped>
.tkt { position: fixed; right: 16px; bottom: 16px; z-index: 900; display: flex; flex-direction: column; align-items: flex-end; gap: var(--t-space-2); }
.tkt.is-mobile { bottom: calc(var(--t-control-h-large) + var(--t-space-4) + env(safe-area-inset-bottom, 0px) + 12px); right: 12px; }
.tkt-fab {
  position: relative; width: 36px; height: 36px; border-radius: 50%; border: 1px solid var(--t-color-border);
  background: var(--t-color-surface); color: var(--t-color-text-muted); cursor: pointer;
  display: inline-flex; align-items: center; justify-content: center; box-shadow: var(--t-shadow-2); opacity: .75;
}
.tkt-fab:hover, .tkt-fab.active { opacity: 1; color: var(--t-color-accent); border-color: var(--t-color-accent); }
.tkt-fab:focus-visible { outline: 2px solid var(--t-color-accent); outline-offset: 2px; }
.tkt-badge {
  position: absolute; top: -6px; right: -6px; min-width: 18px; height: 18px; padding: 0 5px; border-radius: 9px;
  background: var(--t-color-accent); color: var(--t-color-accent-contrast); font-size: 11px; line-height: 18px; text-align: center;
}
.tkt-panel {
  width: min(360px, calc(100vw - 24px)); max-height: min(70vh, 480px); display: flex; flex-direction: column; gap: var(--t-space-2);
  padding: var(--t-space-3); border: 1px solid var(--t-color-border); border-radius: var(--t-radius-medium);
  background: var(--t-color-surface); box-shadow: var(--t-shadow-3);
}
.tkt-head { display: flex; align-items: center; gap: var(--t-space-2); }
.tkt-title { font-weight: var(--t-font-weight-semibold); }
.tkt-actions { display: flex; gap: var(--t-space-2); flex-wrap: wrap; }
.tkt-list { overflow: auto; min-height: 0; }
.tkt-list ul { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; }
.tkt-item { display: flex; align-items: center; gap: var(--t-space-2); padding: 4px 6px; border-radius: var(--t-radius-default); color: var(--t-color-text); min-width: 0; }
.tkt-item:hover { background: var(--t-color-hover); text-decoration: none; }
.tkt-item > :first-child { flex: 1; min-width: 0; }
.tkt-bar {
  position: fixed; top: 12px; left: 50%; transform: translateX(-50%); z-index: 2147483001; display: flex; align-items: center; gap: var(--t-space-2);
  padding: 6px 8px 6px 14px; border-radius: var(--t-radius-default); border: 1px solid var(--t-color-accent);
  background: var(--t-color-surface); box-shadow: var(--t-shadow-3); max-width: calc(100vw - 24px); flex-wrap: wrap;
}
.tkt-hl {
  position: fixed; z-index: 2147483000; pointer-events: none; box-sizing: border-box;
  border: 2px solid var(--t-color-accent); background: color-mix(in srgb, var(--t-color-accent) 14%, transparent); border-radius: 2px;
}
/* Вибраний елемент під відкритим діалогом: над фоном модалки t-components (3990), під самим вікном (4010). */
.tkt-hl.is-selected { z-index: 4000; background: none; }
.tkt-hl-label {
  position: absolute; left: -2px; bottom: 100%; margin-bottom: 2px; max-width: 60vw; padding: 1px 6px; border-radius: 3px;
  background: var(--t-color-accent); color: var(--t-color-accent-contrast); font-size: 11px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}
.tkt-hl-label.below { bottom: auto; top: 100%; margin: 2px 0 0; }
</style>

<style>
html.tkt-picking, html.tkt-picking * { cursor: crosshair !important; }
</style>
