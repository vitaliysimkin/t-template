import { http } from '../../api/http'

export type JobStatus = 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled' | 'interrupted'
export type StepStatus = 'pending' | 'running' | 'succeeded' | 'failed' | 'skipped' | 'cancelled'

export interface JobStep { title: string; status: StepStatus; startedAt: string; finishedAt?: string | null }
export interface JobLogLine { ts: string; text: string; level: 'info' | 'step' | 'error' | string }

export interface Job {
  id: string
  type: string
  title: string
  status: JobStatus
  createdAt: string
  startedAt?: string | null
  finishedAt?: string | null
  error?: string | null
  steps: JobStep[]
  /** Лише в GET /api/jobs/{id}. */
  log?: JobLogLine[]
}

export const jobsApi = {
  list: (limit = 100) => http.get<Job[]>(`/api/jobs?limit=${limit}`),
  active: () => http.get<Job[]>('/api/jobs?active=true'),
  get: (id: string) => http.get<Job>(`/api/jobs/${encodeURIComponent(id)}`),
  cancel: (id: string) => http.post<void>(`/api/jobs/${encodeURIComponent(id)}/cancel`),
  demo: () => http.post<Job>('/api/jobs/demo'),
}
