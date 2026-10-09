<script setup lang="ts">
// Футер бічної панелі: картка «аватар · імʼя · v<версія>», клік — меню (налаштування у вікні, тема).
// Згорнута панель — лише аватар. Імʼя — налаштування «Ваше імʼя» (розділ «Загальне»).
import { useTheme } from '@vitaliysimkin/t-components'
import { computed } from 'vue'
import { icon } from '../lib/icons'
import { app } from '../state/app'

defineProps<{ collapsed?: boolean }>()
const emit = defineEmits<{ settings: [] }>()

const label = computed(() => app.info?.userName || 'Користувач')
const version = computed(() => app.info?.version ?? null)
// Ініціали: «Віталій Сімкін» → «ВС», «admin» → «AD».
const initials = computed(() => {
  const parts = label.value.split(/[^\p{L}\p{N}]+/u).filter(Boolean)
  const s = parts.length > 1 ? parts[0][0] + parts[1][0] : (parts[0] ?? '?').slice(0, 2)
  return s.toUpperCase()
})

const { currentTheme } = useTheme()
const themes = [
  { value: 'light', label: 'Світла тема', icon: icon.sun },
  { value: 'dark', label: 'Темна тема', icon: icon.moon },
  { value: 'auto', label: 'Як у системі', icon: icon.auto },
] as const
</script>

<template>
  <div class="account" :class="{ collapsed }">
    <TDropdown trigger="click" placement="top-start" :close-on-panel-click="true">
      <template #trigger="{ triggerProps }">
        <button
          v-bind="triggerProps" type="button" class="chip" aria-haspopup="menu"
          :title="collapsed ? `${label}${version ? ` · v${version}` : ''}` : 'Налаштування й тема'"
          :aria-label="`${label}. Налаштування й тема`"
        >
          <span class="avatar" aria-hidden="true">{{ initials }}</span>
          <span v-if="!collapsed" class="who">
            <span class="name">{{ label }}</span>
            <span v-if="version" class="sub">v{{ version }}</span>
          </span>
        </button>
      </template>
      <template #default>
        <div class="menu" role="menu">
          <button type="button" class="menu-item" role="menuitem" @click="emit('settings')">
            <TIcon :icon="icon.settings" />Налаштування
          </button>
          <div class="sep" />
          <button
            v-for="t in themes" :key="t.value" type="button" class="menu-item" role="menuitemradio"
            :aria-checked="currentTheme === t.value" :class="{ on: currentTheme === t.value }" @click="currentTheme = t.value"
          >
            <TIcon :icon="t.icon" />{{ t.label }}
            <TIcon v-if="currentTheme === t.value" :icon="icon.check" class="tick" />
          </button>
        </div>
      </template>
    </TDropdown>
  </div>
</template>

<style scoped>
.account { padding: var(--t-space-2); min-width: 0; }
.account.collapsed { display: flex; justify-content: center; padding-left: 0; padding-right: 0; }
.account > :deep(*) { min-width: 0; }
/* Розгорнута панель: картка на всю ширину футера (TDropdown за замовчуванням inline-block). */
.account:not(.collapsed) > :deep(.t-dropdown) { display: block; width: 100%; }
.chip {
  display: flex; align-items: center; gap: var(--t-space-2); width: 100%; min-width: 0; padding: 6px var(--t-space-2);
  border: none; border-radius: var(--t-radius-default); font: inherit; text-align: left; color: var(--t-color-text); cursor: pointer;
  background: none;
}
.chip:hover, .chip[aria-expanded="true"] { background: var(--t-color-surface-2); }
.chip:focus-visible { outline: var(--t-focus-ring) solid var(--t-color-focus-ring); outline-offset: var(--t-focus-offset); }
.collapsed .chip { width: auto; padding: 0; background: none; border-radius: 50%; }
.avatar {
  width: 28px; height: 28px; border-radius: 50%; flex: none; display: inline-flex; align-items: center; justify-content: center;
  background: var(--t-color-accent-plain-bg); color: var(--t-color-accent); font-size: 11px; font-weight: var(--t-font-weight-semibold);
}
.who { display: flex; flex-direction: column; flex: 1; min-width: 0; line-height: 1.25; }
.name { font-size: var(--t-font-size-default); font-weight: var(--t-font-weight-medium); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.sub { font-family: var(--t-font-mono); font-size: var(--t-font-size-mini); color: var(--t-color-text-muted); white-space: nowrap; }
.menu { display: flex; flex-direction: column; min-width: 220px; padding: var(--t-space-1); background: var(--t-color-surface); border: 1px solid var(--t-color-border); border-radius: var(--t-radius-medium); box-shadow: var(--t-shadow-3); }
.menu-item {
  display: flex; align-items: center; gap: var(--t-space-2); width: 100%; padding: 6px var(--t-space-3); border: none; background: none; text-align: left;
  font: inherit; font-size: var(--t-font-size-small); color: var(--t-color-text); border-radius: var(--t-radius-default); cursor: pointer; white-space: nowrap;
}
.menu-item:hover { background: var(--t-color-surface-2); }
.menu-item.on { color: var(--t-color-accent); }
.tick { margin-left: auto; }
.sep { height: 1px; margin: 4px 2px; background: var(--t-color-border); }
</style>
