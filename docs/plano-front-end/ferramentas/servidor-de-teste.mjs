#!/usr/bin/env node
// servidor-de-teste.mjs
//
// PARA QUE SERVE
//   Serve a pasta `dist` (o build do front) com os MESMOS cabeçalhos de segurança que o Azure Static Web
//   Apps vai mandar. Os cabeçalhos e o fallback de navegação são LIDOS de public/staticwebapp.config.json
//   (`globalHeaders` e `navigationFallback`): o teste exerce o arquivo que vai para o ar, sem cópia.
//   A política só vale no Azure; localmente a prova é abrir o build neste servidor com um navegador
//   (veja violacoes-de-politica.mjs, que usa este arquivo).
//
// COMO RODA
//   Sozinho, para olhar o build no navegador (da raiz do repositório, no PowerShell):
//     node front-end/scripts/servidor-de-teste.mjs
//   Argumentos opcionais: a pasta do build (padrão: a pasta dist de front-end, ao lado de scripts) e a porta
//   (padrão: 4173). Abra http://127.0.0.1:4173. Ctrl+C encerra.
//   Importado por outro script:
//     import { iniciarServidor } from './servidor-de-teste.mjs'
//     const s = await iniciarServidor({ dist, config }) // porta 0 = uma livre
//     s.url        // http://127.0.0.1:<porta>
//     s.cabecalhos // os cabeçalhos que ele manda
//     await s.fechar()
//
// ONDE COPIAR
//   Para front-end\scripts\servidor-de-teste.mjs (só Node; nenhuma dependência).
//
// O QUE FAZ
//   - Arquivo que existe em `dist`: devolve com o tipo certo. Arquivo com extensão que não existe: 404
//     (escolha deste servidor: um .js ou .css que falta tem de falhar alto, não virar index.html).
//   - Rota sem extensão que não existe (ex.: /entrar, /rh/colaboradores): devolve o `rewrite` do
//     `navigationFallback` (index.html) com 200.
//   - Todas as respostas, 404 incluído, levam os `globalHeaders` do arquivo de configuração.
//   - Não tem API: quem precisa de /api intercepta no navegador (Playwright `page.route`).
//   - Rota de teste da SONDA DE DOWNLOAD: GET /__sonda/arquivo.pdf devolve um PDF como anexo
//     (`Content-Disposition: attachment`) com os mesmos cabeçalhos de segurança. É o "link temporário da
//     própria origem"; o link de outra origem (o do armazenamento de arquivos) e o CSV por Blob ficam
//     no violacoes-de-politica.mjs (ver "A SONDA DE DOWNLOAD" lá).
import { createServer } from 'node:http'
import { readFile, stat } from 'node:fs/promises'
import { readFileSync } from 'node:fs'
import { extname, join, normalize, resolve, sep } from 'node:path'
import { pathToFileURL } from 'node:url'

const TIPOS = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.woff2': 'font/woff2',
  '.json': 'application/json',
  '.map': 'application/json',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
}

export const ROTA_DA_SONDA = '/__sonda/arquivo.pdf'

export function lerConfiguracao(caminhoDaConfig) {
  const config = JSON.parse(readFileSync(caminhoDaConfig, 'utf8'))
  const cabecalhos = config.globalHeaders
  if (!cabecalhos || !cabecalhos['Content-Security-Policy'])
    throw new Error(`${caminhoDaConfig} não tem globalHeaders com Content-Security-Policy`)
  return { cabecalhos, rewrite: config.navigationFallback?.rewrite ?? '/index.html' }
}

export async function iniciarServidor({ dist, config, porta = 0 }) {
  const raiz = resolve(dist)
  const { cabecalhos, rewrite } = lerConfiguracao(config)

  const servidor = createServer(async (req, res) => {
    try {
      const url = new URL(req.url ?? '/', 'http://localhost')
      if (url.pathname === ROTA_DA_SONDA) {
        res.writeHead(200, {
          ...cabecalhos,
          'content-type': 'application/pdf',
          'content-disposition': 'attachment; filename="sonda-da-origem.pdf"',
        })
        return res.end('%PDF-1.4 sonda')
      }
      const relativo = normalize(decodeURIComponent(url.pathname)).replace(/^[/\\]+/, '')
      let arquivo = join(raiz, relativo)
      if (arquivo !== raiz && !arquivo.startsWith(raiz + sep)) {
        res.writeHead(403, cabecalhos)
        return res.end()
      }
      let info = await stat(arquivo).catch(() => null)
      if (!info || info.isDirectory()) {
        if (extname(relativo)) {
          res.writeHead(404, { ...cabecalhos, 'content-type': 'text/plain; charset=utf-8' })
          return res.end('404')
        }
        arquivo = join(raiz, rewrite)
        info = await stat(arquivo).catch(() => null)
        if (!info) {
          res.writeHead(404, { ...cabecalhos, 'content-type': 'text/plain; charset=utf-8' })
          return res.end('404: o build não tem ' + rewrite)
        }
      }
      const corpo = await readFile(arquivo)
      res.writeHead(200, {
        ...cabecalhos,
        'content-type': TIPOS[extname(arquivo)] ?? 'application/octet-stream',
      })
      res.end(corpo)
    } catch (erro) {
      res.writeHead(500, { ...cabecalhos, 'content-type': 'text/plain; charset=utf-8' })
      res.end(String(erro))
    }
  })

  await new Promise((ok, falha) => {
    servidor.once('error', falha)
    servidor.listen(porta, '127.0.0.1', ok)
  })
  const { port } = servidor.address()
  return {
    url: `http://127.0.0.1:${port}`,
    cabecalhos,
    fechar: () => new Promise((ok) => servidor.close(() => ok())),
  }
}

// Rodado direto: node servidor-de-teste.mjs [dist] [porta]
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const dist = process.argv[2] ?? join(import.meta.dirname, '..', 'dist')
  const porta = Number(process.argv[3] ?? 4173)
  const config = join(import.meta.dirname, '..', 'public', 'staticwebapp.config.json')
  const s = await iniciarServidor({ dist, config, porta })
  console.log(
    `servindo ${resolve(dist)} em ${s.url} com os cabeçalhos de public/staticwebapp.config.json`,
  )
}
