# Ficha da fatia 2: importação por Excel (front-end)

Contrato lido: `contrato\fontes\25-fatia-2-importacao.yaml` (a versão do contrato é o `info.version` de `contrato\openapi.yaml`; confira-a antes de começar e anote-a no `PROGRESSO.md`). Notas de tela: `docs\plano-front-end\telas\fatia-2.md`. Caminhos de tela: `docs\plano-front-end\05-caminhos-de-tela.md` (tabela única).

Esta ficha **não traz passo a passo fino**. Repita o padrão da fatia 1b (`fatia-1b-telas.md`): um passo pequeno por vez, cada um com um comando que prova, `PROGRESSO.md` atualizado a cada passo, commit e push por passo na sua branch.

## 1. Objetivo e entrega demonstrável

O RH, dentro do cadastro de colaborador, troca para "Importar planilha", baixa o modelo, envia um arquivo `.xlsx`, vê a **prévia com os erros por linha e por coluna** e só consegue confirmar quando o arquivo está **sem nenhum erro** (tudo ou nada). Ao confirmar, vê o resultado (quantos foram cadastrados, quantos convites saíram) e abre a ficha de cada um.

Demonstração (com os dados simulados do contrato): enviar a planilha de exemplo com erros, ver as linhas 3 e 5 marcadas, enviar a corrigida, confirmar e ver "2 colaboradores cadastrados" (Tiago Nogueira e Aline Carvalho, ids 105 e 202).

## 2. Telas

**Nenhuma tela desta fatia tem PNG.** Li `Figma\telas\02-rh-colaboradores-adicionar-colaborador.png`: é o painel lateral "Adicionar colaborador" (cadastro de uma pessoa) e **não tem** a alternância "Importar planilha" nem botão de importação. O roadmap (fatia 2, "Telas") e o spec (seção 6) dizem o mesmo: o Figma não desenha esta tela. A descrição completa, em texto, está em `telas\fatia-2.md`.

| Tela | Perfil | PNG | Onde está a descrição |
|---|---|---|---|
| Cadastro, modo "Importar planilha" (escolher arquivo, prévia com erros, prévia pronta, resultado, erros do arquivo) | RH | nenhum (parte de `02-rh-colaboradores-adicionar-colaborador.png` só mostra o cadastro de uma pessoa) | `telas\fatia-2.md`, seção "Tela 02 (modo Importar planilha)" |

## 3. Rotas do contrato por tela

| Tela ou ação | `operationId` | Rota |
|---|---|---|
| Botão "Baixar modelo" | `baixarModeloDeImportacao` | `GET /api/rh/importacoes/modelo` |
| Enviar a planilha e ver a prévia | `validarPlanilhaDeImportacao` | `POST /api/rh/importacoes` (multipart) |
| Botão "Cadastrar N colaboradores" | `confirmarImportacao` | `POST /api/rh/importacoes/{importacaoId}/confirmar` |
| Link "Abrir ficha" no resultado | `obterFichaDoFuncionario` (fatia 1b) | `GET /api/rh/funcionarios/{funcionarioId}` |
| Atualizar a lista de colaboradores depois de importar | `listarFuncionarios` (fatia 1b) | `GET /api/rh/funcionarios` |

As **3 rotas desta fatia** são as três primeiras linhas. As outras duas são da fatia 1b e só são **lidas** (os dados simulados gerados do contrato cobrem todas, então não esperam o back-end). **Esta fatia não chama** `listarTreinamentosDeOnboarding` (fatia 6) nem `listarFormulariosRh` (fatia 5): as caixas que dependem delas ficam desligadas aqui (seção 4) e as fichas 5 e 6 as ligam.

## 4. Regras e cortes que valem

Do contrato (`25-`), do spec (seção 6) e do roadmap (fatia 2). O contrato é lei.

- **Só o RH.** Rota e menu só para o perfil RH; outro perfil recebe 403.
- **Só cadastra.** Atualizar quem já existe por planilha está fora do escopo. Se o CPF ou o e-mail corporativo de uma linha é de um cadastro **inativo**, a API devolve o erro da linha mandando usar "Readmitir" na ficha; a tela mostra o texto como veio e não oferece readmissão aqui.
- **Tudo ou nada.** `podeConfirmar` só é `true` com zero linhas com erro. O botão "Cadastrar" fica desligado enquanto houver erro. O RH corrige a planilha no Excel e **envia de novo** (a tela não edita células).
- **Limites** (a API decide; a tela só avisa antes, por conforto): só `.xlsx`, até 5 MB, até 500 linhas de dados (o contrato não diz o valor exato do MB em bytes para a planilha; use 5 × 1024 × 1024 só como conferência prévia e deixe a API decidir).
- **O front não lê o Excel.** Nenhuma biblioteca de planilha no front: o arquivo vai inteiro para a API, em uma única requisição `multipart/form-data` (campo `arquivo`).
- **Prévia vale 1 hora** (`expiraEmUtc`). Mostre "A prévia vale até HH:mm". Quem decide se venceu é a API (422 `IMPORTACAO_EXPIRADA`), não o relógio do navegador.
- **Ninguém recebe senha.** O convite por e-mail é a caixa "Enviar convite de acesso ao portal" (padrão marcada); com ela desmarcada, o RH envia depois em Cadastros, "Usuários do sistema".
- **Caixas "Depois de salvar"**: as mesmas do cadastro de uma pessoa (tela 02, fatia 1b), valendo para todas as pessoas da planilha, **e no mesmo estado em que a 1b as deixou**: "Enviar convite de acesso ao portal" (`enviarConvite`, padrão marcada) funciona; "Incluir no treinamento de onboarding" e "Enviar o formulário de admissão" ficam **visíveis, desmarcadas e desabilitadas**, com a dica "Indisponível por enquanto". A confirmação envia sempre `incluirNoOnboarding: false` e `formularioAdmissaoId: null`. **Ligar as duas é tarefa das fichas 5 e 6** (cadastro e importação, as duas telas de uma vez); esta ficha não chama `listarTreinamentosDeOnboarding` nem `listarFormulariosRh`. Repita o comportamento que a fatia 1b já deu ao cadastro; não invente regra.
- **Textos de erro**: mostre o `detail` que a API devolve (já em português, com acento). O front não reescreve a mensagem. A prévia traz a `mensagem` de cada erro por coluna, pronta para mostrar.
- **Dados pessoais na tela.** A prévia mostra CPF, telefone e e-mail completos (o RH pode ver). Não guarde a prévia em `localStorage` nem escreva o conteúdo no console (regra de engenharia desta ficha, apoiada na decisão 13 do roadmap).
- **Coluna "Nível de acesso" da planilha** usa `Colaborador`, `Gestor` ou `RH` (texto da planilha), diferente do campo `perfil` do cadastro (`Funcionario`, `Gestor`, `RH`). A tela não converte nada: só mostra o que a API devolve.
- **Decisões do dono ainda abertas (`x-provisorio`, construa normalmente):**
  - `paths > /api/rh/importacoes/modelo > get`: quais são as colunas do modelo. Construído com as 11 colunas do contrato. **O que vale:** as colunas e os rótulos vêm **da nota da tela** (`telas\fatia-2.md`, estado C: a tabela das 11 colunas e o mapa código para título, como `NOME` para "Nome completo", que o contrato dá em `ImportacaoColuna`); **o código as tem como uma tabela de rótulos** (um `Record<ImportacaoColuna, string>`, para o tipo gerado acusar coluna nova ou tirada). **O que vem da API** é o conteúdo da prévia: os `valores` de cada linha e a coluna de cada erro (`erros[].coluna`, o código), que a tela liga a essa tabela; o front não guarda o nome da coluna do arquivo que a pessoa enviou.
  - `paths > /api/rh/importacoes/{importacaoId}/confirmar > post`: se a importação marca as pessoas como "Em admissão". Construído: não marca nada especial; a situação vem da data de admissão de cada linha. **A tela não mostra nada sobre isso.**

## 5. Ordem de construção e dependências

Usa da **fundação** (fatia 1a): cliente de API gerado do contrato, menu e rotas por perfil, componente de envio de arquivo (`SeletorDeArquivo`), auxiliar de download de arquivo (`baixarExportacao`, `src\shared\arquivos\baixar.ts`), mensagens de erro do `application/problem+json`, MSW. Usa da **fatia 1b**: o painel lateral do cadastro (tela 02, com a alternância "Importar planilha" que a 1b deixou desabilitada), a lista de colaboradores e a ficha. Não depende de nenhuma fatia posterior e **não lê nada das fatias 5 e 6**: as caixas de onboarding e de formulário de admissão ficam desligadas.

Ordem sugerida (cada item é um passo com teste):
1. A alternância "Cadastrar um" e "Importar planilha" no painel do cadastro (liga a opção que a 1b deixou desabilitada; só troca o conteúdo; ainda sem rotas). O modo não vai na URL (`05-caminhos-de-tela.md`).
2. "Baixar modelo" (`baixarModeloDeImportacao`; a resposta é o arquivo, não um link: use `baixarExportacao(chamada, nomePadrao)` da fundação, o mesmo auxiliar do "Exportar CSV" do log de auditoria).
3. Escolher o arquivo, conferir extensão e tamanho, enviar (`validarPlanilhaDeImportacao`) e mostrar a prévia (`comErros` e `pronta` dos exemplos).
4. Erros do arquivo inteiro (422 sem prévia: `ARQUIVO_INVALIDO`, `ARQUIVO_MUITO_GRANDE`, `MODELO_ALTERADO`, `PLANILHA_VAZIA`, `LIMITE_DE_LINHAS_EXCEDIDO`).
5. O bloco "Depois de salvar" (convite marcado; as outras duas caixas desligadas) e o botão "Cadastrar N colaboradores" (`confirmarImportacao`).
6. O resultado e os erros de confirmação (409 `IMPORTACAO_JA_CONFIRMADA`; 422 `IMPORTACAO_COM_ERROS`, `IMPORTACAO_EXPIRADA`, `IMPORTACAO_LINHA_RECUSADA`) e a atualização da lista de colaboradores. Os 422 `SEM_TREINAMENTO_DE_ONBOARDING`, `FORMULARIO_ENCERRADO` e `FORMULARIO_NAO_E_POR_PESSOAS` só acontecem com as caixas ligadas: são das fichas 6 e 5.

## 6. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações da fatia), rodado da raiz do repositório (`01-regras.md`, A14). Aqui o teste de cobertura confere as **3 operações** da seção 3 (`baixarModeloDeImportacao`, `validarPlanilhaDeImportacao`, `confirmarImportacao`). O que esta ficha acrescenta é a lista abaixo, que é da fatia.

**Passos novos do `csp`:** **nenhum** além da matriz de perfis das telas desta fatia (a sonda de download da 1a já cobre Blob e link temporário, e o "Baixar modelo" usa o mesmo `baixarExportacao`; a área de envio é o `SeletorDeArquivo` da 1a, o mesmo da 1b: não há recurso de primeira vez nesta fatia).

Testes com MSW que precisam existir (use os `example` do contrato e as pessoas de `ELENCO.md`; os exemplos **nomeados** vêm de `exemploDoContrato(operationId, status?, nome?)`, em `src\shared\teste\`, ver `03-contrato-e-mocks.md`: `('validarPlanilhaDeImportacao', 201, 'comErros')`, `(..., 201, 'pronta')`, `(..., 422, 'naoEExcel')`, `(..., 422, 'limite')`, `('confirmarImportacao', 422, 'linhaRecusada')`):
1. "Baixar modelo" chama `baixarModeloDeImportacao` e entrega o arquivo.
2. Enviar um `.xlsx` válido mostra a prévia `pronta` (importação 16, 2 linhas, `podeConfirmar: true`).
3. Enviar o arquivo com erros mostra a prévia `comErros` (importação 15): as linhas 3 e 5 com a coluna e a mensagem de cada erro, e o botão "Cadastrar" desligado.
4. Extensão errada ou mais de 5 MB: aviso antes de enviar; 422 `ARQUIVO_INVALIDO` e `LIMITE_DE_LINHAS_EXCEDIDO` mostram o `detail`.
5. Confirmar envia `{ enviarConvite, incluirNoOnboarding: false, formularioAdmissaoId: null }` (as duas caixas de onboarding e de formulário de admissão aparecem desabilitadas, com "Indisponível por enquanto") e mostra o resultado **como a API o devolve**: o exemplo 200 de `confirmarImportacao` traz `totalCriados` 2, `convitesEnviados` 2, `incluidosNoOnboarding` 2 e `incluidosNoFormularioDeAdmissao` 2, e o MSW devolve esse exemplo, então a tela mostra "2" nos quatro números. O teste confere **o texto que vem do exemplo** e **não afirma 0** (as caixas desligadas se provam só pelo **corpo enviado**: `incluirNoOnboarding: false` e `formularioAdmissaoId: null`). Os testes de onboarding e de formulário **ligados** ("2 incluídos no onboarding", "2 no formulário 11", com o corpo `true` e `11`) são das fichas 6 e 5, quando elas ligam as caixas.
6. 409 `IMPORTACAO_JA_CONFIRMADA` e 422 `IMPORTACAO_LINHA_RECUSADA` (com `linha: 12`) mostram o `detail` e **nada** é dado como cadastrado.
7. Perfil que não é RH não vê a alternância nem entra na tela.
8. **Teste de contrato** (a lacuna do `tsc` com chave extra em `query` ou `params.path`): o envio usa o campo `arquivo` e o `importacaoId` certo no caminho da confirmação. Testes que montam `multipart` precisam da primeira linha `// @vitest-environment node`.

### Pendente de integração

As **3 rotas** da fatia 2 estão `planejada` em `contrato\STATUS.md`. Enquanto não estiverem `pronta`, tudo roda com MSW e o `PROGRESSO.md` registra "pendente de integração", com o **roteiro pronto** (a sessão **não roda** a API real; a pessoa ou o dono roda o roteiro depois), para: o upload real de `.xlsx`, o download real do modelo, a confirmação com convites e a atualização da lista de colaboradores com as pessoas novas.

## 7. Riscos

- **Prévia grande**: 500 linhas × 11 colunas pesam no DOM. Pagine no cliente (por exemplo 50 linhas por página) e deixe o filtro "só linhas com erro".
- **Largura**: a prévia é uma tabela larga. Planeje rolagem horizontal; a demonstração pode ser em 1366 px (`04-visual.md`).
- **Download autenticado**: o modelo vem direto na resposta (não é link temporário). O download por `Blob` sob a política de segurança do site **é medido** pela sonda do script `csp` (fundação 1a, Task 13; `04-visual.md`), que a prova padrão já roda: não há o que confirmar à parte.
- **Tela sem desenho**: o local e o jeito da alternância são desenho meu (ver `telas\fatia-2.md`); a pessoa confere visualmente uma vez, junto com as outras telas da fatia.
- **Colunas do modelo podem mudar** (`x-provisorio`): se o contrato mudar `ImportacaoColuna`, os tipos regenerados fazem a tabela de rótulos (seção 4) deixar de compilar: é **o único lugar** a ajustar; os `valores` e os erros continuam sendo só o que a API devolve.

## 8. Repita o padrão da fatia 1b

Mesma estrutura de pasta, mesmos hooks de dados gerados do contrato, mesmo componente de envio de arquivo, mesmas mensagens de erro, mesmo ciclo de cada passo (teste que falha, implementação, teste passa, lint e `format:check`, `PROGRESSO.md` com a versão do contrato, commit e push) e a mesma conferência visual no fim: no relatório final, liste "Tela 02 (modo Importar planilha)" para a pessoa conferir ao lado da descrição, já que não há print.
