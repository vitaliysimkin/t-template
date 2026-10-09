// Знімок сторінки для заявки: самодостатній HTML поточного DOM (без <script>/<link>/<style> і без елементів самого інструмента),
// усі CSS-правила з document.styleSheets — одним <style> у <head>, щоб Claude бачив сторінку такою, якою її бачив користувач.
// Cross-origin таблиці пропускаються. Вибраний елемент позначається атрибутом SNAPSHOT_TARGET_ATTR; поки діалог дозволяє змінити вибір
// на предка, весь ланцюжок вибору тимчасово мітиться CHAIN_ATTR="<індекс>", а при відправці потрібна мітка стає SNAPSHOT_TARGET_ATTR.

/** Ліміт знімка: більше — не надсилаємо (діалог показує це). */
export const SNAPSHOT_MAX_BYTES = 3 * 1024 * 1024
export const SNAPSHOT_TARGET_ATTR = 'data-ticket-target'
const CHAIN_ATTR = 'data-ticket-chain'

export interface PageSnapshot {
  /** HTML з мітками ланцюжка (CHAIN_ATTR); у запит — лише через snapshotHtml(). */
  html: string
  bytes: number
  tooLarge: boolean
}

function sheetText(sheet: CSSStyleSheet): string {
  let rules: string
  try {
    rules = Array.from(sheet.cssRules, (r) => r.cssText).join('\n')
  } catch {
    return `/* недоступно (cross-origin): ${sheet.href ?? ''} */`
  }
  const media = sheet.media?.mediaText
  return media && media !== 'all' ? `@media ${media} {\n${rules}\n}` : rules
}

function collectCss(): string {
  const sheets: CSSStyleSheet[] = [...Array.from(document.styleSheets), ...(document.adoptedStyleSheets ?? [])]
  return sheets.filter((s) => !s.disabled).map(sheetText).join('\n')
}

/**
 * Знімок поточної сторінки. chain — ланцюжок вибору (предки → вибраний), skipSelector — елементи, яких у знімку не має бути
 * (кнопка/панель/підсвітка інструмента).
 */
export function capturePageSnapshot(chain: Element[], skipSelector: string): PageSnapshot {
  chain.forEach((el, i) => el.setAttribute(CHAIN_ATTR, String(i)))
  let clone: HTMLElement
  try {
    clone = document.documentElement.cloneNode(true) as HTMLElement
  } finally {
    chain.forEach((el) => el.removeAttribute(CHAIN_ATTR))
  }
  clone.querySelectorAll('script, noscript, style, link[rel~="stylesheet"], link[rel="modulepreload"], link[rel="preload"]').forEach((n) => n.remove())
  clone.querySelectorAll(skipSelector).forEach((n) => n.remove())

  const head = clone.querySelector('head') ?? clone.insertBefore(document.createElement('head'), clone.firstChild)
  const style = document.createElement('style')
  style.textContent = collectCss()
  head.appendChild(style)

  const html = '<!DOCTYPE html>\n' + clone.outerHTML
  const bytes = new TextEncoder().encode(html).length
  return { html, bytes, tooLarge: bytes > SNAPSHOT_MAX_BYTES }
}

/** HTML для запиту: мітка targetIndex → SNAPSHOT_TARGET_ATTR (null — заявка до сторінки); undefined, якщо знімок завеликий. */
export function snapshotHtml(s: PageSnapshot | null | undefined, targetIndex: number | null): string | undefined {
  if (!s || s.tooLarge) return undefined
  return s.html.replace(new RegExp(` ${CHAIN_ATTR}="(\\d+)"`, 'g'), (_m, i: string) =>
    targetIndex !== null && Number(i) === targetIndex ? ` ${SNAPSHOT_TARGET_ATTR}=""` : '')
}
