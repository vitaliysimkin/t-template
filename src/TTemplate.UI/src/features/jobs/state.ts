// Стан фічі «Операції»: активні операції (опитування раз на 3 с — для лічильника в меню) і відкритий drawer.
import { reactive } from 'vue'
import type { TagVariant } from '../../lib/format'
import { toast } from '../../state/app'
import { jobsApi, type Job, type JobStatus } from './api'

export const jobs = reactive({
  active: [] as Job[],
  /** Відкрита в drawer операція. */
  openId: null as string | null,
  /** Лічильник завершень — сторінка списку перезавантажується, коли він росте. */
  finished: 0,
})

export const jobStatusLabel: Record<JobStatus, string> = {
  queued: 'у черзі', running: 'виконується', succeeded: 'готово', failed: 'помилка', cancelled: 'скасовано', interrupted: 'перервано',
}
export const jobStatusTag: Record<JobStatus, TagVariant> = {
  queued: 'gray', running: 'yellow', succeeded: 'green', failed: 'red', cancelled: 'gray', interrupted: 'red',
}
/** Підписи типів операцій (Job.type). Додавай свої. */
export const jobTypeLabel: Record<string, string> = { demo: 'Демо' }

export function openJob(id: string) { jobs.openId = id }

/** Запустити операцію (POST повертає Job) і одразу показати її. */
export async function track(start: Promise<Job>) {
  const job = await start
  jobs.active = [job, ...jobs.active.filter((j) => j.id !== job.id)]
  openJob(job.id)
  return job
}

let timer: ReturnType<typeof setInterval> | null = null
async function poll() {
  try {
    const next = await jobsApi.active()
    const gone = jobs.active.filter((j) => !next.some((n) => n.id === j.id))
    if (gone.length) {
      jobs.finished += gone.length
      for (const j of gone) if (jobs.openId !== j.id) toast(`Операцію «${j.title}» завершено`, 'info')
    }
    jobs.active = next
  } catch { /* бекенд недоступний — спробуємо наступного разу */ }
}

export function startJobsPolling() {
  if (timer) return
  void poll()
  timer = setInterval(poll, 3000)
}
