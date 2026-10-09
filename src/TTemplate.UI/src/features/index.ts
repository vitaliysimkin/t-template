// Реєстр фіч. Порядок тут = порядок пунктів у бічній панелі (між «Головна» і «Налаштування»).
import type { AppFeature } from './types'
// #feature:tickets
import tickets from './tickets'
// #end
// #feature:jobs
import jobs from './jobs'
// #end

export const features: AppFeature[] = [
  // #feature:tickets
  tickets,
  // #end
  // #feature:jobs
  jobs,
  // #end
]
