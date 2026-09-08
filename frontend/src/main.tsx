import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { hydrateAuth } from '@/store/authStore'
import { hydrateTheme } from '@/store/themeStore'

// Hidratar auth antes do primeiro render para evitar flash de tela incorreta
hydrateAuth()

// Idem para o tema: a classe .dark vai no <html>, então precisa estar lá antes
// de qualquer pixel ser pintado.
hydrateTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
