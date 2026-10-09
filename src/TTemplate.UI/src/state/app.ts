// Спільний стан ядра: інформація про застосунок, налаштування, тости.
import { useNotifications } from '@vitaliysimkin/t-components'
import { reactive } from 'vue'
import { coreApi, type AppInfo, type SettingsResponse } from '../api/core'
import { errorText } from '../api/http'

export const app = reactive({
  info: null as AppInfo | null,
  settings: { data: null as SettingsResponse | null, error: null as string | null },
})

export async function refreshInfo() {
  try {
    app.info = await coreApi.info()
    document.title = app.info.name
  } catch {
    // бекенд недоступний — лишаємо назву з index.html
  }
}

export async function refreshSettings() {
  try {
    app.settings.data = await coreApi.settings()
    app.settings.error = null
  } catch (e) {
    app.settings.error = errorText(e)
  }
  // Назва застосунку й імʼя — теж налаштування: після збереження оновлюємо бічну панель.
  void refreshInfo()
}

const notifications = useNotifications()
export type ToastKind = 'ok' | 'bad' | 'info'
export function toast(text: string, kind: ToastKind = 'info') {
  if (kind === 'ok') notifications.success(text, 4000)
  else if (kind === 'bad') notifications.error(text, 8000)
  else notifications.info(text, 4000)
}
