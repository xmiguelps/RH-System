// Valida cada `example`/`examples` do contrato (respostas, erros e corpos de requisição) contra o schema dele.
//   node validar-exemplos.mjs [openapi.yaml]     (padrão: ./openapi.yaml; sai com código 1 se algum exemplo for inválido)
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';
import { parse } from 'yaml';

const aqui = dirname(fileURLToPath(import.meta.url));
const doc = parse(readFileSync(process.argv[2] ?? join(aqui, 'openapi.yaml'), 'utf8'));
const ID = 'https://contrato.test/openapi';

// Reaponta `$ref` locais (#/components/...) para o schema registrado com $id.
const reescrever = (no) => {
  if (Array.isArray(no)) return no.map(reescrever);
  if (no && typeof no === 'object') {
    const saida = {};
    for (const [k, v] of Object.entries(no)) {
      saida[k] = k === '$ref' && typeof v === 'string' && v.startsWith('#') ? ID + v : reescrever(v);
    }
    return saida;
  }
  return no;
};

const ajv = new Ajv2020({ strict: false, allErrors: false });
addFormats(ajv);
ajv.addFormat('markdown', true);
ajv.addSchema({ $id: ID, components: { schemas: doc.components.schemas } });

const resolver = (obj) =>
  obj?.$ref ? resolver(obj.$ref.slice(2).split('/').reduce((no, chave) => no[chave], doc)) : obj;

const falhas = [];
let total = 0;
const conferir = (op, onde, midia) => {
  if (!midia?.schema) return;
  const exemplos = [];
  if (midia.example !== undefined) exemplos.push(['example', midia.example]);
  for (const [nome, ex] of Object.entries(midia.examples ?? {})) {
    if (resolver(ex)?.value !== undefined) exemplos.push([`examples.${nome}`, resolver(ex).value]);
  }
  if (exemplos.length === 0) return;
  const validar = ajv.compile(reescrever(midia.schema));
  for (const [nome, valor] of exemplos) {
    total += 1;
    if (!validar(valor)) {
      const e = validar.errors[0];
      falhas.push(`${op.operationId}: ${onde} ${nome}: ${e.instancePath || '/'} ${e.message} ${JSON.stringify(e.params).slice(0, 80)}`);
    }
  }
};

for (const [caminho, item] of Object.entries(doc.paths)) {
  for (const metodo of ['get', 'post', 'put', 'patch', 'delete']) {
    const op = item[metodo];
    if (!op) continue;
    for (const [tipo, midia] of Object.entries(resolver(op.requestBody)?.content ?? {})) conferir(op, `requisição ${tipo}`, midia);
    for (const [status, resposta] of Object.entries(op.responses)) {
      for (const [tipo, midia] of Object.entries(resolver(resposta)?.content ?? {})) conferir(op, `${status} ${tipo}`, midia);
    }
  }
}

console.log(`${total} exemplos validados, ${falhas.length} inválidos.`);
for (const f of falhas) console.error(`  - ${f}`);
process.exit(falhas.length > 0 ? 1 : 0);
