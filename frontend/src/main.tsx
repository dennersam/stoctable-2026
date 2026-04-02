import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { hydrateAuth } from '@/store/authStore'

// Hidratar auth antes do primeiro render para evitar flash de tela incorreta
hydrateAuth()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
