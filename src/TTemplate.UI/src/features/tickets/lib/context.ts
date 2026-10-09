// Збір контексту вибраного елемента для заявки: CSS-селектор, ланцюжок Vue-компонентів (App > … > Component), видимий текст,
// обрізаний outerHTML, маршрут, розмір вікна, тема. Елементи самого інструмента позначені TICKETS_ATTR — їх не вибираємо.
import { shallowRef } from 'vue'
import type { TicketContext } from '../api'
import type { PageSnapshot } from './pageSnapshot'

export const TICKETS_ATTR = 'data-tickets-tool'
export const TICKETS_HOTKEY = 'Ctrl+Shift+E'

const MAX_TEXT = 300
const MAX_HTML = 2000

export function isHotkey(e: KeyboardEvent): boolean {
  return (e.ctrlKey || e.metaKey) && e.shiftKey && !e.altKey && (e.code === 'KeyE' || e.key.toLowerCase() === 'e')
}

export function isToolElement(el: Element | null): boolean {
  return !!el?.closest(`[${TICKETS_ATTR}]`)
}

function cssEscape(s: string) {
  return typeof CSS !== 'undefined' && CSS.escape ? CSS.escape(s) : s.replace(/[^a-zA-Z0-9_-]/g, '\\$&')
}

/** Короткий стабільний селектор: до найближчого id (або body), класи без згенерованих, :nth-of-type при однакових сусідах. */
export function cssSelector(el: Element): string {
  const parts: string[] = []
  let cur: Element | null = el
  while (cur && cur !== document.body && cur !== document.documentElement && parts.length < 8) {
    if (cur.id && !/^\d|[:]/.test(cur.id)) {
      parts.unshift('#' + cssEscape(cur.id))
      break
    }
    let part = cur.tagName.toLowerCase()
    const classes = Array.from(cur.classList).filter((c) => !/^(is-|has-)?(hover|focus|active)/.test(c) && !/\d{3,}/.test(c)).slice(0, 3)
    if (classes.length) part += '.' + classes.map(cssEscape).join('.')
    const parent: Element | null = cur.parentElement
    if (parent) {
      const same = Array.from(parent.children).filter((c) => c.tagName === cur!.tagName)
      if (same.length > 1) part += `:nth-of-type(${same.indexOf(cur) + 1})`
    }
    parts.unshift(part)
    cur = parent
  }
  return parts.join(' > ')
}

interface VueInstanceLike { type?: { __name?: string; name?: string; __file?: string }; parent?: VueInstanceLike | null }

/** Ланцюжок компонентів від кореня. Vue кладе __vueParentComponent на DOM-елементи (__file — лише в dev). */
export function vueComponentChain(el: Element): { chain?: string; file?: string } {
  let node: Element | null = el
  let inst: VueInstanceLike | undefined
  while (node && !inst) {
    inst = (node as unknown as { __vueParentComponent?: VueInstanceLike }).__vueParentComponent
    node = node.parentElement
  }
  if (!inst) return {}
  const names: string[] = []
  let file: string | undefined
  for (let i: VueInstanceLike | null | undefined = inst; i && names.length < 40; i = i.parent) {
    const t = i.type ?? {}
    const name = t.__name || t.name || (t.__file ? t.__file.split(/[\\/]/).pop()?.replace(/\.vue$/, '') : undefined) || 'Anonymous'
    if (!file && t.__file) file = t.__file
    names.unshift(name)
  }
  return { chain: names.join(' > '), file }
}

function clip(s: string, max: number) {
  return s.length > max ? s.slice(0, max) + '…' : s
}

function visibleText(el: Element): string {
  const t = ((el as HTMLElement).innerText ?? el.textContent ?? '').replace(/\s+/g, ' ').trim()
  if (t) return clip(t, MAX_TEXT)
  const alt = el.getAttribute('aria-label') || el.getAttribute('title') || (el as HTMLInputElement).placeholder || (el as HTMLInputElement).value || ''
  return clip(String(alt).trim(), MAX_TEXT)
}

function themeLabel(t: string): string {
  if (t !== 'auto') return t
  const dark = typeof matchMedia !== 'undefined' && matchMedia('(prefers-color-scheme: dark)').matches
  return `auto (${dark ? 'dark' : 'light'})`
}

/** Контекст сторінки (без елемента). */
export function pageContext(theme: string): TicketContext {
  return {
    url: location.href,
    // Маршрут — з hash (hash-історія); router тут не імпортуємо, щоб не було циклу router → features → tickets.
    route: location.hash.replace(/^#/, '') || '/',
    viewport: `${window.innerWidth}×${window.innerHeight} @${window.devicePixelRatio || 1}x`,
    theme: themeLabel(theme),
    userAgent: navigator.userAgent,
  }
}

/** Поля контексту, що залежать від елемента (діалог перераховує їх при зміні вибору). */
export function elementParts(el: Element): Pick<TicketContext, 'selector' | 'components' | 'componentFile' | 'elementText' | 'outerHtml'> {
  const { chain, file } = vueComponentChain(el)
  return {
    selector: cssSelector(el),
    components: chain,
    componentFile: file,
    elementText: visibleText(el) || undefined,
    outerHtml: clip(el.outerHTML.replace(/\s+data-v-[a-z0-9]+(="")?/g, ''), MAX_HTML),
  }
}

export function elementContext(el: Element, theme: string): TicketContext {
  return { ...pageContext(theme), ...elementParts(el) }
}

// Службові обгортки Vue і компоненти t-components (TCard, TButton…) — у підписі показуємо компонент застосунку, що їх використав.
const SKIP_COMPONENT = /^(T[A-Z]\w*|Transition|TransitionGroup|BaseTransition|KeepAlive|Teleport|Suspense|RouterView|RouterLink|Anonymous)$/

export function ownerComponent(el: Element): string | undefined {
  const names = vueComponentChain(el).chain?.split(' > ') ?? []
  for (let i = names.length - 1; i >= 0; i--) if (!SKIP_COMPONENT.test(names[i])) return names[i]
  return names[names.length - 1]
}

/** «div.t-card.t-card--flat» (до 2 класів) або «section#id». */
export function elementTag(el: Element): string {
  const tag = el.tagName.toLowerCase()
  if (el.id && !/^\d/.test(el.id)) return `${tag}#${el.id}`
  const cls = Array.from(el.classList).slice(0, 2).join('.')
  return cls ? `${tag}.${cls}` : tag
}

/** Підпис підсвітки: «UpdateCard · div.t-card · 412×380». */
export function elementLabel(el: Element): string {
  const r = el.getBoundingClientRect()
  return [ownerComponent(el), elementTag(el), `${Math.round(r.width)}×${Math.round(r.height)}`].filter(Boolean).join(' · ')
}

/** Ланцюжок від найвищого предка під body до самого елемента. */
export function ancestorChain(el: Element): Element[] {
  const chain: Element[] = []
  for (let cur: Element | null = el; cur && cur !== document.body && cur !== document.documentElement; cur = cur.parentElement) chain.unshift(cur)
  return chain
}

export function selectableParent(el: Element): Element | null {
  const p = el.parentElement
  return p && p !== document.body && p !== document.documentElement && !isToolElement(p) ? p : null
}

export interface TicketSelection {
  /** Предки → найглибший вибраний елемент; порожній — заявка до сторінки. */
  chain: Element[]
  /** Поточний вибір у chain (діалог міняє через breadcrumbs). */
  index: number
  snapshot: PageSnapshot | null
}

/** Вибір, поки відкритий діалог: TicketsTool підсвічує chain[index] під діалогом. */
export const ticketSelection = shallowRef<TicketSelection | null>(null)
/** Діалог заявки відкритий. */
export const ticketDialogOpen = shallowRef(false)
