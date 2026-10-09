// API ядра: інформація про застосунок і налаштування.
import { http } from './http'

export interface AppInfo { name: string; userName: string; version: string }

export type SettingType = 'string' | 'bool' | 'int' | 'list'
export type SettingValue = string | boolean | number | string[] | null

export interface SettingView {
  name: string
  section: string
  group?: string | null
  caption: string
  description?: string | null
  type: SettingType
  secret: boolean
  value: SettingValue
  defaultValue: SettingValue
  /** Лише для секретів: чи задано значення (саме значення сервер не віддає). */
  hasValue?: boolean
  isCustomValue: boolean
}

export interface SettingsSectionView { key: string; caption: string; icon?: string | null }
export interface SettingsResponse { sections: SettingsSectionView[]; items: SettingView[] }

export const coreApi = {
  info: () => http.get<AppInfo>('/api/info'),
  settings: () => http.get<SettingsResponse>('/api/settings'),
  setSetting: (name: string, value: SettingValue) => http.put<void>(`/api/settings/${encodeURIComponent(name)}`, { value }),
  resetSetting: (name: string) => http.del<void>(`/api/settings/${encodeURIComponent(name)}`),
}
