<script setup lang="ts">
// Сегментований вибір з чітко видимим обраним станом: TButtonGroup + TButton (обраний — accent/filled, решта — neutral/plain).
// `iconOnly` — лише іконки, підпис у title/aria-label.
import type { TButtonSize } from '@vitaliysimkin/t-components'

export interface ChoiceOption { value: string; label: string; icon?: string }

const props = withDefaults(defineProps<{ modelValue: string; options: ChoiceOption[]; size?: TButtonSize; iconOnly?: boolean }>(), { size: 'small' })
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

function pick(v: string) { if (v !== props.modelValue) emit('update:modelValue', v) }
</script>

<template>
  <TButtonGroup role="group">
    <TButton
      v-for="o in options" :key="o.value" type="button" :size="size" :icon="o.icon"
      :variant="o.value === modelValue ? 'accent' : 'neutral'" :mode="o.value === modelValue ? 'filled' : 'plain'"
      :aria-pressed="o.value === modelValue ? 'true' : 'false'" :label="iconOnly ? undefined : o.label"
      :title="iconOnly ? o.label : undefined" :aria-label="iconOnly ? o.label : undefined" @click="pick(o.value)"
    />
  </TButtonGroup>
</template>
