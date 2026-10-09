// HTTP до бекенду (/api/...). Помилка — ApiError у форматі бекенду {"error":{"code","message","details"}}.

export class ApiError extends Error {
  readonly code: string
  readonly status: number
  readonly details?: Record<string, unknown>

  constructor(code: string, message: string, status: number, details?: Record<string, unknown>) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    this.details = details
  }

  /** details.fields (помилки полів, напр. { value: "Очікується ціле число" }). */
  get fields(): Record<string, string> {
    const f = this.details?.fields
    return f && typeof f === 'object' ? (f as Record<string, string>) : {}
  }
}

export function errorText(e: unknown): string {
  return e instanceof Error ? e.message : String(e)
}

export async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  let res: Response
  try {
    res = await fetch(url, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError('network', 'Сервер недоступний', 0)
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  const data = text ? safeJson(text) : undefined
  if (!res.ok) {
    const err = (data as { error?: { code?: string; message?: string; details?: Record<string, unknown> } } | undefined)?.error
    throw new ApiError(err?.code ?? 'http_' + res.status, err?.message ?? `HTTP ${res.status}`, res.status, err?.details)
  }
  return data as T
}

function safeJson(text: string): unknown {
  try { return JSON.parse(text) } catch { return text }
}

export const http = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T>(url: string, body?: unknown) => request<T>('PUT', url, body ?? {}),
  del: <T>(url: string) => request<T>('DELETE', url),
}
