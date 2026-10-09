import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Geliştirmede /api isteklerini yerel API'ye yönlendiririz: tarayıcı için aynı origin olur, CORS gerekmez.
// Canlıda arayüz ayrı alan adında durur ve VITE_API_URL ile API'yi doğrudan çağırır (docs/DEPLOY.md).
const apiTarget = process.env.VITE_DEV_API_TARGET ?? 'http://localhost:8080'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
