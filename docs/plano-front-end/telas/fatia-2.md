# Notas de tela da fatia 2: importação por Excel

Fontes lidas: `contrato\fontes\25-fatia-2-importacao.yaml`, `contrato\PROVISORIOS.md`, `contrato\CONVENCOES.md`, `docs\superpowers\specs\2026-10-21-rh-system-design.md` (seção 6 e 13.3), roadmap (seção "Fatia 2: importação por Excel"), design dos planos (seção 7) e o PNG `Figma\telas\02-rh-colaboradores-adicionar-colaborador.png`.

Marcas usadas: **(li)** = li no PNG ou no arquivo; **(desenho meu)** = a tela não existe no Figma e a descrição é a proposta deste plano, em estilo simples; **(suponho)** = inferência minha, a confirmar na conferência visual.

Cobertura: **1 tela** (em texto, sem PNG), **3 rotas** da fatia. Nada do que o contrato dá a esta fatia ficou sem tela.

---

## Tela 02 (modo "Importar planilha"): cadastro por planilha (RH)

**PNG:** nenhum para este modo. `Figma\telas\02-rh-colaboradores-adicionar-colaborador.png` mostra só o cadastro de **uma** pessoa (li): painel lateral de 600 px de largura, título "Adicionar colaborador", botão fechar, texto "Os campos marcados entram no cadastro. O restante a própria pessoa completa no formulário de admissão.", grupos "Dados pessoais", "Contrato", "Benefícios" e "Depois de salvar", e os botões "Cancelar" e "Adicionar colaborador". **Não há** alternância nem botão de importação no print (li).

**Rotas:** `baixarModeloDeImportacao`, `validarPlanilhaDeImportacao`, `confirmarImportacao` (lidas em `25-`). Lidas da fatia 1b: `obterFichaDoFuncionario` e `listarFuncionarios`. **Não lidas aqui:** `listarTreinamentosDeOnboarding` (fatia 6) e `listarFormulariosRh` (fatia 5): as caixas que dependem delas ficam desligadas nesta fatia (estado D) e as fichas 6 e 5 as ligam.

**Caminho:** `/rh/colaboradores/novo` (o modo "Importar planilha" é estado do painel, não vai na URL); tabela única em `05-caminhos-de-tela.md`.

### Onde fica (desenho meu)

O spec diz: "O botão na tela de cadastro alterna para o modo 'Importar planilha'" (seção 6). Então a importação **mora dentro do painel "Adicionar colaborador"**:

- No cabeçalho do painel, logo abaixo do título, um seletor de duas opções: **"Cadastrar um colaborador"** (o formulário da tela 02, como está) e **"Importar planilha"** (a 1b a deixou desabilitada, com a dica "Indisponível por enquanto"; esta fatia a liga).
- Ao escolher "Importar planilha", o painel passa a mostrar o conteúdo abaixo. Como a prévia é uma tabela larga, o painel **cresce** para a largura do painel da ficha (cerca de 860 px, como nos PNG 03 e 04) e a tabela rola na horizontal (suponho; a conferência visual decide o tamanho).
- Trocar de modo com um arquivo já enviado e não confirmado **descarta** a prévia (nada foi gravado). Se a pessoa fechar o painel no meio, o mesmo.

### Estado A: escolher o arquivo (inicial)

(desenho meu)
- Título do bloco: "Importar planilha". Texto de apoio: "Cadastre vários colaboradores de uma vez. Baixe o modelo, preencha no Excel e envie aqui. Só cadastra pessoas novas."
- Botão secundário **"Baixar modelo"** (`baixarModeloDeImportacao`; baixa `modelo-importacao-colaboradores.xlsx`).
- Área de envio no mesmo estilo da área de envio de PDF do holerite (PNG 08, li): "Arraste a planilha aqui", linha pequena "Só .xlsx, até 5 MB e 500 linhas", botão "Escolher arquivo".
- Antes de enviar, a tela confere **só por conforto**: extensão `.xlsx` e tamanho até 5 MB. Se não passar, mostra "Escolha um arquivo .xlsx de até 5 MB." e não envia. A API é quem decide de verdade.
- Ao escolher um arquivo válido, o envio começa na hora (uma única requisição `multipart/form-data`, campo `arquivo`).
- Rodapé do painel: botão "Fechar".

### Estado B: enviando

- A área de envio mostra o nome do arquivo e "Lendo a planilha...". Controles desligados. Não há cancelar (a validação é curta; 500 linhas cabem numa requisição).

### Estado C: prévia com erros (`situacao: COM_ERROS`)

(desenho meu; os dados vêm de `ImportacaoPrevia`)
- Faixa de resumo, em cor de alerta: "{totalLinhas} linhas lidas: {linhasValidas} certas e {linhasComErro} com erro. Corrija a planilha e envie de novo. Nada foi cadastrado."
- Linha pequena: "Arquivo: {nomeArquivo}. Esta prévia vale até {HH:mm}." (`expiraEmUtc` em `America/Sao_Paulo`).
- Alternador "Mostrar só as linhas com erro" (padrão: ligado quando há erro).
- Tabela, uma linha por `ImportacaoLinha`:

| Coluna da tela | De onde vem |
|---|---|
| Linha | `numeroLinha` (é o número que o RH vê no Excel; a linha 1 é o cabeçalho) |
| Situação | `situacao`: "OK" (verde) ou "Com erro" (vermelho) |
| Nome, CPF, Nascimento, Telefone, E-mail corporativo, Admissão, Contrato, Cargo, Setor, Nível de acesso, Benefícios | `valores`, **como digitados** (texto; `null` aparece como "(vazio)" em cinza) |

- **Erro por coluna:** a célula da coluna citada em `erros[].coluna` (`ImportacaoColuna`: `NOME`, `CPF`, `DATA_NASCIMENTO`, `TELEFONE`, `EMAIL_CORPORATIVO`, `DATA_ADMISSAO`, `TIPO_CONTRATO`, `CARGO`, `SETOR`, `NIVEL_DE_ACESSO`, `BENEFICIOS`) fica com borda vermelha. Embaixo da linha, uma lista "Coluna: mensagem" com a `mensagem` pronta da API (exemplo do contrato: "CPF: CPF inválido.", "E-mail corporativo: Este e-mail aparece também na linha 2."). O nome da coluna usa o título do modelo (`NOME` "Nome completo", `CPF` "CPF", `DATA_NASCIMENTO` "Data de nascimento", `TELEFONE` "Telefone", `EMAIL_CORPORATIVO` "E-mail corporativo", `DATA_ADMISSAO` "Data de admissão", `TIPO_CONTRATO` "Tipo de contrato", `CARGO` "Cargo", `SETOR` "Setor", `NIVEL_DE_ACESSO` "Nível de acesso", `BENEFICIOS` "Benefícios"). **Esta tabela e este mapa são a fonte** das colunas e dos rótulos: o código os guarda como **uma tabela de rótulos** (`Record<ImportacaoColuna, string>`); o que a API devolve é o conteúdo (`valores` e `erros[].coluna`), não os títulos.
- Uma linha pode ter mais de um erro (o exemplo da linha 5 tem três). Mostre todos.
- Paginação no cliente (por exemplo 50 linhas por página) para não pesar o DOM com 500 linhas.
- Botões: **"Enviar outra planilha"** (volta ao estado A) e **"Cadastrar {linhasValidas} colaboradores"** **desligado**, com a explicação em texto pequeno: "Só dá para cadastrar quando todas as linhas estão certas." (tudo ou nada).

### Estado D: prévia pronta (`situacao: PRONTA`, `podeConfirmar: true`)

(desenho meu)
- Faixa de resumo, em cor de sucesso: "{totalLinhas} linhas lidas, todas certas. Pode cadastrar." e "Esta prévia vale até {HH:mm}.".
- A mesma tabela (todas as linhas com "OK").
- Bloco **"Depois de salvar"**, igual ao do cadastro de uma pessoa **no estado em que a 1b o deixou** (PNG 02, li; `telas\fatia-1.md`, tela 02), valendo para **todas** as pessoas da planilha:
  - caixa **"Enviar convite de acesso ao portal"** (`enviarConvite`, padrão marcada): funciona;
  - caixa **"Incluir no treinamento de onboarding"** (`incluirNoOnboarding`): **desligada**, visível, desmarcada e desabilitada, com a dica "Indisponível por enquanto"; a confirmação envia `false`;
  - caixa **"Enviar o formulário de admissão"** (`formularioAdmissaoId`): **desligada** do mesmo jeito; a confirmação envia `null`.
  - **Quem liga as duas:** a ficha 6 (onboarding, com `listarTreinamentosDeOnboarding`) e a ficha 5 (formulário de admissão, com `listarFormulariosRh`, `tipo=ADMISSAO` e `situacao=PUBLICADO`), **no cadastro e na importação de uma vez** (`telas\fatia-6.md` e `telas\fatia-5.md`, "Ligação com outras telas"). Esta fatia não chama essas rotas.
  - Texto pequeno: "Ninguém recebe senha: cada pessoa recebe um convite por e-mail para criar a sua. Nenhum e-mail sai pelas outras duas opções."
  - Repita aqui o comportamento que a fatia 1b deu a essas caixas no cadastro de uma pessoa (mesmos textos e mesma regra de ligar e desligar).
- Botões: "Enviar outra planilha" e **"Cadastrar {linhasValidas} colaboradores"** (principal).

### Estado E: cadastrando

- Botão principal em "Cadastrando..." e tudo desligado (evita duplo clique; a confirmação é uma transação só e pode levar alguns segundos). Se a conexão cair e o RH tentar de novo, o 409 `IMPORTACAO_JA_CONFIRMADA` explica que já foi feito.

### Estado F: resultado (`ImportacaoResultado`)

(desenho meu)
- Faixa de sucesso: "{totalCriados} colaboradores cadastrados."
- Quatro números: "Convites na fila: {convitesEnviados}", "Incluídos no onboarding: {incluidosNoOnboarding}", "Incluídos no formulário de admissão: {incluidosNoFormularioDeAdmissao}" e a data e hora de `confirmadaEmUtc`. **A tela mostra o que a API devolve**: o exemplo 200 de `confirmarImportacao` traz `totalCriados` 2, `convitesEnviados` 2, `incluidosNoOnboarding` 2 e `incluidosNoFormularioDeAdmissao` 2, e o MSW devolve esse exemplo; **com os dados simulados aparece "2" também nos dois do meio**, embora as caixas estejam desligadas (com a API real, as caixas desligadas mandam `false` e `null` e esses dois números vêm 0). O teste confere o **texto que vem do exemplo** e **não afirma 0**; o teste de "caixas desligadas" é só sobre o **corpo enviado** (`incluirNoOnboarding: false` e `formularioAdmissaoId: null`).
- Lista das pessoas cadastradas (`funcionarios`: nome, cargo, setor) com um link **"Abrir ficha"** para cada uma (ficha da fatia 1b).
- Botões: **"Fechar"** e **"Importar outra planilha"** (volta ao estado A). Ao confirmar, atualize a lista de colaboradores (as abas "Em admissão" e "Ativos" mudam).

### Estado G: erro do arquivo inteiro (sem prévia)

Quando o envio devolve 422, a prévia não existe e a tela volta ao estado A com uma faixa de erro vermelha com o `detail` da API (texto pronto em português):

| `codigo` | Texto (`detail`, do contrato) |
|---|---|
| `ARQUIVO_INVALIDO` | "O arquivo não é uma planilha do Excel (.xlsx). Baixe o modelo e preencha nele." |
| `ARQUIVO_MUITO_GRANDE` | "O arquivo é grande demais. O limite é de 5 MB." |
| `MODELO_ALTERADO` | "As colunas da planilha não são as do modelo. Baixe o modelo de novo e copie os dados para ele." |
| `PLANILHA_VAZIA` | "A planilha não tem nenhuma linha preenchida." |
| `LIMITE_DE_LINHAS_EXCEDIDO` | "A planilha tem 501 linhas. O limite é de 500 por importação." |

Nos erros de campo (400), mostre as mensagens de `errors` na área de envio.

### Estado H: erro ao confirmar (nada foi gravado)

| Resposta | `codigo` | O que a tela faz |
|---|---|---|
| 409 | `IMPORTACAO_JA_CONFIRMADA` | Mostra o `detail` ("Esta importação já foi confirmada.") e leva ao estado F se o resultado for conhecido; senão a "Fechar" e atualizar a lista. |
| 422 | `IMPORTACAO_COM_ERROS` | "A planilha ainda tem linhas com erro. Corrija e envie de novo." Volta ao estado A. |
| 422 | `IMPORTACAO_EXPIRADA` | "A prévia venceu. Envie a planilha de novo." Volta ao estado A. |
| 422 | `IMPORTACAO_LINHA_RECUSADA` | Mostra o `detail`, que cita a linha (campo `linha`) e termina com "Nenhum colaborador foi cadastrado. Envie a planilha de novo." Volta ao estado A. |
| 422 | `SEM_TREINAMENTO_DE_ONBOARDING` | **Não acontece nesta fatia** (a caixa está desligada). Quando a ficha 6 a liga: mostra o `detail`; fica no estado D com a caixa de onboarding desmarcada. |
| 422 | `FORMULARIO_ENCERRADO` ou `FORMULARIO_NAO_E_POR_PESSOAS` | **Não acontece nesta fatia.** Quando a ficha 5 liga a caixa: mostra o `detail`; fica no estado D para escolher outro formulário. |
| 400 | (em `formularioAdmissaoId`) | **Não acontece nesta fatia.** Com a caixa ligada (ficha 5): mostra a mensagem no seletor. |

### O que fica e o que sai (com a razão)

| Item | Decisão | Razão |
|---|---|---|
| Cadastro de uma pessoa (tela 02) | Fica como está (fatia 1b) | A importação só acrescenta o modo "Importar planilha". |
| Atualizar quem já existe por planilha | **Não existe** | "Só cadastra" (spec, seção 6; contrato). |
| Ler o Excel no navegador | **Não existe** | A API valida tudo (spec, seção 6). |
| Corrigir célula na própria prévia | **Não existe** | O RH corrige no Excel e envia de novo (tudo ou nada). |
| Coluna de gestor, de matrícula e de unidade na planilha | **Não existem** | O gestor vem do setor, a matrícula é gerada e não há unidades (contrato, `baixarModeloDeImportacao`). |
| Senha na planilha | **Não existe** | Ninguém recebe senha; vai convite por e-mail. |
| Readmitir quem saiu pela planilha | **Não existe** | A linha recebe um erro que manda usar "Readmitir" na ficha. |

### Estados comuns

- **Carregando**: ao baixar o modelo ("Baixando...") e nos estados B e E.
- **Vazio**: não se aplica (o estado A é o vazio).
- **Erro**: erros de rede ou 500 mostram "Não foi possível concluir. Tente de novo." e o código de rastreio (`traceId`) em letra pequena; o estado atual é mantido.
- **Sem permissão**: 401 leva ao login (fundação); 403 mostra "Você não tem permissão para esta ação." A alternância nem aparece para quem não é RH.

### Textos obrigatórios

Os textos de erro são os do `detail` da API (tabelas acima). Os rótulos de botão e os textos de apoio desta tela são **desenho meu** e podem ser ajustados na conferência visual, **desde que** os números e as mensagens da API continuem como vêm.

### Observações para quem monta

- O envio é **uma única requisição** com o arquivo; o teste que monta o `multipart` roda com `// @vitest-environment node`.
- A prévia e o resultado entram na auditoria no servidor (áreas e ações do contrato); o front não faz nada para isso.
- A tabela de prévia mostra CPF e telefone completos (o RH pode ver). Não guarde nem escreva no console.
- Depois de confirmar, invalide os dados de `listarFuncionarios` (e os totais das abas).

### Perguntas abertas (do `PROVISORIOS.md`)

- `paths > /api/rh/importacoes/modelo > get`: **quais são as colunas do modelo?** Construído com as 11 do contrato. Mudar as colunas muda `ImportacaoColuna` e `ImportacaoLinhaValores`: a tabela de rótulos (estado C) é o único lugar a ajustar; o conteúdo da prévia continua sendo só o que a API devolve.
- `paths > /api/rh/importacoes/{importacaoId}/confirmar > post`: **a importação marca as pessoas como "Em admissão"?** Construído: não marca nada especial; a situação vem da data de admissão de cada linha.

---

## Conferência: toda rota da fatia 2 tem tela

| `operationId` | Rota | Tela e estado |
|---|---|---|
| `baixarModeloDeImportacao` | `GET /api/rh/importacoes/modelo` | Tela 02 (modo Importar planilha), estado A |
| `validarPlanilhaDeImportacao` | `POST /api/rh/importacoes` | Estados B, C, D e G |
| `confirmarImportacao` | `POST /api/rh/importacoes/{importacaoId}/confirmar` | Estados E, F e H |

3 de 3 rotas com tela.
