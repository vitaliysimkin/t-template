// Форматування дат і тривалостей.

export function formatDate(v?: string | null): string {
  if (!v) return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return v
  return d.toLocaleString('uk-UA', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function formatTime(v?: string | null): string {
  if (!v) return ''
  const d = new Date(v)
  return Number.isNaN(d.getTime()) ? v : d.toLocaleTimeString('uk-UA')
}

/** «1 хв 05 с» між двома моментами (кінця нема — до зараз). */
export function duration(from?: string | null, to?: string | null): string {
  if (!from) return '—'
  const ms = (to ? new Date(to).getTime() : Date.now()) - new Date(from).getTime()
  if (!Number.isFinite(ms) || ms < 0) return '—'
  const s = Math.round(ms / 1000)
  if (s < 60) return `${s} с`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m} хв ${String(s % 60).padStart(2, '0')} с`
  return `${Math.floor(m / 60)} год ${String(m % 60).padStart(2, '0')} хв`
}

export function formatBytes(n: number): string {
  if (n < 1024) return `${n} Б`
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} КБ`
  return `${(n / 1024 / 1024).toFixed(1)} МБ`
}

/** Варіанти TTag. */
export type TagVariant = 'gray' | 'red' | 'orange' | 'yellow' | 'green' | 'teal' | 'blue'
