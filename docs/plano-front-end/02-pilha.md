# 02 · Pilha do front-end: versões exatas e configurações medidas

Esta é a pilha do `front-end\`. Ela foi escolhida e **medida**; **não se reabre**. Biblioteca nova, versão diferente ou troca só pelo fluxo de dúvidas (`01-regras.md`, seção D). Aqui só estão a decisão e a medição; a discussão ficou para trás.

Convenção deste arquivo: **medido agora** = reproduzi na execução que escreveu este plano (2026-10-30, Windows 11, Node 24.17.0, npm 11.13.0, Edge 156) num projeto de teste que segue este plano, e li o resultado. As medidas marcadas **(2026-11-02)** vieram da revisão do plano, na mesma máquina e no mesmo projeto de teste, com o contrato regenerado (224 operações), o Windows PowerShell 5.1.26100 e, além do Edge 156, o Chrome 149. **Do teste anterior** = resultado de um teste descartável do mesmo dia, que li nos arquivos de evidência dele e **não repeti**. Se o seu ambiente der outro resultado, vale o seu: registre uma dúvida.

## 1. A política de segurança do site (é ela que decide quase tudo)

O site roda no Azure Static Web Apps com estes cabeçalhos (arquivo `front-end\public\staticwebapp.config.json`, Task 5 da `fatia-0.md`):

```
Content-Security-Policy: default-src 'self'; connect-src 'self' <URL da API>
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
Permissions-Policy: geolocation=(), microphone=(), camera=()
```

Consequências. **Barrados** (medido agora pelos controles do script `csp`: `<style>` injetado, atributo `style=""` escrito no HTML, imagem `data:`, `fetch` para origem fora do `connect-src`, e `eval`; do teste anterior: **imagem** `blob:` (`<img src="blob:...">`), fontes e CSS de outro domínio). **Permitido** (teste anterior): mudar `element.style` por script (é o que o React faz com a prop `style`). **Permitido, medido pela sonda de download do `csp` (2026-11-02, Edge 156 e Chrome 149: 0 violações):** baixar arquivo por `Blob` (`URL.createObjectURL` com um `<a download>` clicado por script: isso não é imagem nem requisição) e por link temporário (`<a href target="_blank" rel="noopener noreferrer">`, de outra origem ou da própria). A política só se exerce no Azure; localmente a prova é o script `csp` (seção 6).

## 2. Versões e por que cada uma

| Peça | Versão | Por quê (uma linha) |
|---|---|---|
| Node | `^22.22.2 \|\| ^24.15.0` | É o piso do `jsdom` 30.1.3 (`engines` do pacote, lido em 2026-11-02: `^22.22.2 \|\| ^24.15.0 \|\| >=26.0.0`); o `react-router` 8.5.1 pede `>=22.22.0`. Regra completa abaixo da tabela. |
| npm | o que vem com o Node | Sem pnpm nem yarn. |
| react, react-dom | 19.3.1 | Versão atual da série 19. |
| typescript | **6.0.3** | O template novo do Vite traz o 7, que o `typescript-eslint` 8.72.1 não aceita (`typescript >=4.8.4 <6.1.0`). |
| vite / @vitejs/plugin-react | 8.3.5 / 6.1.2 | Build e servidor de desenvolvimento. |
| eslint, @eslint/js | 10.14.0, 10.0.1 | Lint. |
| typescript-eslint | 8.72.1 | Regras de TypeScript. |
| eslint-plugin-react-hooks / eslint-plugin-react-refresh / globals | 7.1.1 / 0.5.7 / 17.13.0 | Regras de hooks e de componentes; globais do navegador e do Node. |
| prettier | 3.9.10 | Formatação (`format:check` é prova de pronto). |
| react-router | 8.5.1 | Navegação por perfil (entra na fatia 1a; a fatia 0 não usa). |
| @tanstack/react-query | 5.105.0 | Busca de dados e cache. |
| react-hook-form, zod, @hookform/resolvers | 7.91.0, 4.6.7, 6.0.2 | Formulários e validação. Exige `z.config({ jitless: true })` (seção 5.1). |
| tailwindcss, @tailwindcss/vite | 4.3.3 | CSS estático: não injeta `<style>`. |
| @base-ui/react | 1.9.0 | Componentes sem estilo. **Não use Radix**: o Dialog e o Select dele geram violação `style-src-elem`. Exige `<CSPProvider disableStyleElements>` na raiz (seção 5.2). |
| lucide-react | 1.65.0 | Ícones (SVG em componentes, sem arquivo de imagem). |
| @fontsource-variable/inter | 5.3.0 | Fonte servida do próprio domínio. **Inter, decidida pelo dono em 2026-11-02** (a troca para a Hanken Grotesk só se ele pedir depois: `04-visual.md`, seção 4). A pessoa ainda confere a letra ao lado do PNG na conferência visual. |
| clsx, tailwind-merge, class-variance-authority | 2.1.1, 3.7.0, 0.7.1 | Montar classes de CSS. |
| recharts | 3.10.1 | Gráficos. |
| react-markdown | 10.1.0 | Markdown dos avisos, perguntas e treinamentos, **sem HTML cru e sem imagem** (`04-visual.md`, seção 8: medido sob a política com um texto de ataque). Só estes elementos, sem `rehype-raw` nem `remark-gfm`. Entra na fundação (1a, Task 7). |
| vitest | 5.0.5 | Testes. |
| jsdom | 30.1.3 | Ambiente de navegador dos testes. |
| @testing-library/react, /dom, /user-event, /jest-dom | 16.3.3, 10.4.2, 14.6.8, 7.0.1 | Testes de tela. |
| msw | 3.0.3 | Dados simulados (dev e testes). Na v3 a opção é `onUnhandledFrame`, não `onUnhandledRequest`. |
| openapi-typescript | 7.13.0 | Tipos a partir do contrato. Exige `overrides` (seção 5.3). |
| openapi-fetch, openapi-react-query | 0.17.0, 0.5.4 | Cliente tipado e hooks sobre o TanStack Query. |
| yaml | 2.9.1 | Usada pelo `gerar-mocks.mjs` e pelos testes de contrato. |
| playwright-core | 1.64.1 | Só para o script `csp` (abre o Edge ou o Chrome que já existem na máquina; não baixa navegador). |
| @types/node, @types/react, @types/react-dom | 24.19.2, 19.3.0, 19.3.0 | Tipos. |

**Regra do Node** (o mesmo texto está em `LEIA-PRIMEIRO.md`, "Antes de começar", e em `fatia-0.md`, Task 1, passo 1; vale para a sessão toda). Aceite `^22.22.2 || ^24.15.0` (`node -v`): a pilha foi medida com a 22.22.2 ou mais nova da série 22 e com a 24.15.0 ou mais nova da série 24 (o plano foi medido na 24.17.0). Versão **26 ou mais nova**: siga, mas registre `Node 26: não medido` no `PROGRESSO.md`; se alguma prova falhar **só** nela, peça à pessoa que instale o **Node 24 LTS** (nodejs.org, página de versões anteriores) e registre `pendente: Node`. **Depois de instalar:** feche e reabra o terminal, confira que `node -v` escreve `v24.15.0` ou mais nova, rode `Remove-Item -Recurse -Force front-end\node_modules; npm --prefix front-end ci`, refaça a prova que falhou e troque `pendente: Node` por `feito` no `PROGRESSO.md`. Versão abaixo da faixa (22 anterior à 22.22.2, 24 anterior à 24.15.0, 23, 25 ou 20 e mais antigas): peça a mesma instalação (Node 24 LTS), registre `pendente: Node` e não siga sem ela. **Escopo:** esta regra é do front. O plano do back-end (`docs\superpowers\plans\2026-10-21-fatia-0-esqueleto.md`, Task 1, passo 2) **fixa o Node 24** (`^24.15.0`) e o CI usa `node-version: 24`; o Node 24 LTS atende os dois planos, e as duas regras não se contradizem (a do back-end é mais estreita; a do front aceita também a série 22 e deixa a 26 seguir, registrada).

Os gráficos, o markdown, as rotas e os componentes só serão usados a partir da fatia 1a, mas **todas as peças (a `react-markdown` inclusive) são instaladas já na Task 1 da fatia 0**: um só `package-lock.json`, sem mexer nas versões depois. Nenhuma tarefa seguinte roda `npm install` de biblioteca.

## 3. `package.json` (copie exatamente)

Medido agora: `npm install` deste arquivo instalou sem erro de dependência; `npm ci` a partir do `package-lock.json` gerado, numa pasta limpa, também. **(2026-11-02, com a `react-markdown`)**: `npm install` do zero, 2 min 42 s; `npm ci`, 452 pacotes instalados em cerca de 1 minuto (com o cache do npm aquecido), sem erro.

```json
{
  "name": "rh-system-front-end",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "engines": {
    "node": "^22.22.2 || ^24.15.0"
  },
  "scripts": {
    "contrato:tipos": "openapi-typescript ../contrato/openapi.yaml -o src/shared/api/gerado/schema.d.ts",
    "contrato:mocks": "node scripts/gerar-mocks.mjs ../contrato/openapi.yaml src/shared/mocks/gerado/handlers.ts",
    "contrato:gerar": "npm run contrato:tipos && npm run contrato:mocks",
    "predev": "npm run contrato:gerar",
    "dev": "vite",
    "prebuild": "npm run contrato:gerar",
    "build": "tsc -b && vite build",
    "postbuild": "node -e \"require('node:fs').rmSync('dist/mockServiceWorker.js',{force:true})\"",
    "lint": "eslint .",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "pretest": "npm run contrato:gerar",
    "test": "vitest run",
    "csp": "node scripts/violacoes-de-politica.mjs"
  },
  "dependencies": {
    "@base-ui/react": "1.9.0",
    "@fontsource-variable/inter": "5.3.0",
    "@hookform/resolvers": "6.0.2",
    "@tanstack/react-query": "5.105.0",
    "class-variance-authority": "0.7.1",
    "clsx": "2.1.1",
    "lucide-react": "1.65.0",
    "openapi-fetch": "0.17.0",
    "openapi-react-query": "0.5.4",
    "react": "19.3.1",
    "react-dom": "19.3.1",
    "react-hook-form": "7.91.0",
    "react-markdown": "10.1.0",
    "react-router": "8.5.1",
    "recharts": "3.10.1",
    "tailwind-merge": "3.7.0",
    "zod": "4.6.7"
  },
  "devDependencies": {
    "@eslint/js": "10.0.1",
    "@tailwindcss/vite": "4.3.3",
    "@testing-library/dom": "10.4.2",
    "@testing-library/jest-dom": "7.0.1",
    "@testing-library/react": "16.3.3",
    "@testing-library/user-event": "14.6.8",
    "@types/node": "24.19.2",
    "@types/react": "19.3.0",
    "@types/react-dom": "19.3.0",
    "@vitejs/plugin-react": "6.1.2",
    "eslint": "10.14.0",
    "eslint-plugin-react-hooks": "7.1.1",
    "eslint-plugin-react-refresh": "0.5.7",
    "globals": "17.13.0",
    "jsdom": "30.1.3",
    "msw": "3.0.3",
    "openapi-typescript": "7.13.0",
    "playwright-core": "1.64.1",
    "prettier": "3.9.10",
    "tailwindcss": "4.3.3",
    "typescript": "6.0.3",
    "typescript-eslint": "8.72.1",
    "vite": "8.3.5",
    "vitest": "5.0.5",
    "yaml": "2.9.1"
  },
  "msw": {
    "workerDirectory": [
      "public"
    ]
  },
  "overrides": {
    "openapi-typescript": {
      "typescript": "$typescript"
    }
  }
}
```

## 4. Os scripts e por quê

Os comandos abaixo rodam **da raiz do repositório, no PowerShell**, com `npm --prefix front-end run <script>`. O `npm test` aceita filtro, **sem `--`**: `npm --prefix front-end test src/shared/api` (medido em 2026-11-02 no Windows PowerShell 5.1 com npm 10.9.2 e 11.13.0: o filtro chega ao vitest e roda só os arquivos que casam). Só um **flag** do vitest precisa do `--` antes (`npm --prefix front-end test -- --reporter=dot src/shared/api`): sem ele o npm engole o flag e avisa `Unknown cli config`.

| Script | O que faz | Por quê |
|---|---|---|
| `dev` | Servidor de desenvolvimento com MSW ligado | Trabalhar sem back-end. |
| `build` | `tsc -b` e depois `vite build` | O `tsc` é a verificação de tipos: **é ele que acusa a tela quebrada quando o contrato muda**. |
| `lint` | `eslint .` | Prova de pronto. |
| `format`, `format:check` | Prettier escreve / confere | `format:check` é prova de pronto. |
| `test` | `vitest run` (uma passada) | Prova de pronto. |
| `contrato:tipos` | Gera `src\shared\api\gerado\schema.d.ts` do `contrato\openapi.yaml` | Tipos das rotas, dos corpos e das respostas. |
| `contrato:mocks` | Gera **dois** arquivos em `src\shared\mocks\gerado\` (`ferramentas\gerar-mocks.mjs`): `handlers.ts` (um handler do MSW por operação; exporta `versaoDoContrato`, `exemplos` e `handlers`) e `exemplos.ts` (todos os exemplos de cada resposta; exporta `exemplosPorOperacao`) | Dados simulados vindos dos `example` do contrato. **Os nomes são parecidos, não os confunda:** `exemplos` (de `handlers.ts`) é só o primeiro exemplo 2xx; `exemplosPorOperacao` (de `exemplos.ts`) é todos. **Em teste, importe `exemploDoContrato(operationId, status?, nome?)` de `src\shared\teste\`** (ele lê `exemplosPorOperacao` e devolve uma cópia: `03-contrato-e-mocks.md`, seção 5); o primeiro exemplo 2xx também está em `exemplos.<operationId>.corpo`. |
| `contrato:gerar` | Os dois acima | Atalho. |
| `predev`, `prebuild`, `pretest` | Rodam `contrato:gerar` antes | O arquivo gerado nunca fica velho em relação ao contrato; por isso **não vai para o git** (`.gitignore`). Custa uns 6 s por comando. |
| `postbuild` | Apaga `dist\mockServiceWorker.js` | O Vite copia o service worker do MSW de `public\` para `dist\`; ele não pode ir para o ar (seção 5.4). |
| `csp` | Roda `scripts\violacoes-de-politica.mjs` sobre o `dist` | Prova que o build roda sem violação da política (seção 6). Precisa do build antes. |

## 5. Configurações medidas, uma a uma

### 5.1 zod: `z.config({ jitless: true })`
- **Decisão:** o arquivo `src\shared\zod.ts` faz isso e é importado **primeiro** em `main.tsx`.
- **Medição (agora):** sem essa linha, o build sob a política gera violação `script-src` bloqueando `eval` ao carregar a tela de entrar (o zod tenta `eval` ao criar um esquema); com ela, 0 violações. O script `csp` pega o caso.

### 5.2 Base UI: `<CSPProvider disableStyleElements>` na raiz
- **Decisão:** `main.tsx` envolve o app com `CSPProvider` de `@base-ui/react/csp-provider`, com `disableStyleElements`. Nunca aninhe outro `CSPProvider` sem essa opção: o de dentro a desliga.
- **Medição (do teste anterior):** sem a opção, só o **Select** violou (1 violação `style-src-elem`). Com ela: 0 violações em Dialog, Select, Popover, Tooltip, Tabs, Menu, Checkbox, RadioGroup, Switch, Accordion, Collapsible, Toast, Combobox, Autocomplete, Progress e Slider, em `<input type="date">` nativo e em recharts. Radix, na mesma política: 1 violação no Dialog e 2 no Select.

### 5.3 `overrides` do openapi-typescript
- **Decisão:** o bloco `overrides` do `package.json` (seção 3) faz o `openapi-typescript` usar o TypeScript do projeto.
- **Medição (agora):** sem o bloco, `npm install` falha com `ERESOLVE` (`peer typescript@"^5.x" from openapi-typescript@7.13.0`); com ele, instala. Com o TypeScript 6.0.3 e os tipos gerados do contrato de 224 operações (1,4 MB), `tsc -b` passa sem erro.

### 5.4 `assetsInlineLimit: 0` e `postbuild`
- **Decisão:** `build.assetsInlineLimit: 0` no `vite.config.ts`; `postbuild` apaga `dist\mockServiceWorker.js`; o MSW só roda em desenvolvimento (`import.meta.env.DEV`) e nos testes.
- **Medição (agora):** com `assetsInlineLimit: 0`, um SVG de 94 bytes vira arquivo à parte em `dist\assets`; sem a opção, o Vite o embute no CSS como `data:image/svg+xml`, e a política o barra (`img-src`). O `csp` falha se `dist\mockServiceWorker.js` existir ou se o JS do build mencionar `setupWorker`/`mockServiceWorker` (testado nos dois sentidos).

### 5.5 Testes: polyfills do jsdom, `onUnhandledFrame`, multipart
- **Medição (agora):** no jsdom 30.1.3, `hasPointerCapture`, `setPointerCapture` e `scrollIntoView` **não existem** (`typeof` dá `undefined`); `src\test\setup.ts` os define (os componentes do Base UI e o `userEvent` usam). No msw 3.0.3, `servidor.listen({ onUnhandledFrame: 'error' })` faz a requisição sem handler falhar (teste `ambiente.test.ts`).
- **Upload multipart** (corpo `multipart/form-data`): o teste precisa do ambiente Node (primeira linha `// @vitest-environment node`), porque `File` e `FormData` do jsdom não casam com o `fetch` do Node. Padrão medido agora em `src\shared\api\upload.node.test.ts` (Task 2) e descrito em `03-contrato-e-mocks.md`.

### 5.6 TypeScript, ESLint e Prettier
- `tsconfig.app.json` usa `erasableSyntaxOnly`: nada de `enum` nem de propriedade no parâmetro do construtor (`constructor(private x)`); escreva o campo e atribua.
- O ESLint 10 com `eslint-plugin-react-hooks` 7 tem regras novas que acusam, por exemplo, **reatribuir variável de fora dentro de um componente** (`react-hooks/globals`) e exportar componente e não-componente no mesmo arquivo (`react-refresh/only-export-components`). Medido agora: foi por isso que o contexto de autenticação ficou em `contextoDeAutenticacao.ts`, e que os testes de hook usam `renderHook`.
- Prettier: `{ "semi": false, "singleQuote": true, "printWidth": 100 }`. Os scripts de `scripts\` também passam pelo Prettier e pelo ESLint.

## 6. Como a política é provada em casa

O script `scripts\violacoes-de-politica.mjs` serve o `dist` com os cabeçalhos **lidos de `public\staticwebapp.config.json`** (`scripts\servidor-de-teste.mjs`), abre a página no Edge ou no Chrome em modo sem janela, percorre entrar → validação → shell → recarregar → sair → link direto, e **falha** se houver `securitypolicyviolation`, exceção na página ou requisição que falhou. Ele também faz **controles positivos** (injeta de propósito `<style>`, `style=""`, imagem `data:` e `fetch` para origem proibida) para provar que a política está ativa; se um controle não violar, o resultado "zero violações" **não vale** e o script falha. E faz a **sonda de download** (um CSV por `Blob` e dois links temporários, um de outra origem e um da própria; `03-contrato-e-mocks.md`, seção 8). Ele usa o **navegador já instalado** (Edge ou Chrome); sem nenhum dos dois, sai com código 2 e escreve `pendente: csp`. Cada passo que falha escreve uma linha `[FALHOU]`; os passos da **vitrine** saem `[pulado]` (sem falhar) no build sem `VITE_VITRINE=sim`, e **só** eles podem ser pulados. Detalhes e como estender: `03-contrato-e-mocks.md`, seção 8.

Medido agora (Task 5, projeto de teste): 0 violações reais, 4 de 4 controles violaram. Casos negativos provocados e acusados: sem `jitless` (violação `eval`), `mockServiceWorker.js` em `dist`, MSW importado no `main.tsx` (código do MSW no JS), `connect-src` com outra origem que a do build (violação `connect-src`). **(2026-11-02)** Com a sonda: 9 passos `[ok ]` (os 6 de sempre e os 3 da sonda), 0 violações reais e 4 de 4 controles, no Edge 156 e no Chrome 149; negativo provocado: a sonda por `Blob` apontada para uma origem fora do `connect-src` falhou (código de saída 1) com a violação `connect-src` registrada no passo certo; sem navegador (`--canais` com um canal que não existe) o script saiu com código 2 e escreveu `pendente: csp`.

## 7. Arquivos de configuração (copie exatamente)

`front-end\vite.config.ts`:

```ts
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
```

`front-end\tsconfig.json`:

```json
{
  "files": [],
  "references": [{ "path": "./tsconfig.app.json" }, { "path": "./tsconfig.node.json" }]
}
```

`front-end\tsconfig.app.json`:

```json
{
  "compilerOptions": {
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.app.tsbuildinfo",
    "target": "es2023",
    "lib": ["ES2023", "DOM"],
    "module": "esnext",
    "types": ["vite/client", "node"],
    "allowArbitraryExtensions": true,
    "skipLibCheck": true,
    "strict": true,

    /* Bundler mode */
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "verbatimModuleSyntax": true,
    "moduleDetection": "force",
    "noEmit": true,
    "jsx": "react-jsx",

    /* Linting */
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "erasableSyntaxOnly": true,
    "noFallthroughCasesInSwitch": true
  },
  "include": ["src"]
}
```

`front-end\tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.node.tsbuildinfo",
    "target": "es2023",
    "lib": ["ES2023"],
    "types": ["node"],
    "skipLibCheck": true,

    /* Bundler mode */
    "module": "nodenext",
    "allowImportingTsExtensions": true,
    "verbatimModuleSyntax": true,
    "moduleDetection": "force",
    "noEmit": true,

    /* Linting */
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "erasableSyntaxOnly": true,
    "noFallthroughCasesInSwitch": true
  },
  "include": ["vite.config.ts"]
}
```

`front-end\eslint.config.js`:

```js
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
```

`front-end\.prettierrc`:

```json
{ "semi": false, "singleQuote": true, "printWidth": 100 }
```

`front-end\.prettierignore`:

```
dist
node_modules
package-lock.json
public/mockServiceWorker.js
src/shared/api/gerado
src/shared/mocks/gerado
```

`front-end\.gitignore`:

```
node_modules
dist
*.log
.env
.env.local
.env.*.local
src/shared/api/gerado
src/shared/mocks/gerado
```

`front-end\.env.example`:

```
# Copie para .env.local (não vai para o git). Valores de desenvolvimento.
# URL da API: o mesmo valor que o dono põe no connect-src de public/staticwebapp.config.json.
VITE_API_URL=http://localhost:5000
# Em dev os dados vêm do MSW. Ponha "nao" só para falar com a API real.
VITE_MOCKS=sim
```

`front-end\index.html`:

```html
<!doctype html>
<html lang="pt-BR">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>RH-System</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`front-end\src\vite-env.d.ts`:

```ts
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string
  readonly VITE_MOCKS?: string
}
```

`front-end\public\favicon.svg` (um marcador simples; troque quando houver ícone do produto):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="6" fill="#1d4ed8"/><text x="16" y="22" font-family="sans-serif" font-size="16" font-weight="700" text-anchor="middle" fill="#fff">RH</text></svg>
```

`front-end\src\index.css`:

```css
@import 'tailwindcss';
@import '@fontsource-variable/inter';

@theme {
  --font-sans: 'Inter Variable', ui-sans-serif, system-ui, sans-serif;
}

body {
  font-family: var(--font-sans);
}
```

## 8. Datas, CPF e telefone

A API fala UTC (`...Z`); a tela mostra em `America/Sao_Paulo` com `Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo' })`. **Data de calendário** (`date`, ex.: admissão) não se converte: é o dia. **Horário do dia** (`HH:mm`) também não. CPF e telefone: duas funções simples escritas na fundação (1a), sem biblioteca. Largura de projeto: o Figma é 1440 px; a demonstração pode ser num projetor de 1366×768 (provisório, a conferir com a pessoa).
