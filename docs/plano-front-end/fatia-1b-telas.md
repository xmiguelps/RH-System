# Front-end, fatia 1b: telas da fatia 1 (colaboradores e base transversal): plano de implementação

> **Para quem executa (sessão de IA ou pessoa):** execute este plano tarefa por tarefa, na ordem. Os passos usam a sintaxe de caixa de seleção (`- [ ]`) para acompanhamento. **O método de execução (subagentes ou na própria sessão) ainda não foi escolhido: é decidido depois do planejamento.** Antes de começar, leia `LEIA-PRIMEIRO.md`, `01-regras.md`, `02-pilha.md`, `03-contrato-e-mocks.md`, `04-visual.md`, `fatia-0.md` e `fatia-1a-fundacao.md` (inteira, em especial a seção "Para a fatia 1b e as fichas"); este arquivo assume tudo isso e **segue as convenções da fatia 0 e da 1a**: pastas `src\app`, `src\shared\api|ui|form|formato|arquivos|lista|mocks`, `src\features\<assunto>`, `src\test`; nomes em português sem acento; testes ao lado do arquivo. **A nota de cada tela (`telas\fatia-1.md`) é a especificação funcional**: abra a seção da tela antes de começar a tarefa dela (a nota e o contrato são citados **por seção e por `operationId`**, nunca por número de linha: as notas mudam).

**Goal:** Entregar as telas da fatia 1 do front (lista, cadastro, ficha, documentos, Cadastros, log de auditoria, páginas de senha, Meus documentos e a ficha da equipe do gestor), com dados simulados, estados, textos e erros do contrato, **e** deixar escrito, na última seção, o padrão que as fichas 2 a 8 repetem.

**Architecture:** Cada tela é uma linha da tabela de rotas da 1a (`tabelaDeRotas.tsx`; a família `/rh`, `/gestor` ou `/eu` decide o perfil) e uma página em `src\features\<assunto>`. Dados por `$api` (`openapi-react-query`); formulários por `FormularioComErroDoServidor` (zod, erros do servidor por campo); painéis e janelas sobre uma lista são **rota filha** com `Outlet`; ações destrutivas por `JanelaDeConfirmacao`; arquivos por `useBaixarPorLink` e `baixarExportacao` (download) e por funções `enviar...` em arquivo à parte, testadas em ambiente node (upload). Peças pequenas que a 1a não tinha entram aqui e ficam para as fichas (tabela no fim): `capturar` (teste de contrato por tela), `FiltroDeSelecao`, `usePainelSobreLista`, `erroNoCampo`, `ErroDeApi.corpo`, o cliente público (`$apiPublica`), `useBaixarPorLink`, `MolduraDaFicha` e as tabelas de abas e de cadastros.

**Tech Stack:** o de `02-pilha.md` e da 1a. **Nenhuma biblioteca nova.**

**Spec:** `telas\fatia-1.md` (a nota de cada tela); `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md` (seções 5 a 8); contrato `contrato\fontes\20-fatia-1-colaboradores.yaml` (43 operações; leia a versão em `info.version`), `00-base.yaml` e `CONVENCOES.md` (seções 2 a 6, 11 e 12); `04-visual.md` (tudo o que é visual).

## Global Constraints

- Tudo de `fatia-0.md` e de `fatia-1a-fundacao.md` (Global Constraints) continua valendo: Node `^22.22.2 || ^24.15.0` (a regra do Node 26 está em `02-pilha.md`, seção 2); comandos da **raiz** no **PowerShell 5.1** (sem `&&`; comando de prova com variável de ambiente numa linha só: `01-regras.md`, A14); `npm --prefix front-end run <script>`; a branch é **`feat/front-fatias-1-a-8`** (a 1a já a usa; a 1b continua nela); commit e push a cada passo que manda commitar, mensagem em português **sem acento**, **nunca o Claude como coautor** (`01-regras.md`, C6); sem pull request agora (o PR 2 é o do fim do plano inteiro, C3).
- **Pilha fechada** (A7): nenhuma biblioteca **fora de `02-pilha.md`**. **O contrato é lei** (A6): nenhuma rota, campo ou texto que a nota não traga; o que faltar vira dúvida (`DUVIDAS.md`), nunca edição de `contrato\`.
- **Textos:** os da nota da tela; onde a nota diz "(texto meu)", vale o texto da nota. Nomes de código em português sem acento. Mensagens de erro: o `detail` do contrato (decida pelo `codigo`, nunca pelo `title`; **nunca** mostre `regraDoBanco` sozinha).
- **Política de segurança** (1a, Global Constraints): nada de `<style>`, `style=""`, `dangerouslySetInnerHTML`, imagem `data:`/`blob:`, fonte ou CSS de fora, `eval`, Radix. Só tokens de `04-visual.md`, só componentes de `src\shared\ui` e `src\shared\lista`. Texto escrito por pessoas (descrições, nomes, `descricao` do log) é **texto**, nunca HTML.
- **PROVAS** (o conjunto de comandos de fim de tarefa): `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test` (esperado: tudo passando; `build` com `built in`). O fim de **fatia** tem a sua prova: a **Prova padrão de fatia** (seção final do "Padrão para as fichas 2 a 8", abaixo). **ENVIAR:** `git status; git add <arquivos da tarefa> docs\plano-front-end\PROGRESSO.md; git commit -m "<mensagem>"; git push` (nunca `git add .`; confira no `git status` que não entram `node_modules`, `dist`, `.env*`, `\gerado\`).
- **Teste de contrato por tela:** toda operação que uma tela chama tem um teste que a captura com `capturar('<operationId>')` e a confere com `.conforme()` (o `tsc` não pega chave extra: `03-contrato-e-mocks.md`, seção 6). O teste `coberturaDaFatia1.test.ts` (Task 10) falha se uma das 42 operações com tela ficar sem teste.
- **Dados sensíveis** (CPF, telefone, endereço, e-mail pessoal, documentos, token do link): nada no console, no `localStorage` nem na URL (única exceção: o `token` do endereço `/definir-senha`, que vem do e-mail).
- **Datas:** dia de calendário (`2026-10-07`) **nunca** passa por `new Date('2026-10-07')`; instante (`...Z`) mostra em São Paulo; só as funções de `src\shared\formato`. **CPF e telefone:** o RH vê completos, com pontuação; o gestor vê `telefoneMascarado` como veio e nunca o CPF.
- **Botão que grava:** `carregando` enquanto a chamada está pendente; duplo clique = **uma** requisição. **Upload:** a chamada fica numa função `enviar...` em arquivo à parte (testada com `// @vitest-environment node`) e o componente a chama (testado em jsdom com `vi.mock` da função): o jsdom não monta multipart (`02-pilha.md`, 5.5).
- Cada tela cobre **carregando, vazio, erro e sem permissão** (a guarda da família mostra o 403 ao perfil errado; o 403 que a API devolve mostra `Você não tem permissão para esta ação.`).
- Em teste, `input type="date"` se preenche com `fireEvent.change` (o `userEvent.type` em campo de data **não foi medido**); dia "hoje" com `vi.useFakeTimers({ toFake: ['Date'] })` e `vi.setSystemTime(new Date('2026-10-07T12:42:00Z'))`.

## Review Focus

Falhas que o desenho implica, que a pessoa real encontra e que o teste de contrato por tela não pega (cada uma tem teste na tarefa indicada):

1. **Ficha de quem não tem dados pessoais** (o administrador do seed: `cpf`, `telefone`, `dataNascimento`, `cargo`, `setor`, `tipoContrato`, `perfil` `null`): a ficha mostra "—" sem quebrar e "Editar dados" abre com os campos obrigatórios vazios. Task 3A e 3B.
2. **Página que fica vazia depois de excluir** (excluir o último benefício da página 2 deixa a lista vazia numa página que não existe mais): a lista volta à página 1. Task 5D.
3. **Duplo clique e ação repetida num botão que grava** (Ativar, Desativar, Reenviar convite, interruptor "Gestor ausente") e a linha que mudou por outra pessoa (404 ou 422): uma requisição só, e a linha é refeita. Task 5C e 5F.
4. **Texto de outra pessoa mostrado como HTML ou como fórmula** (descrição de cargo, `descricao` do log, nome do arquivo): aparece escrito, nada executa. Tasks 4, 5A e 6.
5. **Link de senha:** o link vence entre abrir e salvar (422 ao salvar vira o estado "link inválido"), o `token` nunca aparece na tela nem no `localStorage`, e nenhuma chamada da página leva `Authorization`. Task 7.

## Fatos lidos e o que suponho

**Li** (arquivo e linha):
- A nota de cada tela é a especificação: `telas\fatia-1.md`, uma seção por tela, achada pelo título: "Regras comuns a todas as telas desta fatia"; "Tela 01"; "Tela 02"; "Tela 03" (e os fluxos "Editar dados", "Inativar" e "Readmitir"); "Tela 09"; "Tela 10"; "Tela 29" (moldura e Cargos, e as seções de Setores, Gestores, Benefícios e Usuários do sistema); "Tela 30"; "Tela sem print: Cadastros, Empresa"; "Tela 31"; "Tela 52"; as três telas sem print de senha ("Esqueci minha senha", "Definir senha", "Trocar senha"); "Meus documentos"; e "Conferência das 43 rotas".
- A 1a entrega os componentes, os ajudantes e as rotas-base que esta fatia consome (`fatia-1a-fundacao.md`, seção "Para a fatia 1b e as fichas: o que esta fundação entrega"; as assinaturas estão nas Tasks da 1a: `Abas`, `Tabela`, `Paginacao` e `MenuDeAcoes` na Task 5; `Janela`, `JanelaDeConfirmacao` e o `Toast` na Task 6; `esquemas.ts` e `FormularioComErroDoServidor` na Task 8; `$api` e a renovação do token na Task 9; download e upload na Task 10; `useEstadoDaLista` na Task 11; rotas, menu e layout na Task 12); a tabela de caminhos está na mesma seção ("Caminhos": `/eu/meus-documentos`, `/esqueci-senha`, `/definir-senha?token=`) e, valendo sobre ela, em `05-caminhos-de-tela.md`.
- `ErroDeApi` só guarda `status`, `title`, `detail`, `codigo`, `traceId`, `errors` e `retryAfterSegundos` (`ErroDeApi` na fatia 0, Task 2, "Interfaces"); o 409 de CPF ou e-mail repetido traz `cadastroExistente` (schema `ColaboradorDuplicadoProblema`, em `20-fatia-1-colaboradores.yaml`, na operação `criarFuncionario`), que **se perderia**: a Task 2 acrescenta `ErroDeApi.corpo`.
- O `Toast` da 1a só recebe texto (`NotificacoesProvider` na Task 6 da 1a), sem botão: o aviso "foi cadastrado" com "Abrir ficha" (nota da Tela 02, "Estados", item "Sucesso") vira uma faixa `Aviso` na lista (Task 2). `AcaoDoMenu` não tem dica (`MenuDeAcoes` na Task 5 da 1a): a Task 3 acrescenta `dica?` (nota da Tela 03, o item do menu "..." com a dica "Esta pessoa não tem e-mail corporativo cadastrado.").
- A 1a não define cliente sem sessão para as rotas públicas; a ficha 5 o espera (`fichas\fatia-5.md`, seção "Ordem de construção e dependências", item "Da fundação (fatia 1a) esta fatia usa": "variante sem sessão"): a Task 7 o cria (`$apiPublica`).
- `esperarRequisicaoConforme` é descrita só para `query` e corpo JSON (fatia 0, Task 2: "Interfaces" de `src\test\contrato.ts` e os testes de `contrato.test.ts`): a Task 4 testa o multipart e, se faltar, o estende.
- Os exemplos do contrato são o cenário de `ELENCO.md` (hoje = 2026-10-07 09:42); o handler devolve sempre o mesmo exemplo e não olha `query` (`03-contrato-e-mocks.md`, seção 5, "Limites"): cada teste que quer outro cenário sobrescreve.

**Suponho** (marcado `(suponho)` onde entra): preencher `CLT` por padrão no cadastro (o PNG 02 mostra `CLT`; o contrato não diz padrão); abas e subtítulo da lista sem número enquanto `totais` não chega; fechar a ficha da equipe leva a `/gestor/equipe` (a página é da fatia 7; até lá aparece "Página não encontrada." dentro do layout); diálogo aninhado como filho de `<Janela>`; troca de linha de tabela por `invalidarRota` (um refetch da página, que com `keepPreviousData` não pisca) em vez de remendar o cache; `LogoR tom="escuro"` é o quadrado de fundo `primaria` para fundo claro (`04-visual.md`, 6).

**Não abri** os PNG 01 a 03, 09, 10, 29 a 31 e 52 nesta execução: o que digo deles vem da nota. A pessoa os confere ao lado da tela (Task 10).

## Estrutura de arquivos (o que esta fatia cria ou toca)

```
front-end\src\
  test\            capturar.ts  telas.tsx  formulario.ts                       (novos; Task 1 e 2)  textosProibidos.ts (Task 10)
  shared\api\      erroDeApi.ts (acrescenta corpo)  cadastroExistente.ts  apiPublica.ts
  shared\form\     erroNoCampo.ts
  shared\formato\  datas.ts (acrescenta 4)  documentos.ts (acrescenta 2)  ufs.ts
  shared\lista\    valoresDeLista.ts  FiltroDeSelecao.tsx  usePainelSobreLista.ts
  shared\arquivos\ useBaixarPorLink.ts
  shared\ui\       MenuDeAcoes.tsx (acrescenta dica)
  features\colaboradores\  rotulos.ts  EtiquetaDaSituacao.tsx  ListaDeColaboradores.tsx  PainelCadastrar.tsx  CamposDoColaborador.tsx
                           esquemaDoColaborador.ts  useOpcoesDeApoio.ts  PainelDaFicha.tsx  MolduraDaFicha.tsx  abasDaFicha.tsx
                           AbaDados.tsx  AbaDocumentos.tsx  QuadroDeIndicadores.tsx  indicadores.ts  endereco.ts  FormularioDeEdicao.tsx
                           DialogoInativar.tsx  DialogoReadmitir.tsx  useBuscaDePessoas.ts
  features\documentos\     rotulos.ts  enviarDocumento.ts  ModalAdicionarDocumento.tsx  PaginaMeusDocumentos.tsx  ModalEnviarMeuDocumento.tsx
  features\cadastros\      PaginaDeCadastros.tsx  itensDoCadastro.ts  useContagensDosCadastros.ts  ListaDeCadastro.tsx
                           cargos\  setores\  gestores\  beneficios\  empresa\  usuarios\
  features\auditoria\      PaginaDeAuditoria.tsx  rotulosDeAuditoria.ts  periodoDaAuditoria.ts  DetalheDoEvento.tsx
  features\autenticacao\   CartaoPublico.tsx  PaginaEsqueciSenha.tsx  PaginaDefinirSenha.tsx  useValidacaoDoLink.ts  esquemasDeSenha.ts  JanelaTrocarSenha.tsx
  features\equipe\         PainelDaFichaDaEquipe.tsx  abasDaFichaDaEquipe.tsx
  app\             tabelaDeRotas.tsx (acrescenta linhas)  RodapeDoMenu.tsx (acrescenta Trocar senha)  matrizDePerfis.test.tsx  textosProibidos.test.tsx  coberturaDaFatia1.test.ts
```

---

### Task 1: Lista de colaboradores (tela 01) e as peças de lista e de teste que as outras telas reusam

Nota: "Tela 01" em `telas\fatia-1.md`; PNG `Figma\telas\01-rh-colaboradores.png`.

**Files:**
- Create: `front-end\src\test\capturar.ts`, `telas.tsx`, `formulario.ts`; `src\shared\lista\valoresDeLista.ts`, `FiltroDeSelecao.tsx`, `usePainelSobreLista.ts`; `src\features\colaboradores\rotulos.ts`, `EtiquetaDaSituacao.tsx`, `ListaDeColaboradores.tsx`.
- Modify: `src\app\tabelaDeRotas.tsx` (`ROTAS_RH` ganha `{ caminho: 'colaboradores', elemento: <ListaDeColaboradores />, filhas: [] }`).
- Test: `src\test\capturar.test.ts`; `src\shared\lista\valoresDeLista.test.ts`, `FiltroDeSelecao.test.tsx`, `usePainelSobreLista.test.tsx`; `src\features\colaboradores\rotulos.test.ts`, `ListaDeColaboradores.test.tsx`.

**Interfaces:**
- Consumes (1a): `useEstadoDaLista`, `Tabela`, `Paginacao`, `CampoDeBusca`, `Abas`, `Selecao`, `Avatar`, `Selo`, `CabecalhoDePagina`, `BotaoLink`, `AvisoDeErro`, `EstadoVazio`, `$api`, `formatarData`, `formatarNumero`, `entrarComo`, `renderizarApp`, `esperarRequisicaoConforme` (fatia 0), `exemplos`, `servidor`; `keepPreviousData` de `@tanstack/react-query`.
- Produces:
  - `capturar.ts`: `type Capturada = { consulta: URLSearchParams; corpo?: Record<string, unknown>; parametrosDoCaminho: Record<string, string>; cabecalhos: Headers }`; `type RespostaDeTeste = { status?: number; corpo?: unknown; cabecalhos?: Record<string, string>; espera?: Promise<unknown> }`; `type Capturador = { todas: Capturada[]; ultima(): Capturada; quantas(): number; conforme(): void }`; `function capturar(operationId: string, resposta?: RespostaDeTeste): Capturador` (registra com `servidor.use` o handler da operação, lido de `exemplos[operationId]` — método e caminho —, guarda cada requisição e devolve `resposta` ou o exemplo do contrato; `conforme()` chama `esperarRequisicaoConforme(operationId, { consulta, corpo })` com a última; em `multipart/form-data` o `corpo` sai de `request.formData()` com o arquivo como o nome dele); `function problema(status: number, campos?: { title?: string; detail?: string; codigo?: string; errors?: Record<string, string[]> } & Record<string, unknown>): RespostaDeTeste` (corpo `problem+json` com `traceId`; os campos extras, como `cadastroExistente`, entram no corpo; aceita como `campos` o **exemplo inteiro** devolvido por `exemploDoContrato(operationId, status, nome)`, `03-contrato-e-mocks.md`, seção 5).
  - `telas.tsx`: `function renderizarTela(rota: string, opcoes?: { perfil?: Perfil; usuario?: Partial<Usuario>; queryClient?: QueryClient }): RenderResult & { user: UserEvent; roteador: ReturnType<typeof createMemoryRouter> }` (faz `localStorage.clear()`, `tokenDeAcesso.definir(null)`, `entrarComo(perfil ?? 'RH', usuario)` e `renderizarApp({ rota, queryClient })`; `user = userEvent.setup()`).
  - `formulario.ts`: `escolherOpcao(user: UserEvent, nomeDoCampo: string, textoDaOpcao: string): Promise<void>` (clica no `combobox` de nome `nomeDoCampo` e na `option` `textoDaOpcao`); `preencherData(rotulo: string, valor: string): void` (`fireEvent.change` no campo de data).
  - `valoresDeLista.ts`: `valorDeEnum<V extends string>(valor: string | undefined, validos: readonly V[]): V | undefined`; `inteiroDaUrl(valor: string | undefined): number | undefined` (só inteiro `>= 1`).
  - `FiltroDeSelecao.tsx`: `function FiltroDeSelecao<V extends string>(props: { rotulo: string; todos: string; opcoes: readonly OpcaoDeSelecao<V>[]; valor: V | undefined; aoMudar: (valor: V | null) => void; carregando?: boolean })` (uma `Selecao` com a opção "todos" de valor sentinela `TODOS`; `valor` indefinido mostra `todos`; escolher "todos" chama `aoMudar(null)`; `rotulo` é o nome acessível).
  - `usePainelSobreLista.ts`: `function usePainelSobreLista(): { paraPainel(destino: string, parametros?: Record<string, string>): To; fechar(estado?: unknown): void }` (`paraPainel('novo')` devolve `{ pathname: 'novo', search }` com a `search` atual da lista **sem** `aba`; `fechar()` navega a `..` (rota pai) com a mesma `search` sem `aba`, levando `estado` em `location.state`).
  - `rotulos.ts`: `type TipoDeContrato = Esquema<'FuncionarioTipoContrato'>`; `rotuloDoTipoDeContrato(valor: string | null | undefined): string` (`CLT` → `CLT`, `ESTAGIO` → `Estágio`, outro valor como veio, vazio `—`); `rotuloDoNivelDeAcesso(perfil: Perfil | null | undefined): string` (`Funcionario` → `Colaborador`, `Gestor`, `RH`, vazio `—`); `OPCOES_DE_TIPO_DE_CONTRATO: readonly OpcaoDeSelecao<TipoDeContrato>[]`; `OPCOES_DE_NIVEL_DE_ACESSO: readonly OpcaoDeSelecao<Perfil>[]` (rótulo `Colaborador` para `Funcionario`); `etiquetaDaSituacao(situacao: Esquema<'FuncionarioSituacao'>, situacaoHoje: Esquema<'SituacaoHoje'> | null): { texto: string; tom: TomDoSelo }` (`INATIVO` → `Inativo`/`neutro`; `EM_ADMISSAO` → `Em admissão`/`contorno`; `ATIVO` com `FERIAS` → `Férias`/`info`, com `ATESTADO` → `Atestado`/`alerta`, qualquer outro → `Ativo`/`sucesso`: nota, "Regras comuns", item "Rótulos que se repetem", etiqueta "Situação hoje").
  - `EtiquetaDaSituacao.tsx`: `function EtiquetaDaSituacao(props: { situacao: Esquema<'FuncionarioSituacao'>; situacaoHoje: Esquema<'SituacaoHoje'> | null })` (um `Selo`).
  - `ListaDeColaboradores.tsx`: `function ListaDeColaboradores()` (a página `/rh/colaboradores`; termina com `<Outlet />` para os painéis das Tasks 2 e 3).

- [ ] **Step 1: Conferir o chão.** Run: `git status; git branch --show-current; git fetch origin; git merge origin/main; Get-Content contrato\openapi.yaml -TotalCount 12 | Select-String '^\s+version:'; Select-String -Path contrato\STATUS.md -Pattern '^\| [017] '`
  Expected: branch `feat/front-fatias-1-a-8`; `merge` sem conflito; a fundação fechada (`PROGRESSO.md` diz que a Task 14 da 1a terminou); anote a versão do contrato (E4) e que a fatia 1 está `planejada` (tudo é "pendente de integração"). Leia `telas\fatia-1.md`, seções "Regras comuns a todas as telas desta fatia" e "Tela 01", e abra o PNG 01.
- [ ] **Step 2: Escrever os testes que falham.**

```tsx
// capturar.test.ts
it('capturar guarda a consulta, o corpo e os parametros do caminho e devolve o exemplo do contrato')   // capturar('obterFichaDoFuncionario') + fetch GET /api/rh/funcionarios/12 → parametrosDoCaminho { funcionarioId: '12' }; resposta tem nome 'Bruno Lima'
it('conforme falha quando a tela manda uma chave a mais ao lado das certas')   // capturar('listarFuncionarios'); GET ?pagina=1&tamanhoPagin=20 → conforme() lança /tamanhoPagin/
it('problema monta um problem+json com os campos extras e capturar o devolve')   // problema(409, { codigo: 'X', cadastroExistente: { funcionarioId: 1 } }) → content-type application/problem+json, corpo com cadastroExistente
// valoresDeLista.test.ts
it.each(...)('valorDeEnum aceita so valores da lista')   // ('INATIVO', [...]) → 'INATIVO'; ('XYZ') → undefined; (undefined) → undefined
it.each(...)('inteiroDaUrl aceita so inteiro maior ou igual a 1')   // '2' → 2; '0', '-3', 'abc', '1.5', '' → undefined
// FiltroDeSelecao.test.tsx
it('mostra o texto de todos, escolhe uma opcao (aoMudar com o valor) e volta a todos (aoMudar(null))')   // todos 'Todos os setores'; opções 'Comercial' ('1')
// usePainelSobreLista.test.tsx   (createMemoryRouter: rota pai 'colaboradores' com filha 'novo'; entrada '/colaboradores/novo?situacao=INATIVO&aba=documentos')
it('paraPainel mantem a busca da lista e tira aba')   // paraPainel('12') → { pathname: '12', search: '?situacao=INATIVO' }
it('fechar volta a rota pai com os filtros e sem aba, e leva o estado')   // location.pathname '/colaboradores', search '?situacao=INATIVO'; fechar({ x: 1 }) → location.state { x: 1 }
// rotulos.test.ts
it('rotulos de tipo de contrato e de nivel de acesso')   // Funcionario → 'Colaborador'; 'CLT'; 'ESTAGIO' → 'Estágio'; 'NOVO' → 'NOVO'; null → '—'
it.each(...)('etiquetaDaSituacao')   // INATIVO,null → Inativo/neutro; EM_ADMISSAO,null → Em admissão/contorno; ATIVO+FERIAS → Férias/info; ATIVO+ATESTADO → Atestado/alerta; ATIVO+SEM_REGISTRO|TRABALHANDO|null → Ativo/sucesso
// ListaDeColaboradores.test.tsx   (renderizarTela('/rh/colaboradores'); exemplos de listarFuncionarios e listarSetores)
it('mostra o titulo, o subtitulo com os totais e as tres abas com contagem')   // '101 ativos, 3 em admissão e 7 inativos'; tabs 'Ativos 101', 'Em admissão 3', 'Inativos 7'; 'Ativos' selecionada
it('mostra as seis colunas e uma linha por colaborador, sem a etiqueta Experiencia')   // Nome, Cargo, Setor, Admissão, Situação hoje, Ações; 'Bruno Lima' com 'bruno.lima@empresa.com.br' e iniciais 'BL'; '14/03/2022'; Bruno 'Atestado', Carla 'Ativo', Sofia 'Ativo'; queryByText('Experiência') null
it('a requisicao e conforme o contrato e nao pede ordenacao nem tem controle de ordenacao')   // capturar('listarFuncionarios'): pagina=1, tamanhoPagina=20, situacao=ATIVO; sem ordenarPor/direcao; conforme(); nenhum columnheader com aria-sort
it('aba Inativos manda situacao=INATIVO, poe situacao na URL e volta a pagina 1')   // roteador.state.location.search '?situacao=INATIVO'; aba Ativos tira o parametro da URL
it('busca espera 300 ms, manda uma requisicao e corta em 100 caracteres')   // fake timers; digita 101 caracteres → 1 chamada com busca.length 100; placeholder 'Buscar por nome, CPF ou matrícula'
it('filtros de setor e de contrato mandam setorId (numero) e tipoContrato')   // 'Todos os setores' e 'Todos os contratos'; opções de setor vêm de capturar('listarSetores') com tamanhoPagina=100; escolher 'Financeiro' → setorId=2; 'Estágio' → tipoContrato=ESTAGIO
it('o estado vem da URL e valor desconhecido vale o padrao')   // '?situacao=XYZ&setorId=abc&pagina=2' → situacao=ATIVO, sem setorId, pagina=2
it('rodape escreve Mostrando 1 a 20 de 101 e Proxima manda pagina=2')   // o exemplo traz pagina 1, tamanhoPagina 20, total 101; sem rodapé quando total 0
it('carregando mostra o esqueleto, abas sem numero e subtitulo com traco')   // resposta atrasada (espera): abas /^Ativos/ sem número; subtítulo '— ativos, — em admissão e — inativos'
it('vazio sem filtro e vazio com busca ou filtro')   // 'Nenhum colaborador nesta aba.' / 'Nenhum colaborador encontrado.'; itens: [], total: 0
it('erro 500 mostra a mensagem com Tentar de novo e refaz a chamada; 403 da API mostra a mensagem de permissao')
it('Funcionario em /rh/colaboradores ve o 403 da guarda e o menu dele')   // renderizarTela('/rh/colaboradores', { perfil: 'Funcionario' }): 'Você não tem permissão para ver esta página.'
it('os links Adicionar colaborador e Abrir ficha mantem o filtro da URL')   // em '?situacao=INATIVO': href '/rh/colaboradores/novo?situacao=INATIVO' e '/rh/colaboradores/12?situacao=INATIVO'
```

- [ ] **Step 3: Rodar e ver falhar.** Run: `npm --prefix front-end test src/test src/shared/lista src/features/colaboradores`
  Expected: FAIL (arquivos inexistentes).
- [ ] **Step 4: Implementar** os arquivos com as assinaturas acima, nesta ordem: ajudantes de teste, `valoresDeLista`, `FiltroDeSelecao`, `usePainelSobreLista`, `rotulos`, `EtiquetaDaSituacao`, `ListaDeColaboradores`, linha da rota. `ListaDeColaboradores`: `useEstadoDaLista({ filtros: ['situacao', 'setorId', 'tipoContrato'] as const })` **sem** `ordenacaoPadrao`; `SITUACOES = ['ATIVO', 'EM_ADMISSAO', 'INATIVO'] as const` (as do contrato); consulta `$api.useQuery('get', '/api/rh/funcionarios', { params: { query: { ...consulta, situacao: valorDeEnum(filtros.situacao, SITUACOES) ?? 'ATIVO', setorId: inteiroDaUrl(filtros.setorId), tipoContrato: valorDeEnum(...) } } }, { placeholderData: keepPreviousData })`; a aba "Ativos" chama `definirFiltro('situacao', null)`; `Tabela` sem `aoOrdenar`; `rodape` só com `total > 0`; busca cortada em 100 (`texto.slice(0, 100)`); botão e "Abrir ficha" são `BotaoLink to={paraPainel(...)}` (o ícone `Plus` e `PanelLeft`). Estados: `vazio` e `erro` da nota; subtítulo e abas com `—` até `totais` chegar. Visual: `04-visual.md`, seção 3 (abas em pílula, tabela, paginação, selo).
- [ ] **Step 5: Rodar e ver passar.** Run: `npm --prefix front-end test src/test src/shared/lista src/features/colaboradores`
  Expected: PASS.
- [ ] **Step 6: Controle.** Em `ListaDeColaboradores.tsx` troque `inteiroDaUrl(filtros.setorId)` por `filtros.setorId` (texto): o `build` (`tsc`) deve acusar, e se o tipo passar, o teste "filtros ... mandam setorId (numero)" deve FALHAR. Restaure.
- [ ] **Step 7: PROVAS** (Global Constraints).
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md`: linha `F1b·T1`; tela 01 `feita com <versão>`, `usa provisório: FuncionarioTipoContrato`, "conferir ao lado do PNG 01: abas, subtítulo, colunas, etiquetas, rodapé". ENVIAR `front-end` com `feat(front): lista de colaboradores, ajudantes de teste e de lista`.

---

### Task 2: Cadastrar colaborador (tela 02)

Nota: "Tela 02" em `telas\fatia-1.md`; PNG `02-rh-colaboradores-adicionar-colaborador.png`; contrato `criarFuncionario` (em `20-fatia-1-colaboradores.yaml`).

**Files:**
- Modify: `front-end\src\shared\api\erroDeApi.ts` (acrescenta `corpo`), `src\app\tabelaDeRotas.tsx` (`colaboradores` ganha a filha `novo`), `src\features\colaboradores\ListaDeColaboradores.tsx` (faixa do aviso de cadastro).
- Create: `src\shared\api\cadastroExistente.ts`; `src\shared\form\erroNoCampo.ts`; `src\features\colaboradores\useOpcoesDeApoio.ts`, `esquemaDoColaborador.ts`, `CamposDoColaborador.tsx`, `PainelCadastrar.tsx`.
- Test: `src\shared\api\clienteApi.test.ts` (acrescenta), `cadastroExistente.test.ts`; `src\shared\form\erroNoCampo.test.ts`; `src\features\colaboradores\useOpcoesDeApoio.test.tsx`, `esquemaDoColaborador.test.ts`, `PainelCadastrar.test.tsx`.

**Interfaces:**
- Consumes: Task 1 (`capturar`, `problema`, `renderizarTela`, `escolherOpcao`, `preencherData`, `usePainelSobreLista`, `rotulos`); 1a: `FormularioComErroDoServidor`, `esquemas.ts` (`campoTexto`, `campoEmail`, `campoCpf`, `campoTelefone`, `campoData`, `opcional`), `Janela`, `Campo`, `Entrada` (`mascara`), `Selecao`, `CaixaDeSelecao`, `OpcoesEmCartao`, `Abas`, `Aviso`, `Botao`, `BotaoLink`, `Carregando`, `AvisoDeErro`, `invalidarRota`, `mascararCpf`, `mascararTelefone`, `hojeEmSaoPaulo`.
- Produces:
  - `ErroDeApi.corpo: Record<string, unknown> | null` (o `problem+json` inteiro lido por `deResposta`; `null` se vazio); o construtor aceita `extras.corpo`. Nada do que a fatia 0 testa muda.
  - `cadastroExistente.ts`: `type CadastroExistente = NonNullable<Esquema<'ColaboradorDuplicadoProblema'>['cadastroExistente']>`; `function lerCadastroExistente(erro: unknown): CadastroExistente | null` (só `ErroDeApi` 409 com `corpo.cadastroExistente` de `funcionarioId` número, `nome` texto, `ativo` booleano).
  - `erroNoCampo.ts`: `function erroNoCampo(erro: unknown, campoPorCodigo: Readonly<Record<string, string>>): unknown` (se `erro` é `ErroDeApi` cujo `codigo` está no mapa e que não traz `errors`, devolve um `ErroDeApi` igual (mesmos `status`, `title`, `codigo`, `corpo`) com `errors: { [campo]: [detail ?? title] }`, para o formulário marcar o campo; senão devolve o mesmo `erro`).
  - `useOpcoesDeApoio.ts`: `type OpcoesDeApoio = { cargos: readonly OpcaoDeSelecao[]; setores: readonly OpcaoDeSelecao[]; beneficios: readonly { id: number; nome: string }[]; gestorDoSetor(setorId: string | null | undefined): string | null | undefined; carregando: boolean; erro: unknown; tentarDeNovo(): void }`; `function useOpcoesDeApoio(): OpcoesDeApoio` (`listarCargos`, `listarSetores` e `listarBeneficiosDoCatalogo`, cada um com `tamanhoPagina = 100`; `valor` das opções é `String(id)`; `gestorDoSetor`: `undefined` sem setor escolhido, `null` setor sem gestor, senão o nome).
  - `esquemaDoColaborador.ts`: `esquemaDoColaborador` (zod; **um só** esquema para cadastro e edição), `type EntradaDoColaborador = z.input<typeof esquemaDoColaborador>`, `type SaidaDoColaborador = z.output<typeof esquemaDoColaborador>`; `valoresIniciaisDoCadastro: EntradaDoColaborador`; `montarCorpoDeCadastro(v: SaidaDoColaborador): Esquema<'FuncionarioCriarComando'>` (envia **só** as chaves do contrato, com `incluirNoOnboarding: false` e `formularioAdmissaoId: null`); `montarCorpoDeEdicao(v: SaidaDoColaborador): Esquema<'FuncionarioEditarComando'>` (Task 3B). Regras (nota da Tela 02, tabela "Campo (rótulo) / Campo da API / Regra"; mensagens: as de `zod.ts` da 1a, e estas): `nome` 1 a 150 (`Informe o nome.`); `cpf` = `campoCpf()`; `dataNascimento` = `campoData()` e não futura (`A data de nascimento não pode ser no futuro.`, texto meu; compare o texto `YYYY-MM-DD` com `hojeEmSaoPaulo()`); `telefone` = `campoTelefone()`; `emailCorporativo` = `campoEmail()` até 150; `dataAdmissao` = `campoData()` (pode ser futura); `tipoContrato` `CLT` ou `ESTAGIO`; `cargoId` e `setorId` texto não vazio → número (`Escolha o cargo.`, `Escolha o setor.`); `perfil` `Funcionario`, `Gestor` ou `RH`; `beneficioIds` números sem repetição; `enviarConvite` booleano. Só na edição (opcionais, `''` → `null`): `emailPessoal` (e-mail, até 150), `rg` e `rgOrgaoEmissor` (até 20), `endereco` com `cep` (8 dígitos), `logradouro` (150), `numero` (10), `complemento` (100), `bairro` (100), `cidade` (100), `uf` (2 letras maiúsculas).
  - `CamposDoColaborador.tsx`: `function CamposDoColaborador(props: { formulario: UseFormReturn<EntradaDoColaborador, unknown, SaidaDoColaborador>; opcoes: OpcoesDeApoio; modo: 'cadastro' | 'edicao'; perfilBloqueado?: boolean; duplicado?: { campo: 'cpf' | 'emailCorporativo'; cadastro: CadastroExistente } | null })` (os blocos "Dados pessoais" e "Contrato" da nota, com `Campo`, `Entrada` (máscaras de CPF e telefone, `lang="pt-BR"` nos campos de data), `Selecao`; o campo "Gestor" é um `Entrada readOnly` fora do formulário, com o `placeholder` `O gestor do setor escolhido`, o nome do gestor, ou `Este setor não tem gestor`; abaixo do CPF ou do e-mail em conflito, um `BotaoLink variante="texto"` `Abrir a ficha de {nome}` para `/rh/colaboradores/{funcionarioId}`; `perfilBloqueado` desabilita "Nível de acesso" com a dica `Você não pode alterar o seu próprio nível de acesso.`).
  - `PainelCadastrar.tsx`: `function PainelCadastrar()` (rota `/rh/colaboradores/novo`).

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// clienteApi.test.ts (acrescenta)
it('ErroDeApi guarda o corpo inteiro do problem+json (cadastroExistente)')   // 409 { ..., cadastroExistente: { funcionarioId: 201, nome: 'Henrique Alves', ativo: false } } → erro.corpo.cadastroExistente.nome
// cadastroExistente.test.ts
it('le cadastroExistente do 409 e devolve null para qualquer outra coisa')   // 409 com o corpo; 409 sem; 400; Error comum; ativo não booleano → null
// erroNoCampo.test.ts
it('poe o detail no campo do codigo e mantem o resto do erro')   // erroNoCampo(ErroDeApi 409 CPF_JA_CADASTRADO, { CPF_JA_CADASTRADO: 'cpf' }) → errors { cpf: [detail] }, status 409, corpo preservado
it('nao mexe em codigo fora do mapa nem em erro que ja traz errors')
// useOpcoesDeApoio.test.tsx
it('pede as tres listas com tamanhoPagina=100 e monta as opcoes e o gestor do setor')   // capturar('listarCargos'), capturar('listarSetores') e capturar('listarBeneficiosDoCatalogo'), cada um com tamanhoPagina=100 e conforme(); setores: valor '1' → 'Carlos Ribeiro'; '7' (Compras) → null; null → undefined
// esquemaDoColaborador.test.ts
it('valida cada regra da nota com a mensagem certa')   // cpf '111.111.111-11' → 'CPF inválido.'; nome '' → 'Informe o nome.'; dataNascimento futura; telefone curto; cargoId '' → 'Escolha o cargo.'
it('montarCorpoDeCadastro envia so as chaves do contrato e converte os ids')   // cargoId 2 (número), cpf só dígitos, beneficioIds [1,2], incluirNoOnboarding false, formularioAdmissaoId null; sem emailPessoal, rg, endereco
// PainelCadastrar.test.tsx   (renderizarTela('/rh/colaboradores/novo'))
it('mostra os blocos e os rotulos da tela 02 e nao tem matricula nem unidade nem gestorId')   // 'Adicionar colaborador' (dialog), Dados pessoais, Contrato, Benefícios, Depois de salvar; queryByLabelText('Matrícula') null; 'Unidade' null
it('Importar planilha aparece desligado e Cadastrar um colaborador esta ativo')   // tab 'Importar planilha' disabled; 'Cadastrar um colaborador' selected
it('as caixas de formulario de admissao e de onboarding ficam desligadas e Enviar convite vem marcada')   // 'Enviar o formulário de admissão' e 'Incluir no treinamento de onboarding': disabled, desmarcadas, 'Indisponível por enquanto'; 'Enviar convite de acesso ao portal' marcada; **ligar as caixas é tarefa das fichas 5 e 6** (as duas telas): aqui o corpo manda sempre `incluirNoOnboarding: false` e `formularioAdmissaoId: null`
it('os sete beneficios do catalogo aparecem desmarcados')   // 'Vale-refeição' ... 'Vale-cultura' (7), nenhum marcado (suponho: ver D-benef)
it('o gestor aparece conforme o setor escolhido')   // 'O gestor do setor escolhido' (placeholder); Comercial → 'Carlos Ribeiro'; Compras → 'Este setor não tem gestor'
it('valida no cliente, marca cada campo e nao chama a API')   // CPF inválido, nome vazio: mensagens; capturar('criarFuncionario').quantas() === 0
it('envia o corpo do contrato e a requisicao e conforme')   // preenche tudo (preencherData, escolherOpcao); corpo { nome:'Rafael Torres', cpf:'84639271069', dataNascimento:'1995-02-09', telefone:'11991234567', emailCorporativo:'rafael.torres@empresa.com.br', dataAdmissao:'2026-10-13', tipoContrato:'CLT', cargoId:2, setorId:1, perfil:'Funcionario', beneficioIds:[1,2], enviarConvite:true, incluirNoOnboarding:false, formularioAdmissaoId:null }; conforme()
it('sucesso fecha o painel, atualiza a lista e mostra a faixa com Abrir ficha')   // URL '/rh/colaboradores'; listarFuncionarios chamado de novo; faixa 'Rafael Torres foi cadastrado. O convite foi enviado para rafael.torres@empresa.com.br.' com link 'Abrir ficha' → /rh/colaboradores/104; sem convite marcado a segunda frase some
it('409 de CPF de cadastro inativo mostra o detail junto do CPF e oferece Abrir a ficha de Henrique Alves')   // `capturar('criarFuncionario', problema(409, exemploDoContrato('criarFuncionario', 409, 'cpfInativo')))`: o exemplo **nomeado** do contrato, lido pelo ajudante da fatia 0 (`03-contrato-e-mocks.md`, seção 5); link '/rh/colaboradores/201'; o detail aparece uma vez (não duplicado no aviso geral)
it('409 de e-mail repetido marca o e-mail e 422 do gestor inativo marca o Setor, sem regraDoBanco')   // 'O gestor informado e um funcionario inativo.' não aparece
it('400 com errors por campo e falha de rede aparecem no lugar certo') ; it('clique duplo em Adicionar colaborador envia uma unica requisicao')
it('opcoes carregando desabilitam o salvar e erro nas opcoes mostra Tentar de novo')   // 'Carregando…' nos selects
it('Cancelar e o X fecham o painel sem gravar e mantem os filtros da URL')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/api src/shared/form src/features/colaboradores`
  Expected: FAIL.
- [ ] **Step 3: Implementar.** (a) `erroDeApi.ts`: campo `corpo` (declare e atribua; `erasableSyntaxOnly`), preenchido em `deResposta`. (b) `cadastroExistente.ts` e `erroNoCampo.ts`. (c) `useOpcoesDeApoio`. (d) `esquemaDoColaborador` e `CamposDoColaborador`. (e) `PainelCadastrar`: `Janela posicao="direita" largura="media"` (600 px), título `Adicionar colaborador`, o texto de abertura da nota (Tela 02, "Li no print": "Os campos marcados entram no cadastro..."), `Abas` de duas opções (a segunda `desabilitada`) mais a frase `Importar planilha: indisponível por enquanto.`, `FormularioComErroDoServidor id="form-cadastrar"` com os quatro blocos (as caixas `Enviar o formulário de admissão` e `Incluir no treinamento de onboarding` ficam **desabilitadas**, com `Indisponível por enquanto`: **ligá-las é tarefa das fichas 5 e 6**, as duas telas); **rodapé da `Janela`**: `Cancelar` e `Botao type="submit" form="form-cadastrar" carregando={cadastrar.isPending}` `Adicionar colaborador` (a mutação `$api.useMutation('post', '/api/rh/funcionarios')` é da página: `aoEnviar` chama `mutateAsync({ body: montarCorpoDeCadastro(v) })` dentro de `try`; no `catch`, guarda `lerCadastroExistente(e)` em estado e **relança `erroNoCampo(e, { CPF_JA_CADASTRADO: 'cpf', EMAIL_JA_CADASTRADO: 'emailCorporativo', GESTOR_DO_SETOR_INATIVO: 'setorId' })`). Sucesso: `invalidarRota(queryClient, '/api/rh/funcionarios')` e `fechar({ cadastrado: { funcionarioId, nome, emailDoConvite } })` (`emailDoConvite` é `null` sem convite); a lista lê `useLocation().state?.cadastrado` e mostra um `Aviso tom="sucesso"` com `acao={<BotaoLink ...>Abrir ficha</BotaoLink>}` (some quando a URL muda sem `state`). `tipoContrato` começa em `CLT` e `perfil` em `Funcionario` (suponho). Linha da rota: `filhas: [{ caminho: 'novo', elemento: <PainelCadastrar /> }]`.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/api src/shared/form src/features/colaboradores`
  Expected: PASS (inclusive os testes da fatia 0 de `clienteApi`).
- [ ] **Step 5: Controle.** (a) Tire o `erroNoCampo` do `catch`: o teste do 409 deve FALHAR (a mensagem iria para o aviso geral). (b) Mande `matricula: ''` no corpo: o `conforme()` deve FALHAR.
- [ ] **Step 6: Registrar a dúvida `D-benef`** em `DUVIDAS.md`, **com esse código** (texto em "Dúvidas conhecidas desta fatia"; não bloqueia; `Mostrada à pessoa: na hora`).
- [ ] **Step 7: PROVAS.**
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md`: `F1b·T2`; tela 02 `feita`, `usa provisório: FuncionarioCriarComando, FuncionarioTipoContrato, FormularioTipo (texto de abertura)`, "conferir ao lado do PNG 02: blocos, caixas, painel de 600 px". ENVIAR `front-end docs\plano-front-end\DUVIDAS.md` com `feat(front): cadastrar colaborador (painel, erros 400, 409 e 422, aviso na lista)`.

---

### Task 3: Ficha do colaborador, aba Dados (tela 03): leitura, editar, inativar, cancelar desligamento e readmitir

Nota: "Tela 03" e os fluxos "Editar dados", "Inativar" e "Readmitir" em `telas\fatia-1.md`; PNG `03-rh-colaboradores-ficha-aba-dados.png`; contrato (em `20-fatia-1-colaboradores.yaml`): `obterFichaDoFuncionario`, `editarFuncionario`, `obterIndicadoresDoFuncionario`, `inativarFuncionario`, `cancelarDesligamentoAgendado`, `readmitirFuncionario` e `enviarConviteDeAcesso`. Quatro partes, cada uma com seu ciclo de teste e seu commit.

**Files:**
- Modify: `src\shared\formato\datas.ts` (acrescenta `formatarDiaEMes`), `documentos.ts` (acrescenta `mascararCep`, `formatarCep`) e seus testes; `src\shared\ui\MenuDeAcoes.tsx` e `.test.tsx` (acrescenta `dica`); `src\app\tabelaDeRotas.tsx` (filha `:funcionarioId`).
- Create: `src\features\colaboradores\PainelDaFicha.tsx`, `MolduraDaFicha.tsx`, `abasDaFicha.tsx`, `AbaDados.tsx`, `QuadroDeIndicadores.tsx`, `indicadores.ts`, `endereco.ts`, `FormularioDeEdicao.tsx`, `DialogoInativar.tsx`, `DialogoReadmitir.tsx`.
- Test: um `*.test.ts(x)` ao lado de cada.

**Interfaces:**
- Consumes: Tasks 1 e 2 (`useOpcoesDeApoio`, `CamposDoColaborador`, `esquemaDoColaborador`, `montarCorpoDeEdicao`, `erroNoCampo`, `lerCadastroExistente`, `EtiquetaDaSituacao`, `usePainelSobreLista`, `capturar`); 1a: `Janela`, `JanelaDeConfirmacao` (`simples` e `digitar`), `Abas`, `Aviso`, `MenuDeAcoes`, `useNotificacao`, `useAuth`, `invalidarRota`, `formatarCpf`, `formatarTelefone`, `formatarData`, `formatarMinutosComoHoras`, `plural`, `Avatar`, `Cartao`, `Secao`.
- Produces:
  - `datas.ts`: `formatarDiaEMes(data: string | null | undefined): string` (`'1990-06-18'` → `'18 de junho'`; `'2026-11-06'` → `'6 de novembro'`; vazio `—`; **sem `Date`**). `documentos.ts`: `mascararCep(valor: string): string` (`'01310100'` → `'01310-100'`, corta em 8 dígitos); `formatarCep(cep: string | null | undefined): string` (vazio `—`).
  - `MenuDeAcoes`: `AcaoDoMenu` ganha `dica?: string` (vira `title` e descrição acessível do item; item desabilitado continua desabilitado).
  - `abasDaFicha.tsx`: `type PropsDaAba = { ficha: Esquema<'FuncionarioFichaRh'>; editando: boolean; aoEncerrarEdicao: () => void }`; `type AbaDaFicha = { valor: string; rotulo: string; Componente: (props: PropsDaAba) => ReactElement }`; `const ABAS_DA_FICHA_RH: readonly AbaDaFicha[]` (nesta tarefa só `dados`; a Task 4 acrescenta `documentos`; **as fatias 3, 4 e 7 acrescentam `ponto`, `holerites`, `advertencias`, `ferias` aqui**; aba que não usa `editando` o ignora). Nunca mostre aba cujo `Componente` não existe.
  - `MolduraDaFicha.tsx`: `function MolduraDaFicha(props: { trilha: { rotulo: string; aoClicar: () => void }; nome: string | null; linhaDeApoio: string; etiqueta: ReactNode; acoes?: ReactNode; faixas?: ReactNode; abas: readonly { valor: string; rotulo: string }[]; aba: string; aoMudarAba: (valor: string) => void; aoFechar: () => void; children: ReactNode })` (a `Janela posicao="direita" largura="larga"` (864 px) com título `Ficha do colaborador`, a trilha `{rotulo} / {nome}`, o `Avatar tamanho="ficha"`, o nome (`text-ficha`), a linha de apoio, a etiqueta, as ações, as faixas e as `Abas variante="sublinhada"`; **a ficha do gestor (Task 9) usa a mesma moldura**).
  - `indicadores.ts`: `type ItemDeIndicador = { rotulo: string; valor: string; tom: 'neutro' | 'alerta' | 'erro' }`; `itemDeBancoDeHoras(minutos: number | null): ItemDeIndicador` (`380` → `+6h20`; `-225` → `−3h45` (U+2212), `tom: 'alerta'` se negativo; `null` → `—`); `itemDeFerias(dias: number | null)` (`18 dias`, `1 dia`); `itemDeAdvertencias(qtd: number | null)` (`erro` se maior que zero); `itemDeProximoPagamento(data: string | null)` (`6 de novembro`); `itemDeTreinamentosPendentes(qtd: number | null)` (`alerta` se maior que zero).
  - `QuadroDeIndicadores.tsx`: `function QuadroDeIndicadores(props: { rotulo: string; itens: readonly ItemDeIndicador[]; carregando?: boolean; erro?: unknown; aoTentarDeNovo?: () => void })` (quatro números em cartões; `data-tom`; `erro` mostra `—` e `Tentar de novo` **só dentro do quadro**).
  - `endereco.ts`: `formatarEndereco(e: Esquema<'FuncionarioEndereco'>): string` (`{logradouro}, {numero}, {complemento}, {bairro}, {cidade}/{uf}, CEP {cep com hífen}` omitindo o que é `null`; tudo `null` → `—`).
  - `PainelDaFicha.tsx`: `function PainelDaFicha()` (rota `/rh/colaboradores/:funcionarioId`, `?aba=dados|documentos`; id que não é inteiro `>= 1` vale 404).

#### Parte A: leitura (cabeçalho, dados, benefícios, indicadores, abas e o menu "...")

- [ ] **Step A1: Escrever os testes que falham.**

```tsx
// datas.test.ts / documentos.test.ts (acrescentam)
it('formatarDiaEMes nao usa Date: dia 18 de junho, 6 de novembro, vazio e traco')   // '1990-06-18', '2026-11-06', '2026-01-01' → '1 de janeiro', null → '—'
it('mascararCep e formatarCep')   // '01310100' → '01310-100'; '0131' → '0131'; excesso cortado em 8
// MenuDeAcoes.test.tsx (acrescenta)
it('item desabilitado com dica mostra a dica como descricao acessivel')
// indicadores.test.ts, endereco.test.ts: tabelas dos exemplos acima; endereço de Bruno → 'Avenida Paulista, 1578, apto 42, Bela Vista, São Paulo/SP, CEP 01310-100'; só cidade → 'São Paulo'; tudo null → '—'
// PainelDaFicha.test.tsx   (renderizarTela('/rh/colaboradores/12'); exemplos de obterFichaDoFuncionario e obterIndicadoresDoFuncionario)
it('abre direto no endereco com a lista atras e as requisicoes sao conformes')   // dialog 'Ficha do colaborador' e a tabela da lista visíveis; capturar('obterFichaDoFuncionario') e capturar('obterIndicadoresDoFuncionario'): parametrosDoCaminho { funcionarioId: '12' }; conforme() nos dois
it('cabecalho: nome, "Analista pleno, setor Financeiro, matrícula 00482", etiqueta e os botoes')   // etiqueta 'Atestado' (o exemplo traz ATESTADO); botões 'Editar dados' e 'Inativar'; trilha 'Colaboradores' é link
it('cargo, setor e matricula null omitem as partes do cabecalho')
it('Dados pessoais completos para o RH: CPF e telefone sem mascara')   // '123.482.371-30', '(11) 98765-4410', 'Aniversário' '18 de junho', 'E-mail pessoal', 'RG' '45.678.912-3, SSP-SP', endereço montado; nunca '***'
it('Contrato: cargo, setor, gestora, tipo, jornada, admissao e nivel de acesso, sem Unidade')   // 'Marina Costa', 'CLT', '44 h semanais', '14/03/2022', 'Colaborador'; queryByText('Unidade') e 'Matriz' null; gestor null → 'Sem gestor'
it('a data de admissao nao muda de dia')   // dataAdmissao '2026-10-01' → '01/10/2026' (Review Focus: dia de calendario)
it('beneficios como etiquetas, com dependentes, e sem beneficios')   // 'Plano de saúde + 1 dependente'; [] → 'Nenhum benefício ativo.'
it('os quatro numeros vem de outra rota e falham sozinhos')   // '+6h20', '18 dias', '0', '6 de novembro'; 500 em indicadores → '—' e 'Tentar de novo' só no quadro, o resto da ficha segue
it('a ficha sem dados pessoais (administrador do seed) mostra traco em tudo e nao quebra')   // cpf, telefone, dataNascimento, rg, endereço, cargo, setor, tipoContrato, perfil null (Review Focus 1)
it('carregando mostra o esqueleto do cabecalho e dos blocos e o erro da ficha mostra a mensagem com Tentar de novo')
it('so existe a aba Dados nesta parte (a Task 4 acrescenta Documentos) e ?aba desconhecida vale Dados')
it('desligamento agendado mostra a faixa com o ultimo dia e Cancelar desligamento')   // 'Desligamento agendado: o último dia de trabalho é 13/11/2026. A pessoa continua com acesso até o dia seguinte.'
it('Inativar so para quem nao esta inativo; Readmitir so para inativo, no lugar; Editar dados nos dois')
it('menu "...": Reenviar convite so sem SENHA_DEFINIDA e nao inativo; SEM_CONVITE diz Enviar convite de acesso; sem e-mail fica desabilitado com a dica; sem itens o botao some')
it('404 fecha o painel com aviso; id invalido tambem; X e trilha voltam a lista com os filtros')   // 'O registro não foi encontrado.'; '/rh/colaboradores/abc'
it('Gestor em /rh/colaboradores/12 ve o 403 da guarda')
```

- [ ] **Step A2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared src/features/colaboradores`
  Expected: FAIL nos novos.
- [ ] **Step A3: Implementar** os formatadores, `dica` no `MenuDeAcoes`, `indicadores`, `endereco`, `QuadroDeIndicadores`, `abasDaFicha`, `MolduraDaFicha`, `AbaDados`, `PainelDaFicha` e a linha da rota. `PainelDaFicha`: `$api.useQuery('get', '/api/rh/funcionarios/{funcionarioId}', { params: { path: { funcionarioId } } })` e outra para `.../indicadores` (carrega **à parte**); aba na URL (`useSearchParams`, valor desconhecido vale `dados`); em 404 (`useEffect`): `useNotificacao().erro(mensagemDeErro(erro))` e `fechar()`; cores dos números por `data-tom`. Botões do cabeçalho por `situacao` (nota da Tela 03, item "Botões do cabeçalho conforme `situacao`"); "..." com `MenuDeAcoes` e `enviarConviteDeAcesso` (200: toast `Convite enviado para {emailCorporativo}.`; 422 `FUNCIONARIO_INATIVO`, `FUNCIONARIO_SEM_EMAIL_CORPORATIVO`, `SENHA_JA_DEFINIDA`: toast de erro com o `detail`; depois `invalidarRota(qc, '/api/rh/funcionarios')`). Em `AbaDados`, o cartão "Dados pessoais" em três colunas, "Contrato", "Benefícios" e o `QuadroDeIndicadores` abaixo, como a nota da Tela 03 ("Li no print" e "O que fica").
- [ ] **Step A4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared src/features/colaboradores`
  Expected: PASS.
- [ ] **Step A5: PROVAS** e **ENVIAR** `feat(front): ficha do colaborador (aba Dados), indicadores e menu de acoes com dica`.

#### Parte B: Editar dados

- [ ] **Step B1: Escrever os testes que falham** (`PainelDaFicha.test.tsx`, `FormularioDeEdicao.test.tsx`).

```tsx
it('Editar dados troca o corpo da aba pelo formulario preenchido com a ficha e Cancelar volta sem gravar')   // 'Salvar alterações' e 'Cancelar'; sem requisição de PUT
it('o formulario tem os campos de Dados pessoais e Contrato mais e-mail pessoal, RG, orgao emissor, endereco e beneficios, e nao tem matricula nem Depois de salvar')   // beneficios ativos marcados: 1, 2, 4, 5
it('avisa ao trocar o e-mail corporativo e ao trocar o setor')   // 'Trocar o e-mail corporativo troca o e-mail de login: a pessoa precisa usar o novo.'; 'O gestor da pessoa passa a ser o do novo setor.'
it('Nivel de acesso fica desabilitado na propria pessoa logada, com a dica')   // ficha.id = 1 (Ana, funcionarioId do usuário de teste)
it('envia a substituicao completa e a requisicao e conforme')   // capturar('editarFuncionario'): todas as chaves de FuncionarioEditarComando, endereco com as 7 chaves (null quando vazio), cpf só dígitos; conforme()
it('sucesso volta a leitura, atualiza a lista e mostra Dados salvos.')   // toast; listarFuncionarios e a ficha refeitos
it('409 de CPF ou e-mail marca o campo e oferece Abrir a ficha de {nome}; 422 GESTOR_PRECISA_TER_PERFIL_GESTOR e NAO_PODE_ALTERAR_O_PROPRIO_PERFIL marcam Nivel de acesso; 422 GESTOR_DO_SETOR_INATIVO marca Setor; 400 por campo')
it('a ficha sem dados pessoais abre o formulario com os campos obrigatorios vazios e exige o preenchimento')   // Review Focus 1: CPF 'Campo obrigatório.'... sem chamar a API
it('clique duplo em Salvar alteracoes envia uma unica requisicao')
```

- [ ] **Step B2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/colaboradores`
  Expected: FAIL.
- [ ] **Step B3: Implementar** `FormularioDeEdicao` (`FormularioComErroDoServidor id="form-editar"` com `CamposDoColaborador modo="edicao"` mais e-mail pessoal, RG, órgão emissor, endereço (`mascararCep`, UF em maiúsculas), caixas de benefícios; botões **dentro do corpo da aba**, não na `Janela`: `Cancelar` e `Salvar alterações`; mutação `$api.useMutation('put', '/api/rh/funcionarios/{funcionarioId}')`; `montarCorpoDeEdicao`; mesmo `erroNoCampo` da Task 2 mais `GESTOR_PRECISA_TER_PERFIL_GESTOR` e `NAO_PODE_ALTERAR_O_PROPRIO_PERFIL` → `perfil`). "Editar dados" estando em outra aba leva à aba `dados` e liga `editando` (suponho). Os avisos de e-mail e de setor usam `formulario.watch`.
- [ ] **Step B4: Rodar e ver passar.** Run: `npm --prefix front-end test src/features/colaboradores`
  Expected: PASS.
- [ ] **Step B5: PROVAS** e **ENVIAR** `feat(front): editar dados do colaborador (substituicao completa e erros do contrato)`.

#### Parte C: Inativar, Cancelar desligamento e a trava de digitar "desativar"

- [ ] **Step C1: Escrever os testes que falham** (`DialogoInativar.test.tsx` e `PainelDaFicha.test.tsx`).

```tsx
it('Inativar abre o dialogo com o ultimo dia (hoje), a explicacao e a trava de digitar desativar')   // fake Date 2026-10-07: campo 'Último dia de trabalho' = '2026-10-07'; 'Digite "desativar" para confirmar'; botão 'Inativar' desabilitado
it('o botao so habilita com "desativar" (sem diferenca de maiuscula nem de espaco nas pontas)')   // 'Desativar ' habilita; 'desativa' não
it('o corpo e so { ultimoDiaDeTrabalho } (a palavra da trava nao vai para a API) e a requisicao e conforme')   // capturar('inativarFuncionario')
it('Gestor acrescenta a frase dos pedidos que passam para o RH')   // ficha com perfil 'Gestor'
it('data antes da admissao nao chama a API; 400 em ultimoDiaDeTrabalho marca o campo; 409 DESLIGAMENTO_JA_REGISTRADO mostra o detail e refaz a ficha')
it('sucesso fecha o dialogo, mostra "Desligamento de Bruno Lima registrado." e a ficha mostra a faixa de agendamento')   // resposta AGENDADO; lista refeita
it('Cancelar desligamento pede confirmacao simples e 204 some a faixa; 422 SEM_DESLIGAMENTO_AGENDADO recarrega a ficha')   // 'Cancelar o desligamento agendado de Bruno Lima?', 'Manter agendado' / 'Cancelar desligamento'; capturar('cancelarDesligamentoAgendado') conforme()
it('Escape fecha so o dialogo de cima e o painel continua aberto')   // diálogo aninhado (suponho)
```

- [ ] **Step C2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/colaboradores`
  Expected: FAIL.
- [ ] **Step C3: Implementar** `DialogoInativar` (`JanelaDeConfirmacao variante="digitar" textoParaDigitar="desativar" rotuloDeConfirmar="Inativar"`, com o campo de data dentro de `descricao`; o `erro` da `JanelaDeConfirmacao` mais `erro.errors.ultimoDiaDeTrabalho` junto da data; **renderize-o como filho de `<Janela>` do painel**) e o diálogo `simples` de cancelar. Em 404: fechar o painel com aviso; em 409 e 422 de estado: `invalidarRota(qc, '/api/rh/funcionarios')`.
- [ ] **Step C4: Rodar e ver passar.** Run: `npm --prefix front-end test src/features/colaboradores`
  Expected: PASS.
- [ ] **Step C5: Controle.** Mande `desativar: true` no corpo do `inativarFuncionario`: o `conforme()` deve FALHAR. Restaure.
- [ ] **Step C6: PROVAS** e **ENVIAR** `feat(front): inativar colaborador com a trava de digitar e cancelar desligamento agendado`.

#### Parte D: Readmitir

- [ ] **Step D1: Escrever os testes que falham** (`DialogoReadmitir.test.tsx`; ficha de teste: `situacao: 'INATIVO'`, `ativo: false`, `desligamento: { ultimoDiaDeTrabalho: '2026-09-30', situacao: 'EFETIVADO' }`, `acesso: { situacao: 'SENHA_DEFINIDA', ativo: false }`).

```tsx
it('Readmitir aparece so para inativo, no lugar de Inativar, e abre o dialogo com a explicacao')   // 'A pessoa volta com o mesmo cadastro, o mesmo CPF e a mesma matrícula...'
it('os campos sao data de admissao (minimo = ultimo dia do vinculo anterior), tipo, cargo e setor, ja preenchidos; sem nivel de acesso, beneficios, CPF nem e-mail')
it('as caixas de onboarding e de formulario ficam desligadas e a de convite so aparece quando a senha nao foi definida (padrao desmarcada)')   // ligar as caixas é tarefa das fichas 5 e 6
it('envia o corpo e a requisicao e conforme')   // capturar('readmitirFuncionario'): { dataAdmissao, cargoId, setorId, tipoContrato, incluirNoOnboarding:false, formularioAdmissaoId:null, enviarConvite:false }
it('sucesso mostra "Henrique Alves foi readmitido." e, com acesso desativado, o aviso com o link para Usuarios do sistema')   // 'O acesso ao portal continua desativado. Libere em Cadastros, Usuários do sistema (Ativar).' → /rh/cadastros/usuarios
it('400 em dataAdmissao marca o campo; 409 FUNCIONARIO_JA_ATIVO recarrega a ficha; 422 GESTOR_DO_SETOR_INATIVO marca Setor')
```

- [ ] **Step D2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/colaboradores`
  Expected: FAIL.
- [ ] **Step D3: Implementar** `DialogoReadmitir` (`Janela` ao centro, `tamanho="media"`, filha da `Janela` do painel; `FormularioComErroDoServidor id="form-readmitir"`; rodapé `Cancelar` e `Readmitir`; mutação `$api.useMutation('post', '/api/rh/funcionarios/{funcionarioId}/readmissao')`). O aviso do acesso desativado é estado do `PainelDaFicha` (só logo depois de readmitir, como a nota do fluxo "Readmitir", item "Sucesso").
- [ ] **Step D4: Rodar e ver passar.** Run: `npm --prefix front-end test src/features/colaboradores`
  Expected: PASS.
- [ ] **Step D5: Registrar a dúvida `D-editar-inativo`** em `DUVIDAS.md`, **com esse código** (texto adiante).
- [ ] **Step D6: PROVAS.**
- [ ] **Step D7: Registrar e enviar.** `PROGRESSO.md`: `F1b·T3` (A a D); telas "03 Ficha, aba Dados" `feita` e "03 Editar dados", "03 Inativar" e "03 Readmitir" `feita` (as três últimas **sem print**: conferir pela nota); `usa provisório: FuncionarioTipoContrato`; "conferir ao lado do PNG 03: cabeçalho, três cartões, etiquetas de benefícios, quadro de números abaixo, painel de 864 px". ENVIAR `front-end docs\plano-front-end\DUVIDAS.md` com `feat(front): readmitir colaborador e fecho da ficha (tela 03)`.

---

### Task 4: Aba Documentos e modal "Adicionar documento" (telas 09 e 10)

Nota: "Tela 09" e "Tela 10" em `telas\fatia-1.md`; PNG `09-rh-colaboradores-ficha-aba-documentos.png` e `10-rh-colaboradores-ficha-modal-adicionar-documento.png`; contrato (em `20-fatia-1-colaboradores.yaml`): `listarDocumentosDoFuncionario`, `adicionarDocumentoDoFuncionario`, `baixarDocumentoDoFuncionario` e os schemas `DocumentoColaborador*`.

**Files:**
- Modify: `src\shared\formato\datas.ts` (acrescenta `formatarDataDoInstante`) e teste; `src\features\colaboradores\abasDaFicha.tsx` (acrescenta `documentos`); `src\test\contrato.ts` e `contrato.test.ts` (só se o multipart faltar).
- Create: `src\shared\arquivos\useBaixarPorLink.ts`; `src\features\documentos\rotulos.ts`, `enviarDocumento.ts`, `ModalAdicionarDocumento.tsx`; `src\features\colaboradores\AbaDocumentos.tsx`.
- Test: `useBaixarPorLink.test.tsx`, `rotulos.test.ts`, `enviarDocumento.node.test.ts` (primeira linha `// @vitest-environment node`), `ModalAdicionarDocumento.test.tsx`, `AbaDocumentos.test.tsx`.

**Interfaces:**
- Consumes: 1a: `abrirLinkTemporario`, `multipart`, `SeletorDeArquivo`, `validarArquivo`, `clienteHttp`, `OpcoesEmCartao`, `Selecao`, `AreaDeTexto`, `Janela`, `Tabela`, `Selo`, `EstadoVazio`, `useNotificacao`, `mensagemDeErro`, `invalidarRota`; Task 3 (`ABAS_DA_FICHA_RH`, `PropsDaAba`).
- Produces:
  - `datas.ts`: `formatarDataDoInstante(instanteUtc: string | null | undefined): string` (`'2022-03-14T13:20:00Z'` → `'14/03/2022'`; `'2026-10-08T02:30:00Z'` → `'07/10/2026'`; vazio `—`).
  - `useBaixarPorLink.ts`: `function useBaixarPorLink(): { baixar(chamada: () => Promise<{ data?: Esquema<'LinkTemporario'> }>): Promise<void>; baixando: boolean }` (chama `abrirLinkTemporario(data)`; falha vira `useNotificacao().erro(mensagemDeErro(e))`; **ficha 3, 4, 5 e 6 reusam**).
  - `rotulos.ts` (documentos): `rotuloDoTipoDeDocumento(tipo: Esquema<'DocumentoColaboradorTipo'> | string): string` (nota da Tela 09, item "Rótulos do tipo"; valor desconhecido como veio); `OPCOES_DE_TIPO_DE_DOCUMENTO: readonly OpcaoDeSelecao<Esquema<'DocumentoColaboradorTipo'>>[]`; `REGRAS_DE_ARQUIVO_DE_DOCUMENTO = { tiposAceitos: ['application/pdf', 'image/jpeg', 'image/png'], tamanhoMaximoMb: 10 } as const`; `etiquetaDoDocumento(situacao: Esquema<'DocumentoColaboradorSituacao'>): { texto: string; tom: TomDoSelo }` (`ENVIADO` → `Enviado`/`sucesso`, `PENDENTE` → `Pendente`/`alerta`).
  - `enviarDocumento.ts`: `type CamposDoNovoDocumento = { modo: Esquema<'DocumentoColaboradorModo'>; tipo: Esquema<'DocumentoColaboradorTipo'>; descricao?: string; dataValidade?: string; arquivo?: File }`; `adicionarDocumento(funcionarioId: number, campos: CamposDoNovoDocumento, cliente: ClienteHttp = clienteHttp): Promise<Esquema<'DocumentoColaborador'>>` (`POST` multipart com `multipart<Esquema<'DocumentoColaboradorNovoForm'>>`; sem `arquivo` em `PEDIR_PARA_ENVIAR`); `enviarMeuArquivo(documentoId: number, arquivo: File, cliente: ClienteHttp = clienteHttp): Promise<Esquema<'DocumentoColaborador'>>` (usada na Task 8).
  - `AbaDocumentos.tsx`: `function AbaDocumentos(props: PropsDaAba)`; `ModalAdicionarDocumento.tsx`: `function ModalAdicionarDocumento(props: { funcionarioId: number; primeiroNome: string; aberta: boolean; aoFechar: () => void })`.

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// datas.test.ts (acrescenta)
it('formatarDataDoInstante converte para Sao Paulo e nao troca o dia (Review Focus)')   // '2026-10-08T02:30:00Z' → '07/10/2026'
// useBaixarPorLink.test.tsx
it('abre o link temporario como anexo e baixando fica true enquanto espera')   // espiona HTMLAnchorElement.prototype.click
it('falha da API vira notificacao de erro e nao abre nada')   // 422 DOCUMENTO_SEM_ARQUIVO → 'Este documento ainda não foi enviado.'
// rotulos.test.ts: os sete rotulos da nota da Tela 09 (item "Rótulos do tipo"), valor desconhecido como veio, etiquetas
// enviarDocumento.node.test.ts   (// @vitest-environment node; criarClienteHttp com fetch real e MSW)
it('adicionarDocumento envia multipart com modo, tipo, descricao, validade e arquivo, e a requisicao e conforme')   // capturar('adicionarDocumentoDoFuncionario'): content-type multipart/form-data; corpo { modo:'ANEXAR_AGORA', tipo:'COMPROVANTE_DE_RESIDENCIA', descricao, dataValidade:'2027-10-01', arquivo:'comprovante.pdf' }; conforme(); devolve o documento 70 do exemplo
it('PEDIR_PARA_ENVIAR nao manda arquivo') ; it('o multipart tem as mesmas chaves do contrato e uma chave a mais seria acusada')   // esperarRequisicaoConforme com multipart; se o conforme ainda não lê multipart/form-data, este teste falha → Step 3 estende src\test\contrato.ts
it('enviarMeuArquivo envia so o campo arquivo e a requisicao e conforme')   // capturar('enviarMeuDocumento'): parametrosDoCaminho { documentoId: '64' }; corpo { arquivo: 'comprovante.pdf' }; conforme()
// AbaDocumentos.test.tsx   (renderizarTela('/rh/colaboradores/12?aba=documentos'); exemplo de listarDocumentosDoFuncionario)
it('mostra o texto de abertura, o botao Adicionar documento e as colunas Documento, Enviado em, Situacao e a acao')   // 'Documentos guardados na ficha. Só o RH e o próprio colaborador conseguem ver.'; capturar('listarDocumentosDoFuncionario'): parametrosDoCaminho { funcionarioId: '12' }; conforme()
it('linha enviada: tipo, "Enviado" e botao Ver; linha pendente: "Pendente", "Atualização pedida em 01/10", validade e SEM botao Lembrar')   // 'Documento de identidade (RG ou CNH)', '14/03/2022'; 'Comprovante de residência', 'Comprovante atualizado', 'Válido até 01/10/2027'; queryByRole('button', { name: 'Lembrar' }) null
it('Ver chama baixarDocumentoDoFuncionario e abre o link temporario; 422 mostra o detail')   // capturar('baixarDocumentoDoFuncionario'): parametrosDoCaminho { funcionarioId:'12', documentoId:'61' }
it('carregando mostra o esqueleto; vazio; erro com Tentar de novo; 404 fecha o painel; sem paginacao e na ordem da API')   // 'Nenhum documento na ficha ainda.'
it('o texto da descricao e mostrado como texto, nunca como HTML')   // descricao '<img src=x onerror="window.__x=1">' aparece escrita; window.__x undefined (Review Focus 4)
// ModalAdicionarDocumento.test.tsx   (adicionarDocumento com vi.mock)
it('mostra os dois cartoes: Anexar agora e Pedir para a pessoa enviar (Ela envia pelo portal) e a nota com o primeiro nome')   // titulo 'Adicionar documento de Bruno'; 'Só o RH e Bruno conseguem ver os documentos da ficha.'; queryByText(/Ela recebe um aviso/) null
it('ANEXAR_AGORA e o padrao e mostra a area de envio; PEDIR_PARA_ENVIAR a esconde')   // 'Arraste o arquivo aqui', 'PDF, JPG ou PNG, até 10 MB'
it('tipo, validade e descricao; com tipo Outro a dica diz Diga qual e o documento')
it('arquivo invalido nao e aceito e mostra a mensagem do seletor; anexar sem arquivo mostra o erro do campo')
it('salvar chama adicionarDocumento com os campos certos, fecha o modal e refaz a lista')   // vi.mocked(adicionarDocumento) toHaveBeenCalledWith(12, { modo, tipo, descricao, dataValidade, arquivo }); lista refeita
it('400 em arquivo aparece junto da area de envio; 404 fecha e avisa; enviando mostra "Enviando…" e desabilita o botao; clique duplo envia uma vez')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared src/features/documentos src/features/colaboradores`
  Expected: FAIL.
- [ ] **Step 3: Implementar.** `useBaixarPorLink`; `rotulos`; `enviarDocumento` (uma função por rota, corpo só com `multipart(...)`); **se o teste "mesmas chaves do contrato" falhar por o `esperarRequisicaoConforme` não ler `multipart/form-data`, estenda `src\test\contrato.ts` para ler `requestBody.content['multipart/form-data']` quando não há `application/json`** (acrescente os dois casos ao `contrato.test.ts`: chave a mais e chave obrigatória faltando, como os do corpo JSON); `AbaDocumentos` (`Tabela` sem paginação, "Ver" só em `ENVIADO` via `useBaixarPorLink` e `clienteHttp.GET`, `EstadoVazio`, botão `+ Adicionar documento`); `ModalAdicionarDocumento` (`Janela tamanho="media"` (640 px), `OpcoesEmCartao` `Como o documento vai chegar`, `Selecao` `Escolha o tipo`, data `Validade (opcional)`, `AreaDeTexto limite={255}` `Descrição (opcional)`, `SeletorDeArquivo` com `textoDeAjuda="PDF, JPG ou PNG, até 10 MB"`, `<p role="status">Enviando…</p>` enquanto envia, rodapé `Cancelar` e `Salvar documento`; as **mensagens do `SeletorDeArquivo` da 1a** (`Esse tipo de arquivo não é aceito.`...) valem no lugar do texto `Escolha um PDF, JPG ou PNG de até 10 MB.` da nota da Tela 10 (item "Arquivo") (anote em `PROGRESSO.md`); mutação `useMutation` do TanStack com `adicionarDocumento`); acrescente `documentos` a `ABAS_DA_FICHA_RH`.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared src/features/documentos src/features/colaboradores`
  Expected: PASS.
- [ ] **Step 5: Controle.** Em `AbaDocumentos`, mostre o botão "Ver" também em `PENDENTE`: o teste da linha pendente deve FALHAR. Restaure.
- [ ] **Step 6: PROVAS** e **ENVIAR** `feat(front): aba Documentos da ficha e modal de adicionar documento (upload multipart)`.
  Antes: `PROGRESSO.md` `F1b·T4`; telas 09 e 10 `feitas`, `usa provisório: DocumentoColaboradorTipo`; **pendente de integração:** o envio de arquivo de verdade e o link temporário real (`adicionarDocumentoDoFuncionario`, `baixarDocumentoDoFuncionario`); "conferir ao lado do PNG 09 e 10".

---

### Task 5: Cadastros (tela 29) e o modal de cargo (tela 30)

Nota: "Tela 29" (moldura e Cargos; as seções de Setores, Gestores, Benefícios e Usuários do sistema; e a "Tela sem print: Cadastros, Empresa") e "Tela 30" em `telas\fatia-1.md`; PNG `29-rh-cadastros.png` e `30-rh-cadastros-modal-novo-cargo.png`; contrato (em `20-fatia-1-colaboradores.yaml`): as operações de cargos, setores, gestores, benefícios do catálogo, usuários do sistema e empresa (tabela "Operações → tarefa", no fim deste arquivo). Seis partes (A a F), cada uma com ciclo próprio e commit; cada parte acrescenta **uma linha** a `ITENS_DO_CADASTRO` e uma filha a `cadastros` em `tabelaDeRotas.tsx`.

**Files (comuns):**
- Create: `src\features\cadastros\PaginaDeCadastros.tsx`, `itensDoCadastro.ts`, `useContagensDosCadastros.ts`, `ListaDeCadastro.tsx`; `src\features\colaboradores\useBuscaDePessoas.ts` (Parte B); `src\shared\formato\ufs.ts` (Parte E).
- Modify: `src\app\tabelaDeRotas.tsx` (`ROTAS_RH`: `{ caminho: 'cadastros', elemento: <PaginaDeCadastros />, filhas: [...] }`).

**Interfaces (comuns):**
- Consumes: Tasks 1 a 3 (`capturar`, `problema`, `renderizarTela`, `FiltroDeSelecao`, `valorDeEnum`, `erroNoCampo`, `useOpcoesDeApoio`, `rotuloDoNivelDeAcesso`), 1a (`useEstadoDaLista`, `Tabela`, `Paginacao`, `CampoDeBusca`, `Janela`, `JanelaDeConfirmacao`, `Interruptor`, `CaixaDeSelecao`, `ComboDeBusca`, `FormularioComErroDoServidor`, `useNotificacao`, `invalidarRota`, `useAuth`).
- Produces:
  - `itensDoCadastro.ts`: `type ChaveDoCadastro = 'cargos' | 'setores' | 'gestores' | 'beneficios' | 'usuarios' | 'empresa'`; `type ItemDoCadastro = { chave: ChaveDoCadastro; titulo: string; apoio: string; comContagem: boolean }`; `const ITENS_DO_CADASTRO: readonly ItemDoCadastro[]` na ordem Cargos ("Nome e carga horária"), Setores ("Áreas e seus gestores"), Gestores ("Quem gerencia cada setor"), Benefícios ("O que a empresa oferece"), Usuários do sistema ("Logins de acesso ao portal"), Empresa ("Nome, horário e dia de pagamento"; sem número). **A fatia 7 acrescenta `Pagamento` aqui.** Sem "Níveis de acesso" (decisão do dono).
  - `useContagensDosCadastros.ts`: `function useContagensDosCadastros(): Partial<Record<ChaveDoCadastro, number>>` (`total` de `listarCargos`, `listarSetores`, `listarBeneficiosDoCatalogo` e `listarUsuariosDoSistema`, cada um com `tamanhoPagina = 1`, e `itens.length` de `listarGestores`; erro: o número some, sem mensagem; as gravações invalidam estes caminhos).
  - `PaginaDeCadastros.tsx`: `function PaginaDeCadastros()` (`CabecalhoDePagina` `Cadastros` com o subtítulo da nota da Tela 29 ("Li no print": "Tabelas de apoio que o RH mantém..."), a coluna de itens com título, apoio e número, e `<Outlet />`; sem filha na URL (`useOutlet()` nulo), `<Navigate to="cargos" replace />`).
  - `ListaDeCadastro.tsx`: `function ListaDeCadastro<L>(props: { titulo: string; apoio: string; rotuloDaTabela: string; botaoNovo?: { rotulo: string; aoClicar: () => void }; colunas: readonly ColunaDaTabela<L>[]; chaveDaLinha: (linha: L) => string | number; dados: { itens: readonly L[]; pagina?: number; tamanhoPagina?: number; total?: number; totalPaginas?: number } | undefined; carregando: boolean; erro: unknown; aoTentarDeNovo: () => void; busca?: { valor: string; aoMudar: (texto: string) => void }; aoMudarPagina?: (pagina: number) => void; vazio: string; vazioComBusca: string; filtros?: ReactNode })` (cabeçalho e botão **sempre** à vista, mesmo com a lista em erro; busca `Buscar neste cadastro`; `Tabela`; rodapé `Mostrando {itens.length} de {total}` e, só com `totalPaginas` maior que 1, `Paginacao` com `Anterior` e `Próxima`; **se `itens` vem vazio numa `pagina` maior que 1, chama `aoMudarPagina(1)`**).

#### Parte A: moldura e Cargos (telas 29 e 30)

**Produces:** `cargos\CadastroDeCargos.tsx` (`function CadastroDeCargos()`), `cargos\ModalDeCargo.tsx` (`function ModalDeCargo(props: { cargo?: Esquema<'CargoItem'>; aberta: boolean; aoFechar: () => void })`), `cargos\rotulos.ts` (`formatarCargaSemanal(horas: number): string`: `44` → `44 h`, `37.5` → `37,5 h`).

- [ ] **Step A1: Escrever os testes que falham.**

```tsx
// PaginaDeCadastros.test.tsx   (renderizarTela('/rh/cadastros'))
it('/rh/cadastros leva a Cargos e mostra o titulo, o subtitulo e o item ativo')   // URL '/rh/cadastros/cargos'; 'Tabelas de apoio que o RH mantém. Elas viram as opções que aparecem no resto do sistema.'
it('o menu do cadastro tem titulo, apoio e o numero de cada lista e nao tem Niveis de acesso')   // Cargos 14, ... (as outras partes completam); queryByText('Níveis de acesso') null; capturar('listarCargos') com tamanhoPagina=1 e conforme()
it('erro numa contagem some o numero sem mensagem')
// CadastroDeCargos.test.tsx
it('mostra o texto de apoio, o botao Novo cargo, a busca e as colunas Cargo, Descricao, Carga semanal, Pessoas e Editar')   // '44 h'; sem botão Excluir
it('a descricao e mostrada como texto, nunca como HTML (Review Focus 4)')   // descricao '<script>window.__x=1</script>' escrita
it('rodape "Mostrando 3 de 14" sem botoes de pagina quando ha uma pagina so; com totalPaginas 2 aparecem Anterior e Proxima')
it('busca manda busca= e conforme o contrato; carregando mostra o esqueleto; vazio e vazio com busca; erro com Tentar de novo; 403 mostra a permissao')   // 'Nenhum cargo cadastrado.' / 'Nenhum cargo encontrado.'; o cabeçalho e o botão Novo cargo continuam à vista com a lista em erro
// ModalDeCargo.test.tsx
it('Novo cargo: titulo em uma linha, tres campos, a nota e os botoes lado a lado')   // 'Nome do cargo', 'Carga horária semanal' (com 'horas'), 'Descrição'; 'A carga horária vale para todas as pessoas do cargo.'; 'Cancelar' e 'Salvar cargo'
it('Editar cargo vem preenchido e avisa que a nova carga vale para os dias de ponto criados depois')
it('valida o nome (1 a 100), a carga (maior que zero ate 168, 2 casas) e a descricao (255)')
it('envia { nome, descricao, cargaSemanalHoras } (descricao vazia vira null) e e conforme; PUT para editar')   // capturar('criarCargo') e capturar('editarCargo') com parametrosDoCaminho { cargoId: '2' }
it('409 NOME_JA_CADASTRADO marca o Nome; 404 na edicao fecha e recarrega; sucesso fecha e refaz a lista, o menu e as opcoes')
```

- [ ] **Step A2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/cadastros`
  Expected: FAIL.
- [ ] **Step A3: Implementar** os arquivos acima e a linha `cargos` (`ITENS_DO_CADASTRO`, rota). `CadastroDeCargos`: `useEstadoDaLista({ filtros: [] as const })` e `$api.useQuery('get', '/api/rh/cargos', ...)`; `ModalDeCargo` (`Janela tamanho="media"`, `FormularioComErroDoServidor id="form-cargo"`, rodapé lado a lado, `erroNoCampo(e, { NOME_JA_CADASTRADO: 'nome' })`); `Carga horária semanal`: `Entrada type="number" step="any"` (o schema converte o texto e recusa mais de 2 casas). Sem toast de sucesso (a nota não pede).
- [ ] **Step A4: Rodar e ver passar.** Run: `npm --prefix front-end test src/features/cadastros`
  Expected: PASS.
- [ ] **Step A5: PROVAS** e **ENVIAR** `feat(front): Cadastros, moldura e cargos com o modal da tela 30`.

#### Parte B: Setores

**Produces:** `setores\CadastroDeSetores.tsx`, `setores\ModalDeSetor.tsx` (`function ModalDeSetor(props: { setor?: Esquema<'SetorItem'>; aberta: boolean; aoFechar: () => void })`), `useBuscaDePessoas.ts`: `function useBuscaDePessoas(gestorAtual?: { funcionarioId: number; nome: string }): { opcoes: readonly OpcaoDoCombo[]; aoDigitar(texto: string): void; carregando: boolean }` (espera 300 ms; `listarFuncionarios` com `situacao=ATIVO`, `busca` e `tamanhoPagina=20`; `opcoes` = `Sem gestor` (valor `SEM_GESTOR`) + o gestor atual + os resultados; `apoio` = cargo).

- [ ] **Step B1: Escrever os testes que falham.**

```tsx
it('useBuscaDePessoas espera 300 ms, manda uma requisicao com situacao=ATIVO e junta Sem gestor e o gestor atual')
it('tabela: Setor, Descricao, Gestor (Sem gestor quando null), Pessoas e Editar; sem Excluir; todos os setores numa pagina; busca por nome e descricao')
it('Novo setor: Nome, Descricao e Gestor com busca e as duas frases de apoio')   // 'A pessoa escolhida passa a ter o nível Gestor. Quem deixa de ser gestor deste setor volta a Colaborador e continua no setor.'
it('ao trocar ou tirar o gestor de um setor que ja tinha, avisa que os pedidos passam para o RH')   // 'Os pedidos de ponto e de férias que esperavam pelo gestor atual passam para o RH.'
it('envia { nome, descricao, gestorFuncionarioId } com o id do colaborador (nao o gestorId), null para Sem gestor, e e conforme')   // capturar('criarSetor') / capturar('editarSetor')
it('409 NOME_JA_CADASTRADO marca o Nome; 409 GESTOR_JA_RESPONDE_POR_OUTRO_SETOR, 422 GESTOR_PRECISA_TER_PERFIL_GESTOR e 422 FUNCIONARIO_INATIVO marcam o Gestor com o detail')
it('sucesso refaz setores, gestores, colaboradores e o numero do menu')   // invalidarRota × 3
it('nao tenta filtrar quem e do RH na busca de gestor')   // nenhuma chamada com perfil
it('carregando, vazio "Nenhum setor cadastrado.", erro com Tentar de novo e 403')
```

- [ ] **Step B2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/cadastros src/features/colaboradores/useBuscaDePessoas`
  Expected: FAIL.
- [ ] **Step B3: Implementar** (`ComboDeBusca` com `aoDigitar`; erros de gestor via `erroNoCampo(e, { GESTOR_JA_RESPONDE_POR_OUTRO_SETOR: 'gestorFuncionarioId', GESTOR_PRECISA_TER_PERFIL_GESTOR: 'gestorFuncionarioId', FUNCIONARIO_INATIVO: 'gestorFuncionarioId', NOME_JA_CADASTRADO: 'nome' })`; linha `setores`).
- [ ] **Step B4: Rodar e ver passar** (mesmo comando).
- [ ] **Step B5: PROVAS** e **ENVIAR** `feat(front): Cadastros, setores com busca de gestor`.

#### Parte C: Gestores e o interruptor "Gestor ausente"

**Produces:** `gestores\CadastroDeGestores.tsx`, `gestores\DialogoDeAusencia.tsx` (`function DialogoDeAusencia(props: { gestor: Esquema<'GestorItem'>; aberto: boolean; aoFechar: () => void })`), `gestores\ModalCadastrarGestor.tsx`, `gestores\mensagemDeAusencia.ts`: `mensagemDeAusencia(resposta: Esquema<'GestorAusenciaResposta'>): string` (`Marcação salva.` quando os dois números são 0; senão `{n} pedidos de ajuste de ponto e {m} pedidos de férias passaram para a etapa do RH.` com `plural`).

- [ ] **Step C1: Escrever os testes que falham** (`listarGestores` do exemplo: Carlos, Marina, Paulo ausente até 19/10, Ana e Sofia sem setor).

```tsx
it('tabela sem paginacao: Gestor com cargo, Setor ("Só decide pedidos de gestores" para Ana e Sofia), Equipe ("8 pessoas") e o interruptor Gestor ausente')   // switches 'Gestor ausente: Carlos Ribeiro' desligado; 'Gestor ausente: Paulo Vieira' ligado com 'Volta em 19/10/2026'; capturar('listarGestores').conforme()
it('o interruptor reflete ausenteHoje: passada a data de volta fica desligado mesmo com ausente=true (Review Focus)')   // ausente true, ausenteHoje false
it('sem data de volta mostra "Sem data de volta"; o link Mudar a data de volta reabre o dialogo preenchido')
it('quem so decide pedidos de gestores tem o interruptor desabilitado com a dica')   // 'Não tem equipe: não há etapa do gestor para aprovar sozinha.'
it('ligar abre "Marcar Carlos Ribeiro como ausente" com Data de volta (opcional) e o texto do contrato; a data precisa ser depois de hoje')   // texto: 'Enquanto estiver marcado, a etapa do gestor nos pedidos de ponto e de férias da equipe é aprovada sozinha e o RH ainda decide depois. ...'
it('Marcar como ausente envia { ausente: true, ausenteAte } e e conforme; o gestorId do caminho e o da linha')   // capturar('configurarAusenciaDoGestor'): parametrosDoCaminho { gestorId: '1' }
it('desligar envia { ausente: false, ausenteAte: null } na hora, sem dialogo')
it('mensagemDeAusencia: zeros, so pontos, so ferias, singular')
it('o interruptor fica desabilitado enquanto envia e duplo clique envia uma vez; 404 refaz a lista; 422 GESTOR_SEM_EQUIPE mostra o detail (Review Focus 3)')
it('Cadastrar gestor: uma escolha de pessoa e o texto do contrato; envia { funcionarioId } e e conforme; 409 GESTOR_JA_CADASTRADO, 422 GESTOR_SEM_SETOR_SO_PARA_RH e FUNCIONARIO_INATIVO mostram o detail; a linha nova entra como so decide pedidos')   // capturar('cadastrarGestor').conforme()
it('vazio "Nenhum gestor cadastrado." e erro')
```

- [ ] **Step C2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/cadastros/gestores`
  Expected: FAIL.
- [ ] **Step C3: Implementar** (cada linha com `Interruptor ligado={gestor.ausenteHoje} rotulo={`Gestor ausente: ${nome}`}`; ligar abre o diálogo, desligar chama a mutação; uma mutação por linha com `isPending` para desabilitar; após cada resposta `invalidarRota(qc, '/api/rh/gestores')` e toast com `mensagemDeAusencia`; `ModalCadastrarGestor` com `useBuscaDePessoas`; linha `gestores`).
- [ ] **Step C4: Rodar e ver passar** (mesmo comando).
- [ ] **Step C5: Controle.** Troque `gestor.ausenteHoje` por `gestor.ausente` no interruptor: o teste da data passada deve FALHAR. Restaure.
- [ ] **Step C6: PROVAS** e **ENVIAR** `feat(front): Cadastros, gestores com o interruptor Gestor ausente`.

#### Parte D: Benefícios do catálogo (as três marcas)

**Produces:** `beneficios\CadastroDeBeneficios.tsx`, `beneficios\ModalDeBeneficio.tsx` (`function ModalDeBeneficio(props: { beneficio?: Esquema<'BeneficioCatalogoItem'>; aberta: boolean; aoFechar: () => void })`).

- [ ] **Step D1: Escrever os testes que falham.**

```tsx
it('tabela: Beneficio com descricao, Quem pode pedir, Dependentes, Trajeto, Pessoas, Editar e Excluir (sempre visivel)')   // 'A própria pessoa pode pedir' / 'Só o RH concede'; 'Sim'/'Não'; capturar('listarBeneficiosDoCatalogo') com busca e conforme()
it('Novo beneficio: Nome, Descricao e as tres caixas Opcional, Aceita dependentes e Pede o trajeto com a linha de apoio de cada')
it('envia sempre as tres marcas (false quando desmarcadas) e a requisicao e conforme')   // capturar('criarBeneficioDoCatalogo') / capturar('editarBeneficioDoCatalogo') { beneficioId: '3' }
it('409 NOME_JA_CADASTRADO marca o Nome; sucesso refaz a lista, o numero do menu e as caixas do cadastro de colaborador')
it('Excluir pede confirmacao com "Só é possível se ele nunca foi dado a ninguém." e 204 tira a linha')   // capturar('excluirBeneficioDoCatalogo')
it('409 CADASTRO_EM_USO mostra o detail dentro do dialogo e nao fecha')
it('excluir o unico item da pagina 2 volta para a pagina 1 (Review Focus 2)')   // lista com pagina=2, itens [] depois da exclusão → URL sem pagina e primeira página mostrada
it('vazio "Nenhum benefício cadastrado." e erro')
```

- [ ] **Step D2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/cadastros/beneficios`
  Expected: FAIL.
- [ ] **Step D3: Implementar** (`JanelaDeConfirmacao variante="simples" perigo`; `ListaDeCadastro` já trata a página vazia; linha `beneficios`).
- [ ] **Step D4: Rodar e ver passar** (mesmo comando).
- [ ] **Step D5: PROVAS** e **ENVIAR** `feat(front): Cadastros, beneficios do catalogo com as tres marcas e exclusao`.

#### Parte E: Empresa

**Produces:** `empresa\CadastroDeEmpresa.tsx`; `ufs.ts`: `UFS: readonly Esquema<'EmpresaUf'>[]` e `OPCOES_DE_UF: readonly OpcaoDeSelecao<Esquema<'EmpresaUf'>>[]`.

- [ ] **Step E1: Escrever os testes que falham.**

```tsx
// ufs.test.ts   (// @vitest-environment node)
it('UFS tem as 27 siglas do contrato, na ordem do enum')   // lê components.schemas.EmpresaUf.enum de contrato/openapi.yaml com yaml
// CadastroDeEmpresa.test.tsx
it('mostra o formulario preenchido com a empresa do exemplo e os apoios da nota')   // 'Comercial Aurora Ltda', UF 'SP', 'São Paulo', '08:00', '17:48', 10, 6; 'A saída pode ser menor que a entrada (turno que atravessa a meia-noite).'; capturar('obterEmpresa').conforme()
it('404 EMPRESA_NAO_CONFIGURADA nao e erro: formulario vazio com a faixa "A empresa ainda não foi configurada."')
it('valida nome (150), municipio (100), horarios HH:mm, tolerancia de 0 a 120 e dia de pagamento de 1 a 31')
it('envia o corpo de EmpresaConfiguracao e e conforme; sucesso mostra "Configuração salva." e atualiza o nome da empresa no menu')   // capturar('salvarEmpresa'); depois obterEmpresa devolve outro nome → o menu mostra o nome novo (invalidarRota '/api/eu/empresa')
it('400 por campo marca horarioEntrada, toleranciaAtrasoMinutos e diaPagamento; erro de leitura diferente de 404 mostra Tentar de novo; carregando desabilita os campos')
```

- [ ] **Step E2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/formato src/features/cadastros/empresa`
  Expected: FAIL.
- [ ] **Step E3: Implementar** (`FormularioComErroDoServidor` com `Selecao` de UF, `Entrada type="time"` para os horários e `type="number"` para os inteiros; um único botão `Salvar`; linha `empresa`).
- [ ] **Step E4: Rodar e ver passar** (mesmo comando).
- [ ] **Step E5: PROVAS** e **ENVIAR** `feat(front): Cadastros, empresa`.

#### Parte F: Usuários do sistema

**Produces:** `usuarios\CadastroDeUsuarios.tsx`, `usuarios\rotulos.ts` (`rotuloDaSituacaoDoLogin(situacao: Esquema<'UsuarioSistemaSituacao'>): { texto: string; tom: TomDoSelo }`: `Sem convite`/`neutro`, `Convite enviado`/`info`, `Convite vencido`/`alerta`, `Senha definida`/`sucesso`; textos da nota, "Regras comuns", item "Situação do login").

- [ ] **Step F1: Escrever os testes que falham** (exemplo de `listarUsuariosDoSistema`: Bruno `SENHA_DEFINIDA` ativo, Rafael `CONVITE_ENVIADO` ativo, Lucas `SEM_CONVITE` desativado; usuário de teste: Ana, `funcionarioId` 1).

```tsx
it('tabela: Pessoa com e-mail, Nivel de acesso (Colaborador), Situacao, Acesso (Liberado/Desativado), Ultimo acesso ("Nunca entrou"), Convite enviado em e os botoes; busca e filtros Todas as situacoes e Todos os acessos')   // capturar('listarUsuariosDoSistema'): situacao, ativo=true|false, busca; conforme()
it('botoes por linha: Bruno so Desativar; Rafael Reenviar convite e Desativar; Lucas "Enviar convite" e nenhum Ativar nem Desativar')
it('uma linha desativada com convite enviado tem Ativar')   // ativo false, CONVITE_EXPIRADO → 'Ativar' e 'Reenviar convite'
it('Reenviar convite envia e a linha muda; sem e-mail corporativo fica desabilitado com a dica; 422 mostra o detail')   // capturar('enviarConviteDeAcesso') parametrosDoCaminho { funcionarioId: '104' }; 'Convite enviado para rafael.torres@empresa.com.br.'
it('Ativar nao pede confirmacao e Desativar pede: "Desativar o acesso de Bruno Lima? O login fica bloqueado e as sessões abertas dela são encerradas. Nada é apagado: Ativar desfaz."')   // capturar('ativarUsuarioDoSistema') e capturar('desativarUsuarioDoSistema'): parametrosDoCaminho { funcionarioId } da linha, sem corpo; conforme()
it('a propria linha tem Desativar desabilitado com "Você não pode desativar o seu próprio login."')   // linha com funcionarioId 1
it('duplo clique em Ativar ou Desativar envia uma requisicao; 404 e 422 refazem a linha')   // Review Focus 3
it('depois de cada acao a pagina atual e refeita (a linha troca sem piscar)')   // listarUsuariosDoSistema chamado de novo com a mesma consulta
it('vazio "Nenhum usuário encontrado." e erro')
```

- [ ] **Step F2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/cadastros/usuarios`
  Expected: FAIL.
- [ ] **Step F3: Implementar** (`useEstadoDaLista({ filtros: ['situacao', 'ativo'] as const })` com `valorDeEnum` e `ativo` em `true|false`; `ativo` só vai à API quando o filtro foi escolhido; `FiltroDeSelecao` para os dois; mutações `post` de `convite`, `ativar` e `desativar` com `invalidarRota(qc, '/api/rh/usuarios')` (suponho: um refetch da página basta); botões `Botao tamanho="pequeno"` com `carregando` por linha; `JanelaDeConfirmacao variante="simples" perigo` para `Desativar`; linha `usuarios`).
- [ ] **Step F4: Rodar e ver passar** (mesmo comando) e, para fechar a tarefa, `npm --prefix front-end test src/features/cadastros`.
- [ ] **Step F5: PROVAS.**
- [ ] **Step F6: Registrar e enviar.** `PROGRESSO.md`: `F1b·T5` (A a F); telas "29 Cadastros, Cargos" e "30 Novo cargo" `feitas`; "29 Setores", "Gestores", "Benefícios", "Usuários do sistema" e "Empresa" `feitas` (**sem print**: conferir pela nota); `usa provisório`: nenhum; "conferir ao lado do PNG 29 (moldura, menu do cadastro com os números, tabela de cargos) e do PNG 30 (modal Novo cargo)". ENVIAR `feat(front): Cadastros, usuarios do sistema (ativar, desativar e reenviar convite)`.

---

### Task 6: Log de auditoria (tela 31)

Nota: "Tela 31" em `telas\fatia-1.md`; PNG `31-rh-log-de-auditoria.png`; contrato (em `20-fatia-1-colaboradores.yaml`): `listarEventosDeAuditoria`, `obterEventoDeAuditoria`, `exportarAuditoria` e os schemas `Auditoria*`.

**Files:**
- Modify: `src\shared\formato\datas.ts` (acrescenta `formatarDataHoraComSegundos` e `somarDias`) e teste; `src\app\tabelaDeRotas.tsx` (`ROTAS_RH`: `auditoria`).
- Create: `src\features\auditoria\rotulosDeAuditoria.ts`, `periodoDaAuditoria.ts`, `DetalheDoEvento.tsx`, `PaginaDeAuditoria.tsx`.
- Test: um `*.test.ts(x)` ao lado de cada.

**Interfaces:**
- Consumes: Task 1 (`FiltroDeSelecao`, `valorDeEnum`, `capturar`); 1a: `useEstadoDaLista`, `CampoDeBusca`, `Tabela` (`detalhe`), `Paginacao` (`resumo`), `Selo`, `baixarExportacao`, `clienteHttp`, `useNotificacao` (erro do CSV), `formatarInstanteRelativo`, `formatarNumero`, `hojeEmSaoPaulo`.
- Produces:
  - `datas.ts`: `formatarDataHoraComSegundos(instanteUtc: string | null | undefined): string` (`'2026-10-07T12:38:12Z'` → `'07/10/2026 09:38:12'`); `somarDias(data: string, dias: number): string` (`'2026-10-07'` + `-29` → `'2026-09-08'`; sem fuso da máquina).
  - `rotulosDeAuditoria.ts`: `rotuloDaArea(area: Esquema<'AuditoriaArea'>): string`, `rotuloDaAcao(acao: Esquema<'AuditoriaAcao'>): string`, `OPCOES_DE_AREA`, `OPCOES_DE_ACAO` (os rótulos da nota da Tela 31, item "Áreas" e "ações"), `tomDaAcao(acao): TomDoSelo` (`EXCLUIU`, `ACESSO_NEGADO` e `INATIVOU` → `erro`; o resto `neutro`), `rotuloDoQuem(ator: Esquema<'AuditoriaAtor'>): string` (precedência da nota da Tela 31, item "Coluna 'Quem'": `SISTEMA` → `Automático`; senão `desligado` → `Desligado`; senão o perfil, `Funcionario` → `Colaborador`).
  - `periodoDaAuditoria.ts`: `type PeriodoDaAuditoria = 'hoje' | '7' | '30' | 'personalizado'`; `intervaloDoPeriodo(periodo: PeriodoDaAuditoria, de: string | undefined, ate: string | undefined, hoje: string): { de?: string; ate?: string } | null` (`'7'` → `{}` (a API assume 7 dias); `'hoje'` → `{ de: hoje, ate: hoje }`; `'30'` → `{ de: somarDias(hoje, -29), ate: hoje }`; `'personalizado'` → as duas datas, ou `null` se faltar uma ou `ate < de`); `textoDoPeriodo(...)`: `dos últimos 7 dias`, `de hoje`, `dos últimos 30 dias`, `de {dd/mm/aaaa} a {dd/mm/aaaa}`.
  - `DetalheDoEvento.tsx`: `function DetalheDoEvento(props: { eventoId: number })` (chama `obterEventoDeAuditoria` só enquanto montado).
  - `PaginaDeAuditoria.tsx`: `function PaginaDeAuditoria()` (rota `/rh/auditoria`).

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// datas.test.ts (acrescenta)
it('formatarDataHoraComSegundos e somarDias nao dependem do fuso da maquina')   // também com TZ=Pacific/Honolulu (Step 5)
// periodoDaAuditoria.test.ts, rotulosDeAuditoria.test.ts: tabelas (Colaborador, Automático, Desligado; as três ações vermelhas; os 15 rótulos de área e os 14 de ação da nota)
// PaginaDeAuditoria.test.tsx   (renderizarTela('/rh/auditoria'); exemplos de listarEventosDeAuditoria e obterEventoDeAuditoria)
it('subtitulo: "Nenhuma tela do sistema edita ou apaga estes registros." e nunca "Ninguém consegue editar nem apagar"')   // texto completo da nota da Tela 31, item "A frase do subtítulo muda"
it('filtros: Ultimos 7 dias (padrao, sem de nem ate), busca, Todas as areas e Todas as acoes; a requisicao e conforme')   // capturar('listarEventosDeAuditoria'): pagina, tamanhoPagina=20, sem de/ate/area/acao/apenasAlteracoes; conforme()
it('Hoje manda de e ate de hoje; Ultimos 30 dias manda de = hoje - 29; Periodo personalizado mostra De e Ate e so consulta com as duas datas e ate >= de')   // fake Date 2026-10-07; 'periodo' nunca vai à API
it('area e acao mandam o valor do contrato; Todas volta a sem parametro; mudar qualquer filtro volta a pagina 1')   // area=COLABORADORES, acao=ALTEROU
it('colunas Quando, Quem, Area, Acao e O que aconteceu, com Detalhes por linha')   // 'Hoje, 09:38' em vez de absoluto conforme formatarInstanteRelativo; 'Ana Souza' + 'RH'; Henrique Alves 'Desligado'; 'Acesso negado' com data-tom erro, 'Alterou' neutro
it('Detalhes chama obterEventoDeAuditoria so ao abrir, mostra Registro afetado, Data e hora exatas, Origem e O que mudou, e vira Fechar; uma linha aberta por vez')   // '07/10/2026 09:38:12'; 'Portal no navegador, IP 10.0.4.23'; Cargo / Analista / Analista pleno; capturar('obterEventoDeAuditoria') parametrosDoCaminho { eventoId: '48213' }
it('valorOculto escreve "valor não registrado"; mudancas vazia escreve "Este evento não altera dados."; origem SISTEMA escreve Automático sem IP')
it('a descricao do evento e texto, nunca HTML (Review Focus 4)')   // '<img onerror>' escrito; window.__x undefined
it('rodape: "Mostrando 4 de 1.284 registros dos últimos 7 dias" e Anterior e Proxima mudam pagina; ordem fixa, sem ordenacao')
it('Exportar CSV baixa com os mesmos filtros, sem pagina nem apenasAlteracoes, usa o nome do Content-Disposition (ou auditoria-{hoje}.csv) e desabilita o botao enquanto baixa')   // capturar('exportarAuditoria', { cabecalhos: { 'content-type': 'text/csv', 'content-disposition': 'attachment; filename="auditoria-2026-10-07.csv"' } }) + conforme(); baixarBlob com spy; erro 500 → notificação e nada baixado
it('carregando mostra o esqueleto; vazio "Nenhum registro neste periodo."; erro com Tentar de novo; 400 em de e ate marca o campo; 403; o detalhe com 404 fecha a linha e avisa')
it('nao existe botao de editar nem de apagar em lugar nenhum da tela')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/formato src/features/auditoria`
  Expected: FAIL.
- [ ] **Step 3: Implementar.** `useEstadoDaLista({ filtros: ['periodo', 'de', 'ate', 'area', 'acao'] as const })`; a consulta à API **monta as chaves à mão** (`pagina`, `tamanhoPagina`, `busca`, `area`, `acao`, mais `intervaloDoPeriodo(...)`): `periodo` nunca é enviado, e `intervaloDoPeriodo(...) === null` (personalizado incompleto) desliga a query (`enabled: false`) e mostra `Escolha o período.` (texto meu). `Tabela` com a coluna de ação `Detalhes`/`Fechar` e `detalhe={(l) => l.id === aberto ? <DetalheDoEvento eventoId={l.id} /> : null}`. `Paginacao` com `resumo` (`Mostrando {itens.length} de {total} registros {textoDoPeriodo}`, `formatarNumero`). `Exportar CSV`: `baixarExportacao(clienteHttp.GET('/api/rh/auditoria/exportacao', { params: { query: { busca, de, ate, area, acao } }, parseAs: 'blob' }), `auditoria-${hojeEmSaoPaulo()}.csv`)`. Visual: subtítulo, filtros e linha expandida como PNG 31 (`04-visual.md`, 3).
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/formato src/features/auditoria`
  Expected: PASS.
- [ ] **Step 5: Prova em outro fuso.** Run: `$env:TZ = 'Pacific/Honolulu'; npm --prefix front-end test src/shared/formato src/features/auditoria; Remove-Item Env:TZ`
  Expected: PASS também, **com o `fuso.test.ts`** (o controle da 1a, Task 2, passo 5) afirmando o deslocamento: é ele que prova que o `TZ` chegou aos testes, e a máquina da pessoa costuma já estar em `America/Sao_Paulo`.
- [ ] **Step 6: PROVAS** e **ENVIAR** `feat(front): log de auditoria com filtros, detalhe e exportacao em csv`.
  Antes: `PROGRESSO.md` `F1b·T6`; tela 31 `feita`; **pendente de integração:** o `Content-Disposition` só chega ao navegador se a API o expuser no CORS (nota da Tela 31, item "Exportar CSV": "a conferir com o dono, na integração"); sem ele, o nome é `auditoria-{hoje}.csv`; "conferir ao lado do PNG 31: subtítulo, filtros, etiquetas vermelhas, linha expandida, rodapé".

---

### Task 7: Páginas públicas e de senha (Esqueci minha senha, Definir senha, Trocar senha)

Notas: "Esqueci minha senha", "Definir senha" e "Trocar senha" (as três "Tela sem print") em `telas\fatia-1.md`; sem print; contrato (em `20-fatia-1-colaboradores.yaml`): `solicitarRedefinicaoDeSenha`, `validarLinkDeSenha`, `definirSenhaPeloLink`, `trocarMinhaSenha` e os schemas `ConviteLinkInfo` e afins.

**Files:**
- Modify: `src\features\autenticacao\PaginaEntrar.tsx` (usa `CartaoPublico`; link "Esqueci minha senha"), `src\app\RodapeDoMenu.tsx` (botão "Trocar senha"), `src\app\tabelaDeRotas.tsx` (`ROTAS_PUBLICAS`: troca `PaginaEmBreve` nas duas rotas).
- Create: `src\shared\api\apiPublica.ts`; `src\features\autenticacao\CartaoPublico.tsx`, `esquemasDeSenha.ts`, `useValidacaoDoLink.ts`, `PaginaEsqueciSenha.tsx`, `PaginaDefinirSenha.tsx`, `JanelaTrocarSenha.tsx`.
- Delete: `src\app\paginas\PaginaEmBreve.tsx` **se** nada mais a importa (`Grep`); ajuste os testes da 1a que a citavam.
- Test: um `*.test.ts(x)` ao lado de cada; `PaginaEntrar.test.tsx` e `RodapeDoMenu`/`PortalLayout.test.tsx` (acrescentam).

**Interfaces:**
- Consumes: 1a (`FormularioComErroDoServidor`, `Campo`, `Entrada`, `Botao`, `BotaoLink`, `Aviso`, `Carregando`, `Janela`, `useNotificacao`, `mensagemDeErro`, `criarClienteHttp`, `URL_DA_API`, `LogoR`, `campoEmail`, `useAuth`, `renovadorDeAcesso` não); `erroNoCampo` (Task 2); `capturar`.
- Produces:
  - `apiPublica.ts`: `export const clienteHttpPublico: ClienteHttp = criarClienteHttp({ urlBase: URL_DA_API })` (**sem** `obterToken` e **sem** `aoExpirarAcesso`: nunca leva `Authorization` nem renova); `export const $apiPublica = createClient(clienteHttpPublico)` (`createClient` de `openapi-react-query`). **As telas públicas das fatias 5 e 6 usam este cliente.**
  - `CartaoPublico.tsx`: `function CartaoPublico(props: { titulo: string; children: ReactNode })` (coluna de até 400 px que funciona de 360 px em diante, sobre `pagina`; cartão branco; `LogoR tom="escuro"` (suponho) e o `<h1>{titulo}</h1>`; `PaginaEntrar` passa `titulo="RH-System"`).
  - `esquemasDeSenha.ts`: `campoSenhaNova()` (texto de 8 a 128 caracteres, **sem `trim`**); `esquemaDeNovaSenha` (`novaSenha` e `confirmacao`; `As senhas não conferem.` em `confirmacao`; `Repita a nova senha.` se vazia); `esquemaDeTrocaDeSenha` (`senhaAtual` de 1 a 128 sem `trim`, mais os dois acima; `A nova senha precisa ser diferente da atual.` em `novaSenha`).
  - `useValidacaoDoLink.ts`: `function useValidacaoDoLink(token: string | null)` (`useQuery` do TanStack com `queryKey: ['validar-link', token]`, `queryFn` que chama `clienteHttpPublico.POST('/api/auth/senha/validar-link', { body: { token } })`, `retry: false`, `staleTime: Infinity`, `gcTime: 0`, `enabled: token !== null`; devolve o resultado com `data: Esquema<'ConviteLinkInfo'>`).
  - `PaginaEsqueciSenha.tsx`, `PaginaDefinirSenha.tsx`: `function PaginaEsqueciSenha()`, `function PaginaDefinirSenha()` (rotas `/esqueci-senha` e `/definir-senha?token=`).
  - `JanelaTrocarSenha.tsx`: `function JanelaTrocarSenha(props: { aberta: boolean; aoFechar: () => void })`.

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// apiPublica.test.ts
it('nenhuma chamada do cliente publico leva Authorization, mesmo com token de acesso em memoria')   // tokenDeAcesso.definir('tok'); capturar('solicitarRedefinicaoDeSenha').ultima().cabecalhos.get('authorization') null
// esquemasDeSenha.test.ts
it('senha de 8 a 128 sem trim; confirmacao diferente falha; igual a atual falha')   // ' abcdefg ' (com espaços) vale e vai como digitada
// useValidacaoDoLink.test.tsx
it('em StrictMode chama validar-link uma unica vez (o limite e de 5 por minuto)')   // renderHook com wrapper StrictMode + QueryClientProvider
// CartaoPublico / PaginaEntrar.test.tsx (acrescenta)
it('PaginaEntrar mostra o link Esqueci minha senha abaixo do botao e leva o e-mail ja digitado')   // navega a /esqueci-senha com state { email }; os testes da fatia 0 continuam passando
// PaginaEsqueciSenha.test.tsx   (renderizarTela('/esqueci-senha') e, deslogado, render sem sessão)
it('mostra titulo, texto, campo E-mail, botao Enviar link e Voltar para entrar')   // 'Digite o e-mail do seu acesso. Se ele estiver cadastrado, enviaremos um link para definir uma nova senha. O link vale 1 hora.'
it('traz o e-mail digitado no login')
it('202 troca o formulario por "Se o e-mail estiver cadastrado, enviaremos um link para definir uma nova senha." e nunca diz se o e-mail existe')   // queryByText(/enviado para/) null; capturar('solicitarRedefinicaoDeSenha') corpo { email } conforme()
it('e-mail vazio ou mal formado nao chama a API; 400 marca o campo; 429 mostra a mensagem; 500 e rede mostram os textos da fatia 0; clique duplo envia uma vez')
it('abre igual com sessao neste navegador e nao redireciona')
// PaginaDefinirSenha.test.tsx   (renderizarTela('/definir-senha?token=abc123'))
it('sem token vai direto a "Link inválido" e nao chama a API')
it('com token valida o link antes de mostrar o formulario ("Conferindo o link…") e a requisicao e conforme')   // capturar('validarLinkDeSenha') corpo { token: 'abc123' }
it('CONVITE: "Bem-vindo, defina a sua senha" e "Olá, Rafael Torres. Esta será a senha do acesso rafael.torres@empresa.com.br."; REDEFINICAO: "Defina uma nova senha" e "Acesso {email}."')
it('Nova senha e Repita a nova senha, apoio "De 8 a 128 caracteres.", Salvar senha; confirmacao diferente nao chama a API; nao ha medidor de forca')
it('salvar envia so { token, novaSenha } (sem a confirmacao e sem trim) e e conforme; 204 mostra a confirmacao por tipo e Ir para entrar')   // capturar('definirSenhaPeloLink').conforme(); 'Senha definida. Agora você já pode entrar.' / 'Senha alterada. Entre com a nova senha.'; a pessoa não entra sozinha: sem localStorage rh.refresh
it('422 LINK_INVALIDO_OU_VENCIDO ao abrir e ao salvar (link que venceu entre abrir e salvar) mostra o detail e os botoes Esqueci minha senha e Voltar para entrar (Review Focus 5)')   // 'Este link não vale mais. Peça um novo ao RH ou use "Esqueci minha senha".'
it('429 mostra a mensagem; 400 em novaSenha marca o campo e em token vira link invalido; 500 e rede')
it('erro diferente de 422 ao abrir o link (500, rede, 404 do csp) mostra a mensagem com Tentar de novo e nao o formulario')
it('o token nunca aparece na tela nem no localStorage e nenhuma chamada leva Authorization')   // document.body.textContent sem 'abc123'; localStorage vazio
it('com sessao neste navegador a pagina abre igual e ignora a sessao')
// JanelaTrocarSenha.test.tsx   (pelo rodape: renderizarTela('/eu/inicio', { perfil }) × 3 perfis)
it('o rodape tem Trocar senha ao lado de Sair e abre o modal com Senha atual, Nova senha, Repita a nova senha e o apoio')
it('envia { senhaAtual, novaSenha } (sem confirmacao) e e conforme; 204 fecha e mostra "Senha trocada. As suas outras sessões foram encerradas." e a sessao continua')   // capturar('trocarMinhaSenha'); o menu segue visível; localStorage rh.refresh intacto
it('422 SENHA_ATUAL_INCORRETA marca Senha atual sem deslogar; 429; 400 por campo; nova igual a atual nao chama a API; clique duplo envia uma vez')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/api src/features/autenticacao src/app`
  Expected: FAIL.
- [ ] **Step 3: Implementar.** `apiPublica`; `CartaoPublico` e o refator do `PaginaEntrar` (acabamento; os textos e os testes da fatia 0 **não mudam**); `esquemasDeSenha`; `useValidacaoDoLink`; `PaginaEsqueciSenha` (`FormularioComErroDoServidor`, mutação de `$apiPublica`, no 202 troca o corpo por `<Aviso tom="sucesso">`); `PaginaDefinirSenha` (estados `conferindo | invalido | formulario | concluido`; `aoEnviar` captura o 422 `LINK_INVALIDO_OU_VENCIDO` e o 400 de `token` e muda para `invalido` **sem relançar**; o `token` só em `useSearchParams` e no corpo; nunca em log nem em estado persistido); `JanelaTrocarSenha` (mutação de `$api`; `erroNoCampo(e, { SENHA_ATUAL_INCORRETA: 'senhaAtual' })`; **não** chama `sair`); botão `Trocar senha` (`variante="texto"`, `tamanho="pequeno"`) em `RodapeDoMenu` ao lado de `Sair`; troque `PaginaEmBreve` nas duas rotas públicas e apague o arquivo se sobrou sem uso.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/api src/features/autenticacao src/app`
  Expected: PASS (os testes da fatia 0 e da 1a de `PaginaEntrar`, `rotas` e `PortalLayout` continuam passando).
- [ ] **Step 5: Controle.** (a) Passe `obterToken: tokenDeAcesso.obter` ao `clienteHttpPublico`: o teste "nenhuma chamada leva Authorization" deve FALHAR. (b) Faça `PaginaDefinirSenha` relançar o 422: o teste do link vencido ao salvar deve FALHAR. Restaure.
- [ ] **Step 6: PROVAS** e **ENVIAR** `feat(front): esqueci minha senha, definir senha (convite e redefinicao) e trocar senha`.
  Antes: `PROGRESSO.md` `F1b·T7`; telas "Esqueci minha senha", "Definir senha" e "Trocar senha" `feitas` (**sem print**: conferir pela descrição das três notas "Tela sem print" de senha em `telas\fatia-1.md`, a 360 px e a 1366 px) e o acabamento da tela de entrar; **pendente de integração:** o e-mail com o link (convite e redefinição) chega ao `/definir-senha?token=...` do front publicado; o limite de 5 por minuto (`429`); a troca de senha encerrando as outras sessões.

---

### Task 8: Meus documentos (os três perfis)

Nota: "Meus documentos" ("Tela sem print") em `telas\fatia-1.md`; sem print; contrato (em `20-fatia-1-colaboradores.yaml`): `listarMeusDocumentos`, `enviarMeuDocumento`, `baixarMeuDocumento`.

**Files:**
- Create: `src\features\documentos\PaginaMeusDocumentos.tsx`, `ModalEnviarMeuDocumento.tsx`.
- Modify: `src\app\tabelaDeRotas.tsx` (`ROTAS_EU`: `meus-documentos`).
- Test: `PaginaMeusDocumentos.test.tsx`, `ModalEnviarMeuDocumento.test.tsx`.

**Interfaces:**
- Consumes: Task 4 (`enviarMeuArquivo`, `useBaixarPorLink`, `rotuloDoTipoDeDocumento`, `etiquetaDoDocumento`, `REGRAS_DE_ARQUIVO_DE_DOCUMENTO`, `formatarDataDoInstante`); 1a (`Tabela`, `SeletorDeArquivo`, `Janela`, `invalidarContadoresDoMenu`, `useContadoresDoMenu`, `formatarDiaMes`, `formatarData`).
- Produces: `function PaginaMeusDocumentos()` (rota `/eu/meus-documentos`); `function ModalEnviarMeuDocumento(props: { documento: Esquema<'DocumentoColaborador'>; aberta: boolean; aoFechar: () => void })`.

- [ ] **Step 1: Escrever os testes que falham** (`renderizarTela('/eu/meus-documentos', { perfil })`; exemplo de `listarMeusDocumentos`: pendente 64, enviado 61).

```tsx
it.each(['Funcionario', 'Gestor', 'RH'])('abre para %s no menu dele, sem 403')   // o item 'Meus documentos' está no menu dos três (menu.ts da 1a)
it('titulo, apoio e as colunas Documento (com descricao), Validade, Situacao, a data e a acao')   // 'Os documentos que o RH guarda na sua ficha e os que ele pediu para você enviar.'; capturar('listarMeusDocumentos').conforme()
it('pendente: "Pendente", "Pedido em 01/10" e Enviar arquivo; enviado: "Enviado", "Enviado em 14/03/2022" e Ver; nao ha botao Adicionar')
it('Ver chama baixarMeuDocumento e abre o link temporario')   // capturar('baixarMeuDocumento') parametrosDoCaminho { documentoId: '61' }
it('Enviar arquivo abre "Enviar Comprovante de residência" com a area de envio, Cancelar e Enviar documento')
it('enviar chama enviarMeuArquivo (vi.mock) com o id e o arquivo, fecha, refaz a lista e refaz as bolinhas do menu')   // listarMeusDocumentos e obterContadoresDoMenuDoFuncionario chamados de novo
it('400 em arquivo aparece junto da area; 422 DOCUMENTO_NAO_PEDIDO e 404 recarregam a lista; enviando desabilita o botao')
it('a bolinha de Meus documentos no menu mostra documentosPendentes')   // exemplo de obterContadoresDoMenuDoFuncionario
it('carregando mostra o esqueleto; vazio "Você ainda não tem documentos na ficha."; erro com Tentar de novo; a descricao e texto, nao HTML')
```

O envio de verdade (multipart) de `enviarMeuDocumento` já tem teste de contrato em `enviarDocumento.node.test.ts` (Task 4).

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/documentos`
  Expected: FAIL.
- [ ] **Step 3: Implementar** (`$api.useQuery('get', '/api/eu/documentos')`, `Tabela` sem paginação, mutação do TanStack com `enviarMeuArquivo`; depois do envio `invalidarRota(qc, '/api/eu/documentos')` e `invalidarContadoresDoMenu(qc)`; linha `meus-documentos` em `ROTAS_EU`).
- [ ] **Step 4: Rodar e ver passar** (mesmo comando).
- [ ] **Step 5: PROVAS** e **ENVIAR** `feat(front): meus documentos (enviar arquivo e ver)`.
  Antes: `PROGRESSO.md` `F1b·T8`; tela `feita` (**sem print**; `usa provisório: DocumentoColaboradorTipo`); **pendente de integração:** `enviarMeuDocumento` e `baixarMeuDocumento` de verdade.

---

### Task 9: Ficha da pessoa da equipe, aba Dados (tela 52, gestor)

Nota: "Tela 52" em `telas\fatia-1.md`; PNG `52-gestor-painel-da-equipe-ficha.png`; contrato (em `20-fatia-1-colaboradores.yaml`): `obterFichaDaEquipe`, `obterIndicadoresDaEquipe` e os schemas `FuncionarioFichaGestor` e afins.

**Files:**
- Create: `src\features\equipe\PainelDaFichaDaEquipe.tsx`, `abasDaFichaDaEquipe.tsx`.
- Modify: `src\app\tabelaDeRotas.tsx` (`ROTAS_GESTOR`: `equipe/:funcionarioId`).
- Test: `PainelDaFichaDaEquipe.test.tsx`.

**Interfaces:**
- Consumes: Task 3 (`MolduraDaFicha`, `QuadroDeIndicadores`, `indicadores.ts`, `EtiquetaDaSituacao`/`etiquetaDaSituacao`, `rotuloDoTipoDeContrato`); 1a (`Cartao`, `Secao`, `formatarData`).
- Produces: `abasDaFichaDaEquipe.tsx`: `type PropsDaAbaDaEquipe = { ficha: Esquema<'FuncionarioFichaGestor'> }`; `type AbaDaFichaDaEquipe = { valor: string; rotulo: string; Componente: (props: PropsDaAbaDaEquipe) => ReactElement }`; `const ABAS_DA_FICHA_DA_EQUIPE: readonly AbaDaFichaDaEquipe[]` (só `dados`; **as fatias 3, 4, 6 e 7 acrescentam `ponto`, `ferias`, `treinamentos`, `advertencias`**). `function PainelDaFichaDaEquipe()` (rota `/gestor/equipe/:funcionarioId`; ao fechar vai a `/gestor/equipe`: a página é da fatia 7 e até lá aparece "Página não encontrada." dentro do layout; suponho).
- **Dois componentes, uma moldura:** a ficha do gestor **não** esconde campos da do RH; usa o schema próprio e só divide `MolduraDaFicha`.

- [ ] **Step 1: Escrever os testes que falham** (`renderizarTela('/gestor/equipe/21', { perfil: 'Gestor' })`; exemplos `obterFichaDaEquipe` (Diego Farias) e `obterIndicadoresDaEquipe`).

```tsx
it('cabecalho: "Vendedor, setor Comercial" e a etiqueta Ativo (SEM_REGISTRO mostra Ativo), sem Editar dados, Inativar nem menu')
it('so a aba Dados e nao ha botao Registrar advertencia')
it('Contato e contrato: e-mail, telefone mascarado como veio, cargo, admissao, contrato e jornada')   // '(11) 9****-7783', '08/08/2023', 'CLT', '44 h semanais'; sem 'CPF', 'RG', 'Endereço', 'Matrícula', 'Benefícios'
it('os quatro numeros vem de outra rota: "−3h45" (alerta), "22 dias", "2" Treinamentos pendentes (alerta), "2" Advertencias (erro); null mostra traco; falha mostra Tentar de novo so no quadro')
it('a nota do pe continua: "Só o RH altera dados cadastrais. CPF, endereço, holerites e documentos ficam visíveis apenas para o RH."')
it('as requisicoes sao conformes e usam o id do caminho')   // capturar('obterFichaDaEquipe') e capturar('obterIndicadoresDaEquipe') parametrosDoCaminho { funcionarioId: '21' }
it('404 (fora do escopo) fecha o painel e avisa; erro com Tentar de novo; carregando com esqueleto')
it('so o Gestor abre: RH e Funcionario veem o 403 da guarda')
it('o gestor nunca ve nem pede campos do RH: nenhuma chamada a /api/rh/...')   // o servidor de teste falha com onUnhandledFrame se houvesse
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/features/equipe`
  Expected: FAIL.
- [ ] **Step 3: Implementar** (`MolduraDaFicha` com a trilha `Equipe {setor}`; `QuadroDeIndicadores` com `itemDeBancoDeHoras`, `itemDeFerias`, `itemDeTreinamentosPendentes`, `itemDeAdvertencias`; o `Componente` de `dados` com o cartão "Contato e contrato" e a nota do pé; linha em `ROTAS_GESTOR`).
- [ ] **Step 4: Rodar e ver passar** (mesmo comando).
- [ ] **Step 5: PROVAS** e **ENVIAR** `feat(front): ficha da pessoa da equipe do gestor (tela 52)`.
  Antes: `PROGRESSO.md` `F1b·T9`; tela 52 `feita`; `listarEquipeDoGestor` **não** é desta fatia (tela 51, fatia 7); **pendente de integração:** o 404 de pessoa de outro setor com a API real; "conferir ao lado do PNG 52".

---

### Task 10: Verificação final da fatia 1

**Files:**
- Create: `src\app\matrizDePerfis.test.tsx`, `src\app\textosProibidos.test.tsx`, `src\app\coberturaDaFatia1.test.ts` (primeira linha `// @vitest-environment node`); `src\test\textosProibidos.ts` (`rotulosDaTela(contêiner: HTMLElement): string` e `acharTextoProibido(rotulos: string, texto: string): boolean`, com limite de palavra) e `src\test\textosProibidos.test.ts`.
- Modify: `front-end\scripts\violacoes-de-politica.mjs` (passos novos antes da linha `ACRESCENTE AQUI`), `docs\plano-front-end\PROGRESSO.md`, `docs\plano-front-end\DUVIDAS.md`.

**Interfaces:**
- Consumes: tudo das Tasks 1 a 9; `renderizarTela`; `menuVisivel`/`todosOsCaminhosDoMenu` (1a); `contrato\STATUS.md`.
- Produces: a fatia 1 pronta e enviada; a lista de telas a conferir; os pendentes de integração; o relatório da fatia.

- [ ] **Step 1: Escrever os três testes transversais.**

```tsx
// matrizDePerfis.test.tsx
it.each(ROTAS_DA_FATIA_1)('%s: perfil certo abre, perfil errado ve o 403 sem o conteudo e sem perder o menu')
//   tabela { caminho, aguarda (texto do título), perfis: Perfil[] }: '/rh/colaboradores', '/rh/colaboradores/novo', '/rh/colaboradores/12', '/rh/colaboradores/12?aba=documentos', '/rh/cadastros/cargos', .../setores, .../gestores, .../beneficios, .../usuarios, .../empresa, '/rh/auditoria' → ['RH']; '/gestor/equipe/21' → ['Gestor']; '/eu/meus-documentos' → ['Funcionario','Gestor','RH']; para cada um dos outros perfis: 'Você não tem permissão para ver esta página.', nenhuma chamada à API de negócio da rota (capturar a operação principal: quantas() === 0) e o menu do perfil continua à vista
it.each(ROTAS_PROTEGIDAS)('%s sem sessao vai a /entrar?voltar=... e volta ao destino depois de entrar')
it('as paginas publicas /esqueci-senha e /definir-senha abrem sem sessao e com sessao, sem redirecionar')
it('o menu de cada perfil so mostra o que tem rota: RH tem Colaboradores, Cadastros, Log de auditoria e Meus documentos; Gestor e Funcionario tem Meus documentos e nao tem Colaboradores, Cadastros nem Log de auditoria; Painel da equipe do Gestor ainda nao aparece (rota da fatia 7); o grupo "Meu espaço" do RH e do Gestor ainda nao aparece (as rotas pessoais sao das fatias 3 a 7; "Meus documentos" ja esta nos tres menus)')
// textosProibidos.test.tsx
it.each(TELAS_E_TEXTOS_PROIBIDOS)('%s nao escreve os textos cortados pelo dono')
//   lista: 'Experiência'; ficha: 'Unidade', 'Matriz', '***'; Cadastros: 'Níveis de acesso', 'chave de aprovação automática'; aba Documentos: 'Lembrar'; modal de documento: 'Ela recebe um aviso'; auditoria: 'Ninguém consegue editar nem apagar'; usuários: 'Excluir', 'Apagar'; cadastro de colaborador: 'Matrícula', 'Unidade'.
//   COMO PROCURAR (um texto como `nota`, em minúsculo, casa com "anotações" e com dado do contrato: falso positivo): (1) **limite de palavra** com `new RegExp('(?<![\\p{L}\\p{N}])' + escapar(texto) + '(?![\\p{L}\\p{N}])', 'iu')` (o `\b` do JavaScript falha em palavra que começa com letra acentuada: `/\bÓtimo\b/i.test('Ótimo')` dá `false`; medido); (2) **só os rótulos da tela**: o texto procurado é o `textContent` dos elementos de rótulo **dentro do contêiner da tela** (`h1`, `h2`, `h3`, `legend`, `label`, `button`, `th`, `[role="tab"]`, `[role="columnheader"]`, `[role="status"]`, `[role="alert"]` e, para modal, os `p` dentro de `[role="dialog"]`, que abre em portal fora do contêiner), **sem** `td`, `[role="cell"]` nem o que vem dos exemplos do contrato. As funções `rotulosDaTela(contêiner: HTMLElement): string` e `acharTextoProibido(rotulos: string, texto: string): boolean` ficam em `src\test\textosProibidos.ts` e têm teste próprio em `textosProibidos.test.ts`: `it('rotulosDaTela ignora celulas e a busca acha so a palavra inteira')` ('nota' não casa em 'anotações' nem dentro de uma `td`; casa num `button`; `***` é procurado como texto, com o escape).
// coberturaDaFatia1.test.ts
it('cada uma das 42 operacoes com tela aparece num teste de contrato (capturar ou esperarRequisicaoConforme)')
//   lê os *.test.ts(x) de src\features e src\app (menos este; NÃO os de src\test nem de src\shared, que testam o ajudante e a fatia 0 e dariam falso positivo) e procura /(capturar|esperarRequisicaoConforme)\(\s*'(\w+)'/; a lista fixa OPERACOES_DA_FATIA_1 tem as 43 do contrato menos listarEquipeDoGestor (tela 51, fatia 7); falha listando as que faltam
it('listarEquipeDoGestor tem exemplo no contrato mas nao tem tela aqui')
```

- [ ] **Step 2: Rodar.** Run: `npm --prefix front-end test src/app`
  Expected: `matrizDePerfis` e `textosProibidos` **passam** se as tarefas anteriores estão certas (se falhar, o defeito é da tarefa dona da rota ou do texto: corrija lá); `coberturaDaFatia1` **falha** listando as operações sem teste de contrato (acrescente o `capturar(...).conforme()` no teste da tela dona; nunca afrouxe a lista).
- [ ] **Step 3: Controle.** Apague temporariamente um `capturar('excluirBeneficioDoCatalogo')` de um teste: `coberturaDaFatia1` deve FALHAR citando a operação; restaure. Ponha `'Lembrar'` num rótulo de `AbaDocumentos`: `textosProibidos` deve FALHAR; restaure.
- [ ] **Step 4: Estender o `csp`.** Em `front-end\scripts\violacoes-de-politica.mjs`, acrescente **depois** dos passos da 1a (os da vitrine incluídos), antes de `ACRESCENTE AQUI`, estes passos (cada um espera o título e termina com `Escape` quando abre diálogo; a API simulada devolve 404 e as telas mostram o estado de erro: o que se quer é **zero violações**; **os passos da vitrine ficam onde estão e são os únicos que podem sair `[pulado]`**): `abre a lista de colaboradores` (`/rh/colaboradores`, título `Colaboradores`); `abre o painel de cadastro` (`/rh/colaboradores/novo`, diálogo `Adicionar colaborador`, **`await aquietar()` e só então `Escape`**: o painel é rota filha, e fechá-lo antes de as consultas das opções terminarem as cancela e o script contaria `net::ERR_ABORTED`: `03-contrato-e-mocks.md`, seção 8); `abre o modal de novo cargo` (`/rh/cadastros/cargos`, clica `Novo cargo`, diálogo, `await aquietar()`, `Escape`); `abre os outros cadastros e o log` (`/rh/cadastros/setores`, `/gestores`, `/beneficios`, `/usuarios`, `/empresa`, `/rh/auditoria`: **`await aquietar()` antes de cada novo `goto`**, não só esperar o título; o `aquietar()` espera a rede ociosa; **não** ignore `ERR_ABORTED` em geral); `abre Meus documentos` (`/eu/meus-documentos`); `abre o modal de Trocar senha` (botão `Trocar senha` do rodapé, diálogo, `Escape`); `abre as paginas publicas` (`/esqueci-senha` e `/definir-senha?token=teste`: a API simulada responde **404** a `/api/auth/senha/validar-link`, e a tela mostra a mensagem com "Tentar de novo", como os testes da Task 7 esperam; é o que se quer aqui: zero violações).
- [ ] **Step 5: Prova completa, com a vitrine e sem ela.** Run: `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end test; $env:VITE_VITRINE = 'sim'; npm --prefix front-end run build; npm --prefix front-end run csp; Remove-Item Env:VITE_VITRINE; npm --prefix front-end run build; npm --prefix front-end run csp`
  Expected: `lint` e `format:check` limpos; **todos os testes passam** (guarde o número de arquivos e de testes); os dois `csp` com `PASSOU`, `violações reais: 0 · controles mudos: 0` e **nenhuma linha `[FALHOU]`**; o `csp` com a vitrine, todos os passos `[ok ]` (a sonda de download também), `passos pulados: 0`; o `csp` do build normal, os passos da vitrine **todos** `[pulado]` (e só eles; a vitrine acabou de ser provada com a variável ligada, na mesma linha); `dist\mockServiceWorker.js` não existe. Sem Edge nem Chrome o `csp` sai com código 2 (`pendente: csp`, A11 e G2): registre-o e siga. Se o `csp` acusar violação, a causa é código (o `FALHOU:` diz a diretiva e o arquivo): corrija o código, nunca a política.
- [ ] **Step 6: Pendências de integração** (`01-regras.md`, G1). Run: `Select-String -Path contrato\STATUS.md -Pattern '^\| [017] '` (hoje a fatia 1 está `planejada`; mesmo se estiver `pronta`, **a sessão não roda contra a API real**: `01-regras.md`, G1). Registre em `PROGRESSO.md`, tabela "Pendente de integração", **uma linha por item**, com o roteiro (`.env.local` com `VITE_API_URL=http://localhost:5000` e `VITE_MOCKS=nao`; `npm --prefix front-end run dev`; entrar como o administrador). **O roteiro é da pessoa** (ou do dono): você não olha o navegador (`01-regras.md`, A11); ela traz o resultado e você o registra na tabela "Conferências da pessoa" do `PROGRESSO.md`:
  1. **Colaboradores** (`listarFuncionarios`, `criarFuncionario`, `obterFichaDoFuncionario`, `editarFuncionario`, `obterIndicadoresDoFuncionario`): cadastrar Rafael Torres, achar na lista, abrir a ficha, editar; os quatro números aparecem `—` até as fatias 3, 4 e 7.
  2. **Inativar, cancelar e readmitir** (`inativarFuncionario`, `cancelarDesligamentoAgendado`, `readmitirFuncionario`): inativar com data futura (faixa de agendamento), cancelar; readmitir quem está inativo; 409 de CPF de inativo manda a ficha.
  3. **Documentos** (`adicionarDocumentoDoFuncionario`, `enviarMeuDocumento`): enviar PDF, JPG e PNG de verdade, e um arquivo recusado (tipo e tamanho); pedir e a pessoa enviar.
  4. **Download** (`baixarDocumentoDoFuncionario`, `baixarMeuDocumento`, `exportarAuditoria`): o botão Ver abre o link e baixa como anexo; o CSV baixa com o nome do `Content-Disposition` (a API precisa expô-lo no CORS; sem ele o nome é `auditoria-{hoje}.csv`).
  5. **Cadastros** (`listarCargos`... `editarSetor`, `listarGestores`, `cadastrarGestor`, `configurarAusenciaDoGestor`, `listarBeneficiosDoCatalogo`... `excluirBeneficioDoCatalogo`, `obterEmpresa`, `salvarEmpresa`): os números do menu do cadastro, os 409 e 422 de setor, a exclusão com `CADASTRO_EM_USO`; o nome da empresa muda no menu ao salvar.
  6. **Usuários e senha** (`listarUsuariosDoSistema`, `enviarConviteDeAcesso`, `ativarUsuarioDoSistema`, `desativarUsuarioDoSistema`, `solicitarRedefinicaoDeSenha`, `validarLinkDeSenha`, `definirSenhaPeloLink`, `trocarMinhaSenha`): o e-mail de convite e o de redefinição chegam com o link do front publicado; o link vence; 5 tentativas por minuto (429); desativar encerra as sessões e a pessoa vê "Seu acesso está desativado. Fale com o RH." ao entrar; trocar a senha encerra as outras sessões e mantém esta.
  7. **Auditoria** (`listarEventosDeAuditoria`, `obterEventoDeAuditoria`): cada gravação acima aparece no log, com o detalhe "O que mudou".
  8. **Ficha da equipe** (`obterFichaDaEquipe`, `obterIndicadoresDaEquipe`): entrar como o gestor Carlos Ribeiro e abrir `/gestor/equipe/21`; uma pessoa de outro setor dá 404.
- [ ] **Step 7: Lista de telas a conferir ao lado do PNG** (A11; escreva na conversa e em "Conferência visual" do `PROGRESSO.md`): **com print:** 01 Colaboradores (abas, subtítulo, colunas, etiquetas, rodapé), 02 Adicionar colaborador (painel de 600 px, blocos, caixas), 03 Ficha, aba Dados (cabeçalho, cartões, etiquetas de benefícios, quadro de números), 09 aba Documentos, 10 modal Adicionar documento, 29 Cadastros, Cargos (menu do cadastro com números e a tabela), 30 modal Novo cargo (título em uma linha, botões lado a lado), 31 Log de auditoria (subtítulo novo, filtros, etiquetas vermelhas, linha expandida), 52 Ficha da equipe. **Sem print, conferir pela descrição em `telas\fatia-1.md`:** Editar dados, Inativar (com a trava), Readmitir, Cadastros (Setores, Gestores com o interruptor, Benefícios com as três marcas, Usuários, Empresa), Esqueci minha senha, Definir senha (convite e redefinição, link válido e vencido), Trocar senha, Meus documentos e o acabamento da tela de entrar. **Diga à pessoa** que a fonte é a Inter, decidida pelo dono em 2026-11-02 (ela só confere a letra ao lado do PNG), e que a vitrine (`$env:VITE_VITRINE='sim'; npm --prefix front-end run dev`, `/eu/vitrine`) serve para olhar os componentes.
- [ ] **Step 8: Relatório final da fatia no `PROGRESSO.md`**: o que foi feito por tarefa (T1 a T10), o número de arquivos e testes, a versão do contrato, as telas feitas (com "usa provisório" onde couber), as peças novas que as fichas reusam (tabela "Para as fichas 2 a 8", abaixo), as dúvidas abertas desta fatia (`D-benef` e `D-editar-inativo`; as da 1a, `D-menu`, `D-rodape` e `D-fonte`, foram decididas pelo dono em 2026-11-02) e os pendentes de integração. "Onde estou": **próximo passo = `fichas\fatia-2.md`** e, depois, o padrão da seção seguinte.
- [ ] **Step 9: Enviar.** Antes: `git log origin/main..HEAD --format=%B | Select-String -Pattern 'Co-Authored-By|Claude|Generated with'` não pode listar nada (C6). Run: `git status; git add front-end docs\plano-front-end\PROGRESSO.md docs\plano-front-end\DUVIDAS.md; git commit -m "docs(front): fecha a fatia 1 (matriz de perfis, csp, pendencias de integracao e conferencia visual)"; git push`. **Não** abra pull request agora (C3).
- [ ] **Step 10: Seguir para a fatia 2** sem esperar resposta de dúvida (nenhuma bloqueia).

---

## Padrão para as fichas 2 a 8: como se constrói uma tela, do começo ao fim, neste projeto

(Leia isto em vez deste arquivo; cada item aponta o arquivo ou o teste-modelo.)

**Antes de escrever código** (por tela): leia a nota em `telas\fatia-N.md`, abra o PNG, anote a versão do contrato no `PROGRESSO.md`, liste os `operationId` da tela e confira o `x-perfis` (ele diz a **família** da rota: `/rh`, `/gestor` ou `/eu`; o perfil vem dela). Antes de cada tarefa, releia "Decisões do dono" em `DUVIDAS.md`.

**1. Rota e menu.** Uma linha em `src\app\tabelaDeRotas.tsx` (`ROTAS_RH`, `ROTAS_GESTOR` ou `ROTAS_EU`; páginas públicas em `ROTAS_PUBLICAS`). O item de menu **já está** em `menu.ts` e aparece sozinho quando a rota existe. Painel ou janela sobre uma lista é **rota filha** (`filhas: [...]`); a lista termina com `<Outlet />`; abrir e fechar pelo `usePainelSobreLista` (`paraPainel('novo')`, `fechar()`), que mantém os filtros da URL. Janela que não precisa de endereço (modal de cadastro) é estado local.

**2. Dados.** Leitura: `$api.useQuery('get', '<caminho>', { params: { query | path } }, { placeholderData: keepPreviousData })`. Gravação: `$api.useMutation('post' | 'put' | 'delete', '<caminho>')` **na página** (para o botão do rodapé ler `isPending`), seguida de `invalidarRota(queryClient, '<prefixo>')` (e `invalidarContadoresDoMenu` se o número do menu muda). Linha que mudou: refaz a página (um refetch). Rotas públicas: **`$apiPublica`** (nunca leva `Authorization`).

**3. Lista** (modelo: `ListaDeColaboradores`, `ListaDeCadastro`, `PaginaDeAuditoria`). `useEstadoDaLista` (estado na URL) + `valorDeEnum` e `inteiroDaUrl` (valor desconhecido na URL vale o padrão) + `Abas`, `FiltroDeSelecao`, `CampoDeBusca` (300 ms) + `Tabela` + `Paginacao` (`resumo` para o texto). Totais e contagens vêm do que a API manda (`totais`, `total`), não da página. Estados: carregando (esqueleto, números com "—"), vazio **e** vazio com busca, erro com "Tentar de novo", 403. Lista que pode esvaziar a última página volta à 1.

**4. Formulário** (modelo: `PainelCadastrar`, `ModalDeCargo`, `PaginaDefinirSenha`). Esquema zod com `esquemas.ts` (a mensagem do esquema vence a global); `FormularioComErroDoServidor id="form-x"`; botões no **rodapé da `Janela`** com `type="submit" form="form-x"` e `carregando={mutacao.isPending}`; `aoEnviar` chama `mutateAsync` e, no `catch`, `throw erroNoCampo(e, { CODIGO_DO_CONTRATO: 'campo' })` para o 409 e o 422 que pertencem a um campo (o resto vai ao aviso geral). Campo de data: `Entrada type="date" lang="pt-BR"`; valor continua texto. Duplo clique já é travado pelo formulário.

**5. Detalhe com abas** (modelo: `PainelDaFicha`, `PainelDaFichaDaEquipe`). `MolduraDaFicha` + uma **tabela de abas** (`ABAS_DA_FICHA_RH`, `ABAS_DA_FICHA_DA_EQUIPE`): a fatia acrescenta uma linha com o `Componente` da aba (aba que não existe não aparece). Dois perfis, dois schemas, dois componentes que só dividem a moldura. Os números de resumo carregam **à parte** e falham sozinhos.

**6. Ação com confirmação** (modelo: Inativar, Desativar, Excluir benefício). `JanelaDeConfirmacao` (`simples`; `digitar` para a trava de digitar; `pendentes` para encerrar e excluir com 409 `CONFIRMACAO_NECESSARIA`); filha da `Janela` do painel; erro dentro do diálogo; em 404 ou 409 de estado, refaça a tela e avise.

**7. Arquivo.** Download: `useBaixarPorLink().baixar(() => clienteHttp.GET(...))` (link temporário) ou `baixarExportacao(clienteHttp.GET(..., { parseAs: 'blob' }), 'nome.csv')` (CSV direto). Upload: função `enviar...` em `src\features\<assunto>\enviar....ts` (usa `multipart`), testada com `// @vitest-environment node` (modelo: `enviarDocumento.node.test.ts`); o componente a chama e seu teste usa `vi.mock` da função; `SeletorDeArquivo` com `textoDeAjuda` e as regras do contrato.

**8. Tela pública** (modelo: `PaginaEsqueciSenha`, `PaginaDefinirSenha`). `CartaoPublico`, `$apiPublica`, nenhum `Authorization`, o `token` só na URL e no corpo (nunca em log, `localStorage` ou tela), link vencido tratado depois de abrir **e** depois de salvar, funciona de 360 px.

**9. Testes de cada tela** (nomes descritivos; modelo em qualquer `*.test.tsx` desta fatia): **contrato** (`capturar('<operationId>').conforme()` e os `parametrosDoCaminho`; o corpo de um erro ou de um exemplo nomeado do contrato vem de `exemploDoContrato(operationId, status, nome)`, nunca de texto copiado), **estados** (carregando, vazio, erro, sem permissão: guarda de família e 403 da API), **textos obrigatórios** e as mensagens do contrato, **duplo clique**, **datas** perto da meia-noite, **texto de usuário como texto**; o teste de **texto proibido** procura com limite de palavra e só nos rótulos da tela (`rotulosDaTela`, `textosProibidos.test.tsx`). Uma tabela em `textosProibidos.test.tsx` com o que o dono cortou, e a **cobertura da fatia** (`coberturaDaFatiaN.test.ts`: cada operação com tela aparece num teste de contrato; copie `coberturaDaFatia1.test.ts`).

**10. Fim de cada bloco:** PROVAS (as da tarefa: `format; lint; format:check; build; test`) → `PROGRESSO.md` (passo, tela, versão do contrato, "pendente de integração") → commit e push, sem coautor. **Fim de cada fatia:** a **Prova padrão de fatia** (o último bloco desta seção), a matriz de perfis da fatia, os passos novos no `csp`, a lista de telas a conferir ao lado do PNG e das conferências da pessoa, os pendentes de integração com roteiro (`STATUS.md`) e o relatório no `PROGRESSO.md`.

**Armadilhas já medidas ou conhecidas:** o `tsc` não acusa chave extra em `query` e no corpo (por isso o `.conforme()`); o handler gerado devolve **sempre o mesmo exemplo** (para outro cenário, `capturar(op, { corpo, status })` ou `problema(...)`, com `exemploDoContrato(op, status, nome)` quando o contrato já tem o exemplo); `input type="date"` em teste se preenche com `fireEvent.change`; o `Toast` só tem texto (aviso com botão é uma faixa `Aviso` na página); a `Selecao` não tem "nenhum" (use `FiltroDeSelecao`, que traz a opção "todos"); o jsdom não monta multipart; `ErroDeApi.corpo` guarda os campos extras do `problem+json` (`cadastroExistente`, `pendentes`...).

### Prova padrão de fatia

**Todo fim de fatia (2 a 8) roda esta prova.** A ficha da fatia **cita este bloco** ("Prova padrão de fatia", `fatia-1b-telas.md`) e acrescenta só o que é da fatia: os testes por tela e os passos novos do `csp`. Da **raiz do repositório**, no PowerShell 5.1, **um comando por vez, lendo a saída de cada um** (o PowerShell 5.1 não para no que falha). **A exceção são as linhas com variável de ambiente, que vão inteiras numa linha só** (a variável só vale na mesma linha: `01-regras.md`, A14); por isso o `build` leva a limpeza de `VITE_API_URL` **na mesma linha**:

```powershell
npm --prefix front-end run format
npm --prefix front-end run lint
npm --prefix front-end run format:check
npm --prefix front-end test
npm --prefix front-end test src/app/coberturaDaFatiaN
Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run build
npm --prefix front-end run csp
```

Esperado, na ordem:

1. `format` normaliza o que você escreveu; `lint` e `format:check` **limpos**.
2. `test`: **todos passam** (anote no `PROGRESSO.md` o número de arquivos e de testes). Entram os testes por tela da ficha e a matriz de perfis da fatia (as rotas da fatia entram na matriz de `matrizDePerfis.test.tsx`: perfil certo abre; perfil errado vê o 403 sem o conteúdo e sem perder o menu).
3. `coberturaDaFatiaN` (**N = a fatia**; cópia de `coberturaDaFatia1.test.ts` com a lista fixa das operações com tela da fatia, tiradas de `contrato\ROTAS.md`): **passa**. Ela falha listando as operações que ficaram sem `capturar('<operationId>')` ou `esperarRequisicaoConforme`; nunca afrouxe a lista.
4. `build` termina com `built in` e **não existe** `front-end\dist\mockServiceWorker.js`.
5. `csp`: `PASSOU`, `violações reais: 0 · controles mudos: 0 · exceções: 0 · requisições que falharam: 0` e **nenhuma linha `[FALHOU]`** (a sonda de download e os passos novos da fatia `[ok ]`). **`[pulado]` só é aceito nos passos da vitrine** (build normal) **e só vale se a vitrine foi provada ao menos uma vez com a variável ligada** (1a, Task 13, passo 6; e de novo, abaixo, se a fatia mexeu num componente de `src\shared\ui`); qualquer outro `[pulado]` é falha. **Código 2 (`pendente: csp`)** = sem Edge nem Chrome: registre-o (A11 e G2) e siga; não é falha da fatia. Se a fatia mexeu num componente de `src\shared\ui` (que a vitrine mostra), rode **também** com a vitrine, **numa linha só**: `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; $env:VITE_VITRINE = 'sim'; npm --prefix front-end run build; npm --prefix front-end run csp; Remove-Item Env:VITE_VITRINE; npm --prefix front-end run build` (todos os passos `[ok ]`, `passos pulados: 0`).
6. Antes do push: `git log origin/main..HEAD --format=%B | Select-String -Pattern 'Co-Authored-By|Claude|Generated with'` **não escreve nada** (C6).

**"Pronto" de uma fatia** = esta prova + o `PROGRESSO.md` atualizado (telas, versão do contrato, pendentes de integração com roteiro, conferências da pessoa) + a lista de telas a conferir ao lado do PNG. Um `csp` verde não prova dados: os dados são dos testes com MSW.

## Para as fichas 2 a 8: o que a 1b acrescentou (arquivo e assinatura)

| Arquivo | Assinatura resumida |
|---|---|
| `src\test\capturar.ts` | `capturar(operationId, resposta?)` → `{ todas, ultima(), quantas(), conforme() }`; `problema(status, campos?)` |
| `src\test\telas.tsx`, `formulario.ts`, `textosProibidos.ts` | `renderizarTela(rota, { perfil?, usuario? })` → `{ user, roteador, ... }`; `escolherOpcao(user, campo, opcao)`; `preencherData(rotulo, valor)`; `rotulosDaTela(contêiner)` e `acharTextoProibido(rotulos, texto)` (texto proibido: limite de palavra, só rótulos) |
| `src\shared\api\erroDeApi.ts`, `cadastroExistente.ts`, `apiPublica.ts` | `ErroDeApi.corpo`; `lerCadastroExistente(erro)`; `$apiPublica`, `clienteHttpPublico` |
| `src\shared\form\erroNoCampo.ts` | `erroNoCampo(erro, { CODIGO: 'campo' })` |
| `src\shared\lista\` | `valorDeEnum`, `inteiroDaUrl`; `FiltroDeSelecao({ rotulo, todos, opcoes, valor, aoMudar })`; `usePainelSobreLista()` → `{ paraPainel, fechar }` |
| `src\shared\arquivos\useBaixarPorLink.ts` | `useBaixarPorLink()` → `{ baixar(chamada), baixando }` |
| `src\shared\formato\` | `formatarDiaEMes`, `formatarDataDoInstante`, `formatarDataHoraComSegundos`, `somarDias`; `mascararCep`, `formatarCep`; `UFS`, `OPCOES_DE_UF` |
| `src\shared\ui\MenuDeAcoes.tsx` | `AcaoDoMenu.dica?` |
| `src\features\colaboradores\` | `MolduraDaFicha`, `ABAS_DA_FICHA_RH` (acrescente a aba), `QuadroDeIndicadores`, `indicadores.ts`, `rotulos.ts` (`rotuloDoTipoDeContrato`, `rotuloDoNivelDeAcesso`, `etiquetaDaSituacao`), `useOpcoesDeApoio`, `useBuscaDePessoas` |
| `src\features\equipe\abasDaFichaDaEquipe.tsx` | `ABAS_DA_FICHA_DA_EQUIPE` (acrescente a aba) |
| `src\features\cadastros\` | `ITENS_DO_CADASTRO` (a fatia 7 acrescenta Pagamento), `ListaDeCadastro` |
| `src\features\documentos\` | `rotuloDoTipoDeDocumento`, `adicionarDocumento`, `enviarMeuArquivo` (modelo de upload) |
| `src\features\autenticacao\` | `CartaoPublico`, `esquemasDeSenha.ts`, `JanelaTrocarSenha` |

## Dúvidas conhecidas desta fatia (registre em `DUVIDAS.md` no passo indicado; os textos podem ser colados para o dono)

As dúvidas da 1a (`D-fonte`, `D-menu`, `D-rodape`) **foram decididas pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"): o `Trocar senha` e o `Sair` ficam visíveis no rodapé, o grupo "Meu espaço" e o item "Meus documentos" estão nos menus e a fonte é a Inter. Não há o que registrar sobre elas. **Cada dúvida daqui tem um código fixo** (`D-benef`, `D-editar-inativo`): ao registrar, use esse código, **não** `D-001` (`01-regras.md`, D1).

**D-benef** (Task 2, passo 6; não bloqueia)

```
Mensagem para o dono: dúvida D-benef do front-end do RH-System (benefícios já marcados no cadastro de colaborador)

Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

Onde: fatia 1b, Task 2 (tela 02, "Adicionar colaborador"); PNG 02; contrato <versão lida de contrato\openapi.yaml>, criarFuncionario (beneficioIds, padrão []); telas\fatia-1.md, "Perguntas abertas", item 1.
O que achei: o desenho mostra três benefícios já marcados no cadastro (Vale-refeição, Plano de saúde e Seguro de vida). O contrato diz que a lista começa vazia, e nenhum documento diz que existem benefícios que já vêm marcados.
O que sugiro: deixar todos desmarcados (o que o contrato diz); quem cadastra marca os que valem. Se você quiser três já marcados, preciso saber quais e se são sempre os mesmos.
Pergunta: todos desmarcados (A) ou alguns já marcados (B, e diga quais)?
Enquanto isso: sigo com todos desmarcados.
```

**D-editar-inativo** (Task 3, parte D, passo D5; não bloqueia)

```
Mensagem para o dono: dúvida D-editar-inativo do front-end do RH-System ("Editar dados" para quem está inativo)

Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

Onde: fatia 1b, Task 3 (tela 03, ficha do colaborador); telas\fatia-1.md, seção "Tela 03", item "Botões do cabeçalho conforme situacao"; contrato <versão lida de contrato\openapi.yaml>, editarFuncionario.
O que achei: o contrato não proíbe editar os dados de quem está inativo, e o desenho só mostra o botão "Editar dados" de quem está ativo. Para quem está inativo, o contrato manda "Readmitir" no lugar de "Inativar".
O que sugiro: mostrar "Editar dados" também para quem está inativo (por exemplo, para corrigir um CPF ou um endereço antes de readmitir). Se não puder, o botão some quando a pessoa está inativa e nada mais muda.
Pergunta: quem está inativo pode ter os dados editados (A) ou só ser readmitido (B)?
Enquanto isso: sigo com "Editar dados" aparecendo também para inativo.
```

---

## Cobertura e autorrevisão (do autor deste plano)

**Operações → tarefa** (43 de 43; a seção "Conferência das 43 rotas" de `telas\fatia-1.md` e a seção da fatia 1 de `contrato\ROTAS.md` batem com esta lista):

| # | `operationId` | Tarefa | | # | `operationId` | Tarefa |
|---|---|---|---|---|---|---|
| 1 | `listarFuncionarios` | T1 (e filtro de setor, busca de gestor em T5B) | | 23 | `editarSetor` | T5B |
| 2 | `criarFuncionario` | T2 | | 24 | `listarGestores` | T5C |
| 3 | `obterFichaDoFuncionario` | T3A | | 25 | `cadastrarGestor` | T5C |
| 4 | `editarFuncionario` | T3B | | 26 | `configurarAusenciaDoGestor` | T5C |
| 5 | `obterIndicadoresDoFuncionario` | T3A | | 27 | `listarBeneficiosDoCatalogo` | T5D (e opções em T2) |
| 6 | `inativarFuncionario` | T3C | | 28 | `criarBeneficioDoCatalogo` | T5D |
| 7 | `cancelarDesligamentoAgendado` | T3C | | 29 | `editarBeneficioDoCatalogo` | T5D |
| 8 | `readmitirFuncionario` | T3D | | 30 | `excluirBeneficioDoCatalogo` | T5D |
| 9 | `listarEquipeDoGestor` | **nenhuma** (tela 51, fatia 7; T10 confere o menu) | | 31 | `obterEmpresa` | T5E (o menu é da 1a) |
| 10 | `obterFichaDaEquipe` | T9 | | 32 | `salvarEmpresa` | T5E |
| 11 | `obterIndicadoresDaEquipe` | T9 | | 33 | `listarUsuariosDoSistema` | T5F |
| 12 | `listarDocumentosDoFuncionario` | T4 | | 34 | `enviarConviteDeAcesso` | T3C (menu "...") e T5F |
| 13 | `adicionarDocumentoDoFuncionario` | T4 | | 35 | `ativarUsuarioDoSistema` | T5F |
| 14 | `baixarDocumentoDoFuncionario` | T4 | | 36 | `desativarUsuarioDoSistema` | T5F |
| 15 | `listarMeusDocumentos` | T8 | | 37 | `listarEventosDeAuditoria` | T6 |
| 16 | `enviarMeuDocumento` | T8 | | 38 | `exportarAuditoria` | T6 |
| 17 | `baixarMeuDocumento` | T8 | | 39 | `obterEventoDeAuditoria` | T6 |
| 18 | `listarCargos` | T5A (e opções em T2) | | 40 | `solicitarRedefinicaoDeSenha` | T7 |
| 19 | `criarCargo` | T5A | | 41 | `validarLinkDeSenha` | T7 |
| 20 | `editarCargo` | T5A | | 42 | `definirSenhaPeloLink` | T7 |
| 21 | `listarSetores` | T5B (e filtro em T1, opções em T2) | | 43 | `trocarMinhaSenha` | T7 |
| 22 | `criarSetor` | T5B | | | | |

- **Cobertura:** as 42 operações com tela têm tarefa **e** teste de contrato por tela (`coberturaDaFatia1.test.ts` falha se uma ficar sem). A 43ª (`listarEquipeDoGestor`) é da tela 51 (fatia 7) e não ganha tela aqui. Telas: 01, 02, 03 (mais Editar dados, Inativar, Readmitir), 09, 10, 29 (Cargos, Setores, Gestores, Benefícios, Usuários, Empresa), 30, 31, 52 e as sem print (Esqueci, Definir, Trocar senha, Meus documentos).
- **O que li e o que supus:** em "Fatos lidos e o que suponho" (no começo) e marcado `(suponho)` nas tarefas. **Não abri os PNG**; o que digo deles vem das notas.
- **Contradições e lacunas achadas** (resolvidas dentro do plano, sem mexer no contrato): (1) `ErroDeApi` perde `cadastroExistente` (T2: `corpo`); (2) o `Toast` não tem botão (T2: faixa na lista); (3) falta cliente público para as rotas sem sessão, que a ficha 5 espera (T7: `$apiPublica`); (4) `AcaoDoMenu` sem dica (T3: `dica`); (5) a nota pede "Escolha um PDF, JPG ou PNG de até 10 MB." e o `SeletorDeArquivo` da 1a já tem as suas mensagens (T4: valem as da 1a; anote); (6) o exemplo de `listarFuncionarios` traz 3 itens com `total` 101 (a tela mostra "Mostrando 1 a 20 de 101": é o exemplo, não defeito); (7) o exemplo da ficha do Bruno traz `situacaoHoje: ATESTADO` (a etiqueta é "Atestado", não "Ativo" como o PNG 03).
- **Tipos coerentes com a 1a:** `Esquema<...>`, `Perfil`, `Usuario`, `ClienteHttp`, `OpcaoDeSelecao`, `OpcaoDoCombo`, `TomDoSelo`, `ColunaDaTabela`, `PropsDaAba`, `ErroDeApi`, `invalidarRota`, `invalidarContadoresDoMenu`, `useEstadoDaLista`, `FormularioComErroDoServidor` usados com as assinaturas de `fatia-1a-fundacao.md`; cada tipo novo (`Capturador`, `OpcoesDeApoio`, `AbaDaFicha`, `ItemDoCadastro`...) tem **um** dono e as tarefas que o usam o listam em `Consumes`.
- **Review Focus:** cinco itens, cada um com teste (Tasks 3A e 3B; 5D; 5C e 5F; 4, 5A e 6; 7).
- **Proporção:** o plano é longo porque a fatia tem 43 operações e dez conjuntos de tela; o código escrito aqui é só assinatura, nome de teste, asserção e valor do contrato.
- **Escolhas de engenharia** (o dono pode vetar; as que mudam o que o usuário vê estão em dúvida): faixa `Aviso` com "Abrir ficha" depois do cadastro; `CLT` por padrão; `Editar dados` também para inativo (D-editar-inativo); benefícios desmarcados (D-benef); fechar a ficha da equipe leva a `/gestor/equipe`; troca de linha por refetch; o `periodo` do log só existe na URL; "Escolha o período." quando o personalizado está incompleto.
