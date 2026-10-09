import { http } from '../../api/http'

export type TicketStatus =
  | 'new' | 'awaiting-claude' | 'variants-ready' | 'feedback' | 'approved' | 'simple' | 'simple-proposed' | 'applied' | 'rejected' | 'done'
export type TicketSeverity = 'low' | 'medium' | 'high'
export type TicketScope = 'fix' | 'variants'
export type TicketAction = 'iterate' | 'approve' | 'simple' | 'reject' | 'done'

/** Контекст сторінки/елемента, який збирає TicketsTool. */
export interface TicketContext {
  url?: string
  route?: string
  selector?: string
  components?: string
  componentFile?: string
  elementText?: string
  outerHtml?: string
  viewport?: string
  theme?: string
  userAgent?: string
}

export interface TicketRequest extends TicketContext {
  text: string
  severity: TicketSeverity
  scope: TicketScope
  snapshotHtml?: string
}

export interface TicketListItem {
  id: string
  createdAt: string
  status: TicketStatus
  severity?: TicketSeverity | null
  type?: string | null
  caption?: string | null
  text: string
  route?: string | null
  variants: number
}

export interface TicketMessage { ts: string; role: 'user' | 'claude' | string; author: string; variant?: string | null; text: string }
export interface TicketVariant { slug: string; title: string; summary?: string | null }

export interface TicketDetail {
  id: string
  ticket: (TicketContext & { text: string; author?: string; createdAt: string; scope?: TicketScope | null }) | null
  status: TicketStatus
  severity?: TicketSeverity | null
  type?: string | null
  caption?: string | null
  history: Record<string, unknown>[]
  messages: TicketMessage[]
  variants: TicketVariant[]
  hasSnapshot: boolean
  dir: string
}

const enc = encodeURIComponent

export const ticketsApi = {
  list: () => http.get<{ path: string; items: TicketListItem[] }>('/api/tickets'),
  get: (id: string) => http.get<TicketDetail>(`/api/tickets/${enc(id)}`),
  create: (req: TicketRequest) => http.post<{ id: string }>('/api/tickets', req),
  comment: (id: string, text: string, variant?: string | null) => http.post<void>(`/api/tickets/${enc(id)}/messages`, { text, variant: variant ?? null }),
  action: (id: string, action: TicketAction, variant?: string | null, text?: string) =>
    http.post<void>(`/api/tickets/${enc(id)}/action`, { action, variant: variant ?? null, text: text || null }),
  meta: (id: string, meta: { severity?: TicketSeverity | 'none'; caption?: string }) => http.post<void>(`/api/tickets/${enc(id)}/meta`, meta),
  snapshotUrl: (id: string) => `/api/tickets/${enc(id)}/snapshot`,
  variantUrl: (id: string, slug: string) => `/api/tickets/${enc(id)}/variants/${enc(slug)}`,
}
