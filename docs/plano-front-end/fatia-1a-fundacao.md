# Front-end, fatia 1a: fundação do front-end: plano de implementação

> **Para quem executa (sessão de IA ou pessoa):** execute este plano tarefa por tarefa, na ordem. Os passos usam a sintaxe de caixa de seleção (`- [ ]`) para acompanhamento. **O método de execução (subagentes ou na própria sessão) ainda não foi escolhido: é decidido depois do planejamento.** Antes de começar, leia `LEIA-PRIMEIRO.md`, `01-regras.md`, `02-pilha.md`, `03-contrato-e-mocks.md` e `04-visual.md`; este arquivo assume as cinco coisas e **segue as convenções da fatia 0** (`fatia-0.md`): pastas `src\app`, `src\shared\api|ui|form|formato|arquivos|lista|mocks`, `src\features\<assunto>`, `src\test`; nomes em português sem acento; testes ao lado do arquivo.

**Goal:** Entregar a base que todas as telas usam: tokens e estilo, componentes-base testados, formulários com erros do servidor, formatação, ajudantes de lista, download e upload, markdown seguro, `$api` com renovação do token quando uma chamada recebe 401, rotas por perfil com menu dirigido por tabela, layout, início mínimo por perfil e a prova de que tudo roda sob a política de segurança do site.

**Architecture:** O estilo é Tailwind 4 com os tokens de `04-visual.md` em `@theme static` (CSS estático). Os componentes de `src\shared\ui\` usam o `@base-ui/react` 1.9.0 onde há peça (campo, seleção, janela, abas...) e HTML puro nos outros; cada um tem teste com Testing Library. A API é o `$api` (`openapi-react-query`) sobre o `criarClienteHttp` da fatia 0, que ganha **uma** opção nova: ao receber 401 numa chamada com token, pede ao `AuthProvider` uma renovação única e repete a chamada uma vez. As rotas ficam em **uma tabela** (`src\app\tabelaDeRotas.tsx`, uma linha por tela, por família `/rh`, `/gestor`, `/eu`) e o menu em **outra** (`src\app\menu.ts`, uma linha por item); **um item do menu só aparece quando a rota dele existe**, então uma fatia nova só acrescenta a linha da rota. As telas completas (lista, formulário, ficha) são da 1b; esta fatia entrega o chão e a vitrine que o prova.

**Tech Stack:** o da fatia 0 (`02-pilha.md`), que **já inclui** a `react-markdown` **10.1.0** (MIT; sem `eval`; 0 violações sob a política, com a regra de não permitir `img`: `04-visual.md`, seção 8), instalada na Task 1 da fatia 0. **Nenhuma biblioteca nova** nesta fatia.

**Spec:** `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md` (seções 5, 6 e 7; critério de pronto da fundação: menu por perfil, três telas de ponta a ponta e o build sob a política; **as três telas são da 1b**); roadmap (`docs\superpowers\plans\2026-10-28-roadmap-completo.md`: decisão 1, decisão 6 "conteúdo de usuário nunca é HTML", decisão 14, "Fatia 1, incremento 1a"); `04-visual.md` (tudo o que é visual); `telas\fatia-0.md` ("O shell completo") e `telas\fatia-1.md` ("Início mínimo por perfil", "Regras comuns"); contrato `contrato\openapi.yaml` (as rotas desta fatia: `obterEmpresa`, `obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor`, `obterContadoresDoMenuDoRh`, e os schemas `Paginacao`, `LinkTemporario`, `ProblemaDeApi`, `UsuarioResposta`).

## Global Constraints

- Tudo de `fatia-0.md` (Global Constraints) continua valendo: Node `^22.22.2 || ^24.15.0` (a regra do Node 26 está em `02-pilha.md`, seção 2); comandos da **raiz** no **PowerShell 5.1** (sem `&&`; comando de prova com variável de ambiente numa linha só: `01-regras.md`, A14); `npm --prefix front-end run <script>`; commit e push a cada passo que manda commitar, na branch **`feat/front-fatias-1-a-8`**, mensagens em português **sem acento**, **nunca o Claude como coautor** (`01-regras.md`, C6); textos ao usuário em português **com acento**; nomes de código **sem acento**; contrato, banco e back-end **não se tocam**.
- **Pilha fechada:** nenhuma biblioteca **fora de `02-pilha.md`** (a `react-markdown` 10.1.0 já está nela e já foi instalada na Task 1 da fatia 0; nenhuma tarefa daqui roda `npm install`). Outra biblioteca é dúvida (`01-regras.md`, A7).
- **Política de segurança** (`02-pilha.md`, 1; `04-visual.md`, 8): nada de `<style>`, `style="..."` escrito, `dangerouslySetInnerHTML`, `innerHTML`, imagem `data:` ou `blob:`, fonte ou CSS de outro domínio, `eval`, Radix. A prop `style={{...}}` do React só para valor **dinâmico** (largura de barra). `CSPProvider disableStyleElements` na raiz, **uma vez**.
- **Visual:** só valores de `04-visual.md`; cores, raios e tamanhos de letra **só por token** (`bg-primaria`, `rounded-cartao`, `text-corpo`), nunca hexadecimal solto no código. Fonte **Inter** (pilha; **decidida pelo dono em 2026-11-02**, `DUVIDAS.md`, "Decisões do dono").
- Lint: `react-refresh/only-export-components` exige que função auxiliar fique em arquivo **à parte** do componente (`botaoClasses.ts`, `useNotificacao.ts`...); `erasableSyntaxOnly` (nada de `enum` nem propriedade no parâmetro do construtor); `import type` onde só se usa tipo (`verbatimModuleSyntax`).
- Cada componente com variante expõe `data-variante` (ou `data-tom`, `data-tamanho`, `data-posicao`): os testes conferem esse atributo, não classe de CSS.
- Teste de componente com Base UI: `userEvent.setup()` em cada teste; o `setup.ts` da fatia 0 já tem os polyfills do jsdom. Janela, seleção e menu abrem em portal: use `screen`, não `container`. O botão de fechar do `Toast` vem com `aria-hidden`: ache-o por `getByLabelText`.
- Cada tarefa termina com as provas: `test`, `lint`, `format:check`, `build` (e `csp` nas Tasks 12 e 13).
- Medido para este plano (2026-10-30, projeto de teste fora do repositório com a pilha de `02-pilha.md`): os testes de `Dialog`, `AlertDialog`, `Select`, `Combobox`, `Tabs`, `Checkbox`, `Switch`, `Progress`, `Field` com `Input`/`Select`/`Combobox`/`textarea`, `Toast` (com `getByLabelText`), React Hook Form + zod + `Field`/`Select`/`Checkbox`, e `react-router` 8.5.1 com `createMemoryRouter` **passam** no jsdom 30.1.3 com o padrão que as tarefas descrevem.

## Review Focus

Falhas que o desenho implica, que a pessoa real encontra e que nenhum teste de tela exercitaria. Cada uma tem teste na tarefa indicada:

1. **401 numa chamada de negócio quando o refresh também venceu:** a pessoa volta ao login **sem laço de requisições** (uma renovação por vez para N chamadas, **uma** repetição por chamada). Task 9.
2. **Dados de uma pessoa não podem aparecer para a próxima no mesmo navegador:** o cache de consultas é **limpo** ao sair e ao perder a sessão. Task 9.
3. **Texto escrito por usuário** (markdown com `<script>`, `<img onerror>`, `javascript:` e imagem de outro domínio): nada executa, nada é pedido a terceiros. Task 7.
4. **Endereço de retorno malicioso** (`/entrar?voltar=//exemplo.com`) é ignorado; **perfil errado** vê 403 sem o conteúdo piscar; endereço sem sessão vai ao login e **volta ao destino** depois. Task 12.
5. **Datas:** o dia de calendário (`2026-10-07`) nunca muda de dia, e um instante perto da meia-noite UTC (`2026-10-08T02:30:00Z`) aparece como 07/10 às 23:30 em São Paulo; **busca, filtro e página na URL** voltam à página 1 ao mudar e a busca digitada manda **uma** requisição. Tasks 2 e 11.

---

### Task 1: Tokens, estilo base e a prova "sem HTML cru"

**Files:**
- Modify: `front-end\src\index.css` (troca o conteúdo pelo de `04-visual.md`, seção 2), `front-end\public\favicon.svg`.
- Create: `front-end\src\shared\ui\estilo.ts`.
- Test: `src\shared\ui\estilo.test.ts`, `src\app\tokens.test.ts`, `src\app\semHtmlCru.test.ts`.

**Interfaces:**
- Produces: os tokens de `04-visual.md` (cores, raios, tamanhos de letra, `--spacing-menu`) como variáveis CSS e utilitários do Tailwind; `cn(...entradas: ClassValue[]): string` em `estilo.ts` (`clsx` mais `twMerge`, este **estendido** com os nomes dos tokens para resolver conflito entre `text-corpo` e `text-texto`, e entre `rounded-botao` e `rounded-campo`).

- [ ] **Step 1: Conferir o chão.** Run: `git status; git branch --show-current; git fetch origin; git merge origin/main; Get-Content contrato\openapi.yaml -TotalCount 12 | Select-String '^\s+version:'`
  Expected: branch `feat/front-fatias-1-a-8` (criada no fim da fatia 0; senão `git switch -c feat/front-fatias-1-a-8` a partir da ponta de `feat/front-fatia-0`); o `merge` sem conflito; anote a versão do contrato no `PROGRESSO.md` (`01-regras.md`, E4 e F). Existem `docs\plano-front-end\04-visual.md` e `front-end\src\index.css` da fatia 0.
- [ ] **Step 2: Escrever os testes que falham.**

```ts
// src\shared\ui\estilo.test.ts
it('cn junta classes e descarta as falsas')                         // cn('a', false && 'b', undefined, 'c') === 'a c'
it('cn mantem tamanho de letra e cor do texto, que nao conflitam')  // cn('text-corpo', 'text-texto') === 'text-corpo text-texto'; cn('text-corpo', 'text-t2') === 'text-t2'
it('cn resolve conflito de raio e de fundo dos tokens')             // cn('rounded-botao', 'rounded-campo') === 'rounded-campo'; cn('bg-superficie', 'bg-primaria') === 'bg-primaria'

// src\app\tokens.test.ts   (primeira linha: // @vitest-environment node; lê src/index.css com readFileSync)
it('index.css tem o @theme static com os tokens-ancora de 04-visual')
//   cada par: --color-pagina #fffffa, --color-superficie #ffffff, --color-superficie-suave #f2f8f7, --color-superficie-destaque #e6f4f3,
//   --color-borda #d9e4e2, --color-borda-campo #c9d8d6, --color-texto #0e3236, --color-texto-suave #4e6466, --color-primaria #0d5c63,
//   --color-primaria-media #247b7b, --color-primaria-clara #78cdd7, --color-erro-texto #a8322a; --spacing-menu 232px; --radius-cartao 14px; --text-t1 36px
it('todo --color-* e um hexadecimal de 6 digitos ou rgb(), e o arquivo usa @theme static') // regex sobre as linhas; /@theme static/

// src\app\semHtmlCru.test.ts   (// @vitest-environment node; varre src\ e index.html, ignora *.test.ts(x) e a pasta gerado)
it('nenhum arquivo tem <style, style=", dangerouslySetInnerHTML, innerHTML nem insertAdjacentHTML')
it('o detector acha cada um dos cinco num texto de exemplo (controle: prova que a busca funciona)') // a função `achar(texto)` fica no próprio teste
```

- [ ] **Step 3: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/ui/estilo src/app/tokens src/app/semHtmlCru`
  Expected: FAIL (`estilo.ts` não existe; o `index.css` da fatia 0 não tem os tokens).
- [ ] **Step 4: Implementar.** (a) `index.css`: o bloco **exato** de `04-visual.md`, seção 2 (com `@import 'tailwindcss'; @import '@fontsource-variable/inter';`). (b) `estilo.ts`: `export function cn(...entradas: ClassValue[]): string { return twMerge(clsx(entradas)) }` com o `twMerge` criado por `extendTailwindMerge`, acrescentando ao grupo de tamanho de letra (`font-size`) os valores `t1`, `ficha`, `numero`, `t2`, `t3`, `corpo`, `tabela`, `pequeno`, `mini`, `relogio` e ao grupo `rounded` os valores `botao`, `campo`, `cartao`, `janela`, `destaque`. (c) `favicon.svg`: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="8" fill="#0d5c63"/><text x="16" y="23" font-family="sans-serif" font-size="20" font-weight="700" text-anchor="middle" fill="#78cdd7">R</text></svg>`.
- [ ] **Step 5: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/ui/estilo src/app/tokens src/app/semHtmlCru`
  Expected: PASS.
- [ ] **Step 6: Controle: prove que `semHtmlCru` pega o defeito.** Crie `front-end\src\app\tmp.tsx` com `export const x = <p style="color:red">a</p>`, rode o passo 5 (deve FALHAR citando `tmp.tsx`), apague o arquivo e rode de novo (PASS).
- [ ] **Step 7: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando; o `build` mostra `built in`.
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md` (linha `F1a·T1`, "Onde estou"); Run: `git status; git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): tokens de design, cn e prova de que o codigo nao escreve html cru"; git push`

---

### Task 2: Formatação (CPF, telefone, datas, horas, reais)

**Files:**
- Create: `front-end\src\shared\formato\documentos.ts`, `datas.ts`, `numeros.ts`, `texto.ts`.
- Test: um `*.test.ts` ao lado de cada, mais `fuso.test.ts` (o controle da prova "outro fuso": Step 5).

**Interfaces:**
- Produces (todas puras; nenhuma lê o fuso da máquina):
  - `documentos.ts`: `somenteDigitos(valor: string): string`; `mascararCpf(valor: string): string` (para digitar: `'84639271069'` → `'846.392.710-69'`, `'8463'` → `'846.3'`, corta em 11 dígitos); `mascararTelefone(valor: string): string` (`'11987654410'` → `'(11) 98765-4410'`, `'1187654410'` → `'(11) 8765-4410'`, `'119'` → `'(11) 9'`, `'1'` → `'(1'`); `formatarCpf(cpf: string | null | undefined): string` e `formatarTelefone(telefone: string | null | undefined): string` (exibição; vazio ou `null` → `'—'`); `cpfValido(digitos: string): boolean` (11 dígitos, não todos iguais, dígitos verificadores corretos: contrato, `FuncionarioCriarComando`); `telefoneValido(digitos: string): boolean` (`/^\d{10,11}$/`: é só o que o contrato exige).
  - `datas.ts`: `FUSO_DO_PORTAL = 'America/Sao_Paulo'`; `formatarData(data: string | null | undefined): string` (data de calendário `'2026-10-07'` → `'07/10/2026'` **sem usar `Date`**; vazio → `'—'`; texto fora do formato volta como veio); `formatarDataHora(instanteUtc)` (`'2026-10-07T12:42:00Z'` → `'07/10/2026 09:42'`); `formatarHora(instanteUtc)` (`'09:42'`); `formatarDiaMes(instanteUtc)` (`'07/10'`); `formatarInstanteRelativo(instanteUtc: string, agora?: Date): string` (`'Hoje, 09:38'`, `'Ontem, 17:55'`, `'05/10, 18:30'`, e `'05/10/2025, 18:30'` se for de outro ano: o formato da tela 00 e da 31); `hojeEmSaoPaulo(agora?: Date): string` (`'YYYY-MM-DD'`); `dataPorExtenso(agora?: Date): string` (`'Quarta-feira, 7 de outubro'`); `saudacao(agora?: Date): 'Bom dia' | 'Boa tarde' | 'Boa noite'` (hora em São Paulo: 05:00 a 11:59 dia, 12:00 a 17:59 tarde, o resto noite; **suponho** que siga a hora, `telas\fatia-1.md`). Os de instante aceitam `null` e devolvem `'—'`. Use `Intl.DateTimeFormat('pt-BR', { timeZone: FUSO_DO_PORTAL, ... })` e `formatToParts`.
  - `numeros.ts`: `formatarMinutosComoHoras(minutos: number, opcoes?: { sinal?: boolean }): string` (`380` → `'6h20'`; `528` → `'8h48'`; `0` → `'0h00'`; com `sinal`: `380` → `'+6h20'`, `-2` → `'−0h02'` com o sinal de menos U+2212); `formatarReais(centavos: number): string` (`123456` → `'R$ 1.234,56'`, com espaço comum e não o espaço sem quebra do `Intl`); `formatarNumero(n: number): string` (`1284` → `'1.284'`).
  - `texto.ts`: `iniciais(nome: string): string` (`'Ana Souza'` → `'AS'`; `'João Pedro Alves'` → `'JA'`; `'Madonna'` → `'M'`; `'  '` → `''`); `primeiroNome(nome: string): string`; `plural(quantidade: number, singular: string, plural: string): string` (`1` → singular; `0` e o resto → plural).

- [ ] **Step 1: Escrever os testes que falham** (uma tabela `it.each` por função; os casos são os dos exemplos acima mais estes).

```ts
// documentos.test.ts
it('cpfValido aceita os cpfs do contrato e recusa repetido, curto e com digito errado')   // '84639271069', '12348237130', '29384756083' true; '11111111111', '1234567890', '12345678901' false
it('mascararCpf e mascararTelefone ignoram o que nao e digito e cortam o excesso')       // mascararCpf('846.392.710-69999') === '846.392.710-69'
// datas.test.ts
it('formatarData nao muda o dia (Review Focus 5)')                 // '2026-10-07' → '07/10/2026'; '2026-01-01' → '01/01/2026'; null → '—'
it('formatarDataHora converte UTC para Sao Paulo, inclusive perto da meia-noite')  // '2026-10-08T02:30:00Z' → '07/10/2026 23:30'; '2026-10-07T12:42:00Z' → '07/10/2026 09:42'
it('formatarInstanteRelativo escreve Hoje, Ontem e a data')        // agora = new Date('2026-10-07T12:42:00Z'): '2026-10-07T12:38:00Z' → 'Hoje, 09:38'; '2026-10-06T20:55:00Z' → 'Ontem, 17:55'; '2026-10-05T21:30:00Z' → '05/10, 18:30'
it('dataPorExtenso e saudacao usam a hora de Sao Paulo')           // new Date('2026-10-07T12:42:00Z') → 'Quarta-feira, 7 de outubro' e 'Bom dia'; '2026-10-07T20:00:00Z' → 'Boa tarde'; '2026-10-08T02:00:00Z' → 'Boa noite'
// numeros.test.ts, texto.test.ts: as tabelas acima
// fuso.test.ts   (// @vitest-environment node; o controle do Step 5)
it('controle do fuso: o relogio da maquina obedece a variavel TZ (Pacific/Honolulu fica 10 h atras de UTC)')
//   instante = new Date('2026-10-07T12:42:00Z'); só quando process.env.TZ === 'Pacific/Honolulu': instante.getHours() === 2 e instante.getTimezoneOffset() === 600; sem essa variável o teste não afirma o deslocamento e passa (por isso o Step 5 o confere)
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/formato`
  Expected: FAIL (módulos inexistentes).
- [ ] **Step 3: Implementar** os quatro arquivos com as assinaturas acima.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/formato`
  Expected: PASS.
- [ ] **Step 5: Prova em outro fuso** (as funções não podem depender do fuso da máquina; e a máquina da pessoa costuma já estar em `America/Sao_Paulo`, então só trocar o fuso prova alguma coisa). Run, **numa linha só** (a variável só vale na mesma linha: `01-regras.md`, A14): `$env:TZ = 'Pacific/Honolulu'; npm --prefix front-end test src/shared/formato; Remove-Item Env:TZ`
  Expected: PASS também, **incluindo** o `fuso.test.ts` (o controle: sob `TZ=Pacific/Honolulu`, a hora de `12:42Z` é `2`). **Controle do controle (uma vez):** troque, só na sua cópia, `toBe(2)` por `toBe(3)` em `fuso.test.ts` e rode o mesmo comando: deve FALHAR com `expected 2 to be 3` (prova que o `TZ` chegou ao teste: medido em 2026-11-02 no Windows PowerShell 5.1, Node 24.17.0, vitest 5.0.5); desfaça a troca e rode de novo.
- [ ] **Step 6: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 7: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): formatacao de cpf, telefone, datas em sao paulo, horas e reais"; git push`

---

### Task 3: Componentes de exibição e `mensagemDeErro`

**Files:**
- Create: `front-end\src\shared\api\mensagemDeErro.ts`; em `front-end\src\shared\ui\`: `Botao.tsx`, `botaoClasses.ts`, `BotaoLink.tsx`, `Selo.tsx`, `Cartao.tsx`, `Avatar.tsx`, `Bolinha.tsx`, `LogoR.tsx`, `Aviso.tsx`, `AvisoDeErro.tsx`, `EstadoVazio.tsx`, `Carregando.tsx`, `Esqueleto.tsx`, `CabecalhoDePagina.tsx`, `Secao.tsx`, `Progresso.tsx`.
- Test: um `*.test.tsx` ao lado de cada componente (os pequenos podem dividir um arquivo: `exibicao.test.tsx`), mais `src\shared\api\mensagemDeErro.test.ts`.

**Interfaces:**
- Consumes: `cn` (Task 1); `iniciais` (Task 2); `ErroDeApi` (fatia 0).
- Produces (`ReactNode`, `LucideIcon` de `lucide-react`; `ComponentPropsWithRef` do React):
  - `mensagemDeErro(erro: unknown): string`. Tabela: `ErroDeApi` com `status` 0 ou qualquer erro que não seja `ErroDeApi` → `Não foi possível conectar. Tente novamente.`; 400 → `detail` ou `Confira os dados informados.`; 401 → `Sua sessão expirou. Entre de novo.`; 403 → `detail` ou `Você não tem permissão para esta ação.`; 404 → `detail` ou `O registro não foi encontrado.`; 409 e 422 → `detail` ou `title` (**nunca** `regraDoBanco` sozinha); 429 → `Muitas tentativas. Aguarde um minuto e tente de novo.`; `>= 500` → `Erro no servidor. Tente novamente em instantes.`
  - `botaoClasses.ts`: `classesDoBotao(opcoes: { variante?: VarianteDeBotao; tamanho?: TamanhoDeBotao; larguraTotal?: boolean }): string`.
  - `Botao.tsx`: `type VarianteDeBotao = 'primario' | 'secundario' | 'texto' | 'perigo' | 'claro'`; `type TamanhoDeBotao = 'normal' | 'pequeno'`; `type BotaoProps = ComponentPropsWithRef<'button'> & { variante?: VarianteDeBotao; tamanho?: TamanhoDeBotao; carregando?: boolean; icone?: LucideIcon; larguraTotal?: boolean }`; `function Botao(props: BotaoProps)` (padrão `type="button"`, `variante="primario"`, `tamanho="normal"`; `data-variante`, `data-tamanho`).
  - `BotaoLink.tsx`: `type BotaoLinkProps = LinkProps & { variante?: VarianteDeBotao; tamanho?: TamanhoDeBotao; larguraTotal?: boolean }`; `function BotaoLink(props: BotaoLinkProps)` (o `Link` do `react-router`).
  - `Selo.tsx`: `type TomDoSelo = 'neutro' | 'sucesso' | 'info' | 'alerta' | 'erro' | 'contorno'`; `function Selo(props: { tom?: TomDoSelo; children: ReactNode; className?: string })` (`data-tom`).
  - `Cartao.tsx`: `type TomDoCartao = 'branco' | 'suave' | 'destaque' | 'escuro' | 'tracejado'`; `function Cartao(props: ComponentPropsWithoutRef<'section'> & { tom?: TomDoCartao; semRecuo?: boolean })` (`data-tom`).
  - `Avatar.tsx`: `type TamanhoDoAvatar = 'lista' | 'menu' | 'ficha'` (36, 42 e 76 px); `function Avatar(props: { nome: string; tamanho?: TamanhoDoAvatar })` (iniciais de `iniciais(nome)`, `aria-hidden`).
  - `Bolinha.tsx`: `function Bolinha(props: { valor: number | null | undefined })` (devolve `null` se for 0, vazio ou negativo; `99+` acima de 99).
  - `LogoR.tsx`: `function LogoR(props: { tom?: 'claro' | 'escuro' })` (o quadrado com o "R" de `04-visual.md`, seção 6; `aria-hidden`).
  - `Aviso.tsx`: `type TomDoAviso = 'info' | 'alerta' | 'erro' | 'sucesso'`; `function Aviso(props: { tom?: TomDoAviso; titulo?: string; children: ReactNode; acao?: ReactNode })` (`data-tom`; `role="alert"` no tom `erro`, `role="status"` nos outros).
  - `AvisoDeErro.tsx`: `function AvisoDeErro(props: { erro: unknown; aoTentarDeNovo?: () => void; titulo?: string })` (mostra `mensagemDeErro(erro)`; o botão `Tentar de novo` só existe com `aoTentarDeNovo`).
  - `EstadoVazio.tsx`: `function EstadoVazio(props: { titulo: string; descricao?: string; icone?: LucideIcon; acao?: ReactNode })`.
  - `Carregando.tsx`: `function Carregando(props: { texto?: string })` (`role="status"`, padrão `Carregando…`); `Esqueleto.tsx`: `function Esqueleto(props: { className?: string })` (bloco `aria-hidden` com `animate-pulse`).
  - `CabecalhoDePagina.tsx`: `function CabecalhoDePagina(props: { titulo: string; subtitulo?: ReactNode; acoes?: ReactNode })` (o `<h1>`, o subtítulo e os botões à direita: PNG 01, 12, 33); `Secao.tsx`: `function Secao(props: { titulo: string; acao?: ReactNode; children: ReactNode })` (`<section>` com `<h2>`; PNG 00).
  - `Progresso.tsx`: `function Progresso(props: { valor: number; maximo?: number; rotulo: string })` (Base UI `Progress.Root/Track/Indicator`; `role="progressbar"` com `aria-valuenow`, nome = `rotulo`).

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// mensagemDeErro.test.ts
it.each([[0,'Não foi possível conectar. Tente novamente.'],[401,'Sua sessão expirou. Entre de novo.'],[429,'Muitas tentativas. Aguarde um minuto e tente de novo.'],[500,'Erro no servidor. Tente novamente em instantes.'],[503,'Erro no servidor. Tente novamente em instantes.']])('status %i')
it('403, 404, 409 e 422 mostram o detail quando vem e um texto padrao quando nao')   // 409 sem detail → title; 422 com regraDoBanco e detail → só o detail
it('erro que nao e ErroDeApi vira a mensagem de conexao')                           // mensagemDeErro(new Error('x')) === 'Não foi possível conectar. Tente novamente.'
// Botao.test.tsx
it('Botao tem type=button por padrao e chama onClick')
it('Botao carregando fica desabilitado, aria-busy e mantem o nome acessivel (o texto nao muda)')   // getByRole('button', { name: 'Salvar' }); onClick não é chamado
it('Botao expoe data-variante e data-tamanho') ; it('Botao com icone mostra um svg aria-hidden')
it('BotaoLink e um link com href e as classes de botao')                          // dentro de MemoryRouter; getByRole('link', { name: 'Ver' }) com href '/rh'
// exibicao.test.tsx
it('Selo e Cartao expoem data-tom') ; it('Avatar mostra as iniciais e some para leitor de tela')   // 'Ana Souza' → 'AS', aria-hidden
it('Bolinha some com 0, null e undefined, mostra 4 e escreve 99+ acima de 99')
it('Aviso de erro usa role alert e os outros role status')
it('AvisoDeErro mostra a mensagem do erro e so tem Tentar de novo com a funcao')   // clicar chama a função uma vez
it('EstadoVazio mostra titulo, descricao e acao') ; it('Carregando tem role status') ; it('CabecalhoDePagina escreve h1, subtitulo e acoes') ; it('Secao e uma regiao nomeada pelo h2')
it('Progresso expoe role progressbar com aria-valuenow e o nome do rotulo')        // valor 18, maximo 24
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/ui src/shared/api/mensagemDeErro`
  Expected: FAIL.
- [ ] **Step 3: Implementar** os arquivos com as assinaturas acima, **com as medidas e os tokens de `04-visual.md`** (seção 3 para tamanhos; seção 1 para tons: `Selo` usa `sucesso`, `info`, `alerta`, `erro` e `neutro` de `04-visual.md`, 1; `contorno` é borda `primaria-media` e texto `primaria`; `Cartao` tons e raios de `04-visual.md`, 3). `classesDoBotao` usa `class-variance-authority` (`cva`). Ícones do `lucide-react` com `size={16}` (mapa em `04-visual.md`, 5). `Aviso` e `AvisoDeErro` usam `CircleAlert`, `TriangleAlert`, `Info` e `CircleCheck`; `Botao carregando` troca o ícone por `LoaderCircle` com `animate-spin`.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/ui src/shared/api/mensagemDeErro`
  Expected: PASS.
- [ ] **Step 5: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 6: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): botao, selo, cartao, avisos, estados e mensagemDeErro"; git push`

---

### Task 4: Campos de formulário

**Files:**
- Create (em `front-end\src\shared\ui\`): `Campo.tsx`, `Entrada.tsx`, `AreaDeTexto.tsx`, `Selecao.tsx`, `CaixaDeSelecao.tsx`, `Interruptor.tsx`, `OpcoesEmCartao.tsx`, `ComboDeBusca.tsx`, `CampoDeBusca.tsx`.
- Test: um `*.test.tsx` ao lado de cada.

**Interfaces:**
- Consumes: `cn`, `Botao` (Task 3), `mascararCpf` (Task 2, só no teste).
- Produces:
  - `Campo.tsx`: `type CampoProps = { rotulo: string; dica?: string; erro?: string | null; children: ReactNode; className?: string }`; `function Campo(props: CampoProps)` (Base UI `Field.Root invalid={!!erro}`, `Field.Label`, o `children`, `Field.Description` para a `dica`, `Field.Error match` para o `erro`; o controle dentro dele é nomeado pelo rótulo: **provado** para `Input`, `Select`, `Combobox` e `textarea`).
  - `Entrada.tsx`: `type EntradaProps = Omit<ComponentPropsWithRef<'input'>, 'size'> & { icone?: LucideIcon; mascara?: (valor: string) => string }`; `function Entrada(props: EntradaProps)` (o `Input` do Base UI; com `mascara`, aplica-a a `e.target.value` **antes** de repassar o `onChange`, o que serve ao `register` do React Hook Form; campo com `aria-invalid` quando o `Campo` pai está inválido).
  - `AreaDeTexto.tsx`: `function AreaDeTexto(props: ComponentPropsWithRef<'textarea'> & { limite?: number })` (`Field.Control render={<textarea />}`; com `limite`, mostra o contador `n/limite`).
  - `Selecao.tsx`: `type OpcaoDeSelecao<V extends string = string> = { valor: V; rotulo: string; desabilitada?: boolean }`; `function Selecao<V extends string>(props: { opcoes: readonly OpcaoDeSelecao<V>[]; valor: V | null; aoMudar: (valor: V | null) => void; rotulo: string; placeholder?: string; desabilitado?: boolean; carregando?: boolean; invalido?: boolean; aoPerderFoco?: () => void })` (`Select.*`; `rotulo` é o `aria-label` quando não está dentro de um `Campo`; `carregando` mostra `Carregando…` e desabilita).
  - `CaixaDeSelecao.tsx`: `function CaixaDeSelecao(props: { marcada: boolean; aoMudar: (marcada: boolean) => void; rotulo: ReactNode; descricao?: string; desabilitada?: boolean })` (`Checkbox.Root` + `Checkbox.Indicator` com `Check`; o rótulo clicável).
  - `Interruptor.tsx`: `function Interruptor(props: { ligado: boolean; aoMudar: (ligado: boolean) => void; rotulo: string; desabilitado?: boolean })` (`Switch.Root`/`Thumb`).
  - `OpcoesEmCartao.tsx`: `type OpcaoEmCartao<V extends string = string> = { valor: V; titulo: string; descricao?: string; desabilitada?: boolean }`; `function OpcoesEmCartao<V extends string>(props: { opcoes: readonly OpcaoEmCartao<V>[]; valor: V | null; aoMudar: (valor: V) => void; rotulo: string; colunas?: 2 | 3 | 4 })` (`RadioGroup` com `Radio.Root` desenhado como cartão: PNG 10, 17, 28).
  - `ComboDeBusca.tsx`: `type OpcaoDoCombo = { valor: string; rotulo: string; apoio?: string }`; `function ComboDeBusca(props: { opcoes: readonly OpcaoDoCombo[]; valor: string | null; aoMudar: (valor: string | null) => void; rotulo: string; placeholder?: string; textoSemResultado?: string; aoDigitar?: (texto: string) => void; carregando?: boolean })` (`Combobox.*`; filtra aqui mesmo, ignorando acento e maiúscula; **com `aoDigitar`** não filtra aqui: a busca é do servidor; `textoSemResultado` padrão `Nenhum resultado.`).
  - `CampoDeBusca.tsx`: `function CampoDeBusca(props: { valor: string; aoMudar: (texto: string) => void; placeholder?: string; rotulo?: string; esperaMs?: number })` (a lupa `Search` e um `Entrada`; chama `aoMudar` **300 ms** (`esperaMs`) depois da última tecla, ou na hora com Enter; se `valor` mudar por fora, o texto acompanha).

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// Campo.test.tsx
it('Campo liga o rotulo ao Entrada, a dica ao campo e o erro como descricao')          // getByLabelText('Nome completo'); toHaveAccessibleDescription contém 'Obrigatório'; toBeInvalid()
it('Campo sem erro nao esta invalido e nao mostra mensagem')
it('Campo nomeia pelo rotulo o Selecao, o ComboDeBusca e a AreaDeTexto')                 // getByRole('combobox', { name: 'Cargo' }) ...; getByRole('textbox', { name: 'Descrição' })
// Entrada.test.tsx
it('Entrada com mascara escreve o cpf formatado enquanto digita')                      // digitar '84639271069' → value '846.392.710-69'; onChange recebe o valor mascarado
it('Entrada aceita ref e props do register do react-hook-form')                         // coberto também na Task 8
// AreaDeTexto.test.tsx: it('AreaDeTexto com limite mostra o contador')   // 'abc' → '3/280'
// Selecao.test.tsx
it('Selecao mostra o placeholder, abre, escolhe e chama aoMudar com o valor')           // user.click(getByRole('combobox')); click(option 'Analista') → aoMudar('analista')
it('Selecao carregando mostra Carregando… e nao abre') ; it('Selecao desabilitado nao abre')
// CaixaDeSelecao.test.tsx / Interruptor.test.tsx
it('CaixaDeSelecao marca ao clicar no rotulo e chama aoMudar(true)') ; it('Interruptor liga e desliga (role switch)')
// OpcoesEmCartao.test.tsx
it('OpcoesEmCartao e um radiogroup nomeado, escolhe por clique e por seta do teclado')   // aria-checked muda; aoMudar com o valor
it('OpcoesEmCartao mostra titulo e descricao de cada cartao')
// ComboDeBusca.test.tsx
it('ComboDeBusca filtra digitando, sem diferenca de acento, e escolhe')                 // 'logis' acha 'Logística'
it('ComboDeBusca mostra Nenhum resultado. e, com aoDigitar, chama a funcao e nao filtra por conta propria')
// CampoDeBusca.test.tsx   (vi.useFakeTimers({ shouldAdvanceTime: true }); userEvent.setup({ advanceTimers: vi.advanceTimersByTime }))
it('CampoDeBusca chama aoMudar uma vez, 300 ms depois da ultima tecla (Review Focus 5)')  // digitar 'ana' rápido → 0 chamadas; avançar 300 ms → 1 chamada com 'ana'
it('CampoDeBusca com Enter chama na hora') ; it('CampoDeBusca acompanha o valor que muda por fora')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/ui`
  Expected: FAIL nos novos arquivos.
- [ ] **Step 3: Implementar** com as assinaturas e as peças de `04-visual.md`, seção 7; medidas e cores de `04-visual.md`, seção 3 (campo de 44 px, raio `campo`, borda `borda-campo`; foco e erro: seção 10). Se o rótulo do `Campo` não nomear algum controle, ligue com `id`/`aria-labelledby` (o teste do `Campo` acusa).
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/ui`
  Expected: PASS.
- [ ] **Step 5: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 6: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): campo, entrada, selecao, combo, caixa, interruptor, opcoes em cartao e busca"; git push`

---

### Task 5: Abas, tabela, paginação, etapas e menu de ações

**Files:**
- Create (em `front-end\src\shared\ui\`): `Abas.tsx`, `Tabela.tsx`, `Paginacao.tsx`, `Etapas.tsx`, `MenuDeAcoes.tsx`.
- Test: um `*.test.tsx` ao lado de cada.

**Interfaces:**
- Consumes: `AvisoDeErro`, `EstadoVazio`, `Esqueleto`, `Botao` (Task 3); `formatarNumero` (Task 2); `Esquema` (fatia 0).
- Produces:
  - `Abas.tsx`: `type ItemDeAba<V extends string = string> = { valor: V; rotulo: string; contagem?: number | null; desabilitada?: boolean }`; `function Abas<V extends string>(props: { itens: readonly ItemDeAba<V>[]; valor: V; aoMudar: (valor: V) => void; rotulo: string; variante?: 'pilula' | 'sublinhada'; renderizarPainel?: (valor: V) => ReactNode })` (`Tabs.*`; o nome da aba inclui a contagem: `Ativos 101`; `data-variante`; com `renderizarPainel`, só o painel da aba atual existe).
  - `Tabela.tsx`: `type DirecaoDeOrdenacao = 'ASC' | 'DESC'`; `type OrdenacaoDaTabela = { campo: string; direcao: DirecaoDeOrdenacao } | null`; `type ColunaDaTabela<L> = { chave: string; titulo: string; celula: (linha: L) => ReactNode; ordenavel?: boolean; alinhamento?: 'esquerda' | 'direita'; largura?: string }`; `function Tabela<L>(props: { rotulo: string; colunas: readonly ColunaDaTabela<L>[]; linhas: readonly L[] | undefined; chaveDaLinha: (linha: L) => string | number; carregando?: boolean; erro?: unknown; aoTentarDeNovo?: () => void; vazio?: ReactNode; ordenacao?: OrdenacaoDaTabela; aoOrdenar?: (campo: string) => void; detalhe?: (linha: L) => ReactNode | null; rodape?: ReactNode; linhasDoEsqueleto?: number })`. Regras: `erro` vence tudo (`AvisoDeErro` no lugar do corpo); `linhas` indefinido e `carregando` mostram `linhasDoEsqueleto` (5) linhas de `Esqueleto`; `linhas` vazio mostra `vazio`; com `carregando` **e** linhas (consulta nova com dados antigos), mantém as linhas com `aria-busy`; o cabeçalho de coluna `ordenavel` é um `<button>` com `aria-sort` (`ascending`, `descending` ou `none`) e ícone (`ArrowUp`, `ArrowDown`, `ArrowUpDown`); `aoOrdenar(campo)` com a `chave` da coluna; `detalhe` devolve uma linha extra sob a linha (o "Detalhes" do log, PNG 31); `rodape` fica dentro do cartão (a `Paginacao`); a tabela é `<table aria-label={rotulo}>` com `<th scope="col">`; o contêiner tem `overflow-x-auto` (`04-visual.md`, 9).
  - `Paginacao.tsx`: `function Paginacao(props: { dados: Pick<Esquema<'Paginacao'>, 'pagina' | 'tamanhoPagina' | 'total' | 'totalPaginas'>; aoMudar: (pagina: number) => void; resumo?: (de: number, ate: number, total: number) => string })` (texto padrão `Mostrando 1 a 8 de 101` com `formatarNumero`; `total` 0 → `Nenhum registro`; botões `Anterior` e `Próxima` desabilitados nos extremos e quando `totalPaginas` é 0 ou 1).
  - `Etapas.tsx`: `function Etapas(props: { etapas: readonly { chave: string; rotulo: string }[]; atual: string })` (`<ol aria-label="Etapas">`; a atual com `aria-current="step"`; as anteriores com o texto `concluída` só para leitor de tela).
  - `MenuDeAcoes.tsx`: `type AcaoDoMenu = { chave: string; rotulo: string; aoEscolher: () => void; perigo?: boolean; desabilitada?: boolean }`; `function MenuDeAcoes(props: { acoes: readonly AcaoDoMenu[]; rotulo: string })` (botão só de ícone `Ellipsis` de 44 px, nomeado por `rotulo`; `Menu.*`).

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// Abas.test.tsx
it('Abas mostra o rotulo com a contagem, marca a atual e chama aoMudar')              // getByRole('tab', { name: 'Ativos 101' }) aria-selected; clicar em 'Inativos 7' → aoMudar('inativos')
it('Abas troca de aba com as setas e expoe data-variante') ; it('Abas com renderizarPainel mostra so o painel da aba atual')
// Tabela.test.tsx
it('Tabela escreve colunas e linhas e se chama pelo rotulo')                          // getByRole('table', { name: 'Colaboradores' }); 3 colunas; 2 linhas
it('Tabela com coluna ordenavel tem botao com aria-sort e chama aoOrdenar com a chave') // 'Nome' ordenavel; ordenacao { campo: 'nome', direcao: 'ASC' } → aria-sort="ascending"; clicar → aoOrdenar('nome'); coluna sem ordenavel não tem botão
it('Tabela carregando sem linhas mostra o esqueleto, e com linhas as mantem com aria-busy')
it('Tabela com erro mostra a mensagem e Tentar de novo no lugar das linhas')          // ErroDeApi 500 → 'Erro no servidor...'; clicar chama aoTentarDeNovo
it('Tabela com linhas vazias mostra o vazio') ; it('Tabela mostra o detalhe sob a linha e o rodape dentro do cartao')
// Paginacao.test.tsx
it('Paginacao escreve Mostrando 1 a 8 de 101 e desabilita Anterior na primeira pagina')   // { pagina: 1, tamanhoPagina: 8, total: 101, totalPaginas: 13 }
it('Paginacao na ultima pagina escreve 97 a 101 e desabilita Proxima') ; it('Paginacao chama aoMudar com pagina anterior e proxima')
it('Paginacao com total 0 escreve Nenhum registro e desabilita os dois') ; it('Paginacao escreve 1.284 com ponto de milhar')
// Etapas.test.tsx: it('Etapas marca a atual com aria-current=step e as anteriores como concluidas')
// MenuDeAcoes.test.tsx: it('MenuDeAcoes abre, chama a acao escolhida e fecha') ; it('MenuDeAcoes nao chama acao desabilitada')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/ui`
  Expected: FAIL nos novos arquivos.
- [ ] **Step 3: Implementar.** Visual: abas em pílula e sublinhada, tabela (cabeçalho de 45 px, linhas de 73,5 px, borda `borda`, raio `cartao`), paginação e etapas conforme `04-visual.md`, seção 3.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/ui`
  Expected: PASS.
- [ ] **Step 5: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 6: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): abas, tabela com ordenacao, paginacao, etapas e menu de acoes"; git push`

---

### Task 6: Janela, janela de confirmação e notificações

**Files:**
- Create (em `front-end\src\shared\ui\`): `Janela.tsx`, `JanelaDeConfirmacao.tsx`, `NotificacoesProvider.tsx`, `useNotificacao.ts`.
- Test: `Janela.test.tsx`, `JanelaDeConfirmacao.test.tsx`, `Notificacoes.test.tsx`.

**Interfaces:**
- Consumes: `Botao`, `Campo`, `Entrada`, `AvisoDeErro` (Tasks 3 e 4); `plural` (Task 2).
- Produces:
  - `Janela.tsx`: `type JanelaProps = { aberta: boolean; aoFechar: () => void; titulo: string; descricao?: string; posicao?: 'centro' | 'direita'; tamanho?: 'media' | 'larga' | 'extra'; largura?: 'estreita' | 'media' | 'larga'; rodape?: ReactNode; fecharAoClicarFora?: boolean; children: ReactNode }`; `function Janela(props: JanelaProps)`. `Dialog.*` do Base UI. **Centro** (`posicao` padrão): `tamanho` 640, 1040 ou 1180 px (`media`, `larga`, `extra`). **Direita** (painel lateral): `largura` 580, 600 ou 864 px (`estreita`, `media`, `larga`). Cabeçalho com o `titulo` (`t2`) e o botão `X` de 44 px (`aria-label="Fechar"`), corpo que **rola por dentro**, `rodape` fixo embaixo, teto de `calc(100vw - 48px)` e de altura da janela menos 48 px (`04-visual.md`, 3 e 9); atrás, `bg-scrim`. `data-posicao`. `fecharAoClicarFora` padrão `true`.
  - `JanelaDeConfirmacao.tsx`: `type JanelaDeConfirmacaoProps = { aberta: boolean; aoCancelar: () => void; aoConfirmar: () => void | Promise<void>; titulo: string; confirmando?: boolean; erro?: unknown; rotuloDeCancelar?: string } & ( { variante?: 'simples'; descricao: ReactNode; rotuloDeConfirmar?: string; perigo?: boolean } | { variante: 'digitar'; descricao: ReactNode; textoParaDigitar: string; rotuloDeConfirmar?: string } | { variante: 'pendentes'; pendentes: number | null; assunto: string; consequencia?: string; rotuloDeConfirmar?: string } )`; `function JanelaDeConfirmacao(props: JanelaDeConfirmacaoProps)`. `AlertDialog.*` (`role="alertdialog"`). Padrões: `rotuloDeCancelar` `Cancelar`; `rotuloDeConfirmar` `Confirmar` (`simples` e `digitar`) e `Continuar mesmo assim` (`pendentes`). **`digitar`** (o "digite desativar"): um `Entrada` com o rótulo `Digite "{textoParaDigitar}" para confirmar` e o botão de confirmar (variante `perigo`) **desabilitado** até o texto digitado ser igual ao pedido, sem diferença de maiúscula nem de espaço nas pontas; limpa o campo ao fechar. **`pendentes`** escreve `{N} pessoas ainda não concluíram {assunto}. {consequencia} Quer mesmo continuar?` (com `plural`: `1 pessoa ainda não concluiu`); com `pendentes: null` (formulário anônimo, que não revela quantos faltam: `CONVENCOES.md` seção 11) escreve `Ainda há pessoas que não concluíram {assunto}. {consequencia} Quer mesmo continuar?`. Com `confirmando`, os dois botões ficam desabilitados e os rótulos **não mudam**; `erro` aparece num `AvisoDeErro` dentro da janela.
  - `NotificacoesProvider.tsx`: `function NotificacoesProvider(props: { children: ReactNode })` (`Toast.Provider` + `Toast.Portal` + `Toast.Viewport` com `aria-label="Notificações"` e a lista de `Toast.Root`; cartão branco com borda `borda`, canto inferior direito; some sozinho em 5 s). `useNotificacao.ts`: `function useNotificacao(): { sucesso(mensagem: string): void; erro(mensagem: string): void; informacao(mensagem: string): void }` (sobre `Toast.useToastManager`; `data-tom` por tipo).

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// Janela.test.tsx
it('Janela aberta tem role dialog nomeado pelo titulo, mostra descricao, corpo e rodape')
it('Janela fecha com Escape, com o X e clicando fora, e nao fecha clicando fora com fecharAoClicarFora false')   // aoFechar chamado 1 vez em cada
it('Janela fechada nao escreve nada') ; it('Janela expoe data-posicao centro e direita')
// JanelaDeConfirmacao.test.tsx
it('simples: tem role alertdialog; Confirmar chama aoConfirmar e Cancelar e Escape chamam aoCancelar')
it('digitar: o botao so habilita com "desativar" (sem diferenca de maiuscula ou espaco nas pontas) e Enter confirma')   // 'Desativar ' habilita; 'desativa' não; ao reabrir o campo está vazio
it('pendentes mostra "38 pessoas ainda nao concluiram este treinamento." e "1 pessoa ainda nao concluiu"')
it('pendentes com null nao diz quantas pessoas faltam')                                                          // texto 'Ainda há pessoas que não concluíram este formulário.'
it('confirmando desabilita os dois botoes e mantem os rotulos') ; it('erro mostra a mensagem dentro da janela')
// Notificacoes.test.tsx
it('useNotificacao().sucesso mostra a mensagem na regiao Notificacoes e o botao fecha')    // findByText('Pedido enviado.'); getByLabelText('Fechar aviso'); sumiu
it('erro e informacao expoem data-tom')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/ui/Janela src/shared/ui/Notificacoes`
  Expected: FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/ui/Janela src/shared/ui/Notificacoes`
  Expected: PASS.
- [ ] **Step 5: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 6: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): janela, janela de confirmacao com digitar e pendentes, e notificacoes"; git push`

---

### Task 7: Markdown seguro

**Files:**
- Create: `front-end\src\shared\ui\Markdown.tsx` (a dependência `react-markdown` já veio da Task 1 da fatia 0).
- Test: `src\shared\ui\Markdown.test.tsx`.

**Interfaces:**
- Produces: `function Markdown(props: { texto: string; className?: string })`: `react-markdown` 10.1.0 com **só** `allowedElements={['p','br','strong','em','del','ul','ol','li','a','blockquote','code','pre','hr','h1','h2','h3','h4']}` e `unwrapDisallowed`; sem `rehype-raw` e sem `remark-gfm`; `urlTransform` padrão (que zera `javascript:`); `components` para `a` (`target="_blank"`, `rel="noopener noreferrer"`) e para os títulos (`#` e `##` viram `<h3>`; `###` e `####` viram `<h4>`: o `<h1>` é o da página e o `<h2>` o do bloco). Texto simples (aviso `SIMPLES`, até 280 caracteres) **não** passa por aqui: mostre-o em `<p className="whitespace-pre-line">`.

- [ ] **Step 1: Conferir a dependência** (ela veio com a pilha, na Task 1 da fatia 0: **não** rode `npm install`). Run: `Select-String -Path front-end\package.json -Pattern '"react-markdown": "10.1.0"'; Test-Path front-end\node_modules\react-markdown`
  Expected: uma linha com `"react-markdown": "10.1.0",` e `True`. Se faltar, o plano está desatualizado: é dúvida (A7), não `npm install`.
- [ ] **Step 2: Escrever os testes que falham.**

```tsx
// Markdown.test.tsx
it('Markdown escreve negrito, italico, lista, citacao e codigo')
it('Markdown mostra HTML cru como TEXTO e nao executa nada (Review Focus 3)')   // '<script>window.__xss = 1</script> <img src=x onerror="window.__xss = 2"> <b onclick="1">n</b>': nenhum script/img/b no DOM; window.__xss undefined; o texto '<script>' aparece escrito
it('Markdown neutraliza javascript: nos links')                                // '[x](javascript:alert(1))' → a href '' ; '[ok](https://exemplo.com)' → href mantido
it('Markdown nao cria img e nao pede imagem de terceiro')                      // '![a](https://exemplo.com/x.png)' → querySelector('img') null
it('Markdown nao deixa style nem <style> chegar ao DOM')                       // '<div style="color:red">x</div> <style>body{color:red}</style>' → querySelectorAll('[style]').length 0, sem style
it('Markdown abre link em nova aba com noopener noreferrer') ; it('Markdown rebaixa # para h3 e ### para h4') ; it('Markdown de texto vazio nao escreve nada')
```

- [ ] **Step 3: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/ui/Markdown`
  Expected: FAIL (`Markdown.tsx` não existe).
- [ ] **Step 4: Implementar** `Markdown.tsx` como na interface acima.
- [ ] **Step 5: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/ui/Markdown`
  Expected: PASS.
- [ ] **Step 6: Controle: prove que o teste pega o defeito.** Tire temporariamente `allowedElements` e `unwrapDisallowed`: o teste "nao cria img" deve FALHAR. Restaure.
- [ ] **Step 7: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando (o `semHtmlCru` da Task 1 continua passando: o componente não usa HTML cru).
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md` (anote `react-markdown 10.1.0`); Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): markdown seguro com react-markdown, sem html cru nem imagem"; git push`

---

### Task 8: Formulários: esquemas, erros do servidor e `FormularioComErroDoServidor`

**Files:**
- Modify: `front-end\src\shared\zod.ts` (acrescenta mensagens em português ao `z.config`).
- Create: `front-end\src\shared\form\esquemas.ts`, `aplicarErrosDoServidor.ts`, `FormularioComErroDoServidor.tsx`.
- Test: `src\shared\zod.test.ts` (acrescenta), `src\shared\form\esquemas.test.ts`, `aplicarErrosDoServidor.test.ts`, `FormularioComErroDoServidor.test.tsx`.

**Interfaces:**
- Consumes: `ErroDeApi` (fatia 0), `mensagemDeErro` (Task 3), `Campo`, `Entrada`, `Selecao`, `CaixaDeSelecao`, `Botao`, `Aviso` (Tasks 3 e 4), `cpfValido`, `telefoneValido`, `somenteDigitos` (Task 2).
- Produces:
  - `zod.ts`: o `z.config({ jitless: true })` **continua**, e passa a levar também um `customError` em português: `invalid_type` → `Campo obrigatório.`; `too_small` de texto → `Campo obrigatório.` (mínimo 1) ou `Use no mínimo {n} caracteres.`; `too_big` de texto → `Use no máximo {n} caracteres.`; `invalid_format` de e-mail → `Informe um e-mail válido.`; o resto cai no texto do próprio esquema. **A mensagem escrita no esquema vence a global.** (Medido: `z.config` junta as opções, `jitless` continua `true`, e o `locales.pt` do zod é de Portugal, por isso não se usa.)
  - `esquemas.ts`: `campoTexto(mensagem?: string)` (`z.string().trim().min(1, mensagem ?? 'Campo obrigatório.')`); `campoEmail()`; `campoCpf()` (aceita com ou sem pontuação, **devolve só dígitos**, recusa com `CPF inválido.` por `cpfValido`); `campoTelefone()` (só dígitos, 10 ou 11, `Telefone inválido. Use DDD e número.`); `campoData()` (texto `YYYY-MM-DD` de data que existe, `Data inválida.`; **o valor continua texto**, sem `Date`); `campoHora()` (`HH:mm`, `Hora inválida.`); `opcional<T extends z.ZodType>(esquema: T)` (texto vazio vira `null`, senão vale `esquema`).
  - `aplicarErrosDoServidor.ts`: `function aplicarErrosDoServidor(erro: ErroDeApi, opcoes: { definirErro: (campo: string, mensagem: string) => void; camposDoFormulario: readonly string[] }): { mensagemGeral: string | null }`. Para cada `[campo, mensagens]` de `erro.errors` cujo nome (com a primeira letra em minúscula) **está** em `camposDoFormulario`, chama `definirErro(campo, mensagens.join(' '))`. As chaves que o formulário **não tem** não somem: entram em `mensagemGeral`. Sem nenhum campo casado, `mensagemGeral` é `erro.detail ?? mensagemDeErro(erro)`; com campos casados e nenhuma sobra, `null`.
  - `FormularioComErroDoServidor.tsx`: `type FormularioComErroDoServidorProps<E extends z.ZodType> = { esquema: E; valoresIniciais: z.input<E>; aoEnviar: (valores: z.output<E>) => Promise<unknown> | unknown; aoSucesso?: () => void; children: (formulario: UseFormReturn<z.input<E>, unknown, z.output<E>>) => ReactNode; className?: string; id?: string }`; `function FormularioComErroDoServidor<E extends z.ZodType>(props: FormularioComErroDoServidorProps<E>)`. `useForm` com `zodResolver(esquema)` e `defaultValues: valoresIniciais`; `<form noValidate>`; `aoEnviar` recebe os valores **já validados e transformados** (`z.output`); se `aoEnviar` lançar `ErroDeApi`, mostra os erros nos campos e a `mensagemGeral` num `Aviso tom="erro"` acima dos filhos; outro erro vira `mensagemDeErro`; **ignora um segundo envio enquanto o primeiro está em andamento** (RHF não o impede); `camposDoFormulario = Object.keys(valoresIniciais)`.

- [ ] **Step 1: Escrever os testes que falham.**

```tsx
// zod.test.ts (acrescenta)
it('mensagens globais em portugues e a do esquema vence')   // z.string().min(1) vazio → 'Campo obrigatório.'; z.email() 'x' → 'Informe um e-mail válido.'; z.string().min(1, 'Informe o nome.') → 'Informe o nome.'; z.config().jitless continua true
// esquemas.test.ts
it('campoCpf aceita com pontuacao, devolve so digitos e recusa digito errado')     // '846.392.710-69' → '84639271069'; '111.111.111-11' → erro 'CPF inválido.'
it('campoTelefone, campoData, campoHora e opcional')                              // '(11) 98765-4410' → '11987654410'; '2026-02-30' → 'Data inválida.'; '25:00' → 'Hora inválida.'; opcional(campoCpf())('') → null
// aplicarErrosDoServidor.test.ts
it('400 com errors marca cada campo que o formulario tem')                         // { cpf: ['CPF já cadastrado.'], emailCorporativo: [...] } → definirErro chamado 2 vezes; mensagemGeral null
it('chave que o formulario nao tem vai para a mensagem geral (nao some)')          // { nivelDeAcesso: ['Valor inválido.'] } → mensagemGeral contém 'Valor inválido.'
it('chave com inicial maiuscula (Nome) casa com o campo nome')
it('422 sem errors usa o detail; sem detail usa a mensagem de erro') ; it('status 0 vira a mensagem de conexao')
// FormularioComErroDoServidor.test.tsx   (esquema { nome: campoTexto('Informe o nome.'), cpf: campoCpf(), cargo: campoTexto('Escolha o cargo.'), convite: z.boolean() }; filhos: Campo+Entrada(register), Selecao e CaixaDeSelecao por Controller, Botao 'Salvar' disabled com isSubmitting)
it('valida no cliente, mostra a mensagem de cada campo e nao chama aoEnviar')
it('envia o z.output: cpf so com digitos, cargo, convite') ; it('mascara de cpf do Entrada funciona junto do register')
it('400 do servidor marca o campo com aria-invalid e a mensagem do contrato')       // aoEnviar lança ErroDeApi(400, ..., { errors: { cpf: ['CPF já cadastrado.'] } })
it('400 com campo desconhecido, 422 e falha de rede aparecem no aviso geral')
it('clique duplo em Salvar chama aoEnviar uma vez (Review Focus)') ; it('aoSucesso e chamado so quando aoEnviar nao lanca')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/zod src/shared/form`
  Expected: FAIL.
- [ ] **Step 3: Implementar** os arquivos com as assinaturas. Use `zodResolver` de `@hookform/resolvers/zod` e `useForm<z.input<E>, unknown, z.output<E>>`; `useRef` para travar o envio duplo.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/zod src/shared/form`
  Expected: PASS.
- [ ] **Step 5: Controle.** Tire a trava do envio duplo: o teste do clique duplo deve FALHAR. Restaure.
- [ ] **Step 6: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 7: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): esquemas zod em portugues e formulario que mostra os erros do servidor"; git push`

---

### Task 9: `$api`, `QueryClientProvider` e a renovação do token quando uma chamada recebe 401

**Files:**
- Modify: `front-end\src\shared\api\clienteApi.ts` (uma opção nova em `criarClienteHttp`), `front-end\src\features\autenticacao\AuthProvider.tsx` (registra a renovação), `front-end\src\main.tsx`.
- Create: `front-end\src\shared\api\renovadorDeAcesso.ts`, `api.ts`, `consultas.ts`, `invalidar.ts`; `front-end\src\app\Provedores.tsx`, `LimparCacheAoSair.tsx`; `front-end\src\test\renderizar.tsx`, `sessao.ts`.
- Test: `src\shared\api\renovacaoNo401.test.ts`, `api.test.tsx`, `invalidar.test.ts`; `src\features\autenticacao\AuthProvider.test.tsx` (acrescenta); `src\app\Provedores.test.tsx`.

**Interfaces:**
- Consumes: `criarClienteHttp`, `ErroDeApi`, `URL_DA_API` (fatia 0, Task 2), `tokenDeAcesso`, `AuthProvider`, `useAuth` (Task 3 da fatia 0), `NotificacoesProvider` (Task 6).
- Produces:
  - `renovadorDeAcesso.ts`: `const renovadorDeAcesso: { definir(renovar: (() => Promise<string | null>) | null): void; renovar(): Promise<string | null> }` (variável de módulo; `renovar()` devolve `null` se ninguém registrou).
  - `clienteApi.ts`: `criarClienteHttp(opcoes: { urlBase: string; obterToken?: () => string | null; fetchImpl?: typeof fetch; aoExpirarAcesso?: () => Promise<string | null> }): ClienteHttp`. **Regra nova:** numa resposta **401** a uma requisição que **levou** `Authorization`, chama `aoExpirarAcesso()`; se devolver um token, **repete a requisição uma vez** com `Authorization: Bearer <novo>` (o corpo é reenviado: guarde `request.clone()` por `id` no `onRequest`) e a resposta da repetição segue as regras de sempre (`!ok` vira `ErroDeApi`); se devolver `null`, ou se a repetição também der 401, lança o `ErroDeApi` 401 como antes. **Nunca** mais de uma repetição por requisição. 401 sem `Authorization` (o `entrar` com senha errada) **não** chama nada.
  - `AuthProvider.tsx`: a renovação que já existe (única por vez, com `navigator.locks`, regras de `fatia-0.md` Task 3) passa a ser **uma função** `renovarAcesso(): Promise<string | null>` (devolve o token de acesso novo, ou `null` quando a sessão acabou e já foi limpa); o provider a registra em `renovadorDeAcesso.definir(...)` ao montar e a desregistra ao desmontar.
  - `api.ts`: `export const clienteHttp: ClienteHttp = criarClienteHttp({ urlBase: URL_DA_API, obterToken: tokenDeAcesso.obter, aoExpirarAcesso: renovadorDeAcesso.renovar })`; `export const $api = createClient(clienteHttp)` (`createClient` de `openapi-react-query`).
  - `consultas.ts`: `function criarQueryClient(): QueryClient` (`refetchOnWindowFocus: false` **de propósito**: quem precisa de dado novo chama `invalidarRota` ou `invalidarContadoresDoMenu` à mão; nenhuma ficha pede recarregar ao voltar o foco) com `queries: { staleTime: 30_000, refetchOnWindowFocus: false, retry: (tentativas, erro) => !(erro instanceof ErroDeApi && erro.status >= 400 && erro.status < 500) && tentativas < 2 }` e `mutations: { retry: false }`.
  - `invalidar.ts`: `function invalidarRota(queryClient: QueryClient, prefixoDoCaminho: string): Promise<void>` (invalida toda consulta do `$api` cujo caminho — `queryKey[1]` — começa com o prefixo: `invalidarRota(qc, '/api/rh/funcionarios')`; é o que as telas chamam "depois de gravar, atualize a lista").
  - `Provedores.tsx`: `function Provedores(props: { children: ReactNode; queryClient?: QueryClient; clienteApi?: ClienteApi })`: `CSPProvider disableStyleElements` > `QueryClientProvider` > `NotificacoesProvider` > `AuthProvider` (com `criarClienteApi(URL_DA_API)`) > `LimparCacheAoSair` > filhos. `LimparCacheAoSair.tsx`: `function LimparCacheAoSair()` (quando `usuario` passa a `null`, `queryClient.clear()`).
  - `main.tsx`: usa `Provedores` no lugar do `CSPProvider` + `AuthProvider` da fatia 0 (a ordem `import './shared/zod'` primeiro **não muda**).
  - `test\renderizar.tsx`: `function renderizarComProvedores(ui: ReactElement, opcoes?: { rota?: string; queryClient?: QueryClient }): RenderResult` (`CSPProvider` + `QueryClientProvider` com `retry: false` + `NotificacoesProvider` + `MemoryRouter`; **sem** `AuthProvider`). `test\sessao.ts`: `function sessaoDeTeste(perfil: Perfil, usuario?: Partial<Usuario>): Sessao` (parte de `exemplos.entrar.corpo`: Ana Souza para `RH`, Bruno Lima para `Funcionario` e Carlos Ribeiro para `Gestor`, ids e cargos do `ELENCO.md`) e `function entrarComo(perfil: Perfil, usuario?: Partial<Usuario>): void` (grava `rh.refresh` no `localStorage` e responde `POST */api/auth/renovar` com `sessaoDeTeste`).

- [ ] **Step 1: Escrever os testes que falham.**

```ts
// renovacaoNo401.test.ts   (MSW: um handler que olha o cabeçalho Authorization; 'Bearer velho' → 401 problem+json; 'Bearer novo' → 200)
it('401 com token renova e repete a chamada uma vez com o token novo')             // obterToken troca 'velho' → 'novo' dentro de aoExpirarAcesso; o resultado é o 200
it('o corpo de um POST e reenviado na repeticao')                                  // handler guarda request.json() das duas chamadas: iguais
it('aoExpirarAcesso devolvendo null lanca o ErroDeApi 401 sem repetir')           // 1 chamada ao handler
it('repeticao que volta 401 de novo lanca o 401 e nao renova outra vez (Review Focus 1)')   // aoExpirarAcesso chamado 1 vez; handler chamado 2 vezes
it('401 sem Authorization (entrar com senha errada) nao chama aoExpirarAcesso')
it('duas chamadas simultaneas com 401 sao repetidas as duas')                       // aoExpirarAcesso devolve a mesma promessa
// AuthProvider.test.tsx (acrescenta; ClienteApi falso, $api e MSW)
it('chamada de negocio com 401 renova pela API e repete')                           // servidor.use: GET /api/eu/empresa 401 com 'Bearer tok-velho', 200 com 'Bearer tok-novo'; renovar devolve sessão com tokenDeAcesso 'tok-novo'; a tela mostra o nome da empresa
it('tres chamadas simultaneas com 401 renovam uma unica vez (single-flight)')       // renovar.toHaveBeenCalledTimes(1)
it('renovacao que falha (401 no renovar) limpa a sessao e o usuario fica null, sem laco') // contador de chamadas do handler de negócio ≤ 2; localStorage sem 'rh.refresh'
// Provedores.test.tsx
it('ao sair, o cache de consultas e limpo (Review Focus 2)')                         // <Provedores> com um componente de teste (sem roteador): entrarComo('RH'), $api.useQuery('get', '/api/eu/empresa') deixa 1 consulta no cache; o botão Sair (useAuth().sair) → queryClient.getQueryCache().getAll() vazio; entrar de novo como Funcionario não mostra dado da Ana
// api.test.tsx
it('$api.useQuery mostra o example do contrato com o token de acesso e sem retry em 4xx') ; it('criarQueryClient nao repete 404 e repete 500 no maximo duas vezes')
// invalidar.test.ts
it('invalidarRota invalida so as consultas cujo caminho comeca com o prefixo')      // duas consultas ('/api/rh/funcionarios', '/api/rh/cargos'); invalidar '/api/rh/funcionarios' → só a primeira fica `isInvalidated`
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/api src/features src/app/Provedores`
  Expected: FAIL (opção, arquivos e registro inexistentes).
- [ ] **Step 3: Implementar.** Em `criarClienteHttp`: no `onRequest`, guarde `request.clone()` num `Map` por `id` (apague na resposta); no `onResponse`, se `status === 401`, a requisição tinha `Authorization`, e ainda não foi repetida (`Set` de ids), chame `aoExpirarAcesso`; para repetir, use o clone, troque o cabeçalho e chame o `fetch` (`fetchImpl ?? globalThis.fetch`, **por função**, como na fatia 0). A renovação continua **única** no `AuthProvider` (as chamadas dividem a mesma promessa); não duplique a lógica no cliente.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/api src/features src/app/Provedores`
  Expected: PASS (inclusive os testes da fatia 0 de `AuthProvider` e de `clienteApi`, que **não mudam**).
- [ ] **Step 5: Controle.** (a) Troque o limite de repetição por "sem limite": o teste "repeticao que volta 401" deve FALHAR (laço). (b) Tire o `LimparCacheAoSair`: o teste do cache deve FALHAR. Restaure.
- [ ] **Step 6: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 7: Registrar e enviar.** `PROGRESSO.md` (a renovação contra a API real é **pendente de integração**: ver Task 14); Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): api com query client, renovacao do token no 401 e cache limpo ao sair"; git push`

---

### Task 10: Download, upload e `SeletorDeArquivo`

**Files:**
- Create: `front-end\src\shared\arquivos\baixar.ts`, `multipart.ts`, `validarArquivo.ts`; `front-end\src\shared\ui\SeletorDeArquivo.tsx`.
- Test: `src\shared\arquivos\baixar.test.ts`, `multipart.node.test.ts` (primeira linha `// @vitest-environment node`), `validarArquivo.test.ts`; `src\shared\ui\SeletorDeArquivo.test.tsx`.

**Interfaces:**
- Consumes: `Esquema<'LinkTemporario'>` (fatia 0); `clienteHttp` (Task 9, só nos testes); `AvisoDeErro`, `Botao` (Task 3).
- Produces:
  - `baixar.ts`: `abrirLinkTemporario(link: { url: string }): void` (aceita só `http:` e `https:`; qualquer outro protocolo lança `Error('Link de arquivo inválido.')`; cria um `<a href rel="noopener noreferrer" target="_blank">`, clica e remove: **o arquivo vem como anexo, a página não sai do lugar**; a sonda também mede o link, de outra origem e da própria); `baixarBlob(blob: Blob, nomeDoArquivo: string): void` (`URL.createObjectURL`, `<a download>` clicado por script e `revokeObjectURL` depois; a **sonda de download** do `csp` mede esse caminho sob a política: baixa, 0 violações, `03-contrato-e-mocks.md`, seção 8); `nomeDoArquivoDaResposta(response: Response, padrao: string): string` (lê `Content-Disposition`, `filename*=UTF-8''...` e `filename="..."`); `baixarExportacao(chamada: Promise<{ data?: Blob; response: Response }>, nomePadrao: string): Promise<void>` (uso: `baixarExportacao(clienteHttp.GET('/api/rh/auditoria/exportacao', { params: { query }, parseAs: 'blob' }), 'auditoria.csv')`; um erro da API chega como `ErroDeApi` e **não** baixa nada). **Qual usar:** **exportação** (CSV, xlsx: a rota devolve os bytes com `Content-Disposition`) = `baixarExportacao`; **arquivo guardado** (documento, holerite, anexo: a rota devolve `LinkTemporario`) = `abrirLinkTemporario` (`contrato\CONVENCOES.md`, seções 4, 6 e 11).
  - `multipart.ts`: `function multipart<B>(campos: Record<string, string | number | boolean | File | null | undefined>): { body: B; bodySerializer: (corpo: B) => FormData }` (campos `null` ou `undefined` ficam de fora; uso: `cliente.POST('/api/rh/importacoes', { ...multipart<Esquema<'ImportacaoEnvioForm'>>({ arquivo }) })`, o padrão de `03-contrato-e-mocks.md`, seção 4, sem o cast repetido em cada tela).
  - `validarArquivo.ts`: `function validarArquivo(arquivo: File, regras: { tiposAceitos: readonly string[]; tamanhoMaximoMb: number }): string | null` (`null` se vale; senão `Esse tipo de arquivo não é aceito.` ou `O arquivo passa de {n} MB.` ou `O arquivo está vazio.`).
  - `SeletorDeArquivo.tsx`: `function SeletorDeArquivo(props: { arquivo: File | null; aoMudar: (arquivo: File | null) => void; tiposAceitos: readonly string[]; tamanhoMaximoMb: number; textoDeAjuda?: string; erro?: string | null })` (a área tracejada do PNG 10: ícone `ArrowUpFromLine`, `Arraste o arquivo aqui`, `textoDeAjuda`, botão `Escolher arquivo`; aceita soltar e escolher; arquivo inválido **não** é entregue a `aoMudar` e mostra a mensagem de `validarArquivo`; com arquivo, mostra o nome e o botão `Remover arquivo`).

- [ ] **Step 1: Escrever os testes que falham.**

```ts
// baixar.test.ts
it('abrirLinkTemporario clica num link https com noopener e nao troca a pagina')    // espiona HTMLAnchorElement.prototype.click; href, rel
it('abrirLinkTemporario recusa javascript: e data:')                                // lança 'Link de arquivo inválido.'
it('baixarBlob cria o link de download com o nome e libera a url')                  // URL.createObjectURL/revokeObjectURL como vi.fn (o jsdom não os tem)
it('nomeDoArquivoDaResposta le filename* e filename e cai no padrao')               // 'attachment; filename="auditoria-2026-10-07.csv"' ; "attachment; filename*=UTF-8''relat%C3%B3rio.csv"
it('baixarExportacao baixa o csv do MSW com o nome do cabecalho e nao baixa se a API devolve erro')   // servidor.use no GET da rota de exemplos.exportarAuditoria devolvendo o CSV do example com os cabeçalhos content-type text/csv e content-disposition 'attachment; filename="auditoria-2026-10-07.csv"' (o handler gerado só manda o tipo); 500 → rejeita ErroDeApi, baixarBlob não chamado
// multipart.node.test.ts   (// @vitest-environment node)
it('multipart envia multipart/form-data com o arquivo e os outros campos e omite null')   // handler vê content-type /^multipart\/form-data; boundary=/, o arquivo 'planilha.xlsx' e 'tipo'='ASO'; sem a chave de um campo null
// validarArquivo.test.ts
it.each(...)('validarArquivo')   // pdf de 1 MB com {tiposAceitos:['application/pdf','image/jpeg','image/png'], tamanhoMaximoMb:10} → null; .exe → 'Esse tipo de arquivo não é aceito.'; 11 MB → 'O arquivo passa de 10 MB.'; 0 byte → 'O arquivo está vazio.'
// SeletorDeArquivo.test.tsx   (userEvent.upload)
it('SeletorDeArquivo entrega um arquivo valido e mostra o nome') ; it('SeletorDeArquivo recusa tipo invalido sem chamar aoMudar e mostra a mensagem')
it('SeletorDeArquivo aceita soltar o arquivo na area') ; it('SeletorDeArquivo remove o arquivo escolhido (aoMudar(null))')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/arquivos src/shared/ui/SeletorDeArquivo`
  Expected: FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/arquivos src/shared/ui/SeletorDeArquivo`
  Expected: PASS.
- [ ] **Step 5: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 6: Registrar e enviar.** `PROGRESSO.md` (o download de verdade com `LinkTemporario` é **pendente de integração**: Task 14); Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): download por link temporario e por blob, upload multipart e seletor de arquivo"; git push`

---

### Task 11: Ajudantes de lista (busca, filtros, página e ordenação na URL)

**Files:**
- Create: `front-end\src\shared\lista\useEstadoDaLista.ts`.
- Test: `src\shared\lista\useEstadoDaLista.test.tsx`.

**Interfaces:**
- Consumes: `useSearchParams` do `react-router`; `Tabela`, `Paginacao`, `CampoDeBusca` (Tasks 4 e 5, só no uso).
- Produces: `type ConsultaDeLista<F extends string> = { pagina: number; tamanhoPagina: number; busca?: string; ordenarPor?: string; direcao?: 'ASC' | 'DESC' } & { [K in F]?: string }`; `function useEstadoDaLista<F extends string>(opcoes: { filtros: readonly F[]; tamanhoPagina?: number; ordenacaoPadrao?: { campo: string; direcao: 'ASC' | 'DESC' } }): { consulta: ConsultaDeLista<F>; busca: string; pagina: number; filtros: Partial<Record<F, string>>; ordenacao: { campo: string; direcao: 'ASC' | 'DESC' } | null; definirBusca(texto: string): void; definirFiltro(nome: F, valor: string | null): void; definirPagina(pagina: number): void; alternarOrdenacao(campo: string): void }`. Regras: o estado **vive na URL** (`?busca=ana&situacao=ATIVO&pagina=2&ordenarPor=nome&direcao=DESC`: o botão Voltar do navegador mantém o filtro); `definirBusca`, `definirFiltro` e `alternarOrdenacao` **voltam à página 1**; `pagina` 1 e valores vazios **saem** da URL; `pagina` que não é inteiro `>= 1` (`0`, `abc`, `-3`) vale 1; `alternarOrdenacao(campo)`: campo novo → `ASC`; o mesmo → inverte; `tamanhoPagina` padrão **20** (o do contrato); `definirBusca` troca a entrada do histórico (`replace`) para a digitação não encher o Voltar. `consulta` é o objeto para `params.query` (o `build` diz se o tipo serve; se não servir, a tela estreita o tipo e o **teste de contrato por tela** confere o que foi enviado).
- Uso que o plano manda repetir na 1b: `$api.useQuery('get', '/api/rh/funcionarios', { params: { query: consulta } }, { placeholderData: keepPreviousData })`.

- [ ] **Step 1: Escrever os testes que falham** (`renderHook` com `createMemoryRouter` e `RouterProvider`, ou um wrapper `MemoryRouter`; a URL se lê de `router.state.location.search`).

```tsx
it('le busca, filtros, pagina e ordenacao da URL')                                   // '?busca=ana&situacao=ATIVO&pagina=2&ordenarPor=nome&direcao=DESC' → consulta com tamanhoPagina 20
it('mudar a busca, um filtro ou a ordenacao volta a pagina 1 e tira pagina=1 da URL (Review Focus 5)')
it('definirPagina(3) escreve pagina=3 e definirPagina(1) a tira')
it('pagina invalida na URL vale 1')                                                  // '?pagina=0', '?pagina=abc', '?pagina=-3'
it('alternarOrdenacao: campo novo ASC, o mesmo inverte, e usa a ordenacaoPadrao no inicio')
it('definirFiltro com null ou vazio tira o filtro da URL') ; it('so os filtros declarados entram na consulta (outros parametros da URL sao ignorados)')
it('definirBusca troca a entrada do historico (replace)')                            // history length não cresce
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/shared/lista`
  Expected: FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/shared/lista`
  Expected: PASS.
- [ ] **Step 5: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 6: Registrar e enviar.** `PROGRESSO.md`; Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): estado da lista na url (busca, filtros, pagina e ordenacao)"; git push`

---

### Task 12: Rotas por perfil, menu, layout, início mínimo e o `csp`

**Files:**
- Create (em `front-end\src\app\`): `caminhos.ts`, `menu.ts`, `tabelaDeRotas.tsx`, `rotas.tsx`, `RaizDoApp.tsx`, `GuardaDeSessao.tsx`, `GuardaDePerfil.tsx`, `PaginaDeEntrada.tsx`, `PortalLayout.tsx`, `MenuLateral.tsx`, `RodapeDoMenu.tsx`, `linhaDeApoio.ts`, `useContadoresDoMenu.ts`, `useEmpresa.ts`; `paginas\Pagina403.tsx`, `paginas\Pagina404.tsx`, `paginas\PaginaEmBreve.tsx`, `paginas\InicioDaPessoa.tsx`, `paginas\InicioDoRh.tsx`.
- Modify: `front-end\src\app\App.tsx`, `front-end\src\main.tsx`, `front-end\src\test\renderizarApp.tsx`, `front-end\scripts\violacoes-de-politica.mjs` (os passos da lista `PASSOS`).
- Delete: `front-end\src\features\autenticacao\PortaDeAcesso.tsx`, `front-end\src\app\ShellDoPortal.tsx` e os testes deles (**as asserções migram** para os testes novos abaixo).
- Test: `caminhos.test.ts`, `menu.test.ts`, `linhaDeApoio.test.ts`, `tabelaDeRotas.test.ts`, `rotas.test.tsx`, `MenuLateral.test.tsx`, `PortalLayout.test.tsx`, `paginas\inicios.test.tsx` (todos em `src\app\`).

**Interfaces:**
- Consumes: `useAuth`, `rotulosDePerfil` (fatia 0), `Provedores` (Task 9), `$api`, `invalidarRota` (Task 9), `CabecalhoDePagina`, `Avatar`, `Bolinha`, `LogoR`, `Botao`, `BotaoLink`, `Carregando` (Task 3), `PaginaEntrar` (fatia 0), `saudacao`, `dataPorExtenso`, `primeiroNome` (Task 2).
- Produces:
  - `caminhos.ts`: `function inicioDoPerfil(perfil: Perfil): string` (`RH` → `/rh/painel`; `Funcionario` e `Gestor` → `/eu/inicio`); `function destinoSeguro(voltar: string | null): string | null` (só aceita texto que começa com **uma** barra e não com `//` nem `/\`, sem esquema, com menos de 512 caracteres e que não seja `/entrar`; senão `null`); `const PERFIS_POR_FAMILIA: { rh: Perfil[]; gestor: Perfil[]; eu: Perfil[] }` (`rh: ['RH']`, `gestor: ['Gestor']`, `eu: ['Funcionario', 'Gestor', 'RH']`).
  - `menu.ts` (**o arquivo da tabela do menu**): `type ChaveDeContador = 'avisosNaoLidos' | 'formulariosPendentes' | 'documentosPendentes' | 'pendenciasDaEquipe' | 'pedidosDeAjusteDePonto'`; `type ItemDeMenu = { rotulo: string; caminho: string; contador?: ChaveDeContador }`; `type GrupoDeMenu = { rotulo: string; itens: readonly ItemDeMenu[] }`; `const MENU_POR_PERFIL: Record<Perfil, readonly GrupoDeMenu[]>`; `function menuVisivel(perfil: Perfil, caminhosExistentes: ReadonlySet<string>): readonly GrupoDeMenu[]` (só os itens cujo `caminho` está no conjunto; grupo sem item some); `function todosOsCaminhosDoMenu(perfil: Perfil): ReadonlySet<string>`. **Conteúdo** (PNG 00, 32, 46; `telas\fatia-0.md`):
    - **RH:** *Visão geral*: Painel `/rh/painel`. *Pessoas*: Colaboradores `/rh/colaboradores`, Advertências `/rh/advertencias`, Formulários `/rh/formularios`, Treinamentos `/rh/treinamentos`. *Rotina*: Ponto e feriados `/rh/ponto-e-feriados` (`pedidosDeAjusteDePonto`), Avisos `/rh/avisos`, Meus documentos `/eu/meus-documentos` (`documentosPendentes`). *Sistema*: Cadastros `/rh/cadastros`, Log de auditoria `/rh/auditoria`. ***Meu espaço*** (no fim; **decidido pelo dono em 2026-11-02**, `DUVIDAS.md`, "Decisões do dono", `D-menu`): Meu ponto `/eu/meu-ponto`, Férias `/eu/ferias`, Holerites `/eu/holerites`, Minhas advertências `/eu/advertencias`, Meus formulários `/eu/formularios` (`formulariosPendentes`), Meus treinamentos `/eu/treinamentos`, Benefícios `/eu/beneficios`.
    - **Funcionário:** *Seu dia a dia*: Início `/eu/inicio`, Meu ponto `/eu/meu-ponto`, Férias `/eu/ferias`, Holerites `/eu/holerites`. *Comunicação*: Avisos `/eu/avisos` (`avisosNaoLidos`), Formulários `/eu/formularios` (`formulariosPendentes`). *Você na empresa*: Treinamentos `/eu/treinamentos`, Advertências `/eu/advertencias`, Benefícios `/eu/beneficios`, Meus documentos `/eu/meus-documentos` (`documentosPendentes`).
    - **Gestor:** *Seu dia a dia*: Início `/eu/inicio`, Meu ponto `/eu/meu-ponto`, Férias `/eu/ferias`, Holerites `/eu/holerites`, Meus documentos `/eu/meus-documentos` (`documentosPendentes`), Benefícios `/eu/beneficios`. *Seu setor*: Painel da equipe `/gestor/equipe` (`pendenciasDaEquipe`), Ponto da equipe `/gestor/ponto-da-equipe`, Formulários `/gestor/formularios`, Treinamentos `/gestor/treinamentos`, Advertências `/gestor/advertencias`. *Comunicação*: Avisos `/eu/avisos` (`avisosNaoLidos`). ***Meu espaço*** (no fim; **decidido pelo dono em 2026-11-02**, `D-menu`): os itens pessoais que o gestor ainda não tem (Meu ponto, Férias, Holerites e Benefícios já estão em *Seu dia a dia*): Meus formulários `/eu/formularios` (`formulariosPendentes`), Meus treinamentos `/eu/treinamentos`, Minhas advertências `/eu/advertencias`.
    - **"Meu espaço" está ativo no RH e no gestor** (nenhuma linha comentada; o rótulo "Meu/Minha ..." evita repetir os do grupo *Seu setor* do gestor e os do grupo *Pessoas* do RH: o item das advertências pessoais se chama **"Minhas advertências"** nos dois perfis, e o "Advertências" de *Pessoas* continua sendo o da equipe, `/rh/advertencias`). Os itens abrem as telas pessoais da família `/eu` (`05-caminhos-de-tela.md`) e, como todo item, **só aparecem quando a rota existe** (nesta fatia e na 1b o grupo ainda não aparece: as rotas pessoais chegam nas fatias 3 a 7). "Meus documentos" continua nos três menus. Sem dúvida a registrar.
  - `tabelaDeRotas.tsx` (**o arquivo da tabela de rotas**): `type RotaDoPortal = { caminho: string; elemento: ReactElement; filhas?: readonly RotaDoPortal[] }`; `const ROTAS_RH`, `ROTAS_GESTOR`, `ROTAS_EU`, `ROTAS_PUBLICAS: readonly RotaDoPortal[]` (os caminhos de `ROTAS_RH`, `_GESTOR` e `_EU` são **relativos** à família; os públicos são absolutos). **Nesta fatia:** `ROTAS_RH` = `painel` (`InicioDoRh`); `ROTAS_GESTOR` = vazia; `ROTAS_EU` = `inicio` (`InicioDaPessoa`); `ROTAS_PUBLICAS` = `/entrar` (`PaginaDeEntrada`), `/esqueci-senha` e `/definir-senha` (as duas com `PaginaEmBreve`, que a 1b troca pelas telas reais). `function caminhosDasRotas(): ReadonlySet<string>` (caminhos absolutos de `/rh`, `/gestor` e `/eu`, sem as `filhas`).
  - `rotas.tsx`: `function criarRotas(): RouteObject[]` (lê a tabela e monta as rotas). **Quem lê a tabela é o `MenuLateral`, por `tabelaDeRotas.tsx`; nada importa `rotas.tsx` além do `main.tsx` e do `renderizarApp`**: assim não há ciclo de importação entre o menu, o layout e as rotas.
  - Estrutura de `criarRotas()`: raiz `RaizDoApp` (mostra `Carregando` com `role="status"` enquanto `carregando`, senão `Outlet`) com os filhos: as públicas; `/` (redireciona a `inicioDoPerfil`, dentro da guarda de sessão); a `GuardaDeSessao` (sem usuário: `Navigate` para `/entrar?voltar=<caminho e consulta codificados>` com `replace`) que envolve o `PortalLayout`, e dentro dele `/rh` (`GuardaDePerfil perfis={PERFIS_POR_FAMILIA.rh}`), `/gestor` e `/eu`, **cada família com um filho `*` (`Pagina404`) no fim** (assim `/gestor/qualquer-coisa` chega à guarda e um funcionário vê o 403, não o 404), e um `*` geral (`Pagina404`). **A família decide o perfil** (não há `perfis` por rota). `GuardaDePerfil` com perfil errado mostra **no lugar do conteúdo** a `Pagina403` (`Você não tem permissão para ver esta página.`, `BotaoLink` `Ir para o início`) e **mantém o endereço e o menu**; `Pagina404`: `Página não encontrada.` (dentro do layout; sem sessão o `*` nunca é visto: a guarda manda ao login). `PaginaDeEntrada` (`/entrar`): com usuário, `Navigate` para `destinoSeguro(voltar) ?? inicioDoPerfil(perfil)`; sem usuário, o `PaginaEntrar` da fatia 0.
  - `App.tsx`: `function App(props: { roteador: ReturnType<typeof createBrowserRouter> })` (`RouterProvider` de `react-router/dom`); `main.tsx` passa `createBrowserRouter(criarRotas())`. `renderizarApp.tsx` (fatia 0) vira `function renderizarApp(opcoes?: { rota?: string; queryClient?: QueryClient }): RenderResult & { roteador: ReturnType<typeof createMemoryRouter> }` (`Provedores` + `App` com `createMemoryRouter(criarRotas(), { initialEntries: [rota ?? '/'] })`; **sem argumento continua funcionando**).
  - `useContadoresDoMenu.ts`: `function useContadoresDoMenu(perfil: Perfil): Partial<Record<ChaveDeContador, number>>` (todos os perfis chamam `obterContadoresDoMenuDoFuncionario`; o `Gestor` também `obterContadoresDoMenuDoGestor`; o `RH` também `obterContadoresDoMenuDoRh`; refaz ao mudar de página (`useLocation().pathname`) e a cada 60 s; erro **nunca** mostra mensagem: o contador some); `function invalidarContadoresDoMenu(queryClient: QueryClient): Promise<void>` (`invalidarRota(queryClient, '/api/eu/contadores-do-menu')`, e o do gestor e o do RH): o que as telas chamam "atualize as bolinhas". **A fundação já mostra as bolinhas:** `useContadoresDoMenu` lê `contadores-do-menu` (os números do exemplo do contrato nos testes e no desenvolvimento) e `MenuLateral` as escreve; os **valores reais** chegam sozinhos quando o back-end entregar as rotas dos números (pendente de integração, Task 14, passo 2, item 3). **Nenhuma ficha manda "trocar a ausência de bolinhas"**: ninguém precisa "ligá-las". `useEmpresa.ts`: `function useEmpresa(): string | null` (`nomeEmpresa` de `obterEmpresa`; **404 `EMPRESA_NAO_CONFIGURADA`** ou qualquer erro → `null`, sem mensagem; `staleTime` de 5 minutos).
  - `linhaDeApoio.ts`: `function linhaDeApoio(usuario: Usuario): string` (**RH:** `Perfil RH, acesso total`; **Gestor:** `Perfil gestor, setor {setor}`, ou `Perfil gestor` sem setor; **Funcionário:** `{cargo}, {setor}` só com o que existe; ambos `null` → `''`: PNG 00, 32, 46).
  - `MenuLateral.tsx`: `function MenuLateral(props: { caminhosExistentes?: ReadonlySet<string> })` (padrão `caminhosDasRotas()`; `<aside>` de 232 px com `<nav aria-label="Menu principal">`; marca `LogoR` + `Portal de RH` + o nome da empresa de `useEmpresa` (**sem** a segunda linha quando é `null`); grupos com o rótulo `mini`; itens `NavLink` (o da página atual com `aria-current="page"` e fundo `primaria-ativa`); `Bolinha` com o contador; **sem ícone nos itens**); `RodapeDoMenu.tsx`: `function RodapeDoMenu()` (linha `menu-linha`, `Avatar tamanho="menu"`, nome, `linhaDeApoio` e o botão de texto `Sair`, que chama `useAuth().sair`; **só** o `Sair` nesta fatia: o `Trocar senha` entra na 1b, ao lado dele). `PortalLayout.tsx`: `function PortalLayout()` (`flex min-h-screen min-w-[1280px]`; menu `sticky top-0 h-screen` com rolagem própria; `<main className="min-w-0 flex-1 px-12 pb-12 pt-8">` com o `Outlet`: `04-visual.md`, 9).
  - `InicioDaPessoa.tsx` (`/eu/inicio`): `CabecalhoDePagina` com `Olá, {primeiro nome}` e a data por extenso. `InicioDoRh.tsx` (`/rh/painel`): `{saudacao}, {primeiro nome}` e a data por extenso. **Nada mais** (os cartões são da 7d; `telas\fatia-1.md`, "Início mínimo por perfil").

- [ ] **Step 1: Escrever os testes que falham** (`renderizarApp`, `entrarComo`, MSW; `beforeEach`: `localStorage.clear()` e `tokenDeAcesso.definir(null)`).

```tsx
// caminhos.test.ts
it('inicioDoPerfil escolhe o inicio de cada perfil')
it.each([['/rh/painel', '/rh/painel'], ['/eu/inicio?x=1', '/eu/inicio?x=1'], ['//exemplo.com', null], ['https://exemplo.com', null], ['/\\exemplo.com', null], ['javascript:alert(1)', null], ['/entrar', null], ['', null], [null, null]])('destinoSeguro (Review Focus 4)')
// menu.test.ts
it('o menu do RH, do funcionario e do gestor tem os grupos e itens do Figma, na ordem, quando todas as rotas existem')   // menuVisivel(perfil, todosOsCaminhosDoMenu(perfil)) → ['Visão geral: Painel', 'Pessoas: Colaboradores, Advertências, Formulários, Treinamentos', 'Rotina: Ponto e feriados, Avisos, Meus documentos', 'Sistema: Cadastros, Log de auditoria', 'Meu espaço: Meu ponto, Férias, Holerites, Minhas advertências, Meus formulários, Meus treinamentos, Benefícios'] etc. (o gestor termina em 'Meu espaço: Meus formulários, Meus treinamentos, Minhas advertências'; o funcionário não tem o grupo)
it('Meu espaco (RH e gestor): cada item leva a rota pessoal certa e o grupo some enquanto as rotas nao existem')   // decisao D-menu, 2026-11-02: rotas '/eu/meu-ponto', '/eu/ferias', '/eu/holerites', '/eu/advertencias', '/eu/formularios', '/eu/treinamentos', '/eu/beneficios' (RH) e '/eu/formularios', '/eu/treinamentos', '/eu/advertencias' (gestor); com { '/eu/inicio', '/rh/painel' } o RH nao tem o grupo; o item 'Meus formularios' do RH e do gestor leva o contador formulariosPendentes
it('so aparece item cujo caminho tem rota e grupo sem item some')                                                          // com { '/eu/inicio' } só 'Seu dia a dia: Início'
it('todo caminho do menu pertence a uma familia que o perfil pode abrir')          // /rh só no RH, /gestor só no gestor, /eu em todos (pega erro de digitação na tabela)
it('cada contador do menu e entregue ao perfil que o usa')
// linhaDeApoio.test.ts: os quatro casos (RH, Gestor com e sem setor, Funcionario com cargo e setor, com cargo null, com os dois null)
// tabelaDeRotas.test.ts
it('caminhosDasRotas tem /rh/painel e /eu/inicio, e nao tem /eu/vitrine sem VITE_VITRINE')
it('todo caminho de ROTAS_RH, ROTAS_GESTOR e ROTAS_EU e relativo (sem barra no comeco) e sem repeticao')
// rotas.test.tsx
it('sem sessao em /rh/painel vai a /entrar?voltar=%2Frh%2Fpainel e, depois de entrar, volta a /rh/painel')
it('voltar malicioso (//exemplo.com) cai no inicio do perfil')
it('/ leva ao inicio do perfil: RH /rh/painel; Funcionario e Gestor /eu/inicio') ; it('/entrar com sessao vai ao inicio')
it('gestor em /rh/painel ve a mensagem de 403 com o menu do gestor e o endereco mantido (Review Focus 4)') ; it('funcionario em /gestor/equipe ve 403; qualquer perfil abre /eu/inicio')
it('endereco desconhecido logado mostra "Página não encontrada." dentro do layout; deslogado vai a /entrar')
it('com refresh guardado mostra Carregando… (role status) sem piscar a tela de entrar e depois o inicio')    // migra o teste da PortaDeAcesso
// MenuLateral.test.tsx   (caminhosExistentes = todosOsCaminhosDoMenu(perfil))
it('mostra a marca, o nome da empresa de obterEmpresa e some a segunda linha com 404 EMPRESA_NAO_CONFIGURADA')
it('bolinhas: RH Ponto e feriados 4 e Meus formularios 1; funcionario Avisos 2 e Formularios 1; gestor Painel da equipe 3, Avisos 2 e Meus formularios 1; 0 nao mostra') // exemplos do contrato; getByRole('link', { name: /^Ponto e feriados/ })
it('falha ao buscar os contadores nao mostra mensagem nem bolinha') ; it('o item da pagina atual tem aria-current=page')
it('invalidarContadoresDoMenu refaz as tres consultas')
// PortalLayout.test.tsx   (migra o ShellDoPortal.test da fatia 0)
it('rodape mostra iniciais, nome e a linha de apoio do perfil: "Perfil RH, acesso total", "Perfil gestor, setor Comercial", "Analista pleno, Financeiro"')
it('Sair limpa a sessao, o localStorage e volta a tela de entrar') ; it('nao tem item de menu com ícone e o menu e uma nav nomeada')
// inicios.test.tsx   (vi.setSystemTime(new Date('2026-10-07T12:42:00Z')): quarta-feira, 09:42 em Sao Paulo)
it('inicio do RH escreve "Bom dia, Ana" e "Quarta-feira, 7 de outubro"') ; it('inicio do funcionario escreve "Olá, Bruno"')
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/app`
  Expected: FAIL (arquivos inexistentes; os testes migrados da fatia 0 também, até a troca).
- [ ] **Step 3: Implementar** os arquivos com as assinaturas e os conteúdos acima; troque `App.tsx`, `main.tsx` e `renderizarApp.tsx`; **apague** `PortaDeAcesso.tsx`, `ShellDoPortal.tsx` e os dois testes velhos **depois** de os novos cobrirem as mesmas asserções. `rotulosDePerfil.ts` **fica** (é da fatia 0 e traduz `Funcionario` para "Funcionário"; a 1b **não** o usa: ela usa `rotuloDoNivelDeAcesso`, que escreve "Colaborador"; não o apague).
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/app src/features`
  Expected: PASS (os testes da fatia 0 de `AuthProvider` e de `PaginaEntrar` continuam passando).
- [ ] **Step 5: Controle.** (a) Troque em `menu.ts` o caminho de um item do RH por `/gestor/x`: o teste "pertence a uma familia" deve FALHAR. (b) Tire a checagem de `//` em `destinoSeguro`: o teste do `voltar` malicioso deve FALHAR. Restaure.
- [ ] **Step 6: Conferência da pessoa no navegador de desenvolvimento (não bloqueia; vai para o relatório final).** **Você não olha o navegador** (`01-regras.md`, A11). A prova automatizada é a dos testes do passo 4 (menu por perfil, 403, 404, `voltar`) e a do `csp` no passo 7. Anote na tabela "Conferências da pessoa" do `PROGRESSO.md` o roteiro para a pessoa: `npm --prefix front-end run dev`, abrir o endereço do Vite; entrar com qualquer e-mail e senha abre `/rh/painel`: menu **só com "Painel"** (os outros itens, inclusive o grupo "Meu espaço", aparecem quando a rota existir), a marca `Portal de RH`, o nome da empresa (do exemplo do contrato) e o rodapé "Ana Souza / Perfil RH, acesso total" com o botão `Sair`; `/colaboradores-que-nao-existe` mostra `Página não encontrada.`; `/gestor/equipe` mostra o 403.
- [ ] **Step 7: Provas da tarefa, com o `csp`.** Os passos 1 a 6 da lista `PASSOS` de `scripts\violacoes-de-politica.mjs` **continuam valendo sem mudança** (conferi cada texto: `Entrar`, `Preencha o e-mail e a senha.`, `Portal de RH`, o nome `Ana Souza`, `Sair`, `/rh/colaboradores` sem sessão); os três passos da **sonda de download** que vêm depois deles não dependem de texto de tela. Acrescente **no fim** da lista (na linha `ACRESCENTE AQUI`) o passo `{ nome: 'entra e abre o inicio do RH com o menu', fazer: ... }`: **primeiro `await pagina.goto(servidor.url + '/entrar')`, sem `voltar`**, e espera o botão `Entrar`; só então entra de novo (preenche `E-mail` e `Senha`, clica `Entrar`), espera `getByRole('navigation', { name: 'Menu principal' })` e o título que casa `/^(Bom dia|Boa tarde|Boa noite), Ana$/`. O motivo do `goto`: o passo 6 da lista (`/rh/colaboradores` sem sessão) deixa a página em `/entrar?voltar=%2Frh%2Fcolaboradores`, e `destinoSeguro` levaria a pessoa a `/rh/colaboradores`, que **na 1a ainda não existe** (`Página não encontrada.`) e o título esperado nunca apareceria; sem `voltar`, `destinoSeguro` dá `null` e ela cai em `/rh/painel`. Run: `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end test; npm --prefix front-end run build; npm --prefix front-end run csp`
  Expected: tudo passando; `csp` com `PASSOU`, `violações reais: 0`, `controles mudos: 0` e **nenhuma linha `[FALHOU]`**.
- [ ] **Step 8: Registrar e enviar.** `PROGRESSO.md` (as telas "Início do RH" e "Início da pessoa": `sem print`, `feita com <versão>`; `usa provisório`: nenhum); Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): rotas por perfil, menu dirigido por tabela, layout, inicio minimo e 403 e 404"; git push`

---

### Task 13: Vitrine de componentes e a prova sob a política

**Files:**
- Create: `front-end\src\app\vitrine\Vitrine.tsx`.
- Modify: `front-end\src\app\tabelaDeRotas.tsx` (acrescenta `rotasDaVitrine` e a espalha em `ROTAS_EU`), `front-end\src\vite-env.d.ts` (`readonly VITE_VITRINE?: string`), `front-end\scripts\violacoes-de-politica.mjs` (passos da vitrine).
- Test: `src\app\vitrine\Vitrine.test.tsx`.

**Interfaces:**
- Consumes: **todos** os componentes das Tasks 3 a 7 e 10.
- Produces: `function rotasDaVitrine(): RotaDoPortal[]` em `tabelaDeRotas.tsx` (devolve `[]` se `import.meta.env.VITE_VITRINE !== 'sim'` e **só depois** desse `return` declara `const Vitrine = lazy(() => import('./vitrine/Vitrine'))` e devolve `[{ caminho: 'vitrine', elemento: <Vitrine /> }]`: **medido** que com a declaração no topo do módulo o `Vitrine` ia para o `dist` do build normal, e com o `return` antes ele **não** vai); a página `/eu/vitrine` (título `Vitrine de componentes`), **só no build de prova** (`VITE_VITRINE=sim`); no build normal a rota não existe e o código não vai no `dist`. Ela mostra, nesta ordem, com dados fixos: botões (as 5 variantes e os 2 tamanhos, um `carregando`); selos (6 tons); cartões (5 tons); avatar (3 tamanhos), bolinha e `LogoR`; avisos (4 tons) e `AvisoDeErro`; `EstadoVazio`, `Carregando`, `Esqueleto`; campos (`Entrada` com erro, com máscara de CPF e com ícone, `AreaDeTexto`, `Selecao` rotulada `Cargo (vitrine)`, `ComboDeBusca`, `CaixaDeSelecao`, `Interruptor`, `OpcoesEmCartao`, `SeletorDeArquivo`, `CampoDeBusca`); `Abas` (as duas variantes); `Tabela` (com linhas e ordenável, carregando, vazia e com erro) e `Paginacao`; `Progresso`; `Etapas`; `MenuDeAcoes`; os botões `Abrir janela`, `Abrir painel`, `Abrir confirmação` e `Mostrar notificação`; os botões `Baixar CSV de teste` (chama `baixarExportacao(clienteHttp.GET('/api/rh/auditoria/exportacao', { parseAs: 'blob' }), 'auditoria.csv')`) e `Abrir link de teste` (chama `clienteHttp.GET('/api/rh/funcionarios/{funcionarioId}/documentos/{documentoId}/download', { params: { path: { funcionarioId: 12, documentoId: 61 } } })` e `abrirLinkTemporario(data)`): são as duas **rotas reais do contrato** que a API simulada do `csp` atende (a sonda de download, `03-contrato-e-mocks.md`, seção 8), e é por elas que o `csp` prova o código de verdade; e um `Markdown` com o texto de ataque de `04-visual.md`, seção 8.

- [ ] **Step 1: Escrever o teste que falha.**

```tsx
// Vitrine.test.tsx   (renderizarComProvedores(<Vitrine />))
it('a vitrine renderiza todos os componentes sem erro e sem HTML cru')   // título 'Vitrine de componentes'; getByRole('combobox', { name: 'Cargo (vitrine)' }); getByRole('table', { name: ... }); nenhum <script> nem <img> dentro do markdown; window.__xss undefined
it('Abrir janela, Abrir painel e Abrir confirmacao abrem os dialogos e Escape os fecha') ; it('Mostrar notificacao mostra a mensagem')
it('Baixar CSV de teste e Abrir link de teste chamam baixarExportacao e abrirLinkTemporario')   // vi.mock de '../../shared/arquivos/baixar'; os handlers gerados respondem as duas rotas com o exemplo do contrato
// src\app\rotas.test.tsx (acrescenta)
it('sem VITE_VITRINE a rota /eu/vitrine nao existe (404)')   // import.meta.env.VITE_VITRINE ≠ 'sim' nos testes
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `npm --prefix front-end test src/app/vitrine src/app/rotas`
  Expected: FAIL.
- [ ] **Step 3: Implementar** `Vitrine.tsx` (uma seção por grupo, `Secao` da Task 3), `rotasDaVitrine()` como na interface e a linha `...rotasDaVitrine()` em `ROTAS_EU`.
- [ ] **Step 4: Rodar e ver passar.** Run: `npm --prefix front-end test src/app/vitrine src/app/rotas`
  Expected: PASS.
- [ ] **Step 5: Estender o `csp`.** Em `scripts\violacoes-de-politica.mjs`, acrescente passos **depois** do passo da Task 12, **todos criados com `vitrine.passo(nome, fazer)`** (o ajudante que o script já traz, com o `abrir()` e o `aquietar()`): `vitrine.passo('abre a vitrine', () => vitrine.abrir())` (o `abrir()` faz `goto(servidor.url + '/eu/vitrine')` e espera o título `Vitrine de componentes` ou `Página não encontrada.`; **se a vitrine não existe neste build, ele marca a vitrine como indisponível e TODOS os passos criados com `vitrine.passo` saem `[pulado] vitrine ausente (build de produção)`, sem falhar**: pular só o primeiro não bastaria, porque os seguintes esperariam 8 s por um botão que não existe e estourariam; **só passo da vitrine pode ser pulado**), `abre a janela, o painel e a confirmação` (clica em `Abrir janela`, espera o `getByRole('dialog')`, `Escape`; o mesmo para `Abrir painel` e, com `getByRole('alertdialog')`, `Abrir confirmação`), `abre a seleção e o combo` (clica em `getByRole('combobox', { name: 'Cargo (vitrine)' })`, espera `getByRole('option')`, `Escape`; digita no `ComboDeBusca`), `abre o menu de ações e a notificação` (`MenuDeAcoes`, `Mostrar notificação`) e `renderiza o markdown de ataque` (confere que `window.__xss` é `undefined`) e `baixa pelo código do app (CSV por Blob e link temporário)` (clica em `Baixar CSV de teste`, espera o download com `esperarDownload()` e confere com `conferirDownload(download, 'sonda.csv', 'a;b')`; clica em `Abrir link de teste` e confere `conferirDownload(download, 'sonda.pdf', '%PDF')`: os dois ajudantes já existem no script, usados pela sonda de download dos três passos fixos; **aqui o download passa pelo código de verdade do app**, `baixarExportacao` e `abrirLinkTemporario`). **Atenção ao nome dos passos:** mantenha a regra do script (cada passo registra suas violações).
- [ ] **Step 6: Prova sob a política, com a vitrine.** Run, **numa linha só** (a variável só vale na mesma linha: `01-regras.md`, A14): `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; $env:VITE_VITRINE = 'sim'; npm --prefix front-end run build; npm --prefix front-end run csp; Remove-Item Env:VITE_VITRINE`
  Expected (últimas linhas): **nenhuma linha `[FALHOU]`**; `[ok ]` em **todos** os passos, inclusive a sonda de download, os da vitrine e `baixa pelo código do app` (**nenhum** `[pulado]`: `passos pulados: 0`), `violações reais: 0 · controles mudos: 0 · exceções: 0 · requisições que falharam: 0` e `PASSOU`. **Esta é a prova que vale a vitrine**: um `[pulado]` só é aceito se esta execução, com a variável ligada, passou ao menos uma vez. (Sem Edge nem Chrome: código 2, `pendente: csp`, A11.)
- [ ] **Step 7: Prova de que a vitrine não vai para o ar.** Run: `Remove-Item Env:VITE_VITRINE -ErrorAction SilentlyContinue; Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; npm --prefix front-end run build; (Select-String -Path front-end\dist\assets\*.js -Pattern 'Vitrine de componentes' -List | Measure-Object).Count; npm --prefix front-end run csp`
  Expected: o `build` termina com `built in`; a contagem é **`0`**; o `csp` passa, **nenhuma linha `[FALHOU]`**, com **todos** os passos da vitrine `[pulado] vitrine ausente (build de produção)` (e só eles), `passos pulados: N` (N = quantos passos da vitrine você criou) e o aviso de que a vitrine só está provada com a variável ligada. Se a contagem não for 0, o `lazy` foi declarado **antes** do `return` de `rotasDaVitrine`: corrija.
- [ ] **Step 8: Controle: prove que o `csp` enxerga um defeito da fundação.** Em `Markdown.tsx`, tire temporariamente `unwrapDisallowed` e permita `img` (acrescente `'img'` a `allowedElements`), refaça o passo 6: deve FALHAR com `img-src` bloqueando `https://exemplo.com/...` (a imagem do texto de ataque). Restaure e refaça o passo 6.
- [ ] **Step 9: Provas da tarefa.** Run: `npm --prefix front-end run format; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end test`
  Expected: tudo passando.
- [ ] **Step 10: Registrar e enviar.** `PROGRESSO.md` (a vitrine é a página que a pessoa usa para conferir os componentes ao lado dos PNG: `04-visual.md`; registre o comando `$env:VITE_VITRINE='sim'; npm --prefix front-end run dev`); Run: `git add front-end docs\plano-front-end\PROGRESSO.md; git commit -m "feat(front): vitrine de componentes so no build de prova e passos do csp para ela"; git push`

---

### Task 14: Verificação final, pendências de integração e relatório da fatia

**Files:**
- Modify: `docs\plano-front-end\PROGRESSO.md`, `docs\plano-front-end\DUVIDAS.md`.

**Interfaces:**
- Consumes: tudo das Tasks 1 a 13; `contrato\STATUS.md`.
- Produces: a fundação pronta, enviada, e o relatório que a 1b lê.

- [ ] **Step 1: Prova completa a partir de uma instalação limpa** (o que a CI fará). Run, **numa linha só** (as variáveis só valem na mesma linha: `01-regras.md`, A14): `Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue; Remove-Item -Recurse -Force front-end\node_modules; npm --prefix front-end ci; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end test; $env:VITE_VITRINE = 'sim'; npm --prefix front-end run build; npm --prefix front-end run csp; Remove-Item Env:VITE_VITRINE; npm --prefix front-end run build; npm --prefix front-end run csp`
  Expected: `npm ci` sem erro; `lint` e `format:check` limpos; **todos os testes passam** (guarde o número de arquivos e de testes para o `PROGRESSO.md`); o `csp` com a vitrine: `PASSOU`, nenhuma linha `[FALHOU]` e nenhum passo `[pulado]` (a sonda de download e `baixa pelo código do app` incluídas; sem Edge nem Chrome, código 2, `pendente: csp`: A11 e G2); o `csp` do build normal: `PASSOU`, nenhuma linha `[FALHOU]`, com **todos** os passos da vitrine `[pulado] vitrine ausente` (e só eles; a vitrine já foi provada com a variável ligada na linha anterior); `dist\mockServiceWorker.js` não existe.
- [ ] **Step 2: Pendências de integração** (`01-regras.md`, G1; leia `contrato\STATUS.md`: `Select-String -Path contrato\STATUS.md -Pattern '^\| [017] '`). Registre no `PROGRESSO.md`, tabela "Pendente de integração", **uma linha por item**, `pendente de integração` com o roteiro (as fatias 0, 1 e 7 estão `planejada` hoje; mesmo se uma estiver `pronta`, **a sessão não roda contra a API real**: `01-regras.md`, G1). **Os roteiros são da pessoa** (ou do dono; você não olha o navegador, `01-regras.md`, A11): ela os roda quando a API existir e traz o resultado, que você registra na tabela "Conferências da pessoa" do `PROGRESSO.md`:
  1. **Renovação do token no 401** (`renovarSessao` real): roteiro: `.env.local` com `VITE_API_URL=http://localhost:5000` e `VITE_MOCKS=nao`; `npm --prefix front-end run dev`; entre; **deixe a aba aberta mais de 15 minutos** (vida do token de acesso: `acessoExpiraEmUtc`) e clique num item do menu: a página abre **sem pedir login** e o Network mostra um `POST /api/auth/renovar` e a repetição da chamada; abra duas abas e repita: **uma** renovação só.
  2. **Nome da empresa no menu** (`obterEmpresa`, fatia 1): roteiro: com a empresa configurada, a segunda linha da marca mostra o `nomeEmpresa`; sem configurar, só `Portal de RH`.
  3. **Bolinhas do menu** (`obterContadoresDoMenuDoFuncionario`, `...DoGestor`, `...DoRh`, fatia 7): roteiro: números iguais aos das telas de origem (avisos, formulários, documentos, pedidos).
  4. **Download por link temporário e exportação CSV reais** (fatias 1 e 3): roteiro: botão "Ver" de um documento abre o `url` e baixa como anexo; "Exportar CSV" baixa com o nome do cabeçalho `Content-Disposition`.
  5. **`Sair` e perda de sessão entre abas** com a API real (como a fatia 0, Task 6, passo 3).
- [ ] **Step 3: Lista de telas a conferir ao lado do PNG** (`01-regras.md`, A11; escreva na conversa e em "Conferência visual" do `PROGRESSO.md`): o **menu e o rodapé** de cada perfil (PNG 00 e 01 do RH, 32 do funcionário, 46 do gestor; a 1a mostra só os itens que já têm rota; o grupo "Meu espaço" do RH e do gestor só aparece quando as rotas pessoais existirem); o **início mínimo** do RH (PNG 00, só a saudação) e da pessoa (PNG 32, só a saudação); e, **na vitrine** (`$env:VITE_VITRINE='sim'; npm --prefix front-end run dev`, endereço `/eu/vitrine`): botões, selos e tabela (PNG 01), campos e painel (PNG 02), janela, cartões de opção e área de soltar arquivo (PNG 10), etapas (PNG 17 e 18), selos de estado (PNG 31 e 33), abas (PNG 01 e 03). **Diga à pessoa que a fonte é a Inter, decidida pelo dono em 2026-11-02** (ela só confere a letra ao lado do PNG) e que o ícone do dente e o da creche são aproximações (`04-visual.md`, 5).
- [ ] **Step 4: Fechar o `PROGRESSO.md`** da 1a: "Onde estou" = próximo passo exato é a primeira tarefa de `fatia-1b-telas.md`; telas "Início do RH" e "Início da pessoa" como `feita`. A 1a não deixa dúvida aberta: `D-menu`, `D-rodape` e `D-fonte` foram decididas pelo dono em 2026-11-02 (`DUVIDAS.md`, "Decisões do dono").
- [ ] **Step 5: Enviar.** Run: `git status; git add front-end docs\plano-front-end\PROGRESSO.md docs\plano-front-end\DUVIDAS.md; git commit -m "docs(front): fecha a fundacao (provas, pendencias de integracao e conferencia visual)"; git push`
  Antes do push: `git log origin/main..HEAD --format=%B | Select-String -Pattern 'Co-Authored-By|Claude|Generated with'` não pode listar nada (`01-regras.md`, C6). **Não** abra pull request agora: o PR 2 é o do fim do plano inteiro (`01-regras.md`, C3).
- [ ] **Step 6: Seguir para a 1b** sem esperar resposta de dúvida (nenhuma bloqueia): `fatia-1b-telas.md`.

---

## Decisões do dono que esta fatia constrói (2026-11-02; sem dúvida a registrar)

Os três pontos que a 1a deixava como dúvida foram **decididos pelo dono em 2026-11-02**; a linha de cada um está em `DUVIDAS.md`, "Decisões do dono". Nada a registrar como dúvida:

- **`D-menu` = sim.** O RH e o gestor ganham o grupo **"Meu espaço"** no fim do menu, com os itens pessoais (Task 12, `menu.ts`, **linhas ativas**, nenhuma comentada). "Meus documentos" continua nos três menus.
- **`D-rodape` = botões no rodapé do menu.** Dois botões de texto pequenos, abaixo do nome e do cargo: "Trocar senha" (chega na 1b) e "Sair" (Task 12, `RodapeDoMenu.tsx`).
- **`D-fonte` = manter a Inter.** A troca para a Hanken Grotesk só se o dono pedir depois (`04-visual.md`, seção 4). A pessoa ainda confere a letra ao lado do PNG, sem bloquear.

---

## Para a fatia 1b e as fichas: o que esta fundação entrega (arquivo e assinatura)

**Como acrescentar uma tela (3 linhas):** (1) uma linha em `src\app\tabelaDeRotas.tsx` (`ROTAS_RH`, `ROTAS_GESTOR` ou `ROTAS_EU`: `{ caminho: 'colaboradores', elemento: <ListaDeColaboradores />, filhas: [...] }`; o **perfil vem da família**); (2) o item do menu **já está** em `src\app\menu.ts` e **aparece sozinho** quando a rota existe; (3) o teste de contrato da tela (`esperarRequisicaoConforme`, `03-contrato-e-mocks.md`, seção 6). Painel lateral ou janela sobre uma lista = rota **filha** (`/rh/colaboradores/novo`, `/rh/colaboradores/:funcionarioId`) cuja página renderiza `<Janela aberta aoFechar={() => navigate('..')} posicao="direita">`, e a lista renderiza um `<Outlet />`.

**Caminhos: a tabela única é `05-caminhos-de-tela.md`** (**todas** as telas, inclusive as das fatias 2 a 8; vale sobre qualquer caminho de nota e sobre a tabela abaixo). A tabela abaixo é só a **equivalência** do que as notas propuseram com o que a fundação fixou, para quem lê uma nota que ainda cita o caminho antigo; **onde divergir do `05-`, vale o `05-`** (as notas dizem "se a fundação já fixou outro padrão, adapte e anote em `PROGRESSO.md`"):

| Nota propõe | Fundação |
|---|---|
| `/formularios`, `/formularios/:formularioId/responder`, `/formularios/:formularioId/minhas-respostas` | `/eu/formularios`, `/eu/formularios/:formularioId/responder`, `/eu/formularios/:formularioId/minhas-respostas` |
| `/treinamentos`, `/treinamentos/:treinamentoId` | `/eu/treinamentos`, `/eu/treinamentos/:treinamentoId` |
| `/meus-documentos` | `/eu/meus-documentos` |
| `/esqueci-minha-senha` | `/esqueci-senha` |
| `/definir-senha?token=` | `/definir-senha?token=` (**igual**; é o do contrato) |
| `/rh/...`, `/gestor/...`, `/rh/auditoria`, `/gestor/equipe/:funcionarioId` | iguais; o menu do gestor "Painel da equipe" aponta a `/gestor/equipe` |
| `/formulario/:token`, `/certificado`, `/certificado/:codigo` (públicas) | acrescente em `ROTAS_PUBLICAS` (caminho absoluto) |
| Meu ponto, Férias, Holerites, Avisos, Benefícios, Advertências do funcionário | `/eu/meu-ponto`, `/eu/ferias`, `/eu/holerites`, `/eu/avisos`, `/eu/beneficios`, `/eu/advertencias` (os caminhos do menu em `menu.ts`) |

**Bolinhas do menu:** a fundação **já as mostra** (`useContadoresDoMenu` lê `contadores-do-menu`; `invalidarContadoresDoMenu(queryClient)` as refaz). Os números reais chegam sozinhos quando o back-end entregar as rotas dos números; **nenhuma ficha** precisa "ligá-las" nem "trocar a ausência de bolinhas".

**Componentes (`front-end\src\shared\ui\`; todos com teste; a vitrine os mostra):**

| Arquivo | Assinatura resumida |
|---|---|
| `Botao.tsx`, `botaoClasses.ts`, `BotaoLink.tsx` | `Botao({ variante?: 'primario'\|'secundario'\|'texto'\|'perigo'\|'claro', tamanho?: 'normal'\|'pequeno', carregando?, icone?, larguraTotal?, ...button })`; `BotaoLink({ to, variante?, tamanho? })` |
| `Selo.tsx`, `Cartao.tsx`, `Avatar.tsx`, `Bolinha.tsx`, `LogoR.tsx` | `Selo({ tom?: 'neutro'\|'sucesso'\|'info'\|'alerta'\|'erro'\|'contorno' })`; `Cartao({ tom?: 'branco'\|'suave'\|'destaque'\|'escuro'\|'tracejado' })`; `Avatar({ nome, tamanho?: 'lista'\|'menu'\|'ficha' })`; `Bolinha({ valor })` |
| `Aviso.tsx`, `AvisoDeErro.tsx`, `EstadoVazio.tsx`, `Carregando.tsx`, `Esqueleto.tsx` | `AvisoDeErro({ erro, aoTentarDeNovo? })`; `EstadoVazio({ titulo, descricao?, icone?, acao? })`; `Carregando({ texto? })` |
| `CabecalhoDePagina.tsx`, `Secao.tsx`, `Progresso.tsx` | `CabecalhoDePagina({ titulo, subtitulo?, acoes? })` (o `<h1>` de **toda** tela); `Secao({ titulo, acao?, children })`; `Progresso({ valor, maximo?, rotulo })` |
| `Campo.tsx`, `Entrada.tsx`, `AreaDeTexto.tsx` | `Campo({ rotulo, dica?, erro?, children })`; `Entrada({ icone?, mascara?, ...input })` (serve ao `register`) |
| `Selecao.tsx`, `ComboDeBusca.tsx`, `CaixaDeSelecao.tsx`, `Interruptor.tsx`, `OpcoesEmCartao.tsx` | controlados: `Selecao({ opcoes, valor, aoMudar, rotulo, placeholder?, carregando? })`; `ComboDeBusca({ opcoes, valor, aoMudar, rotulo, aoDigitar? })`; `CaixaDeSelecao({ marcada, aoMudar, rotulo })`; `Interruptor({ ligado, aoMudar, rotulo })`; `OpcoesEmCartao({ opcoes, valor, aoMudar, rotulo, colunas? })` (no formulário, por `Controller`) |
| `CampoDeBusca.tsx`, `SeletorDeArquivo.tsx` | `CampoDeBusca({ valor, aoMudar, esperaMs? })` (300 ms); `SeletorDeArquivo({ arquivo, aoMudar, tiposAceitos, tamanhoMaximoMb, erro? })` |
| `Abas.tsx`, `Tabela.tsx`, `Paginacao.tsx`, `Etapas.tsx`, `MenuDeAcoes.tsx` | `Abas({ itens, valor, aoMudar, rotulo, variante? })`; `Tabela<L>({ rotulo, colunas, linhas, chaveDaLinha, carregando?, erro?, aoTentarDeNovo?, vazio?, ordenacao?, aoOrdenar?, detalhe?, rodape? })`; `Paginacao({ dados, aoMudar })`; `Etapas({ etapas, atual })`; `MenuDeAcoes({ acoes, rotulo })` |
| `Janela.tsx`, `JanelaDeConfirmacao.tsx` | `Janela({ aberta, aoFechar, titulo, posicao?: 'centro'\|'direita', tamanho?, largura?, rodape?, children })`; `JanelaDeConfirmacao({ aberta, aoCancelar, aoConfirmar, titulo, variante?: 'simples'\|'digitar'\|'pendentes', ... })` (`digitar`: `textoParaDigitar`; `pendentes`: `pendentes`, `assunto`, `consequencia?`) |
| `NotificacoesProvider.tsx`, `useNotificacao.ts`, `Markdown.tsx` | `useNotificacao().sucesso(mensagem)`; `Markdown({ texto })` (use **sempre** que o contrato diz "texto ou markdown") |
| `estilo.ts` | `cn(...classes)` |

**Ajudantes:**

| Arquivo | Assinatura |
|---|---|
| `src\shared\formato\documentos.ts` | `mascararCpf`, `mascararTelefone`, `formatarCpf`, `formatarTelefone`, `cpfValido`, `telefoneValido`, `somenteDigitos` |
| `src\shared\formato\datas.ts` | `formatarData` (dia de calendário), `formatarDataHora`, `formatarHora`, `formatarDiaMes`, `formatarInstanteRelativo`, `hojeEmSaoPaulo`, `dataPorExtenso`, `saudacao` |
| `src\shared\formato\numeros.ts`, `texto.ts` | `formatarMinutosComoHoras(min, { sinal? })`, `formatarReais(centavos)`, `formatarNumero`, `iniciais`, `primeiroNome`, `plural` |
| `src\shared\form\FormularioComErroDoServidor.tsx`, `esquemas.ts`, `aplicarErrosDoServidor.ts` | `<FormularioComErroDoServidor esquema valoresIniciais aoEnviar>{(form) => ...}</...>`; `campoTexto`, `campoEmail`, `campoCpf`, `campoTelefone`, `campoData`, `campoHora`, `opcional` |
| `src\shared\api\api.ts` | `$api` (hooks `useQuery`, `useMutation`); `clienteHttp` (para `POST` com `multipart`) |
| `src\shared\api\invalidar.ts`, `mensagemDeErro.ts`, `consultas.ts` | `invalidarRota(queryClient, '/api/rh/funcionarios')` ("depois de gravar, atualize"); `mensagemDeErro(erro)`; `criarQueryClient()` |
| `src\app\useContadoresDoMenu.ts` | `invalidarContadoresDoMenu(queryClient)` ("atualize as bolinhas": avisos, formulários, documentos e pedidos) |
| `src\shared\lista\useEstadoDaLista.ts` | `useEstadoDaLista({ filtros: ['situacao', 'setorId'] as const })` → `{ consulta, busca, pagina, filtros, ordenacao, definirBusca, definirFiltro, definirPagina, alternarOrdenacao }`; combine com `Tabela`, `Paginacao` e `CampoDeBusca` |
| `src\shared\arquivos\baixar.ts`, `multipart.ts`, `validarArquivo.ts` | `abrirLinkTemporario(link)`, `baixarExportacao(chamada, nomePadrao)`, `baixarBlob`, `multipart<Corpo>({ arquivo, ...})`, `validarArquivo(arquivo, regras)` |
| `src\test\renderizar.tsx`, `sessao.ts`, `renderizarApp.tsx` | `renderizarComProvedores(ui, { rota? })`; `entrarComo(perfil)`; `renderizarApp({ rota? })` |

**Páginas públicas que a 1b troca:** `/esqueci-senha` e `/definir-senha` hoje apontam a `PaginaEmBreve` em `ROTAS_PUBLICAS`; o link `Esqueci minha senha` entra na `PaginaEntrar`. O botão `Trocar senha` entra no `RodapeDoMenu.tsx`, ao lado do `Sair`.

---

## Cobertura e autorrevisão (do autor deste plano)

- **Cobertura do pedido (roadmap, decisão 1; critério de pronto "menu por perfil, três telas de ponta a ponta e o build sob a política", sendo as três telas da 1b):** tokens e estilo (Task 1); formatação com testes em outro fuso (Task 2); componentes-base com a peça do Base UI e testes (Tasks 3 a 6); markdown sob a política (Task 7); formulários com o mapa `errors` do 400 (Task 8); `$api`, `QueryClientProvider` e renovação do token no 401 (Task 9); download, upload e CSV (Task 10); estado da lista na URL (Task 11); rotas por perfil, menu por tabela, layout, rodapé, bolinhas de `contadores-do-menu`, início mínimo, 403 e 404 (Task 12); a prova sob a política estendida à fundação, com a vitrine (Task 13); verificação final e pendências de integração (Task 14). O teste de contrato por tela (`esperarRequisicaoConforme`) é **da fatia 0** (`src\test\contrato.ts`); a fundação não o refaz e manda usá-lo (seção "Para a fatia 1b").
- **Medido para este plano, no projeto de teste** (fora do repositório; `04-visual.md` traz os números): 0 violações com todas as peças do Base UI usadas aqui abertas ao mesmo tempo (inclusive `AlertDialog`, `Drawer`, `Field`, `Input` e `Meter`, que a fatia 0 não tinha medido); a imagem externa do markdown **viola** `img-src` (por isso `img` fica fora); a prop `style` do React passa e o download por blob também (a sonda de download do `csp` o mede: `03-contrato-e-mocks.md`, seção 8); **a vitrine só sai do build normal quando o `lazy(import())` é declarado depois do `return` condicional** (com a declaração no topo do módulo, ela continuava no `dist`: foi testado); os padrões de teste de `Dialog`, `Select`, `Combobox`, `Field`, `Toast`, React Hook Form + zod, e `createMemoryRouter` rodam no jsdom.
- **Escolhas de engenharia deste plano** (o dono pode vetar; nenhuma muda o que o usuário vê, exceto as marcadas): o **item do menu só aparece quando a rota existe** (a pessoa nunca vê link para página que não existe); o **perfil vem da família da rota** (`/rh`, `/gestor`, `/eu`), não de cada rota; perfil errado mostra **403 no lugar** (o endereço fica) e não redireciona; **cache de consultas limpo ao sair**; `retry` das consultas: nunca em 4xx, no máximo 2 em outros erros; `staleTime` de 30 s; **uma** repetição por chamada no 401; `Sair` e `Trocar senha` visíveis no rodapé (**decidido pelo dono**, D-rodape, 2026-11-02); `Meus documentos` nos três menus, `Benefícios` no do gestor e o grupo "Meu espaço" no RH e no gestor (**decidido pelo dono**, D-menu, 2026-11-02); saudação por hora (`Bom dia`, `Boa tarde`, `Boa noite`) só no RH, como no PNG 00 (suposto, `telas\fatia-1.md`); `favicon.svg` trocado pelo "R" da paleta; a tela de entrar da fatia 0 **não** é redesenhada aqui (a 1b traz o "Esqueci minha senha" e o acabamento).
- **Passos que contradizem a fatia 0, de propósito:** o `PortaDeAcesso` e o `ShellDoPortal` saem (a fatia 0 avisou: "navegação é da 1a"); o rodapé não escreve mais `Funcionário`/`Gestor`/`RH`, escreve a linha de apoio do Figma (`telas\fatia-0.md`); **as asserções dos testes velhos migram** (login sem usuário, `Carregando…` ao renovar, nome e `Sair`, `Sair` limpa a sessão) e os passos do `csp` da fatia 0 continuam iguais.
- **Fora desta fatia, de propósito:** cartões das telas iniciais, barra de presença e calendário de férias (7d); o `Trocar senha` e o `Esqueci minha senha` (1b); gráficos (`recharts`, quando a fatia 7 pedir); notificação por e-mail (não existe no front).
- **Autorrevisão da skill:** (1) cobertura: cada requisito do pedido tem tarefa (acima). (2) passos: cada um tem uma coisa só e um resultado (comando e saída esperada); onde a assinatura não decide (a lógica de repetição no 401, o `twMerge` estendido) o passo diz a escolha. (3) tipos entre tarefas: `Esquema<'Paginacao'>`, `ErroDeApi`, `Perfil`, `Usuario`, `cn`, `mensagemDeErro`, `Botao`, `Campo`, `Entrada`, `AvisoDeErro`, `renovadorDeAcesso`, `$api`, `Provedores`, `invalidarRota`, `caminhosDasRotas`, `menuVisivel`, `ChaveDeContador` têm **um** dono e as tarefas que os usam listam-no em `Consumes`. (4) Review Focus: cinco itens, cada um com teste (Tasks 2, 7, 9, 11 e 12). (5) proporção: o plano é longo porque a fundação tem 40 peças; o código escrito aqui é só assinatura, nome de teste e valor exato.
