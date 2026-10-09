<script setup lang="ts">
// Деталі операції: статус, кроки, лог. Поки операція активна — опитування раз на секунду.
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { errorText } from '../../api/http'
import AppBanner from '../../components/AppBanner.vue'
import AppDrawer from '../../components/AppDrawer.vue'
import { duration, formatTime } from '../../lib/format'
import { toast } from '../../state/app'
import { jobsApi, type Job, type JobStep, type StepStatus } from './api'
import { jobStatusLabel, jobStatusTag } from './state'

const props = defineProps<{ jobId: string }>()
const emit = defineEmits<{ close: [] }>()

const job = ref<Job | null>(null)
const error = ref<string | null>(null)
const active = computed(() => job.value?.status === 'running' || job.value?.status === 'queued')
const logEl = ref<HTMLElement | null>(null)

const stepIcon: Record<StepStatus, string> = {
  pending: 'system-uicons:circle', running: 'system-uicons:record', succeeded: 'system-uicons:check',
  failed: 'system-uicons:cross', skipped: 'system-uicons:minus', cancelled: 'system-uicons:no-sign',
}
const stepDur = (s: JobStep) => duration(s.startedAt, s.finishedAt)

let timer: ReturnType<typeof setTimeout> | null = null
async function load() {
  try {
    const stick = logEl.value ? logEl.value.scrollHeight - logEl.value.scrollTop - logEl.value.clientHeight < 24 : true
    job.value = await jobsApi.get(props.jobId)
    error.value = null
    if (stick) void nextTick(() => logEl.value?.scrollTo({ top: logEl.value.scrollHeight }))
  } catch (e) {
    error.value = errorText(e)
  }
  if (active.value || !job.value) timer = setTimeout(load, 1000)
}
onMounted(load)
onBeforeUnmount(() => { if (timer) clearTimeout(timer) })
watch(() => props.jobId, () => { job.value = null; void load() })

const cancelling = ref(false)
async function cancel() {
  cancelling.value = true
  try {
    await jobsApi.cancel(props.jobId)
  } catch (e) {
    toast(errorText(e), 'bad')
  } finally {
    cancelling.value = false
  }
}
</script>

<template>
  <AppDrawer :title="job?.title ?? 'Операція'" :subtitle="jobId" @close="emit('close')">
    <template #head>
      <TTag v-if="job" :variant="jobStatusTag[job.status]"><span v-if="job.status === 'running'" class="pulse-dot" />{{ jobStatusLabel[job.status] }}</TTag>
    </template>

    <AppBanner v-if="error && !job" tone="bad">{{ error }}</AppBanner>
    <div v-else-if="!job" class="muted">Завантаження…</div>
    <template v-else>
      <AppBanner v-if="job.status === 'failed'" tone="bad"><strong>{{ job.error ?? 'Операція завершилась з помилкою' }}</strong></AppBanner>
      <AppBanner v-else-if="job.status === 'succeeded'" tone="ok">Готово за {{ duration(job.startedAt ?? job.createdAt, job.finishedAt) }}</AppBanner>
      <AppBanner v-else-if="job.status === 'interrupted'" tone="warn">Перервано: застосунок зупинився під час виконання.</AppBanner>

      <ol v-if="job.steps.length" class="steps">
        <li v-for="(s, i) in job.steps" :key="i" :class="`st-${s.status}`">
          <TIcon :icon="stepIcon[s.status]" class="mark" aria-hidden="true" />
          <span class="grow">{{ s.title }}</span>
          <span class="muted xs nowrap">{{ stepDur(s) }}</span>
        </li>
      </ol>

      <section class="stack tight">
        <h3>Лог</h3>
        <div ref="logEl" class="log logbox">
          <div v-for="(l, i) in job.log ?? []" :key="i" :class="`l-${l.level}`"><span class="muted">{{ formatTime(l.ts) }}</span> {{ l.text }}</div>
          <span v-if="!job.log?.length" class="muted">Порожньо</span>
        </div>
      </section>
    </template>

    <template #footer>
      <TButton v-if="active" type="button" variant="danger" mode="plain" label="Скасувати" :disabled="cancelling" @click="cancel" />
      <TButton type="button" :label="active ? 'Згорнути' : 'Закрити'" @click="emit('close')" />
    </template>
  </AppDrawer>
</template>

<style scoped>
.steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; }
.steps li { display: flex; align-items: center; gap: var(--t-space-2); padding: 6px var(--t-space-2); border-radius: var(--t-radius-default); }
.mark { flex: none; color: var(--t-color-text-muted); }
.st-running { background: var(--t-color-surface-2); }
.st-running .mark { color: var(--t-color-warning); }
.st-succeeded .mark { color: var(--t-color-success); }
.st-failed .mark { color: var(--t-color-danger); }
.logbox { height: min(46vh, 520px); }
.l-step { font-weight: var(--t-font-weight-semibold); }
.l-error { color: var(--t-color-danger); }
</style>
