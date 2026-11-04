#!/usr/bin/env node
// violacoes-de-politica.mjs
//
// PARA QUE SERVE
//   Prova que o BUILD do front (dist) roda sob a política de segurança do site sem nenhuma violação.
//   Serve o `dist` com os cabeçalhos de public/staticwebapp.config.json (servidor-de-teste.mjs), abre a
//   página num navegador headless (Playwright, `playwright-core`), percorre a tela de entrada e o shell, e
//   FALHA se houver qualquer evento `securitypolicyviolation`, exceção não tratada na página ou requisição
//   que falhou. A API é simulada no navegador (`page.route`): a política é aplicada antes, então uma chamada
//   a uma origem que o `connect-src` não permite aparece como violação, como no Azure.
//   Faz também 4 CONTROLES POSITIVOS (injeta de propósito um <style>, um style="", uma imagem data: e um
//   fetch para uma origem fora do connect-src) para provar que a política está ativa e o ouvinte funciona:
//   sem eles, "zero violações" não prova nada. Se um controle não violar, o script falha e diz que o
//   resultado não vale.
//
// A SONDA DE DOWNLOAD (três passos fixos, antes da linha ACRESCENTE AQUI)
//   Prova que baixar arquivo funciona sob a política, sem violação: (1) um CSV por Blob (fetch, Blob,
//   URL.createObjectURL e um <a download> clicado por script: o que `baixarBlob` do front faz); (2) um
//   link temporário de OUTRA origem (o armazenamento de arquivos: <a href target=_blank rel=noopener>,
//   o que `abrirLinkTemporario` faz); (3) o mesmo link para a PRÓPRIA origem do site (rota de teste
//   /__sonda/arquivo.pdf do servidor-de-teste.mjs). Cada passo espera o evento `download` do navegador,
//   confere o nome e o começo do conteúdo do arquivo e conta as violações (o relatório falha se houver).
//   As rotas de teste da API simulada (stubDaApi) são rotas REAIS do contrato: GET
//   /api/rh/auditoria/exportacao (exportarAuditoria, devolve os bytes do CSV direto) e GET
//   /api/rh/funcionarios/12/documentos/61/download (baixarDocumentoDoFuncionario, devolve um
//   LinkTemporario). Assim a fundação (1a, Task 13) chama as funções de verdade contra elas.
//
// COMO RODA
//   Da raiz do repositório, no PowerShell (precisa do build antes):
//     npm --prefix front-end run build
//     npm --prefix front-end run csp
//   O package.json chama: node scripts/violacoes-de-politica.mjs
//   Opções: --dist <pasta> (padrão: dist), --config <arquivo> (padrão: public/staticwebapp.config.json),
//           --contrato <openapi.yaml> (padrão: ../contrato/openapi.yaml; de onde sai o exemplo da sessão),
//           --canais <lista> (padrão: msedge,chrome; os navegadores que ele tenta abrir, na ordem).
//   Código de saída 0 = nenhuma violação e controles ok; 1 = violação, falha de passo ou controle mudo;
//   2 = nenhum navegador instalado (Edge ou Chrome): escreve "pendente: csp", não baixa nada e a pessoa
//   roda o script numa máquina que tenha um deles.
//   Navegador: o que JÁ está instalado, pelo playwright-core: tenta o Edge (msedge) e depois o Chrome
//   (chrome). Não baixa navegador nenhum.
//
// ONDE COPIAR
//   Para front-end\scripts\violacoes-de-politica.mjs, junto com servidor-de-teste.mjs (mesma pasta).
//   Dependências: `playwright-core` e `yaml` (já no package.json do plano).
//
// COMO ESTENDER (uma vez por fatia, na tarefa que fecha a fatia)
//   Acrescente um passo no fim da lista PASSOS abaixo: abra a rota principal da tela nova (e, se ela abre
//   um diálogo, uma lista de seleção ou um menu, abra-os). A API é a do `stubDaApi`: SÓ `entrar`,
//   `renovar`, `sair` e `eu` (e as duas rotas da sonda de download) respondem de verdade; todo o resto,
//   inclusive `/api/auth/senha/*` (esqueci a senha, validar link, definir senha), devolve 404
//   problem+json, e a tela mostra o estado de erro, o que também exercita a política. Os dados de verdade
//   ficam por conta dos testes com MSW; aqui o objetivo é só "sem violação".
//
// REDE OCIOSA ANTES DE DESMONTAR A TELA (medido em 2026-11-02, Edge 156 e Chrome 149, playwright-core 1.64.1)
//   A tela mostra o título na hora e pede os dados em seguida. O que o teste mediu: (1) um `goto` com um
//   pedido ainda em andamento NÃO gera `requestfailed` (o navegador descarta a página inteira); (2) um
//   pedido cancelado por `AbortController` GERA `net::ERR_ABORTED`, e é isso que o react-query faz com a
//   consulta que perde a tela (o `openapi-react-query` repassa o `signal`): o painel que é rota e é
//   fechado com `Escape` assim que o diálogo aparece cancela as consultas dele, e o script as contaria
//   como "requisição que falhou". Por isso: o laço dos passos espera `aquietar()` (a rede ociosa) ao fim
//   de CADA passo, e um passo que desmonta a tela por interação (`Escape` num painel que é rota, clique
//   num link do menu) ou que faz vários `goto` seguidos chama `await aquietar()` ANTES de cada ação.
//   Não ignore `ERR_ABORTED` em geral: ele é um sinal.
//
// PASSOS DA VITRINE (1a, Task 13): `[pulado]` SÓ para a vitrine
//   A página /eu/vitrine só existe no build feito com VITE_VITRINE=sim. Os passos dela são criados com
//   `vitrine.passo(nome, fazer)`; o primeiro é `vitrine.passo('abre a vitrine', () => vitrine.abrir())`. Se
//   a vitrine não existe neste build (aparece "Página não encontrada."), `abrir()` marca
//   `vitrine.indisponivel` e TODOS os passos da vitrine saem `[pulado]` sem falhar (se só o primeiro se
//   pulasse, os outros esperariam 8 s por um botão que não existe e falhariam). Passo que não é da vitrine
//   nunca pode ser pulado. Um `csp` só prova a vitrine se ela rodou ao menos uma vez com a variável ligada
//   (`$env:VITE_VITRINE = 'sim'; npm --prefix front-end run build`, na mesma linha).
//   Saída: linha `[FALHOU]` = passo que falhou; `[pulado]` = passo da vitrine sem vitrine; nenhuma linha
//   `[FALHOU]` e o `PASSOU:` final é o que a "Prova padrão de fatia" exige.
//
// ATENÇÃO: os passos iniciais usam os textos da fatia 0 (botão "Entrar", rótulos "E-mail" e "Senha", a
//   mensagem "Preencha o e-mail e a senha.", o texto "Portal de RH" do shell e o botão "Sair"). Se a
//   fundação (fatia 1a) mudar algum deles, atualize os passos junto, na mesma tarefa.
//
// O QUE ESTE SCRIPT NÃO PROVA
//   O Azure mesmo: se o arquivo staticwebapp.config.json será aplicado do jeito que o servidor de teste
//   imita, só se confirma no ambiente (Task 14 do plano do back-end).
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { chromium } from 'playwright-core'
import { parse } from 'yaml'
import { iniciarServidor, ROTA_DA_SONDA } from './servidor-de-teste.mjs'

function opcao(nome, padrao) {
  const i = process.argv.indexOf(nome)
  return i >= 0 ? process.argv[i + 1] : padrao
}
const pasta = join(import.meta.dirname, '..')
const dist = resolve(opcao('--dist', join(pasta, 'dist')))
const config = resolve(opcao('--config', join(pasta, 'public', 'staticwebapp.config.json')))
const contrato = resolve(opcao('--contrato', join(pasta, '..', 'contrato', 'openapi.yaml')))
const canais = opcao('--canais', 'msedge,chrome').split(',')

// Rotas da sonda de download: duas rotas reais do contrato, simuladas aqui, e o "armazenamento".
const CAMINHO_DO_CSV = '/api/rh/auditoria/exportacao'
const CAMINHO_DO_LINK = '/api/rh/funcionarios/12/documentos/61/download'
const ARMAZENAMENTO = 'https://armazenamento.exemplo.test'

// ---- Verificações estáticas do build --------------------------------------------------------------
const falhas = []
if (!existsSync(join(dist, 'index.html')))
  throw new Error(
    `Não achei ${join(dist, 'index.html')}. Rode antes: npm --prefix front-end run build`,
  )
if (existsSync(join(dist, 'mockServiceWorker.js')))
  falhas.push('dist/mockServiceWorker.js existe: o script postbuild do package.json deve apagá-lo.')
const pastaDeJs = join(dist, 'assets')
if (existsSync(pastaDeJs))
  for (const f of readdirSync(pastaDeJs).filter((n) => n.endsWith('.js')))
    if (/mockServiceWorker|setupWorker/.test(readFileSync(join(pastaDeJs, f), 'utf8')))
      falhas.push(
        `dist/assets/${f} contém código do MSW: ele só pode rodar em desenvolvimento e nos testes.`,
      )

// ---- A sessão simulada vem do exemplo do contrato (POST /api/auth/entrar, 200) --------------------
const docDoContrato = parse(readFileSync(contrato, 'utf8'))
const sessaoDeExemplo =
  docDoContrato.paths['/api/auth/entrar'].post.responses['200'].content['application/json'].example
const problema = (status, titulo, detalhe) => ({
  title: titulo,
  status,
  detail: detalhe,
  traceId: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00',
})

const servidor = await iniciarServidor({ dist, config })
const politica = servidor.cabecalhos['Content-Security-Policy']
const origemDaApi = /connect-src 'self' (\S+)/.exec(politica)?.[1]
if (!origemDaApi) throw new Error(`A política não tem origem da API no connect-src: ${politica}`)
console.log(`servidor de teste em ${servidor.url}\npolítica: ${politica}`)
if (/exemplo\.test|invalid/.test(politica))
  console.log(
    'AVISO: o connect-src ainda tem a URL-marcador da API (o dono a preenche na Task 14 do back-end).',
  )

// ---- Navegador -------------------------------------------------------------------------------------
let navegador
let qual
for (const canal of canais) {
  try {
    navegador = await chromium.launch({ channel: canal, headless: true })
    qual = `${canal} ${navegador.version()}`
    break
  } catch (erro) {
    console.log(`não abriu ${canal}: ${String(erro).split('\n')[0]}`)
  }
}
if (!navegador) {
  await servidor.fechar()
  console.log(
    `pendente: csp. Nenhum navegador instalado (tentei: ${canais.join(', ')}). O script usa o Edge ou o Chrome que já estão na máquina e não baixa nenhum. Rode-o numa máquina que tenha um deles: npm --prefix front-end run build; npm --prefix front-end run csp`,
  )
  process.exit(2)
}
console.log(`navegador: ${qual}`)

const contexto = await navegador.newContext({ locale: 'pt-BR', timezoneId: 'America/Sao_Paulo' })
const violacoes = []
let passoAtual = 'inicio'
// Chega ao Node na hora (sobrevive a reload e navegação).
await contexto.exposeFunction('registrarViolacao', (v) =>
  violacoes.push({ passo: passoAtual, ...v }),
)
await contexto.addInitScript(() => {
  document.addEventListener(
    'securitypolicyviolation',
    (e) =>
      window.registrarViolacao({
        diretiva: e.effectiveDirective,
        bloqueado: e.blockedURI,
        arquivo: e.sourceFile,
        linha: e.lineNumber,
        coluna: e.columnNumber,
        amostra: e.sample,
      }),
    true,
  )
})

const pagina = await contexto.newPage()
const excecoes = []
const requisicoesQueFalharam = []
let cabecalhoVisto
pagina.on('pageerror', (e) => excecoes.push({ passo: passoAtual, erro: String(e).slice(0, 300) }))
pagina.on('requestfailed', (r) => {
  if (!r.url().endsWith('/favicon.ico'))
    requisicoesQueFalharam.push({ passo: passoAtual, url: r.url(), erro: r.failure()?.errorText })
})
pagina.on('response', (r) => {
  if (r.url() === `${servidor.url}/` && !cabecalhoVisto)
    cabecalhoVisto = r.headers()['content-security-policy']
})

// A API simulada. O CORS é necessário porque a origem da API (do connect-src) não é a do servidor.
async function stubDaApi(rota) {
  const req = rota.request()
  const cors = {
    'access-control-allow-origin': servidor.url,
    'access-control-allow-headers': 'authorization, content-type',
    'access-control-allow-methods': 'GET, POST, PUT, DELETE, OPTIONS',
  }
  const json = (status, corpo, tipo = 'application/json') =>
    rota.fulfill({ status, headers: cors, contentType: tipo, body: JSON.stringify(corpo) })
  if (req.method() === 'OPTIONS') return rota.fulfill({ status: 204, headers: cors })
  const caminho = new URL(req.url()).pathname
  if (req.method() === 'POST' && caminho === '/api/auth/entrar') return json(200, sessaoDeExemplo)
  if (req.method() === 'POST' && caminho === '/api/auth/renovar') return json(200, sessaoDeExemplo)
  if (req.method() === 'POST' && caminho === '/api/auth/sair')
    return rota.fulfill({ status: 204, headers: cors })
  if (req.method() === 'GET' && caminho === '/api/auth/eu')
    return json(200, sessaoDeExemplo.usuario)
  // Sonda de download: o CSV vem direto (os bytes) e o arquivo guardado vem por link temporário.
  if (req.method() === 'GET' && caminho === CAMINHO_DO_CSV)
    return rota.fulfill({
      status: 200,
      headers: {
        ...cors,
        'access-control-expose-headers': 'content-disposition',
        'content-disposition': 'attachment; filename="sonda.csv"',
      },
      contentType: 'text/csv; charset=utf-8',
      body: 'a;b\n1;2\n',
    })
  if (req.method() === 'GET' && caminho === CAMINHO_DO_LINK)
    return json(200, { url: `${ARMAZENAMENTO}/sonda.pdf`, expiraEmUtc: '2026-10-07T13:00:00Z' })
  return json(
    404,
    problema(404, 'Não encontrado.', 'O registro não foi encontrado.'),
    'application/problem+json',
  )
}
await pagina.route('**/api/**', stubDaApi)
// O armazenamento de arquivos (outra origem): devolve o PDF como anexo. É no contexto (e não na página)
// porque o link abre numa aba nova, que não herda as rotas da página.
await contexto.route(`${ARMAZENAMENTO}/**`, (rota) =>
  rota.fulfill({
    status: 200,
    contentType: 'application/pdf',
    headers: { 'content-disposition': 'attachment; filename="sonda.pdf"' },
    body: '%PDF-1.4 sonda',
  }),
)

// Espera o próximo download (da página ou de uma aba aberta por ela) e o devolve.
function esperarDownload() {
  return new Promise((resolver, rejeitar) => {
    const parar = () => {
      clearTimeout(limite)
      pagina.off('download', achou)
      contexto.off('page', aoAbrirAba)
    }
    const achou = (download) => {
      parar()
      resolver(download)
    }
    const aoAbrirAba = (aba) => aba.once('download', achou)
    const limite = setTimeout(() => {
      parar()
      rejeitar(new Error('nenhum download começou em 8 s'))
    }, 8000)
    pagina.on('download', achou)
    contexto.on('page', aoAbrirAba)
  })
}
async function conferirDownload(download, nome, comeco) {
  if (download.suggestedFilename() !== nome)
    throw new Error(
      `o arquivo baixado se chama "${download.suggestedFilename()}", esperava "${nome}"`,
    )
  const caminho = await download.path()
  const texto = caminho ? readFileSync(caminho, 'utf8') : ''
  if (!texto.startsWith(comeco))
    throw new Error(`o conteúdo baixado não começa com "${comeco}": "${texto.slice(0, 40)}"`)
}
// Clica, na página, num link que o código acaba de montar (como `abrirLinkTemporario` do front).
const abrirLink = (url) =>
  pagina.evaluate((href) => {
    const a = document.createElement('a')
    a.href = href
    a.rel = 'noopener noreferrer'
    a.target = '_blank'
    document.body.appendChild(a)
    a.click()
    a.remove()
  }, url)

const T = { timeout: 8000 }
pagina.setDefaultTimeout(8000) // passo que não acha o que espera falha em 8 s, não em 30 s

// Espera a rede ficar ociosa (500 ms sem pedido). O laço dos passos a chama ao fim de cada um; um passo
// que desmonta a tela por interação (Escape num painel que é rota, clique no menu) a chama antes.
async function aquietar() {
  try {
    await pagina.waitForLoadState('networkidle')
  } catch {
    throw new Error('a rede não ficou ociosa em 8 s (a página não para de pedir dados?)')
  }
}

// Passo que só existe quando a vitrine existe (build com VITE_VITRINE=sim). `[pulado]` vale SÓ para ele.
class Pulado extends Error {}
const vitrine = {
  indisponivel: false,
  // Cria um passo da vitrine: sem a vitrine neste build, ele sai `[pulado]` sem falhar.
  passo: (nome, fazer) => ({ nome, fazer, vitrine: true }),
  // Primeiro passo da vitrine: abre /eu/vitrine e descobre se ela existe neste build. Se não existe
  // ("Página não encontrada."), marca `indisponivel` e TODOS os passos da vitrine saem `[pulado]`.
  async abrir() {
    await pagina.goto(servidor.url + '/eu/vitrine')
    const existe = pagina.getByRole('heading', { name: 'Vitrine de componentes' })
    const ausente = pagina.getByText('Página não encontrada.')
    await existe.or(ausente).first().waitFor(T)
    if (await ausente.isVisible()) {
      vitrine.indisponivel = true
      throw new Pulado('vitrine ausente (build de produção)')
    }
  },
}
// ---- Passos ---------------------------------------------------------------------------------------
// Cada passo roda e, depois dele, o que violou fica registrado com o nome do passo.
const PASSOS = [
  {
    nome: 'abre a tela de entrar',
    fazer: async () => {
      await pagina.goto(servidor.url + '/')
      await pagina.getByRole('button', { name: 'Entrar' }).waitFor(T)
      // A fonte (Inter) é servida do próprio domínio: tem de ter carregado sem violação.
      await pagina.evaluate(() => document.fonts.ready)
    },
  },
  {
    // Sem `z.config({ jitless: true })` o zod tenta `eval` e a política o barra (medido). A violação
    // já aparece ao carregar a tela (o esquema é criado lá); este passo faz a validação rodar de fato.
    nome: 'envia o formulário vazio (validação com zod)',
    fazer: async () => {
      await pagina.getByRole('button', { name: 'Entrar' }).click()
      await pagina.getByText('Preencha o e-mail e a senha.').waitFor(T)
    },
  },
  {
    nome: 'entra (API simulada) e vê o shell',
    fazer: async () => {
      await pagina.getByLabel('E-mail').fill('ana.souza@empresa.com.br')
      await pagina.getByLabel('Senha').fill('Senha#Forte123')
      await pagina.getByRole('button', { name: 'Entrar' }).click()
      await pagina.getByText('Portal de RH').waitFor(T)
      await pagina.getByText(sessaoDeExemplo.usuario.nome).waitFor(T)
    },
  },
  {
    nome: 'recarrega a página e continua logado (renovação com o refresh guardado)',
    fazer: async () => {
      await pagina.reload()
      await pagina.getByText('Portal de RH').waitFor(T)
    },
  },
  {
    nome: 'sai e volta para a tela de entrar',
    fazer: async () => {
      await pagina.getByRole('button', { name: 'Sair' }).click()
      await pagina.getByRole('button', { name: 'Entrar' }).waitFor(T)
    },
  },
  {
    nome: 'abre um link direto (/rh/colaboradores): o navigationFallback devolve o app',
    fazer: async () => {
      await pagina.goto(servidor.url + '/rh/colaboradores')
      await pagina.getByRole('button', { name: 'Entrar' }).waitFor(T)
    },
  },
  {
    // Sonda de download 1/3: CSV por Blob (como `baixarBlob`). O Blob vira uma URL blob: que só o <a download>
    // usa; não é imagem (img-src) nem requisição (connect-src), e a medição diz: baixa, 0 violações.
    nome: 'sonda de download: CSV por Blob',
    fazer: async () => {
      const [download] = await Promise.all([
        esperarDownload(),
        pagina.evaluate(async (url) => {
          const resposta = await fetch(url)
          const nome = /filename="([^"]+)"/.exec(
            resposta.headers.get('content-disposition') ?? '',
          )?.[1]
          const blob = await resposta.blob()
          const endereco = URL.createObjectURL(blob)
          const a = document.createElement('a')
          a.href = endereco
          a.download = nome ?? 'arquivo.csv'
          document.body.appendChild(a)
          a.click()
          a.remove()
          setTimeout(() => URL.revokeObjectURL(endereco), 1000)
        }, origemDaApi + CAMINHO_DO_CSV),
      ])
      await conferirDownload(download, 'sonda.csv', 'a;b')
    },
  },
  {
    nome: 'sonda de download: link temporário de outra origem (o armazenamento)',
    fazer: async () => {
      const [download] = await Promise.all([
        esperarDownload(),
        pagina
          .evaluate(async (url) => (await fetch(url)).json(), origemDaApi + CAMINHO_DO_LINK)
          .then((link) => abrirLink(link.url)),
      ])
      await conferirDownload(download, 'sonda.pdf', '%PDF')
    },
  },
  {
    nome: 'sonda de download: link para a própria origem do site',
    fazer: async () => {
      const [download] = await Promise.all([
        esperarDownload(),
        abrirLink(servidor.url + ROTA_DA_SONDA),
      ])
      await conferirDownload(download, 'sonda-da-origem.pdf', '%PDF')
    },
  },
  {
    // Fatia 1a, Task 12. Sem `voltar` (o passo 6 deixou /entrar?voltar=/rh/colaboradores, que ainda não existe).
    nome: 'entra e abre o inicio do RH com o menu',
    fazer: async () => {
      await pagina.goto(servidor.url + '/entrar')
      await pagina.getByRole('button', { name: 'Entrar' }).waitFor(T)
      await pagina.getByLabel('E-mail').fill('ana.souza@empresa.com.br')
      await pagina.getByLabel('Senha').fill('Senha#Forte123')
      await pagina.getByRole('button', { name: 'Entrar' }).click()
      await pagina.getByRole('navigation', { name: 'Menu principal' }).waitFor(T)
      await pagina.getByRole('heading', { name: /^(Bom dia|Boa tarde|Boa noite), Ana$/ }).waitFor(T)
    },
  },
  // Fatia 1a, Task 13: a vitrine (só no build com VITE_VITRINE=sim; sem ela, todos saem [pulado]).
  vitrine.passo('abre a vitrine', () => vitrine.abrir()),
  vitrine.passo('abre a janela, o painel e a confirmação', async () => {
    for (const [botao, papel] of [
      ['Abrir janela', 'dialog'],
      ['Abrir painel', 'dialog'],
      ['Abrir confirmação', 'alertdialog'],
    ]) {
      await aquietar()
      await pagina.getByRole('button', { name: botao }).click()
      await pagina.getByRole(papel).waitFor(T)
      await pagina.keyboard.press('Escape')
      await pagina.getByRole(papel).waitFor({ state: 'detached', ...T })
    }
  }),
  vitrine.passo('abre a seleção e o combo', async () => {
    await pagina.getByRole('combobox', { name: 'Cargo (vitrine)' }).click()
    await pagina.getByRole('option').first().waitFor(T)
    await pagina.keyboard.press('Escape')
    await pagina.getByRole('combobox', { name: 'Setor (vitrine)' }).fill('logi')
    await pagina.getByRole('option', { name: /Logística/ }).waitFor(T)
    await pagina.keyboard.press('Escape')
  }),
  vitrine.passo('abre o menu de ações e a notificação', async () => {
    await pagina.getByRole('button', { name: 'Mais ações (vitrine)' }).click()
    await pagina.getByRole('menuitem', { name: 'Editar' }).waitFor(T)
    await pagina.keyboard.press('Escape')
    await pagina.getByRole('button', { name: 'Mostrar notificação' }).click()
    await pagina.getByText('Alterações salvas.').waitFor(T)
  }),
  vitrine.passo('renderiza o markdown de ataque', async () => {
    await pagina.getByTestId('markdown-de-ataque').waitFor(T)
    const xss = await pagina.evaluate(() => window.__xss)
    if (xss !== undefined) throw new Error(`o markdown executou código: window.__xss = ${xss}`)
  }),
  vitrine.passo('baixa pelo código do app (CSV por Blob e link temporário)', async () => {
    const [csv] = await Promise.all([
      esperarDownload(),
      pagina.getByRole('button', { name: 'Baixar CSV de teste' }).click(),
    ])
    await conferirDownload(csv, 'sonda.csv', 'a;b')
    const [pdf] = await Promise.all([
      esperarDownload(),
      pagina.getByRole('button', { name: 'Abrir link de teste' }).click(),
    ])
    await conferirDownload(pdf, 'sonda.pdf', '%PDF')
  }),
  // Fatia 1b, Task 10. A API simulada devolve 404 ao que não conhece: as telas mostram o estado de erro;
  // o que se quer aqui é zero violações. A sessão é a do passo "entra e abre o inicio do RH" (perfil RH).
  {
    nome: 'abre a lista de colaboradores',
    fazer: async () => {
      await pagina.goto(servidor.url + '/rh/colaboradores')
      await pagina.getByRole('heading', { level: 1, name: 'Colaboradores' }).waitFor(T)
    },
  },
  {
    nome: 'abre o painel de cadastro',
    fazer: async () => {
      await pagina.goto(servidor.url + '/rh/colaboradores/novo')
      await pagina.getByRole('dialog', { name: 'Adicionar colaborador' }).waitFor(T)
      await aquietar() // o painel é rota filha: fechar antes cancelaria as consultas das opções
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre o modal de novo cargo',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/rh/cadastros/cargos')
      await pagina.getByRole('heading', { level: 1, name: 'Cadastros' }).waitFor(T)
      await aquietar()
      await pagina.getByRole('button', { name: 'Novo cargo' }).click()
      await pagina.getByRole('dialog', { name: 'Novo cargo' }).waitFor(T)
      await aquietar()
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre os outros cadastros e o log',
    fazer: async () => {
      for (const [caminho, titulo] of [
        ['/rh/cadastros/setores', 'Cadastros'],
        ['/rh/cadastros/gestores', 'Cadastros'],
        ['/rh/cadastros/beneficios', 'Cadastros'],
        ['/rh/cadastros/usuarios', 'Cadastros'],
        ['/rh/cadastros/empresa', 'Cadastros'],
        ['/rh/auditoria', 'Log de auditoria'],
      ]) {
        await aquietar()
        await pagina.goto(servidor.url + caminho)
        await pagina.getByRole('heading', { level: 1, name: titulo }).waitFor(T)
      }
    },
  },
  {
    nome: 'abre Meus documentos',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/eu/meus-documentos')
      await pagina.getByRole('heading', { level: 1, name: 'Meus documentos' }).waitFor(T)
    },
  },
  {
    nome: 'abre o modal de Trocar senha',
    fazer: async () => {
      await aquietar()
      await pagina.getByRole('button', { name: 'Trocar senha' }).click()
      await pagina.getByRole('dialog', { name: 'Trocar senha' }).waitFor(T)
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre as paginas publicas',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/esqueci-senha')
      await pagina.getByRole('heading', { level: 1 }).waitFor(T)
      await aquietar()
      // A API simulada responde 404 ao validar-link: a tela mostra a mensagem com "Tentar de novo".
      await pagina.goto(servidor.url + '/definir-senha?token=teste')
      await pagina.getByRole('button', { name: 'Tentar de novo' }).waitFor(T)
    },
  },
  // Fatia 4 (ficha 4, seção 6: só a matriz das telas da fatia; a sessão é a do RH, que também abre /eu).
  {
    nome: 'abre os avisos do RH e o painel Novo aviso',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/rh/avisos')
      await pagina.getByRole('heading', { level: 1, name: 'Avisos' }).waitFor(T)
      await aquietar()
      await pagina.goto(servidor.url + '/rh/avisos/novo')
      await pagina.getByRole('dialog', { name: 'Novo aviso' }).waitFor(T)
      await aquietar()
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre as advertencias do RH e o painel Nova advertencia',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/rh/advertencias')
      await pagina.getByRole('heading', { level: 1, name: 'Advertências' }).waitFor(T)
      await aquietar()
      await pagina.goto(servidor.url + '/rh/advertencias/nova')
      await pagina.getByRole('dialog', { name: 'Nova advertência' }).waitFor(T)
      await aquietar()
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre o envio de holerites em lote',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/rh/colaboradores')
      await pagina.getByRole('heading', { level: 1, name: 'Colaboradores' }).waitFor(T)
      await aquietar()
      await pagina.getByRole('button', { name: 'Enviar holerites em lote' }).click()
      await pagina.getByRole('dialog', { name: 'Enviar holerites em lote' }).waitFor(T)
      await aquietar()
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre Avisos, Holerites e Minhas advertencias da pessoa',
    fazer: async () => {
      for (const [caminho, titulo] of [
        ['/eu/avisos', 'Avisos'],
        ['/eu/holerites', 'Holerites'],
        ['/eu/advertencias', 'Minhas advertências'],
      ]) {
        await aquietar()
        await pagina.goto(servidor.url + caminho)
        await pagina.getByRole('heading', { level: 1, name: titulo }).waitFor(T)
      }
    },
  },
  // Fatia 5 (ficha 5, seção 7): a matriz das telas da fatia (sessão RH) e a página pública do
  // candidato, a primeira página pública com formulário, envio de arquivo e markdown sem sessão.
  {
    nome: 'abre os formularios do RH e o assistente',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/rh/formularios')
      await pagina.getByRole('heading', { level: 1, name: 'Formulários' }).waitFor(T)
      await aquietar()
      await pagina.goto(servidor.url + '/rh/formularios/novo')
      await pagina.getByRole('dialog', { name: 'Novo formulário' }).waitFor(T)
      await aquietar()
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre Meus formularios da pessoa',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/eu/formularios')
      await pagina.getByRole('heading', { level: 1, name: 'Formulários' }).waitFor(T)
    },
  },
  {
    nome: 'abre a pagina publica do candidato sem login (/formulario/<token>)',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/formulario/token-de-teste')
      // A API simulada do script responde 404 ao que não conhece: a página mostra a mensagem dela.
      await pagina.getByText('Este link não existe ou não está mais disponível.').waitFor(T)
    },
  },
  // Fatia 6 (ficha 6, seção 7): a matriz das telas da fatia (sessão RH) e a conferência pública
  // do certificado, uma página pública nova, sem sessão.
  {
    nome: 'abre os treinamentos do RH e o assistente',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/rh/treinamentos')
      await pagina.getByRole('heading', { level: 1, name: 'Treinamentos' }).waitFor(T)
      await aquietar()
      await pagina.goto(servidor.url + '/rh/treinamentos/novo')
      await pagina.getByRole('dialog', { name: 'Novo treinamento' }).waitFor(T)
      await aquietar()
      await pagina.keyboard.press('Escape')
    },
  },
  {
    nome: 'abre Meus treinamentos da pessoa',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/eu/treinamentos')
      await pagina.getByRole('heading', { level: 1, name: 'Meus treinamentos' }).waitFor(T)
    },
  },
  {
    nome: 'abre a conferencia publica do certificado (/certificado e /certificado/<codigo>)',
    fazer: async () => {
      await aquietar()
      await pagina.goto(servidor.url + '/certificado')
      await pagina.getByRole('heading', { level: 1, name: 'Conferir certificado' }).waitFor(T)
      await aquietar()
      await pagina.goto(servidor.url + '/certificado/a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b57')
      // A API simulada do script responde 404 ao que não conhece: a página mostra a mensagem dela.
      await pagina.getByText('Certificado não encontrado').waitFor(T)
    },
  },
  // ACRESCENTE AQUI, uma vez por fatia: { nome: 'abre /rota-da-tela', fazer: async () => { ... } }
  // (passos da vitrine, 1a Task 13: vitrine.passo('abre a vitrine', () => vitrine.abrir()), e os outros
  // com vitrine.passo(nome, async () => { ... }))
]

const resultados = []
for (const passo of PASSOS) {
  passoAtual = passo.nome
  const antes = violacoes.length
  if (passo.vitrine && vitrine.indisponivel) {
    resultados.push({
      nome: passo.nome,
      pulado: true,
      motivo: 'vitrine ausente (build de produção)',
    })
  } else {
    try {
      await passo.fazer()
      await aquietar()
      resultados.push({ nome: passo.nome, ok: true })
    } catch (erro) {
      if (erro instanceof Pulado && passo.vitrine)
        resultados.push({ nome: passo.nome, pulado: true, motivo: erro.message })
      else
        resultados.push({
          nome: passo.nome,
          ok: false,
          erro:
            erro instanceof Pulado
              ? 'só passo da vitrine pode ser pulado'
              : String(erro).split('\n').slice(0, 3).join(' | '),
        })
    }
  }
  // Dá tempo de a violação chegar ao Node, também quando o passo falhou (senão ela cairia no passo seguinte).
  await pagina.waitForTimeout(200)
  const r = resultados.at(-1)
  const marca = r.pulado ? 'pulado' : r.ok ? 'ok ' : 'FALHOU'
  console.log(
    `[${marca}] ${passo.nome}: ${violacoes.length - antes} violação(ões)${r.pulado ? ' :: ' + r.motivo : r.ok ? '' : ' :: ' + r.erro}`,
  )
}

// ---- Controles positivos (têm de violar) --------------------------------------------------------
passoAtual = 'controle'
const controles = [
  [
    '<style> injetado (style-src-elem)',
    () => {
      const s = document.createElement('style')
      s.textContent = 'body { color: rgb(255, 0, 255) !important }'
      document.head.appendChild(s)
    },
  ],
  [
    'atributo style="" por innerHTML (style-src-attr)',
    () => {
      const d = document.createElement('div')
      d.innerHTML = '<span style="color: rgb(0,0,255)">x</span>'
      document.body.appendChild(d)
    },
  ],
  [
    'imagem data: (img-src)',
    () => {
      const i = new Image()
      i.src = 'data:image/svg+xml;base64,' + btoa('<svg xmlns="http://www.w3.org/2000/svg"/>')
    },
  ],
  [
    'fetch para origem fora do connect-src',
    () => {
      fetch('https://origem-nao-permitida.test/x', { mode: 'no-cors' }).catch(() => {})
    },
  ],
]
const controlesMudos = []
for (const [nome, acao] of controles) {
  const antes = violacoes.length
  await pagina.evaluate(acao)
  await pagina.waitForTimeout(250)
  const violou = violacoes.length > antes
  console.log(
    `[controle ${violou ? 'ok ' : 'MUDO'}] ${nome}: ${violou ? 'violou, como deve' : 'NÃO violou'}`,
  )
  if (!violou) controlesMudos.push(nome)
}

await navegador.close()
await servidor.fechar()

// ---- Relatório -----------------------------------------------------------------------------------
const reais = violacoes.filter((v) => v.passo !== 'controle')
const problemas = []
if (cabecalhoVisto !== politica)
  problemas.push(
    `O cabeçalho Content-Security-Policy da página não é o do arquivo de configuração (visto: ${cabecalhoVisto}).`,
  )
problemas.push(...falhas)
if (controlesMudos.length)
  problemas.push(
    `CONTROLE MUDO (${controlesMudos.join('; ')}): a política não está ativa ou o ouvinte não funciona; "zero violações" não vale.`,
  )
for (const r of resultados.filter((x) => !x.ok && !x.pulado))
  problemas.push(`Passo falhou: ${r.nome} :: ${r.erro}`)
for (const v of reais)
  problemas.push(
    `Violação no passo "${v.passo}": ${v.diretiva} bloqueou ${v.bloqueado} (${v.arquivo}:${v.linha}:${v.coluna}) ${v.amostra ?? ''}` +
      (v.diretiva === 'connect-src'
        ? '\n  Dica: o build chamou uma origem que o connect-src não permite. Refaça o build com a origem do connect-src: $env:VITE_API_URL = "<origem>"; npm --prefix front-end run build (e apague o .env.local se ele define outra).'
        : ''),
  )
for (const e of excecoes) problemas.push(`Exceção na página no passo "${e.passo}": ${e.erro}`)
for (const r of requisicoesQueFalharam)
  problemas.push(`Requisição falhou no passo "${r.passo}": ${r.url} (${r.erro})`)

const pulados = resultados.filter((x) => x.pulado)
console.log(
  `\nviolações reais: ${reais.length} · controles mudos: ${controlesMudos.length} · exceções: ${excecoes.length} · requisições que falharam: ${requisicoesQueFalharam.length} · passos pulados: ${pulados.length}`,
)
if (problemas.length) {
  console.log('\nFALHOU:\n- ' + problemas.join('\n- '))
  process.exitCode = 1
} else {
  console.log(
    'PASSOU: nenhuma violação da política no build, e os controles provaram que ela está ativa.',
  )
  if (pulados.length)
    console.log(
      `AVISO: ${pulados.length} passo(s) da vitrine ficaram pulados porque este build não tem a vitrine. Ela só está provada se um csp rodou com ela: $env:VITE_VITRINE = 'sim'; npm --prefix front-end run build (numa linha só), e depois npm --prefix front-end run csp.`,
    )
}
