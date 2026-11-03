# 03 · Contrato, tipos, cliente e dados simulados

O **contrato é lei**: tudo o que as telas e a API precisam está em `contrato\`. **Só o dono o muda.** Você nunca edita nada ali; se faltar algo, é dúvida (`01-regras.md`, seção D). Este arquivo diz como usar o contrato, como gerar tipos e dados simulados dele, como testar contra ele e o que fazer quando ele muda.

## 1. Os arquivos do contrato (e para que cada um serve)

| Arquivo | Para que serve | Como usar |
|---|---|---|
| `contrato\openapi.yaml` | O contrato (OpenAPI 3.1.0, 224 operações, 1,3 MB). **Gerado** das `contrato\fontes\*.yaml`; é daqui que saem tipos e dados simulados. `info.version` é a versão do contrato: **leia-a do arquivo** (E4 de `01-regras.md`); quando este plano foi escrito era `0.2.0`, rascunho, e a `1.0.0` é a da publicação na `main`. | **Nunca leia inteiro.** Procure uma operação com a ferramenta de busca: `operationId: listarFuncionarios` (traz parâmetros, corpo, respostas e `example`). |
| `contrato\ROTAS.md` | Todas as rotas por fatia, uma linha cada, em português simples, com o `operationId` e quem usa. | **É o seu índice**: ache aqui a rota da tela, depois busque o `operationId` no `openapi.yaml`. `*` marca rota que depende de decisão aberta. |
| `contrato\CONVENCOES.md` | Regras que valem para todas as rotas (seção 2). | Leia **uma vez, inteiro**, antes da fatia 1. |
| `contrato\PROVISORIOS.md` | Itens que dependem de decisão do dono ainda aberta. | Construa normalmente em cima do que está escrito; anote no `PROGRESSO.md` que a tela usa o provisório (`usa provisório: <rota>`), para o dono saber o que pode mudar. |
| `contrato\STATUS.md` | Quais rotas **já existem de verdade** na API (`planejada`, `em andamento`, `pronta`), por fatia. Mantido pelo dono. | Diz se a rota já existe de verdade. **Isso não libera a sessão a rodar contra a API real** (ela nunca roda: seção 7); serve para escrever o motivo do "pendente de integração" e para a pessoa saber o que já pode testar. |
| `contrato\ELENCO.md` | O cenário de demonstração: as mesmas pessoas, ids e data (quarta-feira 2026-10-07, 09:42) em **todos** os exemplos. | Dados simulados e textos de teste usam estes nomes e ids; **não invente pessoas**. |
| `contrato\CHANGELOG.md` | O que mudou em cada versão. | Leia a entrada nova quando a versão mudar (seção 9). |
| `contrato\fontes\*.yaml` | Os arquivos de origem do contrato. | Não precisa ler; o `openapi.yaml` é o que vale. |

## 2. O que `CONVENCOES.md` diz que o front precisa saber (resumo; o arquivo vale)

- **Rotas** (`CONVENCOES.md` seção 2): prefixo `/api`, português sem acento, `kebab-case`. Famílias: `/api/auth` (público), `/api/rh/...` (só RH), `/api/gestor/...` (só Gestor, sobre a equipe dele), `/api/eu/...` (a própria pessoa, qualquer perfil), `/api/publico/...` (sem login). Perfil errado: **403**. Objeto fora do escopo: **404**.
- **JSON** (seção 3): `camelCase`; ids inteiros; enums em `MAIÚSCULAS_COM_SUBLINHADO` (exceção: `perfil` é `Funcionario`, `Gestor` ou `RH`); **instante** é UTC com `Z`; **data de calendário** é `2026-10-30`; **horário do dia** é `HH:mm`; duração em minutos (`...Minutos`); dinheiro em centavos (`...EmCentavos`); campo vazio é `null`, lista vazia é `[]`. O RH recebe CPF e telefone **completos**; o gestor não recebe CPF e recebe `telefoneMascarado`.
- **Listas** (seção 4): `pagina` (de 1), `tamanhoPagina` (padrão 20, máximo 100), `busca`, `ordenarPor`, `direcao` (`ASC` ou `DESC`) e um parâmetro por filtro. Resposta: `itens`, `pagina`, `tamanhoPagina`, `total`, `totalPaginas`.
- **Erros** (seção 5): sempre `application/problem+json` em português com acento: `title`, `status`, `detail`, `traceId` e, às vezes, `codigo` (texto estável: decida pela regra por ele, nunca pelo `title`) e `errors` (no 400, por campo). Respostas: 400, 401, 403, 404, 409, 422 (regra de negócio; traz `regraDoBanco`), 429 (com `Retry-After`), 500.
- **Arquivos** (seções 4, 6 e 11): envio em `multipart/form-data`. **Exportação (CSV, xlsx) devolve os bytes direto**, com `Content-Disposition: attachment` (seção 4). **Só arquivo guardado** (PDF, documento, holerite, anexo) vem por **link temporário**: `GET .../{id}/download` devolve `LinkTemporario` (`url` e `expiraEmUtc`), nunca os bytes (seção 6).
- **Aprovação em duas etapas** (seção 9), **situação de hoje** (seção 10) e **códigos de erro que se repetem** (seção 11): leia quando chegar ao ponto, férias e painéis.
- **E-mails** (seção 12) e **treinamentos sem prazo** (seção 13): decisões do dono que cortam botões e campos; as notas de tela as repetem.

## 3. Tipos: `openapi-typescript`

`npm --prefix front-end run contrato:tipos` escreve `front-end\src\shared\api\gerado\schema.d.ts` com `paths` (rotas), `components` (schemas) e `operations` (por `operationId`). **Não edite e não commite** (o `.gitignore` o exclui; `pretest`, `predev` e `prebuild` o regeneram).

`src\shared\api\tipos.ts` expõe `Esquema<'Nome'>` (um schema do contrato pelo nome), `Usuario`, `Sessao` e `Perfil`. **Toda fixture escrita à mão nos testes e todo tipo de tela vêm daí**, nunca de um tipo copiado à mão: quando o contrato muda, o `tsc` acusa o arquivo exato (seção 9). Exemplo: `const lista: Esquema<'FuncionarioListaRespostaRh'> = {...}` (o nome do schema se acha no `openapi.yaml`).

## 4. Cliente: `openapi-fetch` e `openapi-react-query`

- `src\shared\api\clienteApi.ts` (Task 2 da `fatia-0.md`) cria o cliente tipado: `criarClienteHttp({ urlBase, obterToken?, fetchImpl? })`. Ele manda `Authorization: Bearer <token>` quando `obterToken()` devolve algo, e **toda resposta fora de 2xx vira `ErroDeApi`** (lançado); falha de rede vira `ErroDeApi` com `status: 0`.
- **`ErroDeApi` usa os mesmos nomes de campo do corpo `problem+json`** (`title`, `status`, `detail`, `codigo`, `traceId`, `errors`) mais `retryAfterSegundos`. Isso importa nos hooks: o `openapi-react-query` **descreve** `error` com o tipo do corpo do contrato, mas **em tempo de execução ele é um `ErroDeApi`** (medido agora; sem os nomes iguais, `error.title` compilava e dava `undefined`). Para decidir pela regra: `error.codigo`.
- Hooks (a fundação 1a cria `$api` uma vez, em `src\shared\api\api.ts`): `export const $api = createClient(clienteHttp)`, com `createClient` do `openapi-react-query` e `clienteHttp = criarClienteHttp({ urlBase: URL_DA_API, obterToken: tokenDeAcesso.obter, aoExpirarAcesso: renovadorDeAcesso.renovar })` (1a, Task 9; **não** existe `createQueryClient` em `openapi-react-query`); uso: `$api.useQuery('get', '/api/rh/funcionarios', { params: { query: { pagina, tamanhoPagina: 20 } } })` e `$api.useMutation('post', '/api/...')`. Nos testes, `new QueryClient({ defaultOptions: { queries: { retry: false } } })` (senão uma consulta com erro tenta 3 vezes antes de mostrar o erro).
- **Upload** (`multipart/form-data`): o tipo gerado para `format: binary` é `string`; use o cast e um `bodySerializer` (padrão medido agora, teste `upload.node.test.ts`):

```ts
await cliente.POST('/api/rh/importacoes', {
  body: { arquivo: arquivo as unknown as string },
  bodySerializer: (b) => {
    const formulario = new FormData()
    formulario.append('arquivo', b.arquivo as unknown as Blob)
    return formulario
  },
})
```

  O teste que envia arquivo começa com `// @vitest-environment node`.
- **Download.** Dois casos, e a rota diz qual: (1) **arquivo guardado** (documento, holerite, anexo): a rota devolve `LinkTemporario`; abra `url` com `abrirLinkTemporario` (1a, Task 10: um `<a href target="_blank" rel="noopener noreferrer">`); nunca monte o caminho do arquivo. (2) **Exportação** (CSV, xlsx): a rota devolve os **bytes** com `Content-Disposition`; chame com `parseAs: 'blob'` e baixe com `baixarExportacao` (1a, Task 10: `Blob`, `URL.createObjectURL` e `<a download>`). Os dois caminhos foram **medidos sob a política de segurança** pela sonda de download do `csp` (seção 8): baixam, 0 violações.
- O `fetch` é passado ao `openapi-fetch` **por função** (`(req) => globalThis.fetch(req)`): o MSW troca o `fetch` global ao ligar, e um `fetch` capturado antes escaparia dele.

## 5. Dados simulados (MSW) gerados do contrato

`front-end\scripts\gerar-mocks.mjs` (cópia de `docs\plano-front-end\ferramentas\gerar-mocks.mjs`, Task 1) lê o `openapi.yaml` e escreve **dois** arquivos em `front-end\src\shared\mocks\gerado\`:

- `handlers.ts`: **um handler por operação** (224 hoje, em cerca de 1 s), cada um devolvendo o `example` da primeira resposta 2xx. Exporta `versaoDoContrato`, `exemplos` (mapa `operationId` → `{ metodo, caminho, status, contentType, corpo }`) e `handlers` (rotas literais antes das com `{parâmetro}`, porque o MSW usa o primeiro que casa).
- `exemplos.ts`: **todos** os exemplos de **cada resposta de cada operação**, erros incluídos. Exporta `exemplosPorOperacao[operationId][status][nome]`: o status é o texto do código (`'200'`, `'409'`); o `example` único tem o nome `padrao` e os `examples` nomeados do contrato mantêm o nome (por exemplo `exemplosPorOperacao.criarFuncionario['409'].cpfInativo`). Hoje: 1236 respostas com exemplo, 1394 exemplos, 104 nomes além de `padrao` (o resumo do gerador mostra `respostasComExemplo` e `totalDeExemplos`). Ninguém o lê direto: o teste usa o ajudante abaixo. (Os nomes se parecem: `exemplos` é a exportação de `handlers.ts`, o primeiro 2xx; `exemplosPorOperacao` é a de `exemplos.ts`, todos. Em teste, importe `exemploDoContrato`, não nenhum dos dois.)

**Ajudante de teste `exemploDoContrato(operationId, status?, nome?)`** (`src\shared\teste\exemploDoContrato.ts`, Task 1 da fatia 0): devolve **uma cópia** do exemplo do contrato. Sem `status`, o primeiro 2xx; sem `nome`, o primeiro exemplo da resposta. Operação, status ou nome que não existem lançam um erro que diz **o que existe**. **Todo teste que "carrega o exemplo X do contrato"** com um **nome** ou de um status que não é o 2xx padrão (um erro nomeado como o 409 `cpfInativo`, um `example` de um erro 422) usa este ajudante, **nunca um texto copiado do contrato à mão**. O primeiro exemplo 2xx continua à mão em `exemplos.<operationId>.corpo` (é o que o handler gerado devolve; `exemplos.entrar.corpo` é a sessão de base dos testes). O tipo de retorno é genérico (`exemploDoContrato<Esquema<'ColaboradorDuplicadoProblema'>>('criarFuncionario', 409, 'cpfInativo')`).

- **No desenvolvimento** (`npm run dev`): `main.tsx` liga o worker (`src\shared\mocks\navegador.ts`) quando `import.meta.env.DEV` e `VITE_MOCKS` não é `nao`. O `public\mockServiceWorker.js` vem de `npx msw init public --save` (Task 1) e **não vai para o ar** (`postbuild`). O login simulado aceita qualquer e-mail e senha e devolve a sessão da Ana Souza (RH).
- **Nos testes**: `src\test\setup.ts` liga `servidor` (`src\shared\mocks\servidor.ts`) com `onUnhandledFrame: 'error'` (requisição sem handler **falha**) e zera as sobrescritas a cada teste.
- **Sobrescrever um handler num teste** (outro cenário: erro, lista vazia, outro perfil). O padrão de rota é `*` + caminho do contrato, com `{id}` escrito `:id`:

```ts
import { http, HttpResponse } from 'msw'
import { servidor } from '../mocks/servidor'
import { exemploDoContrato } from '../teste/exemploDoContrato'

// lista vazia, partindo do exemplo do contrato
servidor.use(
  http.get('*/api/rh/funcionarios', () =>
    HttpResponse.json({
      ...exemploDoContrato<Record<string, unknown>>('listarFuncionarios'),
      itens: [],
      total: 0,
      totalPaginas: 0,
    }),
  ),
)
// erro problem+json NOMEADO do contrato (o 409 de CPF de um cadastro inativo)
servidor.use(
  http.post('*/api/rh/funcionarios', () =>
    HttpResponse.json(
      exemploDoContrato<Record<string, unknown>>('criarFuncionario', 409, 'cpfInativo'),
      { status: 409, headers: { 'content-type': 'application/problem+json' } },
    ),
  ),
)
// erro problem+json escrito no teste (quando o contrato não tem um exemplo para o caso)
servidor.use(
  http.post('*/api/auth/entrar', () =>
    HttpResponse.json({ title: 'Muitas tentativas.', status: 429 }, {
      status: 429,
      headers: { 'content-type': 'application/problem+json', 'Retry-After': '60' },
    }),
  ),
)
```

- **Limites** (medidos): o handler casa por método e caminho (não olha `query` nem corpo) e devolve **sempre o mesmo exemplo**; onde a operação tem vários `examples`, sai o primeiro. Para outro caso (um erro, um exemplo nomeado, uma lista vazia), sobrescreva como acima com `servidor.use(...)` e `exemploDoContrato(...)`. O contrato **não pode** ter rota 2xx com corpo sem exemplo (o `montar` o recusa); se o gerador acusar `operação sem exemplo utilizável` (saída 1), é defeito do contrato: dúvida.
- **Revisão do gerador** (feita ao copiá-lo do teste descartável): tirei a opção `--sem-ordenar` (só servia de controle do teste); acrescentei `versaoDoContrato` na saída; faz o código de saída ser 1 quando falta exemplo; o resumo no console não lista os pares de rotas (use `--detalhes`); ele avisa se houver rotas **ambíguas** (hoje: 0 de 7 pares que casam com a mesma URL). **(2026-11-02)** Passou a emitir também o `exemplos.ts` (acima); medido contra o contrato de 224 operações: o primeiro exemplo 2xx de `exemplosPorOperacao` é igual ao do handler nas 224, e `exemplosPorOperacao.criarFuncionario['409']` tem `cpf`, `email`, `cpfInativo` e `emailInativo`.

## 6. A lacuna do `tsc` e o teste de contrato por tela

**Medido (teste anterior, e reconfirmado agora com o `criarClienteHttp` e com os hooks):** o TypeScript acusa um nome **errado sozinho** em `query` e em `params.path`, e uma chave a mais no corpo **dos hooks** (`mutate`). Ele **não** acusa uma **chave a mais ao lado das certas** em `query` (ex.: `{ pagina: 1, tamanhoPagin: 20 }`, no cliente e nos hooks) nem em `params.path`, e **não** acusa chave a mais no corpo quando se chama o cliente direto (`cliente.POST(...)`). Em `path` a chave a mais é ignorada na URL (sem efeito); em `query` e no corpo ela **é enviada** ao servidor.

Por isso **cada tela que envia `query` ou corpo tem um teste de contrato**: captura a requisição de verdade (`servidor.use` guardando `new URL(request.url).searchParams` e `await request.json()`) e chama `esperarRequisicaoConforme(operationId, { consulta, corpo })` de `src\test\contrato.ts` (Task 2). Ele lê o contrato e falha se a tela enviou parâmetro ou chave que o contrato não tem, ou esqueceu um obrigatório. Modelo: `src\shared\api\contrato.test.ts`.

## 7. "pendente de integração" e `STATUS.md`

**Integração** é o passo que precisa da **API de verdade** (back-end rodando com banco). **A sessão nunca a roda**: nem por conta própria, nem porque o `STATUS.md` diz `pronta`, nem depois de a pessoa dizer que a API está no ar. É a regra do design aprovado (`docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md`, seção 5: "Passo que exige a API real (integração) a sessão **não roda**"). Quem roda o roteiro é **a pessoa** (ou o dono). Para cada passo desses:

1. Leia `contrato\STATUS.md` (no PowerShell, da raiz: `Select-String -Path contrato\STATUS.md -Pattern '^\| 0 '` mostra a linha da fatia 0). Ele só diz **o que escrever no motivo**:
   - fatia **não** `pronta` (`planejada` ou `em andamento`): o motivo é "a rota ainda não existe (`STATUS.md`: `planejada`)";
   - fatia `pronta`: o motivo é "a rota já existe, mas a sessão não roda contra a API real: quem roda é a pessoa ou o dono".
2. Nos dois casos, registre em `PROGRESSO.md` (tabela "Pendente de integração") a linha `pendente de integração`, com a rota (`operationId`), o motivo e **o roteiro exato pronto para rodar** (`.env.local` com `VITE_API_URL=http://localhost:5000` e `VITE_MOCKS=nao`; `npm --prefix front-end run dev`; os passos e o que se espera ver); e **siga em frente**. Quando a fatia estiver `pronta`, **diga no relatório** que o roteiro já pode ser rodado, sem perguntar nada sobre a API.
3. A pessoa traz o resultado; você o registra na tabela "Conferências da pessoa" do `PROGRESSO.md`.
4. **Nunca** peça senha ou segredo para registrá-lo em arquivo do repositório.

## 8. O servidor de teste e o script `csp`

Copiados de `docs\plano-front-end\ferramentas\` para `front-end\scripts\` na Task 5 (cada arquivo tem no cabeçalho para que serve, como roda e onde copiar):

- `servidor-de-teste.mjs`: serve o `dist` com os cabeçalhos **lidos de `public\staticwebapp.config.json`**; arquivo com extensão que falta dá 404; rota sem extensão devolve o `index.html` (o `navigationFallback`). Tem **uma rota de teste**, `/__sonda/arquivo.pdf`, que devolve um PDF como anexo (é o "link da própria origem" da sonda de download). Para olhar o build no navegador: `node front-end\scripts\servidor-de-teste.mjs` e abra `http://127.0.0.1:4173` (**conferência da pessoa**, não da sessão: A11).
- `violacoes-de-politica.mjs` (script `csp`): ver `02-pilha.md`, seção 6. **Depende do `build` feito antes.** A API é simulada no navegador (`page.route`) com o exemplo de sessão do contrato: **só** `POST /api/auth/entrar`, `POST /api/auth/renovar`, `POST /api/auth/sair` e `GET /api/auth/eu` respondem de verdade, mais as duas rotas **reais do contrato** que a sonda de download usa (abaixo). **Todo o resto devolve 404 problem+json**, inclusive `/api/auth/senha/*` (esqueci a senha, validar link, definir senha): é o que a 1b espera (a tela de definir senha mostra a mensagem com "Tentar de novo" e não o formulário). Linha `[FALHOU]` = passo que falhou; linha `[pulado]` = passo da **vitrine** num build sem a vitrine (abaixo). Usa o **navegador já instalado**: tenta o Edge e depois o Chrome (`--canais msedge,chrome`) e **não baixa nada**. Saída: **0** = passou; **1** = violação, passo que falhou ou controle mudo; **2** = nenhum navegador instalado (escreve `pendente: csp`: registre-o no `PROGRESSO.md`, A11 e G2 de `01-regras.md`, e a pessoa o roda onde houver Edge ou Chrome).
- **A sonda de download** (três passos fixos do `csp`; é daqui que as fichas tiram "o download passa sob a política", **não** de hipótese): (1) **CSV por `Blob`**: `fetch` de `GET /api/rh/auditoria/exportacao` (a API simulada devolve os bytes do CSV com `Content-Disposition: attachment; filename="sonda.csv"`), `URL.createObjectURL` e um `<a download>` clicado por script (o que `baixarBlob` faz); (2) **link temporário de outra origem**: `GET /api/rh/funcionarios/12/documentos/61/download` devolve um `LinkTemporario` para `https://armazenamento.exemplo.test/sonda.pdf` e um `<a href target="_blank" rel="noopener noreferrer">` clicado por script o abre (o que `abrirLinkTemporario` faz); (3) **link para a própria origem** (`/__sonda/arquivo.pdf`, rota do servidor de teste). Cada passo espera o evento `download` do navegador, confere o nome do arquivo e o começo do conteúdo, e **conta as violações** (qualquer uma faz o `csp` falhar). **Medido (2026-11-02):** os 3 passos `[ok ]`, 0 violações, no Edge 156 e no Chrome 149; negativo provocado: apontar o passo 1 para uma origem fora do `connect-src` faz o `csp` falhar (código 1) com a violação `connect-src` no passo certo. **O que a sonda não prova:** o `Content-Disposition` só chega ao `fetch` do navegador se a API o expuser no CORS (`Access-Control-Expose-Headers`); a API simulada o expõe, a de verdade é **pendente de integração** (1b, Task 6), e sem ele o nome do arquivo é o padrão da tela.
- **Quando o script acusa `connect-src` bloqueando a URL da API:** o build foi feito com `VITE_API_URL` diferente da origem do `connect-src` do arquivo de configuração. Refaça o build com a origem do `connect-src` (`$env:VITE_API_URL = 'https://...'; npm --prefix front-end run build`) e apague o `.env.local` se ele define outra. O valor `https://api.exemplo.test` do `connect-src` é um **marcador**: o dono o troca na Task 14 do plano do back-end; **você não o troca**.
- **Como estender** (uma vez por fatia, na tarefa que a fecha): acrescente um passo no fim da lista `PASSOS` do script (a linha `ACRESCENTE AQUI`, **depois** dos três passos da sonda): abra a rota principal da tela nova e, se ela abre diálogo, lista de seleção ou menu, abra-os. Sem dados simulados no build, a tela mostra o estado de erro; o que se quer ali é só "zero violações". Os dados são conferidos pelos testes com MSW. Se um passo novo precisa que a API simulada devolva algo, acrescente a rota **real do contrato** ao `stubDaApi` (como a sonda faz); nunca uma rota inventada.
- **Rede ociosa antes de desmontar a tela** (medido em 2026-11-02, Edge 156 e Chrome 149, `playwright-core` 1.64.1): um `goto` com pedido em andamento **não** gera "requisição que falhou"; um pedido cancelado por `AbortController` **gera** `net::ERR_ABORTED`, e é o que o react-query faz com a consulta cuja tela some (o `openapi-react-query` repassa o `signal`). Medido: um painel que é rota, fechado com `Escape` assim que o diálogo aparece, deu 3 de 3 consultas abortadas e o `csp` falhou (com a API simulada demorando 60 ms ou mais; com resposta imediata o pedido já tinha terminado e nada abortou); com `await aquietar()` antes do `Escape`, 0 nos dois casos. O projeto de teste imitava a tela com o `useQuery` e o `signal`, não era o app da 1b: a frequência no app real é **suposição**. Por isso o laço dos passos já espera a rede ociosa ao fim de cada passo, e **o passo que desmonta a tela por interação** (`Escape` num painel que é rota, clique no menu) ou abre várias páginas seguidas chama `await aquietar()` **antes de cada ação**. **Não** ignore `ERR_ABORTED` em geral: ele é um sinal.
- **Passos da vitrine** (1a, Task 13): crie-os com `vitrine.passo(nome, fazer)`; o primeiro é `vitrine.passo('abre a vitrine', () => vitrine.abrir())`. No build sem `VITE_VITRINE=sim`, `abrir()` vê "Página não encontrada.", marca a vitrine como indisponível e **todos** os passos da vitrine saem `[pulado]` sem falhar (se só o primeiro se pulasse, os outros esperariam 8 s por um botão que não existe e estourariam). **Só passo da vitrine pode ser pulado**; um `csp` só prova a vitrine se rodou ao menos uma vez com ela ligada (`$env:VITE_VITRINE = 'sim'; npm --prefix front-end run build`, numa linha só). Medido no projeto de teste (2026-11-02): build normal, 3 passos da vitrine `[pulado]` e `PASSOU` (código 0); build com `VITE_VITRINE=sim`, os mesmos passos `[ok ]`; passo da vitrine que falha com a vitrine ligada, `[FALHOU]` (código 1).
- **O que ele não prova:** o Azure mesmo. Se o arquivo de configuração será aplicado como o servidor de teste imita, só se confirma no ambiente (Task 14 do back-end).

## 9. Quando o contrato muda

O dono muda o contrato e sobe a versão (`info.version`; `CONVENCOES.md` seção 14: tirar ou renomear rota ou campo, ou tornar um campo obrigatório é versão maior; acrescentar rota ou campo opcional, menor; só texto ou exemplo, correção). A mudança chega à sua branch pela `main` (`01-regras.md`, seção F). O procedimento completo está em **`01-regras.md`, seção F**; em resumo: (1) o `tsc` (no `build`) e os testes apontam as telas; (2) você atualiza tipos e mocks (regerados sozinhos) e refaz **só o que quebrou**; (3) registra no `PROGRESSO.md`.
