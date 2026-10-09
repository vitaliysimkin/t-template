<script setup lang="ts">
// Монтує drawer операції, коли jobs.openId задано (openJob(id) з будь-якого місця).
import JobDrawer from './JobDrawer.vue'
import { jobs } from './state'
</script>

<template>
  <Transition name="slide">
    <JobDrawer v-if="jobs.openId" :key="jobs.openId" :job-id="jobs.openId" @close="jobs.openId = null" />
  </Transition>
</template>
