import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// Dev: Vite (:5173) проксіює /api на бекенд (APP_API або http://localhost:5180 — Properties/launchSettings.json бекенду).
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: process.env.APP_API ?? 'http://localhost:5180' },
    },
  },
})
