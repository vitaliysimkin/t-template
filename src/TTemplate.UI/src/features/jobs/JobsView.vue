<script setup lang="ts">
// «Операції» (#/jobs): історія фонових операцій. Статус активних — живий зі state.jobs.active (опитування фічі).
import type { TTableColumn } from '@vitaliysimkin/t-components'
import { computed, onMounted, ref, watch } from 'vue'
import { errorText } from '../../api/http'
import AppBanner from '../../components/AppBanner.vue'
import { duration, formatDate } from '../../lib/format'
import { toast } from '../../state/app'
import { jobsApi, type Job } from './api'
import { jobs, jobStatusLabel, jobStatusTag, jobTypeLabel, openJob, track } from './state'

type Row = Job & Record<string, unknown>

const rows = ref<Job[]>([])
const loaded = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)

async function load() {
  loading.value = true
  try {
    rows.value = await jobsApi.list(100)
    error.value = null
  } catch (e) {
    error.value = errorText(e)
  } finally {
    loading.value = false
    loaded.value = true
  }
}
onMounted(load)
watch(() => jobs.finished, load)

const displayRows = computed<Row[]>(() => {
  const fresh = jobs.active.filter((a) => !rows.value.some((r) => r.id === a.id))
  return [...fresh, ...rows.value].map((r) => (jobs.active.find((j) => j.id === r.id) ?? r) as Row)
})

async function startDemo() {
  try {
    await track(jobsApi.demo())
  } catch (e) {
    toast(errorText(e), 'bad')
  }
}

const columns: TTableColumn<Row>[] = [
  { key: 'title', label: 'Операція' },
  { key: 'status', label: 'Статус' },
  { key: 'createdAt', label: 'Початок' },
  { key: 'duration', label: 'Тривалість', align: 'right' },
  { key: 'actions', label: '', align: 'right' },
]
</script>

<template>
  <section class="page">
    <header class="page-head">
      <div class="grow">
        <h1>Операції</h1>
        <p class="small muted">Фонові задачі з кроками й логом</p>
      </div>
      <TButton type="button" label="Демо-операція" icon="system-uicons:play-button" @click="startDemo" />
      <TButton type="button" label="Оновити" :disabled="loading" @click="load" />
    </header>

    <AppBanner v-if="error && !rows.length" tone="bad">
      Не вдалося отримати список: {{ error }}
      <template #action><TButton type="button" size="small" label="Повторити" @click="load" /></template>
    </AppBanner>

    <TTable v-else :columns="columns" :rows="displayRows" row-key="id" :loading="loading && !loaded">
      <template #loading><span class="muted">Завантаження…</span></template>
      <template #empty><TEmpty description="Операцій ще не було." /></template>

      <template #cell-title="{ row: j }">
        <span class="row">
          <TTag variant="blue" size="small">{{ jobTypeLabel[j.type] ?? j.type }}</TTag>
          <span class="ellipsis">{{ j.title }}</span>
        </span>
      </template>
      <template #cell-status="{ row: j }">
        <TTag :variant="jobStatusTag[j.status]"><span v-if="j.status === 'running'" class="pulse-dot" />{{ jobStatusLabel[j.status] }}</TTag>
      </template>
      <template #cell-createdAt="{ row: j }"><span class="small">{{ formatDate(j.createdAt) }}</span></template>
      <template #cell-duration="{ row: j }"><span class="small nowrap">{{ duration(j.startedAt ?? j.createdAt, j.finishedAt) }}</span></template>
      <template #cell-actions="{ row: j }">
        <TButton type="button" mode="ghost" size="small" label="Деталі" @click="openJob(j.id)" />
      </template>
    </TTable>
  </section>
</template>
