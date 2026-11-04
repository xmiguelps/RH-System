import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores([
    'dist',
    'public/mockServiceWorker.js',
    'src/shared/api/gerado',
    'src/shared/mocks/gerado',
  ]),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
  {
    files: ['scripts/**/*.mjs', 'vite.config.ts'],
    extends: [js.configs.recommended],
    languageOptions: {
      // o violacoes-de-politica.mjs tem funcoes que rodam dentro do navegador (page.evaluate)
      globals: { ...globals.node, ...globals.browser },
    },
  },
])
