import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    // A política do site não tem img-src nem font-src próprios: nada de data: URI
    // (o padrão do Vite embute no JS arquivos abaixo de 4 KiB, SVG pequeno incluído).
    assetsInlineLimit: 0,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: false,
  },
})
