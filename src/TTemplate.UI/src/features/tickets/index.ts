// Фіча «Заявки»: плаваюча кнопка (Ctrl+Shift+E) поверх усіх сторінок, список #/tickets і сторінка заявки (бекенд — Features/Tickets).
import type { AppFeature } from '../types'
import TicketsTool from './TicketsTool.vue'
import TicketsView from './TicketsView.vue'
import TicketView from './TicketView.vue'

const feature: AppFeature = {
  id: 'tickets',
  menu: [{ route: '/tickets', title: 'Заявки', icon: 'system-uicons:flag', activeRoutes: ['/tickets'] }],
  routes: [
    { path: '/tickets', name: 'tickets', component: TicketsView },
    { path: '/tickets/:id', name: 'ticket', component: TicketView },
  ],
  overlays: [TicketsTool],
}

export default feature
