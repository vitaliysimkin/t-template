// Статуси заявок: для користувача — три групи «чия черга» (У вас / У Claude / Закрито), детальний статус — підстатус.
import type { TagVariant } from '../../../lib/format'
import type { TicketSeverity, TicketStatus } from '../api'

export type TicketGroup = 'user' | 'claude' | 'closed'

export const ticketGroupOf: Record<TicketStatus, TicketGroup> = {
  'new': 'claude',
  'awaiting-claude': 'claude',
  'feedback': 'claude',
  'approved': 'claude',
  'simple': 'claude',
  'variants-ready': 'user',
  'simple-proposed': 'user',
  'applied': 'user', // застосоване — на перевірку; закриває лише користувач («Зроблено»)
  'rejected': 'closed',
  'done': 'closed',
}
export const GROUP_ORDER: TicketGroup[] = ['user', 'claude', 'closed']
const groupLabels: Record<TicketGroup, string> = { user: 'У вас', claude: 'У Claude', closed: 'Закрито' }
const groupTags: Record<TicketGroup, TagVariant> = { user: 'orange', claude: 'blue', closed: 'gray' }

const statusLabels: Record<TicketStatus, string> = {
  'new': 'нова',
  'awaiting-claude': 'уточнення',
  'feedback': 'уточнення',
  'approved': 'до застосування',
  'simple': 'до застосування (дрібне)',
  'variants-ready': 'варіанти готові',
  'simple-proposed': 'пропозиція фіксу',
  'applied': 'застосовано — перевірте',
  'rejected': 'відхилено',
  'done': 'зроблено',
}

export const groupOf = (s: TicketStatus): TicketGroup => ticketGroupOf[s] ?? 'claude'
export const groupLabel = (s: TicketStatus) => groupLabels[groupOf(s)]
export const statusTag = (s: TicketStatus): TagVariant => groupTags[groupOf(s)]
export const subStatusLabel = (s: TicketStatus) => statusLabels[s] ?? s

const severityLabels: Record<TicketSeverity, string> = { high: 'Висока', medium: 'Середня', low: 'Низька' }
export const severityLabel = (s?: string | null) => (s ? severityLabels[s as TicketSeverity] ?? s : 'Не вказано')
export const SEVERITY_OPTIONS = [
  { value: 'low', label: 'Низька', icon: 'system-uicons:signal-low' },
  { value: 'medium', label: 'Середня', icon: 'system-uicons:signal-medium' },
  { value: 'high', label: 'Висока', icon: 'system-uicons:signal-full' },
]

// Тип ставить Claude (meta-рядок events.jsonl).
const typeLabels: Record<string, string> = { feature: 'Фіча', fix: 'Фікс', improvement: 'Покращення' }
const typeTags: Record<string, TagVariant> = { feature: 'teal', fix: 'red', improvement: 'yellow' }
export const typeLabel = (t?: string | null) => (t ? typeLabels[t] ?? t : '')
export const typeTag = (t?: string | null): TagVariant => (t ? typeTags[t] ?? 'gray' : 'gray')

/** «Що потрібно від Claude» — мінімум: Claude може зробити більше, але не менше. */
export const SCOPE_OPTIONS = [
  { value: 'fix', label: 'Фікс', icon: 'system-uicons:pen' },
  { value: 'variants', label: 'Варіанти', icon: 'system-uicons:versions' },
]
