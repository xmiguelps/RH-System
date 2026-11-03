# Front-end, fatia 0: projeto, login, shell e política de segurança: plano de implementação

> **Para quem executa (sessão de IA ou pessoa):** execute este plano tarefa por tarefa, na ordem. Os passos usam a sintaxe de caixa de seleção (`- [ ]`) para acompanhamento. **O método de execução (subagentes ou na própria sessão) ainda não foi escolhido: é decidido depois do planejamento.** Antes de começar, leia `LEIA-PRIMEIRO.md` e `01-regras.md`; este arquivo assume as duas coisas.

**Goal:** Entregar o projeto `front-end\` (Vite, React, TypeScript) com a pilha do plano, o cliente de API e os dados simulados gerados do contrato, a tela de entrar, o shell do portal e a prova de que o build roda sob a política de segurança do site.

**Architecture:** O app tem uma única porta de acesso (`PortaDeAcesso`): sem usuário mostra a tela de entrar, com usuário mostra o shell; **não há biblioteca de rotas nesta fatia** (a navegação é da fatia 1a). O cliente de API é `openapi-fetch` tipado pelo contrato; todo erro `problem+json` vira `ErroDeApi`. O token de acesso fica em memória; o refresh token, no `localStorage` (`rh.refresh`), renovado com uma renovação por vez, serializada entre abas. Tipos e dados simulados (MSW) saem do `contrato\openapi.yaml` por geradores, e a política de segurança é provada por um servidor de teste mais um navegador sem janela.

**Tech Stack:** Vite 8.3.5, React 19.3.1, TypeScript 6.0.3, Vitest 5.0.5, Testing Library, MSW 3.0.3, openapi-typescript 7.13.0, openapi-fetch 0.17.0, openapi-react-query 0.5.4, react-hook-form 7.91.0 com zod 4.6.7, react-markdown 10.1.0 (só usada a partir da 1a, mas instalada aqui), Tailwind 4.3.3, Base UI 1.9.0, Playwright Core 1.64.1 (lista completa e versões exatas em `02-pilha.md`).

**Spec:** `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md` (seções 5, 6 e 8); contrato `contrato\openapi.yaml` (o que vale para a fatia 0 está em `contrato\fontes\10-fatia-0-autenticacao.yaml`: `entrar`, `renovarSessao`, `sair`, `obterUsuarioLogado`, `verificarSaude`); decisões de sessão e de política em `docs\superpowers\plans\2026-10-21-fatia-0-esqueleto.md` (Tasks 10 e 11 foram para cá).

## Global Constraints

- Node `^22.22.2 || ^24.15.0` (a 26 ou mais nova segue, registrada como `Node 26: não medido`; a regra completa está no passo 1 da Task 1 e em `02-pilha.md`, seção 2); versões exatas de `02-pilha.md`; nada fora de `02-pilha.md`.
- Comandos da **raiz do repositório**, no **Windows PowerShell 5.1** (sem `&&`): `npm --prefix front-end run <script>`; filtro de teste **sem `--`**, com barras normais: `npm --prefix front-end test src/shared/api` (`01-regras.md`, A14).
- Git: autorização pedida **uma vez**, **antes** de criar a branch (`LEIA-PRIMEIRO.md`, "Primeira ação"); commit e push a cada passo que manda commitar, na branch `feat/front-fatia-0`; mensagem em português **sem acento**; **nunca o Claude como coautor** (`01-regras.md`, C6).
- Textos ao usuário em português do Brasil **com acento**; nomes de código em português **sem acento**.
- Política de segurança: `default-src 'self'; connect-src 'self' <URL da API>` mais `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy: geolocation=(), microphone=(), camera=()`. Sem `style-src`, sem `img-src`: nada de `<style>` injetado, `style=""` no HTML, imagem `data:`/`blob:`, fonte externa, `eval`.
- Token de acesso **só em memória**; refresh token no `localStorage`, chave **`rh.refresh`**.
- O contrato é lei e **não se toca** (`contrato\`, `back-end\`, `Banco de Dados\`, `.github\`, `docs\superpowers\`, arquivos da raiz).
- Mensagens da tela de entrar: 401 `E-mail ou senha inválidos.`; 403 `Seu acesso está desativado. Fale com o RH.`; 400 `Preencha o e-mail e a senha.` (é o `detail` do exemplo do 400 de `entrar`, `Erro400Login` no contrato 0.2.1; só essa rota o usa, e os outros 400 dizem `Confira os campos indicados.`); 429 `Muitas tentativas. Aguarde um minuto e tente de novo.`; 500 `Erro no servidor. Tente novamente em instantes.`; falha de rede `Não foi possível conectar. Tente novamente.`
- Cada tarefa termina com os comandos de prova: `test`, `lint`, `format:check`, `build` (e `csp`, a partir da Task 5).

## Review Focus

Falhas que o contrato e o desenho implicam, que um usuário real encontra e que nenhum teste do plano antigo exercia (cada uma tem teste na tarefa indicada):

1. **`localStorage` indisponível** (navegação privada, dados do site bloqueados): ler, gravar e limpar não podem lançar; a tela de entrar continua funcionando. Task 3.
2. **API devolve 200 que não é uma sessão** (defeito do back-end): o cliente lança `ErroDeApi` 500 (Task 2), que a tela de entrar mostra como erro do servidor (Task 4), em vez de quebrar.
3. **Senha com espaço no começo ou no fim** é válida e **não pode ser alterada** pela tela (nada de `trim` na senha). Task 4.
4. **Clique duplo em "Entrar"**: uma única requisição. Task 4.
5. **Perfil que o contrato não conhece** (valor novo no enum): `ehSessao` rejeita, em vez de o shell mostrar um rótulo vazio. Task 2.

---

### Task 1: Projeto, ferramentas e pipeline do contrato

**Files:**
- Create: `front-end\package.json`, `package-lock.json` (gerado), `vite.config.ts`, `tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, `eslint.config.js`, `.prettierrc`, `.prettierignore`, `.gitignore`, `.env.example`, `index.html`, `public\favicon.svg`, `public\mockServiceWorker.js` (gerado por `msw init`), `src\vite-env.d.ts`, `src\index.css`, `src\main.tsx` (provisório), `src\shared\zod.ts`, `src\shared\mocks\servidor.ts`, `src\shared\mocks\navegador.ts`, `src\shared\teste\exemploDoContrato.ts`, `src\test\setup.ts`, `scripts\gerar-mocks.mjs` (cópia).
- Test: `src\test\ambiente.test.ts`, `src\shared\zod.test.ts`, `src\shared\mocks\mocks.test.ts`, `src\shared\teste\exemploDoContrato.test.ts`.

**Interfaces:**
- Produces: os scripts de `02-pilha.md` (seção 4); `src\shared\api\gerado\schema.d.ts` (gerado; exporta `paths`, `components`, `operations`); `src\shared\mocks\gerado\handlers.ts` (gerado; exporta `versaoDoContrato: string`, `exemplos: Record<string, Exemplo>` e `handlers`); `src\shared\mocks\gerado\exemplos.ts` (gerado pelo mesmo script; exporta `exemplosPorOperacao: Record<string, Record<string, Record<string, unknown>>>`, ou seja, `operationId` → status (texto) → nome → corpo, **todas** as respostas e **todos** os exemplos, erros e `examples` nomeados incluídos; o `example` único tem o nome `padrao`); `servidor` (`setupServer(...handlers)`, em `servidor.ts`) e `worker` (`setupWorker(...handlers)`, em `navegador.ts`); `src\shared\zod.ts` (efeito colateral: `z.config({ jitless: true })`); `src\shared\teste\exemploDoContrato.ts`: `function exemploDoContrato<T = unknown>(operationId: string, status?: number, nome?: string): T` (cópia do exemplo; sem `status` o primeiro 2xx, sem `nome` o primeiro exemplo da resposta; operação, status ou nome inexistente lança erro que diz o que existe). **Todo teste que "carrega o exemplo X do contrato" com um nome ou de um status que não é o 2xx padrão usa este ajudante**, nunca um texto copiado à mão (`03-contrato-e-mocks.md`, seção 5); o primeiro exemplo 2xx segue em `exemplos.<operationId>.corpo` (por exemplo `exemplos.entrar.corpo`, a sessão de base).

- [ ] **Step 1: Conferir o ambiente.** Rode `node -v; npm -v; git status; git branch --show-current`. Expected: Node na faixa da **regra do Node**, que é esta (o mesmo texto está em `LEIA-PRIMEIRO.md` e em `02-pilha.md`, seção 2): aceite `^22.22.2 || ^24.15.0` (`node -v`): a pilha foi medida com a 22.22.2 ou mais nova da série 22 e com a 24.15.0 ou mais nova da série 24 (o plano foi medido na 24.17.0). Versão **26 ou mais nova**: siga, mas registre `Node 26: não medido` no `PROGRESSO.md`; se alguma prova falhar **só** nela, peça à pessoa que instale o **Node 24 LTS** (nodejs.org, página de versões anteriores) e registre `pendente: Node`. **Depois de instalar:** feche e reabra o terminal, confira que `node -v` escreve `v24.15.0` ou mais nova, rode `Remove-Item -Recurse -Force front-end\node_modules; npm --prefix front-end ci`, refaça a prova que falhou e troque `pendente: Node` por `feito` no `PROGRESSO.md`. Versão abaixo da faixa (22 anterior à 22.22.2, 24 anterior à 24.15.0, 23, 25 ou 20 e mais antigas): peça a mesma instalação (Node 24 LTS), registre `pendente: Node` e não siga sem ela. **Escopo:** esta regra é do front. O plano do back-end (`docs\superpowers\plans\2026-10-21-fatia-0-esqueleto.md`, Task 1, passo 2) **fixa o Node 24** (`^24.15.0`) e o CI usa `node-version: 24`; o Node 24 LTS atende os dois planos, e as duas regras não se contradizem (a do back-end é mais estreita; a do front aceita também a série 22 e deixa a 26 seguir, registrada). Você está na branch `feat/front-fatia-0` (criada na primeira ação de `LEIA-PRIMEIRO.md`, depois do "autorizo"; sem autorização, não há branch e o trabalho segue sem commit: `01-regras.md`, C1); existem `contrato\openapi.yaml` e `docs\plano-front-end\ferramentas\gerar-mocks.mjs`. Registre a versão do contrato (`01-regras.md`, E4).
- [ ] **Step 2: Criar os arquivos de configuração** de `front-end\`, com o conteúdo **exato** de `02-pilha.md`: `package.json` (seção 3, que já traz a `react-markdown` 10.1.0: nenhuma tarefa seguinte instala biblioteca) e, da seção 7, `vite.config.ts`, `tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, `eslint.config.js`, `.prettierrc`, `.prettierignore`, `.gitignore`, `.env.example`, `index.html`, `src\vite-env.d.ts`, `public\favicon.svg` e `src\index.css`. Crie também `src\main.tsx` provisório: `createRoot(document.getElementById('root')!).render(<StrictMode><p>RH-System</p></StrictMode>)` importando `./index.css` (a Task 4 o substitui).
- [ ] **Step 3: Instalar.** Run: `npm --prefix front-end install; Push-Location front-end; npx msw init public --save; Pop-Location`
  Expected: instala sem `ERESOLVE` (cerca de 2 minutos), cria `front-end\package-lock.json` e `front-end\public\mockServiceWorker.js` (o `--save` repete o bloco `msw` que já está no `package.json`).
- [ ] **Step 4: Copiar o gerador e gerar tipos e mocks.** Run: `New-Item -ItemType Directory -Force front-end\scripts; Copy-Item docs\plano-front-end\ferramentas\gerar-mocks.mjs front-end\scripts\gerar-mocks.mjs; npm --prefix front-end run contrato:gerar`
  Expected: `schema.d.ts` e `src\shared\mocks\gerado\exemplos.ts` gerados e, no resumo do gerador, `"handlersGerados": 224`, `"respostasComExemplo"` e `"totalDeExemplos"` maiores que zero (hoje 1236 e 1394), `"semExemploUtilizavel": []`, `"ambiguos": []` e a `versaoDoContrato` do passo 1.
- [ ] **Step 5: Escrever os testes que falham.**

```ts
// src\test\ambiente.test.ts
it('jsdom ganhou os polyfills que o Base UI e o userEvent usam')   // document.createElement('div'): hasPointerCapture(1) === false; scrollIntoView() não lança
it('requisicao sem handler falha (onUnhandledFrame: error)')        // await expect(fetch('https://api.exemplo.test/api/rota-que-nao-existe')).rejects.toThrow()

// src\shared\zod.test.ts
it('z.config jitless fica ligado (sem isso o zod gera eval e a politica do site barra)')  // import './zod'; expect(z.config().jitless).toBe(true); e um safeParse simples funciona

// src\shared\mocks\mocks.test.ts   (primeira linha: // @vitest-environment node; chame com fetch('https://api.exemplo.test' + caminho))
it('ha um handler por operacao do contrato e a versao e a do contrato')
//   contrato = parse(readFileSync('../contrato/openapi.yaml', 'utf8')) (pacote yaml; o teste roda dentro de front-end\);
//   n = quantidade de pares (caminho, método em get|post|put|patch|delete) de contrato.paths;
//   expect(Object.keys(exemplos)).toHaveLength(n); expect(handlers).toHaveLength(n); expect(versaoDoContrato).toBe(contrato.info.version)
it('as rotas de autenticacao devolvem o example do contrato')
//   POST /api/auth/entrar -> usuario toMatchObject { nome: 'Ana Souza', perfil: 'RH', cargo: 'Gerente de RH', setor: 'Recursos Humanos' }
//   POST /api/auth/sair -> status 204;  GET /health -> { status: 'ok' }
it('rota literal vence a rota com parametro (resumo antes de {avisoId})')
//   GET /api/rh/avisos/resumo -> corpo tem a chave 'contagens';  GET /api/rh/avisos/409 -> corpo.id === 409
it('exportacao devolve o tipo de arquivo do contrato')
//   GET exemplos.exportarAuditoria.caminho -> cabeçalho content-type contém 'text/csv'

// src\shared\teste\exemploDoContrato.test.ts
it('sem status devolve o primeiro exemplo 2xx, o mesmo que o handler gerado devolve')   // exemploDoContrato('entrar') toEqual exemplos.entrar.corpo; o mesmo para 'listarFuncionarios'
it('devolve o exemplo nomeado de um erro (o 409 cpfInativo de criarFuncionario)')   // codigo 'CPF_JA_CADASTRADO' e cadastroExistente { funcionarioId: 201, nome: 'Henrique Alves', ativo: false }
it('sem nome devolve o primeiro exemplo da resposta (cpf, no 409 de criarFuncionario)')   // cadastroExistente.funcionarioId 12
it('devolve uma copia: alterar o resultado nao muda o proximo')   // entrar: usuario.nome = 'Outra Pessoa'; a chamada seguinte devolve 'Ana Souza'
it('operacao, status ou nome que nao existem falham dizendo o que existe')   // 'operacaoInventada' lança citando o nome; ('criarFuncionario', 418) lança /tem: .*409/; ('criarFuncionario', 409, 'nada') lança /tem: cpf, email, cpfInativo, emailInativo/
```

- [ ] **Step 6: Rodar e ver falhar.** Run: `npm --prefix front-end test`
  Expected: FAIL (`src\test\setup.ts` e os módulos `zod.ts`, `servidor.ts` e `exemploDoContrato.ts` não existem).
- [ ] **Step 7: Implementar.**
  - `src\test\setup.ts`: `import '@testing-library/jest-dom/vitest'`; `beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }))`; `afterEach`: `cleanup()` (se houver `document`) e `servidor.resetHandlers()`; `afterAll(() => servidor.close())`; e, se `window` existir, definir em `HTMLElement.prototype` `hasPointerCapture = () => false`, `setPointerCapture`, `releasePointerCapture` e `scrollIntoView` como funções vazias (o jsdom 30.1.3 não as tem: `02-pilha.md`, seção 5.5).
  - `src\shared\mocks\servidor.ts` (`export const servidor = setupServer(...handlers)`, de `msw/node`) e `src\shared\mocks\navegador.ts` (`export const worker = setupWorker(...handlers)`, de `msw/browser`), ambos importando `handlers` de `./gerado/handlers`.
  - `src\shared\zod.ts`: `import { z } from 'zod'` e `z.config({ jitless: true })`, com um comentário de uma linha dizendo por quê.
  - `src\shared\teste\exemploDoContrato.ts`: lê `exemplosPorOperacao` de `../mocks/gerado/exemplos`; sem `status` usa o primeiro código `2xx` da operação; sem `nome` usa o primeiro exemplo da resposta; devolve uma cópia (`JSON.parse(JSON.stringify(...))`); as mensagens de erro, em português, citam a operação e terminam com `tem: <o que existe>`.
- [ ] **Step 8: Rodar e ver passar.** Run: `npm --prefix front-end test`
  Expected: 4 arquivos de teste, 12 testes passando (2 + 1 + 4 + 5).
- [ ] **Step 9: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build`
  Expected: `lint` e `format:check` sem erro (o `format` normaliza o que você escreveu); `build` termina com `built in` e **não existe** `front-end\dist\mockServiceWorker.js` (o `postbuild` o apagou).
- [ ] **Step 10: Registrar e enviar.** Atualize o `PROGRESSO.md` (linha do passo, "Onde estou"). Run: `git status; git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): projeto do front-end com a pilha, MSW e tipos gerados do contrato"; git push -u origin feat/front-fatia-0`
  Expected: o `git status` não lista `node_modules`, `dist`, `.env*` nem `\gerado\`; o push envia o commit para a branch que a primeira ação já criou e enviou (o `-u` repetido é inofensivo). Se o push da primeira ação falhou por permissão e `pendente: push` está registrado, este também falha: **não** tente contornar; siga com o commit local (`01-regras.md`, C1).

---

### Task 2: Tipos, erro de API e cliente

**Files:**
- Create: `front-end\src\shared\api\config.ts`, `tipos.ts`, `erroDeApi.ts`, `clienteApi.ts`; `front-end\src\test\contrato.ts`.
- Test: `src\shared\api\clienteApi.test.ts`, `contrato.test.ts`, `hooks.test.tsx`, `upload.node.test.ts`; `src\test\contrato.test.ts`.

**Interfaces:**
- Consumes: `paths` e `components` de `gerado\schema.d.ts`; `servidor`, `exemplos` e `handlers` (Task 1).
- Produces:
  - `config.ts`: `export const URL_DA_API: string` = `import.meta.env.VITE_API_URL ?? 'https://api.exemplo.test'`.
  - `tipos.ts`: `type Esquema<K extends keyof components['schemas']> = components['schemas'][K]`; `type Perfil = Esquema<'Perfil'>`; `type Usuario = Esquema<'UsuarioResposta'>` (tem `cargo` e `setor`, `string | null`); `type Sessao = Esquema<'SessaoResposta'>`; `function ehSessao(x: unknown): x is Sessao` (confere `tokenDeAcesso`, `acessoExpiraEmUtc`, `refreshToken` como texto e `usuario` com `id` e `funcionarioId` numéricos, `nome` e `email` texto, `perfil` entre `Funcionario`, `Gestor`, `RH`, e `cargo` e `setor` texto ou `null`).
  - `erroDeApi.ts`: `class ErroDeApi extends Error` com `readonly status: number` (**0 = sem conexão**), `title: string`, `detail: string | null`, `codigo?: string`, `traceId?: string`, `errors: Record<string, string[]>` (`{}` se não vier), `retryAfterSegundos: number | null` (do cabeçalho `Retry-After`); `constructor(status: number, title: string, extras?: { detail?: string | null; codigo?: string; traceId?: string; errors?: Record<string, string[]>; retryAfterSegundos?: number | null })`; `static deResposta(resposta: Response): Promise<ErroDeApi>` (lê o problem+json; corpo vazio ou que não é JSON dá só o status e `title` = `Erro <status>`); `static semConexao(causa: unknown): ErroDeApi` (`status 0`, `title` `Sem conexão com a API.`). Os nomes de campo **são os do corpo `problem+json` do contrato** de propósito (`03-contrato-e-mocks.md`, seção 4). `erasableSyntaxOnly` proíbe propriedade no parâmetro do construtor: declare os campos e atribua.
  - `clienteApi.ts`: `type ClienteHttp = Client<paths>` (de `openapi-fetch`); `function criarClienteHttp(opcoes: { urlBase: string; obterToken?: () => string | null; fetchImpl?: typeof fetch }): ClienteHttp`; `type ClienteApi = { entrar(email: string, senha: string): Promise<Sessao>; renovar(refreshToken: string): Promise<Sessao>; sair(refreshToken: string): Promise<void>; eu(tokenDeAcesso: string): Promise<Usuario> }`; `function criarClienteApi(urlBase: string, fetchImpl?: typeof fetch): ClienteApi`.
  - `src\test\contrato.ts`: `type Enviado = { consulta?: URLSearchParams; corpo?: Record<string, unknown> }`; `function esperarRequisicaoConforme(operationId: string, enviado: Enviado): void`.

- [ ] **Step 1: Escrever os testes que falham.**

```ts
// src\shared\api\clienteApi.test.ts   (servidor.use(http.post('*/api/auth/entrar', ...)); URL_BASE = 'https://api.exemplo.test')
it('clienteApi.entrar envia POST /api/auth/entrar e devolve a sessao')
//   o corpo recebido é { email: 'ana.souza@empresa.com.br', senha: 'Senha#Forte123' }; a sessão devolvida tem usuario { nome: 'Ana Souza', perfil: 'RH', cargo: 'Gerente de RH' }
it.each([401, 403, 400, 429, 500])('clienteApi transforma resposta %i em ErroDeApi com o status')
//   resposta application/problem+json { title: 'Titulo do erro.', status }; o erro é instanceof ErroDeApi com { status, title: 'Titulo do erro.', message: 'Titulo do erro.' }
it('clienteApi guarda detail, codigo, errors e Retry-After do problem+json')
//   429 com { detail: 'Aguarde.', codigo: 'LIMITE', errors: { email: ['Obrigatório.'] } } e cabeçalho Retry-After: 60 -> { detail, codigo, errors, retryAfterSegundos: 60 }
it('clienteApi transforma falha de rede em ErroDeApi com status 0')    // HttpResponse.error()
it('clienteApi transforma 502 sem corpo JSON em ErroDeApi so com o status')   // new HttpResponse('Bad Gateway', { status: 502 }) -> { status: 502, detail: null }
it('clienteApi.entrar com 200 que nao e uma sessao (defeito do back-end) lanca ErroDeApi 500')  // Review Focus 2: corpo { token: 'x' } -> { status: 500, title: 'Resposta inesperada da API.' }
it('clienteApi.renovar e sair usam as rotas do contrato')  // POST /api/auth/renovar e /api/auth/sair recebem { refreshToken: 'r1' }
it('clienteApi.eu envia GET /api/auth/eu com Authorization Bearer')    // eu('tok-123') -> cabeçalho 'Bearer tok-123'; devolve o usuário do example (Ana Souza)
it('criarClienteHttp manda o token de obterToken em toda requisicao')  // obterToken: () => 'tok-9' -> GET /api/auth/eu com 'Bearer tok-9'

// src\shared\api\contrato.test.ts
it('ehSessao aceita o example do contrato e rejeita um objeto sem usuario')   // exemplos.entrar.corpo -> true; { ...exemplo, usuario: undefined } -> false; null -> false
it('ehSessao rejeita um perfil que o contrato nao conhece')    // Review Focus 5: usuario.perfil = 'Admin' -> false
it('ErroDeApi tem os campos do problem+json do contrato (os nomes sao os do contrato)')
//   const problema: Esquema<'ProblemaDeApi'> = new ErroDeApi(429, 'Muitas tentativas.', { detail: 'x' }) (o tsc confere os nomes: campo renomeado = não compila); toMatchObject { status: 429, title: 'Muitas tentativas.', detail: 'x' }
it('entrar, renovar e sair enviam exatamente as chaves do contrato')
//   captura o corpo de cada POST e chama esperarRequisicaoConforme('entrar' | 'renovarSessao' | 'sair', { corpo })

// src\shared\api\hooks.test.tsx   (padrão para a 1a: openapi-react-query sobre criarClienteHttp)
it('useQuery mostra o example do contrato')            // $api.useQuery('get', '/api/auth/eu', {}, { retry: false }) -> texto 'Ana Souza'
it('useQuery recebe ErroDeApi quando a API devolve problem+json')
//   401 { title: 'Não autenticado.', status: 401 }: q.error instanceof ErroDeApi, q.error.status === 401 e q.error.title === 'Não autenticado.'  (error.title funciona em tempo de execução: nomes iguais aos do contrato)

// src\shared\api\upload.node.test.ts   (primeira linha: // @vitest-environment node)
it('validarPlanilhaDeImportacao envia o arquivo como multipart/form-data e recebe a previa do example')
//   POST '/api/rh/importacoes' com body { arquivo: new File(['x'], 'planilha.xlsx') as unknown as string } e bodySerializer com FormData (03-contrato-e-mocks.md, seção 4);
//   o handler vê content-type /^multipart\/form-data; boundary=/ e o arquivo 'planilha.xlsx'; data.nomeArquivo === 'novos-colaboradores-outubro.xlsx' (status 201)

// src\test\contrato.test.ts
it('aceita o corpo de entrar com as chaves do contrato')            // esperarRequisicaoConforme('entrar', { corpo: { email, senha } }) não lança
it('recusa chave extra ao lado das certas no corpo')                // + senhaa: 'y' -> lança /senhaa/
it('recusa corpo sem chave obrigatoria')                            // só { email } -> lança /senha/
it('confere os parametros de consulta de uma lista paginada')
//   'listarFuncionarios' com pagina, tamanhoPagina, busca -> não lança; com { pagina, tamanhoPagin } -> lança /tamanhoPagin/ (a lacuna do tsc, 03 seção 6)
it('operacao que nao existe no contrato falha com mensagem clara')  // 'operacaoInventada' -> lança com o nome
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/api src/test`
  Expected: FAIL (os módulos não existem).
- [ ] **Step 3: Implementar** os quatro arquivos de `src\shared\api\` e `src\test\contrato.ts` com as assinaturas acima.
  - `criarClienteHttp`: `createClient<paths>({ baseUrl: urlBase, fetch: (req) => (fetchImpl ?? globalThis.fetch)(req) })` (o `fetch` **por função**: o MSW troca o `fetch` global ao ligar) e **um** `cliente.use({ onRequest, onResponse, onError })`: `onRequest` põe `Authorization: Bearer <token>` quando `obterToken?.()` devolve algo; `onResponse` **lança** `await ErroDeApi.deResposta(response)` quando `!response.ok`; `onError` devolve `erro` se já é `ErroDeApi`, senão `ErroDeApi.semConexao(erro)`. (Medido: a exceção lançada no `onResponse` chega ao chamador de `cliente.POST(...)` e ao `error` do hook.)
  - `criarClienteApi`: cada método chama a rota (`POST /api/auth/entrar` com `{ email, senha }`; `POST /api/auth/renovar` e `/api/auth/sair` com `{ refreshToken }`; `GET /api/auth/eu` com o cabeçalho `Authorization` passado) e valida o 200: se `!ehSessao(data)`, lança `new ErroDeApi(500, 'Resposta inesperada da API.')` (`eu` faz o mesmo quando falta `data`).
  - `esperarRequisicaoConforme` (código com `any` só aqui: `/* eslint-disable @typescript-eslint/no-explicit-any */` no topo): lê `../contrato/openapi.yaml` com `yaml`, acha a operação por `operationId`, resolve `$ref` locais (e `allOf` do corpo), e usa `expect` do Vitest para falhar com mensagem que cita o `operationId` quando a consulta tem parâmetro que o contrato não tem, falta parâmetro obrigatório, o corpo tem chave que o contrato não tem ou falta chave obrigatória do corpo.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/api src/test`
  Expected: PASS (6 arquivos de teste, 27 testes, contando os 2 da Task 1 em `src\test` e a expansão do `it.each`).
- [ ] **Step 5: Controle: prove que os testes pegam o defeito.** Troque temporariamente, em `erroDeApi.ts`, o campo `title` por `titulo` e rode o passo 4: deve FALHAR em `hooks.test.tsx` e em `clienteApi.test.ts`, e o `tsc` (`Push-Location front-end; npx tsc -b; Pop-Location`, ou o `build`) deve acusar `contrato.test.ts` (o teste do `ErroDeApi` atribuído ao tipo do contrato). Restaure o arquivo (`git checkout -- front-end\src\shared\api\erroDeApi.ts` só vale depois do commit; antes, desfaça à mão).
- [ ] **Step 6: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando (todos os testes das Tasks 1 e 2).
- [ ] **Step 7: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): tipos do contrato, ErroDeApi e cliente de API com openapi-fetch"; git push`

---

### Task 3: Sessão: armazenamento, `AuthProvider` e `useAuth`

**Files:**
- Create: `front-end\src\features\autenticacao\armazenamentoDaSessao.ts`, `contextoDeAutenticacao.ts`, `useAuth.ts`, `AuthProvider.tsx`.
- Test: `src\features\autenticacao\armazenamentoDaSessao.test.ts`, `AuthProvider.test.tsx`.

**Interfaces:**
- Consumes: `ClienteApi`, `ErroDeApi`, `Sessao`, `Usuario` (Task 2).
- Produces:
  - `armazenamentoDaSessao.ts`: `const CHAVE_DO_REFRESH = 'rh.refresh'`; `function lerRefresh(): string | null`; `function gravarRefresh(refreshToken: string): void`; `function limparRefresh(): void` (as três **engolem** o erro do `localStorage`); `const tokenDeAcesso: { obter(): string | null; definir(valor: string | null): void }` (variável de módulo, **só em memória**).
  - `contextoDeAutenticacao.ts`: `type ValorDoContexto = { usuario: Usuario | null; carregando: boolean; entrar(email: string, senha: string): Promise<void>; sair(): Promise<void> }` e `const ContextoDeAutenticacao = createContext<ValorDoContexto | null>(null)` (o lint `react-refresh/only-export-components` proíbe exportar componente e não-componente no mesmo arquivo).
  - `useAuth.ts`: `function useAuth(): ValorDoContexto` (lança `useAuth só funciona dentro de <AuthProvider>.` fora do provider).
  - `AuthProvider.tsx`: `function AuthProvider(props: { cliente: ClienteApi; children: ReactNode })`. Regras: (a) ao montar, **se há refresh guardado**, `carregando` começa `true` e o provider tenta renovar; sem refresh, `carregando` é `false` e **não chama a API**; (b) **uma única renovação em andamento por vez** na aba (as chamadas dividem a mesma promessa; `useRef`); (c) a renovação roda dentro de `navigator.locks.request('rh.refresh', ...)` quando `navigator.locks` existe, senão sem lock; **dentro do lock relê o `localStorage`** antes de usar o refresh; (d) renovação que recebe **401**: relê o `localStorage`; se outra aba gravou um refresh **diferente** do usado, tenta **uma vez** com o novo; se não mudou, **limpa a sessão** (`localStorage` e memória) e fica sem usuário; (e) falha que **não é 401** (rede, 5xx) **não apaga** o refresh (escolha de engenharia deste plano: o refresh ainda pode valer): fica sem usuário e o localStorage intacto; (f) sucesso: token em memória, refresh novo no `localStorage`, `usuario` (com `cargo` e `setor`) no estado; (g) `entrar` chama `cliente.entrar`; com erro **não guarda nada e propaga** o erro; (h) `sair` limpa memória, `localStorage` e estado **primeiro** e depois chama `cliente.sair(refresh)` ignorando falha; (i) o evento `storage` com `key === 'rh.refresh'` (ou `key === null`) e `newValue === null` **desloga a aba** sem chamar a API; troca de valor do refresh (renovação em outra aba) **não** desloga.

- [ ] **Step 1: Escrever os testes que falham.** Use `renderHook` do Testing Library (o lint `react-hooks/globals` proíbe reatribuir variável de fora dentro de um componente de teste), um `ClienteApi` falso com `vi.fn()` e, em `beforeEach`, `localStorage.clear()` e `tokenDeAcesso.definir(null)`. A sessão de base é `exemplos.entrar.corpo` (Ana Souza, RH) com o `refreshToken` trocado.

```ts
// src\features\autenticacao\armazenamentoDaSessao.test.ts
it('grava, le e limpa o refresh na chave rh.refresh')
it('nao lanca quando o localStorage lanca (navegacao privada, dados bloqueados)')
//   Review Focus 1: vi.spyOn(Storage.prototype, 'getItem' | 'setItem' | 'removeItem') lançando DOMException('bloqueado', 'SecurityError'):
//   lerRefresh() === null; gravarRefresh('r1') e limparRefresh() não lançam
it('o token de acesso fica so em memoria')   // definir('tok'); obter() === 'tok'; localStorage.length === 0

// src\features\autenticacao\AuthProvider.test.tsx
it('AuthProvider renova a sessao ao montar quando ha refresh no localStorage (inclusive depois de "fechar e reabrir")')
//   refresh 'r-guardado': carregando true -> usuario.nome 'Ana Souza'; renovar chamado com 'r-guardado'; localStorage 'r-novo';
//   unmount + tokenDeAcesso.definir(null) + montar de novo: logado outra vez, renovar chamado com 'r-novo'
it('AuthProvider sem refresh guardado nao chama a API e fica sem usuario')
it('AuthProvider em StrictMode (montagem dupla) chama renovar uma unica vez')           // wrapper com <StrictMode>; renovar.toHaveBeenCalledTimes(1)
it('AuthProvider com renovacao que falha ao montar (e o refresh nao mudou) limpa o localStorage e fica sem usuario')   // renovar rejeita ErroDeApi 401; chamado 1 vez
it('AuthProvider com 401 na renovacao, quando outra aba ja gravou um refresh novo, tenta uma vez com o novo e continua logado')
//   localStorage 'r-velho'; 1ª chamada: grava 'r-da-outra-aba' no localStorage e rejeita 401; 2ª: devolve a sessão com refresh 'r-final';
//   renovar chamado com 'r-velho' e depois 'r-da-outra-aba' (2 chamadas); localStorage termina 'r-final'; logado
it('AuthProvider com falha de rede na renovacao mantem o refresh guardado e fica sem usuario')   // ErroDeApi.semConexao('x')
it('AuthProvider pede o lock rh.refresh ao renovar quando o navegador tem navigator.locks')
//   Object.defineProperty(navigator, 'locks', { value: { request: vi.fn((nome, tarefa) => tarefa()) }, configurable: true }); request chamado 1 vez com 'rh.refresh'; limpar com Reflect.deleteProperty
it('AuthProvider.entrar guarda o refresh no localStorage e o token de acesso so em memoria')
//   Object.keys(localStorage) === ['rh.refresh'] com 'r-entrar'; tokenDeAcesso.obter() === token do contrato; JSON do localStorage não contém o token; usuario tem cargo 'Gerente de RH' e setor 'Recursos Humanos'
it('AuthProvider.entrar com erro nao guarda nada e propaga o erro')   // await expect(act(() => entrar(...))).rejects.toBe(falha); localStorage.length === 0
it('AuthProvider.sair limpa o localStorage e o usuario')              // cliente.sair chamado com o refresh em uso ('r-novo')
it('AuthProvider.sair limpa a sessao mesmo se a API falhar')
it('AuthProvider desloga quando outra aba remove rh.refresh (evento storage)')
//   dentro de act: localStorage.removeItem + window.dispatchEvent(new StorageEvent('storage', { key: 'rh.refresh', oldValue: 'r-novo', newValue: null })); usuario null; cliente.sair NÃO chamado
it('AuthProvider nao desloga quando outra aba so troca o refresh (renovacao)')   // StorageEvent com newValue 'b'
it('useAuth fora do AuthProvider lanca erro')                          // expect(() => renderHook(() => useAuth())).toThrow('AuthProvider') (silencie o console.error)
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features`
  Expected: FAIL (módulos inexistentes).
- [ ] **Step 3: Implementar** os quatro arquivos com as assinaturas e as regras acima. Se o `lint` acusar `react-hooks/set-state-in-effect` ou `exhaustive-deps`, resolva mudando o código (estado inicial de `carregando` calculado na criação; `setState` só dentro de callbacks assíncronos), **não** com `eslint-disable`.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/features`
  Expected: PASS (2 arquivos, 17 testes).
- [ ] **Step 5: Controle: prove que os testes dos pontos delicados pegam o defeito.** (a) Apague temporariamente a verificação "já há renovação em andamento": o teste do `StrictMode` deve FALHAR. (b) Faça o provider **não** tentar o refresh novo depois do 401: o teste "outra aba já gravou um refresh novo" deve FALHAR. Restaure o código e rode o passo 4 de novo.
- [ ] **Step 6: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 7: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): AuthProvider com refresh serializado entre abas e evento storage"; git push`

---

### Task 4: Tela de entrar, porta de acesso, shell e `main.tsx`

**Files:**
- Create: `front-end\src\features\autenticacao\mensagemDeErroDeEntrada.ts`, `PaginaEntrar.tsx`, `PortaDeAcesso.tsx`; `front-end\src\app\ShellDoPortal.tsx`, `rotulosDePerfil.ts`, `App.tsx`; `front-end\src\test\renderizarApp.tsx`.
- Modify: `front-end\src\main.tsx` (substitui o provisório).
- Test: `src\features\autenticacao\PaginaEntrar.test.tsx`, `PortaDeAcesso.test.tsx`; `src\app\ShellDoPortal.test.tsx`.

**Interfaces:**
- Consumes: `useAuth`, `AuthProvider` (Task 3); `ErroDeApi`, `criarClienteApi`, `URL_DA_API`, `Perfil` (Task 2).
- Produces:
  - `mensagemDeErroDeEntrada.ts`: `const MENSAGEM_CAMPOS_VAZIOS = 'Preencha o e-mail e a senha.'`; `function mensagemDeErroDeEntrada(erro: unknown): string`: `ErroDeApi` 401 → `E-mail ou senha inválidos.`; 403 → `erro.detail` **se vier**, senão `Seu acesso está desativado. Fale com o RH.` (o contrato tem dois 403: acesso desativado e perfil não configurado; o `detail` diz qual); 400 → `MENSAGEM_CAMPOS_VAZIOS` (o `detail` do exemplo do 400 de `entrar`, `Erro400Login`; **não** o do `Erro400Validacao`, que diz `Confira os campos indicados.`); 429 → `Muitas tentativas. Aguarde um minuto e tente de novo.`; status `>= 500` → `Erro no servidor. Tente novamente em instantes.`; **qualquer outra coisa** (status 0, erro desconhecido) → `Não foi possível conectar. Tente novamente.`
  - `PaginaEntrar.tsx`: `function PaginaEntrar()`: título `RH-System`; campos `E-mail` (`type="email"`) e `Senha` (`type="password"`) com rótulos ligados; botão `Entrar` (`disabled` enquanto envia); mensagem em `role="alert"`. Formulário com `react-hook-form` e `zodResolver`: `email` com `trim().min(1)` e `senha` **sem `trim`** (`min(1)`), ambos com a mensagem `MENSAGEM_CAMPOS_VAZIOS`; campos vazios **não chamam a API**. Layout e cores: nota da tela de entrar em `telas\fatia-0.md` (a tela não tem print) e `04-visual.md`; **os textos desta tarefa valem**. Estilo mínimo com classes do Tailwind; o acabamento visual é da fatia 1a.
  - `PortaDeAcesso.tsx`: `function PortaDeAcesso()`: `carregando` → `<p role="status">Carregando…</p>`; sem usuário → `PaginaEntrar`; com usuário → `ShellDoPortal`.
  - `rotulosDePerfil.ts`: `const rotulosDePerfil: Record<Perfil, string>` = `Funcionario` → `Funcionário`, `Gestor` → `Gestor`, `RH` → `RH`.
  - `ShellDoPortal.tsx`: `function ShellDoPortal()`: `Portal de RH`, o nome do usuário, o rótulo do perfil e o botão `Sair` (chama `useAuth().sair`). Não mostra `cargo` e `setor` (o menu da 1a os usa).
  - `App.tsx`: `function App()` devolve `<PortaDeAcesso />`.
  - `test\renderizarApp.tsx`: `function renderizarApp(): RenderResult`: `AuthProvider` com `criarClienteApi('https://api.exemplo.test')` e `<App />`.
  - `main.tsx` (ordem importa): `import './shared/zod'` **primeiro entre os imports do projeto**, depois `./index.css`; função `iniciar()`: se `import.meta.env.DEV && import.meta.env.VITE_MOCKS !== 'nao'`, `await import('./shared/mocks/navegador')` e `worker.start({ onUnhandledFrame: 'bypass' })`; depois `createRoot(...).render(<StrictMode><CSPProvider disableStyleElements><AuthProvider cliente={criarClienteApi(URL_DA_API)}><App /></AuthProvider></CSPProvider></StrictMode>)`, com `CSPProvider` de `@base-ui/react/csp-provider`.

- [ ] **Step 1: Escrever os testes que falham** (usam `renderizarApp()`, o MSW do contrato e `userEvent`; em `beforeEach`, `localStorage.clear()` e `tokenDeAcesso.definir(null)`; o auxiliar `problema(status, title, detail?)` devolve `HttpResponse.json(..., { status, headers: { 'content-type': 'application/problem+json' } })`).

```tsx
// PaginaEntrar.test.tsx   (preencher: 'E-mail' = ana.souza@empresa.com.br, 'Senha' = Senha#Forte123, clicar em 'Entrar')
it.each([[401, 'E-mail ou senha inválidos.'], [403, 'Seu acesso está desativado. Fale com o RH.'], [400, 'Preencha o e-mail e a senha.'],
         [429, 'Muitas tentativas. Aguarde um minuto e tente de novo.'], [500, 'Erro no servidor. Tente novamente em instantes.']])
  ('PaginaEntrar mostra a mensagem certa para %i')   // POST entrar -> problema(status, 'Titulo do servidor.'); o role="alert" contém a mensagem
it('PaginaEntrar mostra a mensagem certa para falha de rede')   // HttpResponse.error() -> 'Não foi possível conectar. Tente novamente.'
it('PaginaEntrar no 403 de perfil nao configurado mostra o detail do contrato')   // detail 'Seu perfil de acesso não está configurado. Fale com o RH.'
it('PaginaEntrar com campos vazios mostra a mensagem e nao chama a API')   // clicar sem preencher -> alert 'Preencha o e-mail e a senha.'; contador do handler === 0
it('PaginaEntrar desabilita o botao enquanto envia')   // handler espera uma promessa; botão 'Entrar' disabled; ao liberar, aparece o alert e o botão volta a habilitar
it('PaginaEntrar envia a senha exatamente como digitada (espacos nas pontas contam)')   // Review Focus 3: digita ' Senha#Forte123 ' -> o corpo recebido tem senha === ' Senha#Forte123 '
it('PaginaEntrar com clique duplo em Entrar envia uma unica requisicao')   // Review Focus 4: userEvent.dblClick; contador === 1
it('PaginaEntrar com sucesso troca o login pelo shell')   // aparece 'Portal de RH'; o campo 'Senha' some

// PortaDeAcesso.test.tsx
it('PortaDeAcesso mostra o login sem usuario e o shell com usuario')   // sem refresh: botão 'Entrar'; com 'rh.refresh' no localStorage: 'Portal de RH'
it('PortaDeAcesso mostra um indicador enquanto renova a sessao')       // renovar pendente: role="status" com 'Carregando' e sem botão 'Entrar'; ao liberar, 'Portal de RH'

// ShellDoPortal.test.tsx   (entra por refresh no localStorage + POST renovar com o perfil e o nome desejados)
it('Shell mostra nome do usuario, "Funcionário" para o perfil Funcionario e o botao Sair')   // 'Bruno Lima' + 'Funcionário' + botão 'Sair'
it.each([['Gestor', 'Gestor'], ['RH', 'RH']])('Shell mostra o rotulo do perfil %s')
it('Sair volta para o login e limpa a sessao')   // clicar 'Sair': botão 'Entrar' aparece; localStorage sem 'rh.refresh'; tokenDeAcesso.obter() === null
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features src/app`
  Expected: FAIL (componentes inexistentes).
- [ ] **Step 3: Implementar** os arquivos com as assinaturas acima e substituir o `main.tsx` provisório.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/features src/app`
  Expected: PASS (5 arquivos, 35 testes nesta seleção; `npm --prefix front-end test` inteiro: 14 arquivos, 72 testes).
- [ ] **Step 5: Controle: prove que os testes dos riscos pegam o defeito.** (a) Tire o `disabled={isSubmitting}` do botão: os testes "desabilita o botao" e "clique duplo" devem FALHAR. (b) Ponha `trim()` na senha: o teste "envia a senha exatamente como digitada" deve FALHAR. Restaure e rode o passo 4 de novo.
- [ ] **Step 6: Conferência da pessoa no navegador de desenvolvimento (não bloqueia; vai para o relatório final).** **Você não olha o navegador** (`01-regras.md`, A11). A prova automatizada do mesmo comportamento já existe: os testes do passo 4 (login, shell e "Sair" com o MSW no Node) e o passo "entra (API simulada) e vê o shell" do `csp` (Task 5, no build). O que só o navegador de desenvolvimento mostra é o **worker do MSW ligado**: anote na tabela "Conferências da pessoa" do `PROGRESSO.md` o roteiro para a pessoa: `npm --prefix front-end run dev`, abrir o endereço que o Vite mostrar (`http://localhost:5173`); o console mostra `[MSW] Mocking enabled`; entrar com qualquer e-mail e senha abre o shell com "Ana Souza" e "RH"; "Sair" volta ao login (na execução que escreveu este plano a requisição foi a `POST https://api.exemplo.test/api/auth/entrar`, respondida pelo MSW). Siga para o passo 7.
- [ ] **Step 7: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando; o `build` sem `dist\mockServiceWorker.js`.
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md` (linhas da tela de entrar e do shell, "Telas": `sem print`, `feita com <versão do contrato>`); Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): tela de entrar, porta de acesso, shell e main com MSW em desenvolvimento"; git push`

---

### Task 5: Política de segurança: configuração, servidor de teste e `csp`

**Files:**
- Create: `front-end\public\staticwebapp.config.json`; `front-end\scripts\servidor-de-teste.mjs` e `violacoes-de-politica.mjs` (cópias de `docs\plano-front-end\ferramentas\`).
- Test: `src\app\staticwebapp.config.test.ts`.

**Interfaces:**
- Consumes: o `dist` do `build` e a tela de entrar com o shell (Task 4); o exemplo de `entrar` do contrato (`contrato\openapi.yaml`), que o script lê para simular a API.
- Produces: `public\staticwebapp.config.json` (copiado para `dist` no build); script `csp` (`node scripts/violacoes-de-politica.mjs`), que sai com 0 se não houver violação, os 4 controles violarem e os 3 passos da **sonda de download** baixarem o arquivo; com 1 no contrário; e com **2** se não houver Edge nem Chrome instalados (escreve `pendente: csp`: `01-regras.md`, A11 e G2); `scripts\servidor-de-teste.mjs` exporta `iniciarServidor({ dist, config, porta? })`, `lerConfiguracao(caminho)` e `ROTA_DA_SONDA` (a rota de teste `/__sonda/arquivo.pdf`).

`front-end\public\staticwebapp.config.json` (conteúdo exato; **a URL `https://api.exemplo.test` é um marcador que o dono troca na Task 14 do plano do back-end: não a troque**):

```json
{
  "navigationFallback": {
    "rewrite": "/index.html"
  },
  "globalHeaders": {
    "Content-Security-Policy": "default-src 'self'; connect-src 'self' https://api.exemplo.test",
    "X-Content-Type-Options": "nosniff",
    "X-Frame-Options": "DENY",
    "Referrer-Policy": "no-referrer",
    "Permissions-Policy": "geolocation=(), microphone=(), camera=()"
  }
}
```

- [ ] **Step 1: Escrever o teste que falha** (`src\app\staticwebapp.config.test.ts`; primeira linha `// @vitest-environment node`; lê `public/staticwebapp.config.json` com `readFileSync` e `JSON.parse`).

```ts
it('staticwebapp.config.json e JSON valido, tem navigationFallback e os cabecalhos de seguranca')
//   navigationFallback.rewrite === '/index.html'; globalHeaders contém X-Content-Type-Options 'nosniff', X-Frame-Options 'DENY',
//   Referrer-Policy 'no-referrer', Permissions-Policy 'geolocation=(), microphone=(), camera=()'
it('a politica fecha tudo em self e libera so a API no connect-src, sem unsafe-inline nem unsafe-eval')
//   a política tem exatamente as diretivas default-src e connect-src; default-src é ["'self'"]; connect-src é ["'self'", <uma origem /^https:\/\/\S+$/>];
//   o texto não casa /unsafe-inline|unsafe-eval|data:|blob:|\*/
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/app/staticwebapp`
  Expected: FAIL (o arquivo não existe).
- [ ] **Step 3: Criar o arquivo de configuração** acima e **copiar as duas ferramentas**. Run: `Copy-Item docs\plano-front-end\ferramentas\servidor-de-teste.mjs front-end\scripts\; Copy-Item docs\plano-front-end\ferramentas\violacoes-de-politica.mjs front-end\scripts\`
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/app/staticwebapp`
  Expected: PASS (2 testes).
- [ ] **Step 5: Build e prova da política.** Garanta que nenhuma variável sobrescreve a URL da API e, se existir, renomeie `front-end\.env.local`. Run (a limpeza da variável vai **na mesma linha** do build, porque ela só vale ali: `01-regras.md`, A14): `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run build; npm --prefix front-end run csp`
  Expected (últimas linhas): **nenhuma linha `[FALHOU]`**, `[ok ]` nos 9 passos (os 6 de sempre: abre a tela de entrar; envia o formulário vazio; entra e vê o shell; recarrega e continua logado; sai; link direto; e os 3 da **sonda de download**: CSV por `Blob`, link temporário de outra origem e link para a própria origem: `03-contrato-e-mocks.md`, seção 8), `[controle ok ]` nos 4 controles, `violações reais: 0 · controles mudos: 0 · exceções: 0 · requisições que falharam: 0` e `PASSOU: ...`. O script também avisa que o `connect-src` ainda tem a URL-marcador (esperado). **Se falhar, leia a lista `FALHOU:`** (cada item diz a diretiva, o que foi bloqueado e o arquivo): corrija a causa no código, nunca a política. **Se o script sair com código 2** (`pendente: csp`: nenhum Edge nem Chrome instalado), **não** instale navegador: registre `pendente: csp` no `PROGRESSO.md` ("Último `csp`") e siga; os passos 6 e 7 que dependem do `csp` ficam pendentes, e a pessoa roda `npm --prefix front-end run build; npm --prefix front-end run csp` onde houver um navegador.
- [ ] **Step 6: Controle: prove que o `csp` enxerga o defeito.** Troque temporariamente `src\shared\zod.ts` para **não** chamar `z.config({ jitless: true })`, rode `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run build; npm --prefix front-end run csp`: deve FALHAR com `script-src bloqueou eval` (medido na execução que escreveu este plano). Restaure o arquivo (`git checkout -- front-end\src\shared\zod.ts`), refaça o build e rode o `csp` de novo (PASSOU).
- [ ] **Step 7: Provas da tarefa.** Run: `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end test; npm --prefix front-end run build; npm --prefix front-end run csp`
  Expected: tudo passando e **nenhuma linha `[FALHOU]`** no `csp`.
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): politica de seguranca do site, servidor de teste e script csp"; git push`

---

### Task 6: Fechamento da fatia 0: provas da CI, README, integração e PR 1

**Files:**
- Modify: `README.md` da raiz (**só** a seção `## Front-end`, **só se ela existir**); `docs\plano-front-end\PROGRESSO.md`.

**Interfaces:**
- Consumes: tudo das Tasks 1 a 5; `contrato\STATUS.md`.
- Produces: a fatia 0 pronta e enviada (PR 1 aberto); a lista de provas que o job `front-end` da CI do dono deve rodar (abaixo); o roteiro de integração registrado.

- [ ] **Step 1: Prova completa a partir de uma instalação limpa** (é o que a CI fará). Run: `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; Remove-Item -Recurse -Force front-end\node_modules; npm --prefix front-end ci; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end test; npm --prefix front-end run build; npm --prefix front-end run csp`
  Expected: `npm ci` termina sem erro (cerca de 1 a 3 minutos, 452 pacotes); `lint` e `format:check` limpos; **todos os testes passam** (nesta fatia: 15 arquivos de teste, 74 testes); `build` com `built in`; `csp` com `PASSOU` e **nenhuma linha `[FALHOU]`** (ou `pendente: csp`, código 2, sem navegador). Guarde os números reais para o `PROGRESSO.md`.
- [ ] **Step 2: Seção `## Front-end` do `README.md` da raiz (só se existir).** Se o arquivo existe **e** tem essa seção, escreva nela, em poucas linhas: pré-requisito (Node `^22.22.2 || ^24.15.0`), `npm --prefix front-end ci`, `npm --prefix front-end run dev` (dados simulados), `npm --prefix front-end test`, `lint`, `format:check`, `build`, `csp`, e um link para `docs\plano-front-end\LEIA-PRIMEIRO.md`. **Não crie o arquivo e não mexa em mais nada dele.** Se o arquivo ou a seção não existirem, **não os crie**: registre uma dúvida que **não bloqueia** (dono: criar a seção ou dizer onde os comandos ficam) e siga; o `git add` do passo 6 **não** inclui o `README.md` nesse caso.
- [ ] **Step 3: Integração ponta a ponta (a antiga verificação manual da Task 11): roteiro para a pessoa, não bloqueia.** **A sessão nunca roda contra a API real** (`01-regras.md`, G1; design aprovado, seção 5). Leia `contrato\STATUS.md` (`Select-String -Path contrato\STATUS.md -Pattern '^\| 0 '`) só para escrever o motivo: **registre `pendente de integração` no `PROGRESSO.md` com o roteiro abaixo e siga**, esteja a fatia 0 `planejada`, `em andamento` ou `pronta` (se `pronta`, o motivo é "a rota já existe, mas a sessão não roda contra a API real" e o relatório diz que a pessoa já pode rodar o roteiro; não pergunte nada sobre a API). **Você não olha o navegador** (`01-regras.md`, A11): o roteiro é **da pessoa** (ou do dono); ela traz o resultado de cada item e você o registra na tabela "Conferências da pessoa" do `PROGRESSO.md`. **Roteiro** (para quando a API estiver no ar; a pessoa passa o e-mail e a senha do administrador local, que **nunca** vão para um arquivo do repositório): crie `front-end\.env.local` com `VITE_API_URL=http://localhost:5000` e `VITE_MOCKS=nao`; `npm --prefix front-end run dev`; (1) entrar com o administrador mostra o shell com o nome e o perfil dele; (2) senha errada mostra `E-mail ou senha inválidos.`; (3) **F5 não desloga** e **fechar e reabrir o navegador não desloga**; (4) **sair numa aba desloga a outra**; (5) se o navegador reclamar de CORS, registre uma dúvida para o dono (a origem `http://localhost:5173` precisa estar permitida na API).
- [ ] **Step 4: Lista de provas para o job `front-end` da CI** (o dono a usa na Task 12 do plano do back-end; escreva-a no corpo do PR e no `PROGRESSO.md`): `npm --prefix front-end ci`, `run lint`, `run format:check`, `test`, `run build`, `run csp`. Observações para o dono: (a) o `build` deve rodar com `VITE_API_URL` **igual à origem do `connect-src`** de `public\staticwebapp.config.json` (senão o `csp` acusa `connect-src`); (b) o `csp` precisa do Edge ou do Chrome; o runner `ubuntu-latest` do GitHub traz o Chrome (**não medido por mim**); se ele preferir, deixe o `csp` fora da CI e rode-o no deploy; (c) a Task 14 do back-end troca **só** a URL do `connect-src` e confere o ambiente.
- [ ] **Step 5: Fechar o `PROGRESSO.md`** da fatia 0 e escrever na conversa a **lista de telas a conferir ao lado do PNG** (`01-regras.md`, A11): a **tela de entrar** (sem print: conferir a descrição em `telas\fatia-0.md`) e o **shell** (provisório; o menu real é da 1a). A fonte é a **Inter, decidida pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"): a pessoa só confere a letra ao lado do PNG.
- [ ] **Step 6: Enviar e abrir o PR 1.** Run: `git add front-end docs\plano-front-end\PROGRESSO.md docs\plano-front-end\DUVIDAS.md; git status; git commit -m "docs(front): fecha a fatia 0 (provas da ci e progresso)"; git push` (acrescente `README.md` ao `git add` **só se o Step 2 o alterou**; antes do push, a conferência de commits do C6). Depois, no PowerShell, monte o título e o corpo em variáveis e **confira o C6 antes de abrir o PR** (`01-regras.md`, C3, C6 e C8):

```powershell
$titulo = 'feat(front): fatia 0 do front-end (projeto, login, shell e politica de seguranca)'
$corpo = @'
<o texto>
'@
($titulo + "`n" + $corpo) | Select-String -Pattern 'Co-Authored-By|Claude|Generated with'
gh pr create --base main --head feat/front-fatia-0 --title $titulo --body $corpo
```

(O `'@` que fecha o texto fica **sozinho numa linha, na coluna 0**, regra do PowerShell 5.1: copie o bloco sem recuo.) A busca **não pode escrever nada**; se escrever, corrija o texto antes do `gh`. O texto: o que foi feito; os comandos de prova e os resultados (Step 1); a lista de provas para a CI (Step 4); pendências de integração (Step 3); **as dúvidas abertas só pelo título** (o **código fixo** e uma linha, por exemplo `D-benef: benefícios já marcados no cadastro`; a dúvida que a sessão achou sozinha leva `D-NNN`, por exemplo `D-007: botão sem rota`; **nunca** o texto pronto); a versão do contrato usada (`info.version`); e o pedido "**mesclar com merge commit, não squash**, porque a branch das fatias seguintes parte desta". **Sem** `Co-Authored-By` nem menção a Claude. Sem o `gh`, entregue o título e o texto à pessoa.
- [ ] **Step 7: Criar a branch das fatias seguintes** a partir da ponta desta: `git switch -c feat/front-fatias-1-a-8`. Registre no `PROGRESSO.md` ("Onde estou": próximo passo = primeira tarefa de `fatia-1a-fundacao.md`) e siga para a fatia 1a **sem esperar o merge do PR 1**.

---

## Cobertura e autorrevisão (do autor deste plano)

- **Antigas Tasks 10 e 11:** todos os testes e regras estão aqui (cliente com 401/403/400/429/500 e sessão; `AuthProvider` com refresh em `localStorage`, renovação única, `StrictMode`, `navigator.locks`, 401 com refresh trocado por outra aba, `storage`; textos e mensagens da tela de entrar; `PortaDeAcesso`; shell sem biblioteca de rotas; `staticwebapp.config.json`). O que mudou: o cliente é `openapi-fetch` com tipos do contrato (não um cliente escrito à mão); `ehSessao` lê o `example` do contrato (não existe mais `contrato\sessao.exemplo.json`); `Usuario` tem `cargo` e `setor`; o teste da política passou de "JSON válido" para **servidor de teste mais navegador** (Task 5); a verificação ponta a ponta (Step 5 da antiga Task 11) virou o Step 3 da Task 6, **pendente de integração**; o README do front não é criado pela fatia, só preenche a seção existente.
- **Escolhas de engenharia deste plano** (o dono pode vetar): falha de rede ou 5xx ao renovar **não** apaga o refresh; validação de campos vazios na tela, com a mesma mensagem do 400; 403 mostra o `detail` do contrato quando existe; `ErroDeApi.status === 0` para "sem conexão"; PR 1 ao fim da fatia 0.
- **Ficou para a fatia 1a** (de propósito): renovar o token de acesso (15 minutos) **quando uma chamada de negócio receber 401**, ou perto do vencimento; esta fatia só renova ao montar. Também: rotas, menu por perfil, `QueryClientProvider`, `$api`, formatação de datas, CPF e telefone.
