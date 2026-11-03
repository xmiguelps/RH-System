// Junta contrato/fontes/*.yaml em contrato/openapi.yaml e gera ROTAS.md e PROVISORIOS.md.
//   node montar.mjs                          monta tudo (escreve openapi.yaml, ROTAS.md e PROVISORIOS.md)
//   node montar.mjs --so 00,40 --saida x.yaml monta só as fontes com esses prefixos (00-base é sempre incluída)
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parse, stringify } from 'yaml';

const aqui = dirname(fileURLToPath(import.meta.url));
const pastaFontes = join(aqui, 'fontes');
const args = process.argv.slice(2);
const opcao = (nome) => {
  const i = args.indexOf(nome);
  return i >= 0 ? args[i + 1] : undefined;
};
const so = opcao('--so')?.split(',');
const saidaArg = opcao('--saida');
const completo = !so && !saidaArg;
const saida = saidaArg ?? join(aqui, 'openapi.yaml');

const METODOS = ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'];
const PERFIS_VALIDOS = ['Funcionario', 'Gestor', 'RH', 'Publico'];
const erros = [];
const erro = (msg) => erros.push(msg);

const arquivos = readdirSync(pastaFontes)
  .filter((n) => n.endsWith('.yaml'))
  .sort()
  .filter((n) => !so || n.startsWith('00-') || so.some((p) => n.startsWith(p)));

const doc = { openapi: undefined, info: undefined, servers: undefined, security: undefined, tags: [], paths: {}, components: {} };
const origemDaOperacao = new Map();
const operationIds = new Map();

for (const arq of arquivos) {
  const f = parse(readFileSync(join(pastaFontes, arq), 'utf8')) ?? {};
  if (arq.startsWith('00-')) {
    for (const chave of ['openapi', 'info', 'servers', 'security']) doc[chave] = f[chave];
  } else {
    for (const chave of ['openapi', 'info', 'servers', 'security']) {
      if (f[chave] !== undefined) erro(`${arq}: "${chave}" só pode estar em 00-base.yaml`);
    }
  }
  for (const tag of f.tags ?? []) {
    if (!doc.tags.some((t) => t.name === tag.name)) doc.tags.push(tag);
  }
  for (const [caminho, item] of Object.entries(f.paths ?? {})) {
    doc.paths[caminho] ??= {};
    for (const [chave, valor] of Object.entries(item)) {
      if (METODOS.includes(chave)) {
        const id = `${chave.toUpperCase()} ${caminho}`;
        if (doc.paths[caminho][chave]) erro(`${arq}: ${id} já definido em ${origemDaOperacao.get(id)}`);
        doc.paths[caminho][chave] = valor;
        origemDaOperacao.set(id, arq);
        if (valor.operationId) {
          if (operationIds.has(valor.operationId)) {
            erro(`${arq}: operationId "${valor.operationId}" repetido (já em ${operationIds.get(valor.operationId)})`);
          }
          operationIds.set(valor.operationId, arq);
        }
      } else if (doc.paths[caminho][chave] === undefined) {
        doc.paths[caminho][chave] = valor;
      } else {
        erro(`${arq}: "${chave}" do caminho ${caminho} já definido`);
      }
    }
  }
  for (const [secao, itens] of Object.entries(f.components ?? {})) {
    doc.components[secao] ??= {};
    for (const [nome, valor] of Object.entries(itens)) {
      if (doc.components[secao][nome] !== undefined) erro(`${arq}: components.${secao}.${nome} já definido em outra fonte`);
      doc.components[secao][nome] = valor;
    }
  }
}

if (!doc.openapi) erro('00-base.yaml não encontrada ou sem "openapi"');

// Conferências do que as convenções exigem.
const temExemplo = (midia) => midia && (midia.example !== undefined || midia.examples !== undefined);
const resolver = (obj) => {
  let atual = obj;
  for (let i = 0; i < 5 && atual && atual.$ref; i += 1) {
    const partes = atual.$ref.replace(/^#\//, '').split('/');
    atual = partes.reduce((no, p) => (no ? no[p] : undefined), doc);
  }
  return atual;
};
for (const [caminho, item] of Object.entries(doc.paths)) {
  for (const metodo of METODOS.filter((m) => item[m])) {
    const op = item[metodo];
    const id = `${metodo.toUpperCase()} ${caminho}`;
    const onde = `${origemDaOperacao.get(id)}: ${id}`;
    if (!op.operationId) erro(`${onde}: falta operationId`);
    if (!op.summary) erro(`${onde}: falta summary`);
    if (!Array.isArray(op['x-perfis']) || op['x-perfis'].length === 0) {
      erro(`${onde}: falta x-perfis`);
    } else if (!op['x-perfis'].every((p) => PERFIS_VALIDOS.includes(p))) {
      erro(`${onde}: x-perfis com valor fora de ${PERFIS_VALIDOS.join(', ')}`);
    }
    const publico = op['x-perfis']?.includes('Publico');
    if (publico && !(Array.isArray(op.security) && op.security.length === 0)) erro(`${onde}: rota Publico precisa de "security: []"`);
    const corpo = resolver(op.requestBody)?.content;
    for (const [tipo, midia] of Object.entries(corpo ?? {})) {
      if (tipo === 'application/json' && !temExemplo(midia)) erro(`${onde}: corpo da requisição sem example`);
    }
    for (const [status, resposta] of Object.entries(op.responses ?? {})) {
      if (!/^2/.test(status)) continue;
      const conteudo = resolver(resposta)?.content;
      const midia = conteudo?.['application/json'];
      if (midia && !temExemplo(midia)) erro(`${onde}: resposta ${status} em JSON sem example`);
    }
  }
}

if (erros.length > 0) {
  console.error(`${erros.length} problema(s) no contrato:`);
  for (const e of erros) console.error(`  - ${e}`);
  process.exit(1);
}

const cabecalho = '# GERADO por contrato/montar.mjs a partir de contrato/fontes/*.yaml. Não edite este arquivo.\n';
mkdirSync(dirname(saida), { recursive: true });
writeFileSync(saida, cabecalho + stringify(doc, { lineWidth: 0 }), 'utf8');
console.log(`${saida}: ${Object.keys(doc.paths).length} caminhos, ${operationIds.size} operações, ${arquivos.length} fontes.`);

if (completo) {
  // ROTAS.md: lista em português simples, por fonte (uma fonte por fatia ou parte de fatia).
  const porFonte = new Map();
  for (const [id, arq] of origemDaOperacao) {
    const [metodo, caminho] = [id.split(' ')[0], id.slice(id.indexOf(' ') + 1)];
    const op = doc.paths[caminho][metodo.toLowerCase()];
    if (!porFonte.has(arq)) porFonte.set(arq, []);
    porFonte.get(arq).push({ metodo, caminho, op });
  }
  let rotas = '# Rotas do contrato, por fatia\n\n> GERADO por `montar.mjs`. Cada linha: o que a rota faz, em uma frase, e quem pode usá-la. `*` = depende de decisão ainda aberta (ver `PROVISORIOS.md`).\n';
  for (const [arq, lista] of porFonte) {
    const titulo = arq.replace(/^\d+-/, '').replace(/\.yaml$/, '').replace(/-/g, ' ');
    rotas += `\n## ${titulo} (${lista.length} rotas)\n\n| Rota | operationId | O que faz | Quem usa |\n|---|---|---|---|\n`;
    for (const { metodo, caminho, op } of lista) {
      const prov = JSON.stringify(op).includes('"x-provisorio"') ? ' *' : '';
      rotas += `| \`${metodo} ${caminho}\` | \`${op.operationId}\` | ${op.summary}${prov} | ${op['x-perfis'].join(', ')} |\n`;
    }
  }
  writeFileSync(join(aqui, 'ROTAS.md'), rotas, 'utf8');

  // PROVISORIOS.md: tudo o que tem x-provisorio, com o lugar no documento.
  const achados = [];
  const andar = (no, trilha) => {
    if (!no || typeof no !== 'object') return;
    for (const [chave, valor] of Object.entries(no)) {
      if (chave === 'x-provisorio') achados.push({ trilha: trilha.join(' > '), texto: valor });
      else andar(valor, [...trilha, chave]);
    }
  };
  andar({ paths: doc.paths, components: doc.components }, []);
  let prov = '# Itens provisórios do contrato\n\n> GERADO por `montar.mjs`. Cada item depende de uma decisão do dono ainda aberta. O front constrói normalmente em cima do que está escrito; se a decisão mudar o item, o contrato muda e o `CHANGELOG.md` registra.\n\n';
  prov += achados.length === 0 ? 'Nenhum.\n' : '| Onde | Decisão pendente |\n|---|---|\n' + achados.map((a) => `| \`${a.trilha}\` | ${String(a.texto).replace(/\n/g, ' ')} |`).join('\n') + '\n';
  writeFileSync(join(aqui, 'PROVISORIOS.md'), prov, 'utf8');
}
