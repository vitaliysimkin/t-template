// Фіча «Операції»: сторінка #/jobs, лічильник активних у меню, drawer з кроками й логом (бекенд — Features/Jobs).
import type { AppFeature } from '../types'
import JobDrawerHost from './JobDrawerHost.vue'
import JobsView from './JobsView.vue'
import { jobs, startJobsPolling } from './state'

const feature: AppFeature = {
  id: 'jobs',
  menu: [{ route: '/jobs', title: 'Операції', icon: 'system-uicons:files-history', badge: () => jobs.active.length }],
  routes: [{ path: '/jobs', name: 'jobs', component: JobsView }],
  overlays: [JobDrawerHost],
  start: startJobsPolling,
}

export default feature
