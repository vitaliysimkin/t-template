import { createApp } from 'vue'
import TComponents from '@vitaliysimkin/t-components'
import '@vitaliysimkin/t-components/style.css'
import '@vitaliysimkin/t-components/icons'
import './lib/icons'
import './styles/base.css'
import App from './App.vue'
import { router } from './router'

createApp(App).use(TComponents).use(router).mount('#app')
