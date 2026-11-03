# Notas de tela da fatia 1: colaboradores e base transversal

Contrato lido: `contrato\fontes\20-fatia-1-colaboradores.yaml` (a versão do contrato é o `info.version` de `contrato\openapi.yaml`; confira-a antes de começar e anote-a no `PROGRESSO.md`, tela por tela) e `00-base.yaml` (erros e schemas comuns). A fatia tem **43 rotas** (`contrato\ROTAS.md`, "fatia 1 colaboradores (43 rotas)"): a tabela da seção "Conferência das 43 rotas", no fim, liga cada uma a uma tela. Outras fontes: `contrato\CONVENCOES.md`, `contrato\ELENCO.md`, `contrato\PROVISORIOS.md`, o roadmap (fatia 1, "Telas" e "Regras") e o design `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md` (seção 7: decisões sobre as telas 03, 29 e 31). Nota da fatia 0 (login e shell): `telas\fatia-0.md`.

## Como ler estas notas

- **(li)** é o que está escrito no PNG indicado (`Figma\telas\NN-...png`, 1440 px de largura) ou no arquivo citado. **(contrato)** é o que o `operationId` citado diz. **(desenho meu)** é uma tela ou um estado que o Figma não tem: a descrição é a proposta deste plano, em estilo simples. **(suponho)** é inferência minha onde nada decide: confira na conferência visual ou registre em `DUVIDAS.md`. **(texto meu)** é um texto de interface que nenhum documento traz.
- **Onde o Figma e o contrato divergem, vale o contrato** (e a seção 7 do design). Cada nota diz o que o PNG mostra e o front não terá, com a razão.
- Nomes e números dos PNG são só ilustração. Os dados simulados (MSW) vêm dos exemplos do contrato e de `contrato\ELENCO.md`: por exemplo, o contrato tem **3** pessoas em admissão (o PNG 01 diz 2) e **104** logins em "Usuários do sistema" (o PNG 29 diz 103).
- **Telas desta fatia:** 8 com print (RH 01, 02, 03, 09, 10, 29, 30, 31) mais o gestor 52 (9 PNG); sem print (descritas aqui): o conteúdo dos cadastros **Setores, Gestores, Benefícios e Usuários do sistema** (a moldura e o menu estão na tela 29; só Cargos tem conteúdo desenhado), **Empresa**, **Esqueci minha senha**, **Definir senha** (convite e redefinição), **Trocar senha**, **Meus documentos**, e os fluxos **Editar dados**, **Inativar** e **Readmitir** da ficha (tela 03).
- O **gestor** reaproveita telas do funcionário (decisão 14 do roadmap): ver a seção "O que o gestor e o RH reaproveitam".

## Caminhos de tela

**O que não muda:** `/definir-senha?token=...` é o endereço que o e-mail traz (contrato, `solicitarRedefinicaoDeSenha`: "`<endereço do front>/definir-senha?token=<token>`"; `validarLinkDeSenha`: "o front a chama ao abrir `/definir-senha?token=...`"); e as três páginas de senha ficam **fora da área logada e sem menu**.

**A tabela única de caminhos é `05-caminhos-de-tela.md`** e vale sobre qualquer caminho escrito nesta nota. Os desta fatia, para quem lê só esta nota:

| Tela | Caminho |
|---|---|
| 01 lista (RH) | `/rh/colaboradores`, com `?situacao=`, `setorId`, `tipoContrato`, `busca` e `pagina` no endereço (o botão Voltar do navegador mantém o filtro) |
| 02 adicionar (RH) | `/rh/colaboradores/novo`, painel lateral sobre a lista |
| 03 ficha (RH) | `/rh/colaboradores/:funcionarioId`, painel lateral sobre a lista, com `?aba=dados` (padrão) ou `documentos` (as outras abas chegam nas fatias 3, 4 e 7: `ponto`, `holerites`, `advertencias`, `ferias`) |
| 09 aba Documentos (RH) | `/rh/colaboradores/:funcionarioId?aba=documentos` |
| 29 e 30 cadastros (RH) | `/rh/cadastros/cargos`, `.../setores`, `.../gestores`, `.../beneficios`, `.../usuarios`, `.../empresa`; o modal 30 abre sobre `/rh/cadastros/cargos` e não tem caminho próprio |
| 31 log de auditoria (RH) | `/rh/auditoria` |
| 52 ficha da equipe (gestor) | `/gestor/equipe/:funcionarioId`, **painel lateral sobre o Painel da equipe** (`/gestor/equipe`, fatia 7; a ficha 7 reestrutura a rota) |
| Esqueci minha senha | `/esqueci-senha` (público) |
| Definir senha | `/definir-senha?token=...` (público; fixo pelo contrato) |
| Trocar senha | modal aberto pelo rodapé do menu, sem endereço próprio |
| Meus documentos | `/eu/meus-documentos` (os três perfis) |

## Mapa tela → rotas

| Tela | Perfil | PNG | Rotas que a tela chama (`operationId`) |
|---|---|---|---|
| 01 Colaboradores | RH | `01-rh-colaboradores.png` | `listarFuncionarios`; o filtro de setores usa `listarSetores` |
| 02 Adicionar colaborador | RH | `02-rh-colaboradores-adicionar-colaborador.png` | `criarFuncionario`; as opções vêm de `listarCargos`, `listarSetores` e `listarBeneficiosDoCatalogo` |
| 03 Ficha, aba Dados | RH | `03-rh-colaboradores-ficha-aba-dados.png` | `obterFichaDoFuncionario`, `obterIndicadoresDoFuncionario`; menu "...": `enviarConviteDeAcesso` |
| 03 Editar dados (sem print) | RH | nenhum | `editarFuncionario` (mais `listarCargos`, `listarSetores`, `listarBeneficiosDoCatalogo`) |
| 03 Inativar (sem print) | RH | nenhum | `inativarFuncionario`, `cancelarDesligamentoAgendado` |
| 03 Readmitir (sem print) | RH | nenhum | `readmitirFuncionario` (mais `listarCargos` e `listarSetores`) |
| 09 Ficha, aba Documentos | RH | `09-rh-colaboradores-ficha-aba-documentos.png` | `listarDocumentosDoFuncionario`, `baixarDocumentoDoFuncionario` |
| 10 Modal Adicionar documento | RH | `10-rh-colaboradores-ficha-modal-adicionar-documento.png` | `adicionarDocumentoDoFuncionario` |
| 29 Cadastros, Cargos | RH | `29-rh-cadastros.png` | `listarCargos`, `editarCargo` |
| 30 Modal Novo cargo (e Editar cargo) | RH | `30-rh-cadastros-modal-novo-cargo.png` | `criarCargo`, `editarCargo` |
| 29 Cadastros, Setores (sem print) | RH | nenhum | `listarSetores`, `criarSetor`, `editarSetor` |
| 29 Cadastros, Gestores (sem print) | RH | nenhum | `listarGestores`, `cadastrarGestor`, `configurarAusenciaDoGestor` |
| 29 Cadastros, Benefícios (sem print) | RH | nenhum | `listarBeneficiosDoCatalogo`, `criarBeneficioDoCatalogo`, `editarBeneficioDoCatalogo`, `excluirBeneficioDoCatalogo` |
| 29 Cadastros, Usuários do sistema (sem print) | RH | nenhum | `listarUsuariosDoSistema`, `enviarConviteDeAcesso`, `ativarUsuarioDoSistema`, `desativarUsuarioDoSistema` |
| Cadastros, Empresa (sem print) | RH | nenhum | `obterEmpresa`, `salvarEmpresa` |
| 31 Log de auditoria | RH | `31-rh-log-de-auditoria.png` | `listarEventosDeAuditoria`, `obterEventoDeAuditoria`, `exportarAuditoria` |
| 52 Ficha da equipe, aba Dados | Gestor | `52-gestor-painel-da-equipe-ficha.png` | `obterFichaDaEquipe`, `obterIndicadoresDaEquipe`; entrada: `listarEquipeDoGestor` (tela 51, fatia 7) |
| Esqueci minha senha (sem print) | público | nenhum | `solicitarRedefinicaoDeSenha` |
| Definir senha: convite e redefinição (sem print) | público | nenhum | `validarLinkDeSenha`, `definirSenhaPeloLink` |
| Trocar senha (sem print) | Funcionário, Gestor, RH | nenhum | `trocarMinhaSenha` |
| Meus documentos (sem print) | Funcionário, Gestor, RH | nenhum | `listarMeusDocumentos`, `enviarMeuDocumento`, `baixarMeuDocumento` |
| Shell do portal, nome da empresa (nota da fatia 0; 1a) | os três | PNG 01, 32, 52 | `obterEmpresa` |

`listarEquipeDoGestor` é a lista da equipe do **contrato**: quem a chama é a tela 51 da fatia 7 (quadro "Hoje" e tabela "Pessoas da equipe", `telas\fatia-7.md`), e é dessa tabela que o gestor abre a ficha 52. Esta fatia **não** cria tela de lista da equipe (ver a tela 52).

## Regras comuns a todas as telas desta fatia

**Datas.** `date` (`2026-11-13`) é o dia de calendário: mostre `13/11/2026` **sem converter fuso** e **nunca** com `new Date('2026-11-13')` (o JavaScript lê como meia-noite UTC e o dia aparece como 12). `date-time` (`...Z`) é um instante em UTC: mostre em `America/Sao_Paulo`. Campos de data dos formulários em `dd/mm/aaaa` (o PNG 02 mostra `mm/dd/yyyy` e `10/13/2026` por causa do navegador do desenhista; use `lang="pt-BR"` e a formatação do projeto).

**CPF e telefone.** A API devolve e recebe **só dígitos** (11 do CPF; 10 ou 11 do telefone). A tela mostra `000.000.000-00` e `(11) 98765-4410` (com 10 dígitos, `(11) 8765-4410`). A decisão do dono de 2026-10-30 ("CPF e telefone completos para o RH, sem máscara") **tira a ocultação dos dígitos** que o PNG 03 mostra (`***.482.371-**`, `(11) 9****-4410`); a pontuação continua, **padrão adotado**: "sem máscara" quer dizer "sem esconder dígitos", não "sem pontos e traço" (CPF `846.392.710-69`, telefone `(11) 98765-4410`). O gestor **não** vê CPF e vê `telefoneMascarado` pronto da API, como veio (`(11) 9****-7783`).

**Rótulos que se repetem** (o valor do contrato, à esquerda, é o que a API manda):
- **Etiqueta "Situação hoje"** (tela 01 e cabeçalho das telas 03 e 52; contrato `listarFuncionarios`): `situacao = INATIVO` mostra "Inativo"; `EM_ADMISSAO` mostra "Em admissão"; para `ATIVO`, `situacaoHoje = FERIAS` mostra "Férias", `ATESTADO` mostra "Atestado" e **qualquer outro valor** mostra "Ativo". (O PNG 52 mostra "Ativo" para Diego Farias, que no contrato está `SEM_REGISTRO`: bate com a regra.) Cores (li no PNG 01): "Ativo" verde-claro, "Atestado" pêssego, "Férias" azul-claro; "Em admissão" usa o contorno que o Figma deu a "Experiência" (suponho) e "Inativo", cinza (suponho).
- **Nível de acesso** (`perfil`): `Funcionario` aparece como **"Colaborador"**, `Gestor` como "Gestor", `RH` como "RH" (contrato; li "Colaborador" nos PNG 02 e 31).
- **Tipo de contrato** (`FuncionarioTipoContrato`): `CLT` "CLT" e `ESTAGIO` "Estágio" (o texto "Estágio" é meu; o PNG só mostra "CLT"). Valor que o front não conhece (o contrato pode ganhar tipos em versão menor): mostre o texto como veio. **Provisório** (`x-provisorio`): a lista de tipos de contrato ainda não foi decidida pelo dono.
- **Situação do login** (`UsuarioSistemaSituacao`): `SEM_CONVITE` "Sem convite", `CONVITE_ENVIADO` "Convite enviado", `CONVITE_EXPIRADO` "Convite vencido", `SENHA_DEFINIDA` "Senha definida" (textos meus; o contrato só dá os valores e o sentido).

**Erros que se repetem** (todo erro é `application/problem+json`; mostre o `detail`, já em português; decida pelo `codigo`, nunca pelo `title`):

| Código HTTP | O que a tela faz |
|---|---|
| 400 com `errors` | Marca cada campo com a mensagem dele (o nome do campo é o do corpo enviado: `cpf`, `emailCorporativo`, `cargoId`, `arquivo`, `novaSenha`...). |
| 401 | A fundação trata (renovar a sessão ou voltar ao login). |
| 403 | Telas do RH e do gestor só abrem para o perfil certo: a fundação mostra o acesso negado. |
| 404 | Para uma ficha ou linha: some e a tela atualiza ("O registro não foi encontrado."). |
| 409 e 422 | Mostre o `detail`, junto do campo ou do botão que causou. **Nunca** mostre `regraDoBanco` sozinha (é texto técnico, sem acento): ela vem junto do `detail`. |
| 429 | `Muitas tentativas. Aguarde um minuto e tente de novo.` |
| 500 ou sem conexão | `Erro no servidor. Tente novamente em instantes.` / `Não foi possível conectar. Tente novamente.` (mesmos textos da fatia 0). |

**Botões que gravam:** `disabled` enquanto a requisição está em andamento (clique duplo = uma requisição só). Depois de gravar, atualize o que aparece em outras telas (lista, totais, contadores do menu).

**Dados sensíveis.** CPF, telefone, endereço, e-mail pessoal, documentos e o trajeto não vão para o console, para `localStorage` nem para o endereço da página.

---

## Tela 01: Colaboradores, lista (RH)

PNG: `Figma\telas\01-rh-colaboradores.png`.

**Li no print**
- Título "Colaboradores"; subtítulo "101 ativos, 2 em admissão e 7 inativos"; botão cheio "+ Adicionar colaborador" no alto à direita.
- Abas com número: **Ativos 101**, **Em admissão 2**, **Inativos 7**. Busca "Buscar por nome, CPF ou matrícula" e dois seletores, "Todos os setores" e "Todos os contratos".
- Tabela com as colunas **Nome** (avatar com as iniciais, nome e, embaixo, o e-mail corporativo), **Cargo**, **Setor**, **Admissão** (`dd/mm/aaaa`), **Situação hoje** (etiqueta) e **Ações** (botão "Abrir ficha"). Oito linhas: Bruno Lima, Carla Mendes, João Pedro Alves, Júlia Nunes, Lucas Prado, Marina Costa, Pedro Santos, Sofia Ramos (ordem alfabética).
- Etiquetas vistas: "Ativo", "Atestado", **"Experiência"** (contornada, em Lucas Prado) e "Férias".
- Rodapé: "Mostrando 1 a 8 de 101" e os botões "Anterior" e "Próxima".
- Menu lateral do RH (ver a nota da fatia 0): "Colaboradores" ativo.

**O que fica**
- Título, subtítulo, as três abas, a busca, os dois filtros, as seis colunas, "Abrir ficha", "Adicionar colaborador" e o rodapé com a paginação.

**O que sai ou muda (e por quê)**
- **Sai a etiqueta "Experiência"** (li em Lucas Prado): não há período de experiência (decisão do dono, 2026-10-28; contrato: "A etiqueta 'Experiência' do Figma não existe"). A situação de Lucas vem do contrato como as demais.
- **Os números vêm de `totais`**, que é da empresa inteira e **ignora todos os filtros** (inclusive a busca): subtítulo "{ativos} ativos, {emAdmissao} em admissão e {inativos} inativos" e os números das abas. O `total` da paginação (que respeita os filtros) só vale para "Mostrando N de M" e para o número de páginas. No cenário de demonstração são **101 ativos, 3 em admissão, 7 inativos** (o PNG diz 2 em admissão: o contrato tem Rafael Torres, Tiago Nogueira e Aline Carvalho; `ELENCO.md`).
- **A aba é o filtro `situacao`** (`ATIVO` padrão, `EM_ADMISSAO`, `INATIVO`). "Ativos" **inclui** quem está de férias ou com atestado.
- **Etiqueta de cada linha:** a regra de "Rótulos que se repetem" (acima), com `situacao` e `situacaoHoje`. `situacaoHoje` vem `null` em quem não é `ATIVO`.
- **"Mostrando 1 a 8 de 101":** o PNG mostra 8 linhas só por causa da altura do desenho. Use `tamanhoPagina = 20` (padrão do contrato; máximo 100) e escreva "Mostrando {de} a {até} de {total}" (suponho). Em uma lista vazia, o rodapé some.
- **Ordem:** `NOME` crescente (padrão). O contrato aceita ordenar por `NOME` ou `DATA_ADMISSAO` (`ordenarPor`, `direcao`), mas o PNG não desenha controle de ordenação: **não** construa um (o padrão basta).
- **Filtro "Todos os contratos"** oferece os valores de `FuncionarioTipoContrato` (CLT, Estágio). **Provisório** (`x-provisorio` em `components > schemas > FuncionarioTipoContrato`): se o dono acrescentar tipos, o filtro e o cadastro ganham o valor novo; a tela não muda de forma.
- **Busca:** procura no nome (sem diferenciar maiúsculas nem acentos), no CPF (só os dígitos contam: pontos e traço são ignorados) e na matrícula. Envie o texto como foi digitado, com no máximo 100 caracteres (limite do contrato), e espere uns 300 ms depois da última tecla (suponho). Mudar a aba, a busca ou um filtro volta à página 1.
- **Iniciais do avatar:** primeira letra do primeiro nome e do último ("João Pedro Alves" = "JA").

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Lista, abas, busca, filtros, paginação e totais (`pagina`, `tamanhoPagina`, `busca`, `situacao`, `setorId`, `tipoContrato`, `ordenarPor`, `direcao`) | `listarFuncionarios` |
| Opções do filtro "Todos os setores" (`tamanhoPagina = 100`) | `listarSetores` |
| "Abrir ficha" e "Adicionar colaborador" abrem os painéis das telas 03 e 02 | (as rotas dessas telas) |

**Estados**
- Carregando: esqueleto da tabela; as abas e o subtítulo mostram "—" até `totais` chegar. Vazio sem filtro: "Nenhum colaborador nesta aba." (texto meu); com busca ou filtro: "Nenhum colaborador encontrado." (texto meu). Erro: o `detail` e o botão "Tentar de novo". Sem permissão: 403 mostra a tela de acesso negado da fundação (rota só do RH; o menu esconde o item de outros perfis).

**Observações para quem monta**
- A tabela tem 6 colunas e cabe em 1366 px sem rolagem horizontal da página (a conferência é da fatia 8).
- Teste de contrato: confira os parâmetros enviados (`esperarRequisicaoConforme('listarFuncionarios', ...)`): a lacuna do `tsc` com chave extra está em `03-contrato-e-mocks.md`.

**Perguntas abertas (`x-provisorio`)**: `FuncionarioTipoContrato` (acima).

---

## Tela 02: Adicionar colaborador (RH)

PNG: `Figma\telas\02-rh-colaboradores-adicionar-colaborador.png`.

**Li no print**
- Painel lateral de uns **600 px** (do x = 840 ao 1440) sobre a lista escurecida. Título "Adicionar colaborador" e botão fechar "X".
- Texto de abertura: "Os campos marcados entram no cadastro. O restante a própria pessoa completa no formulário de admissão."
- **"Dados pessoais":** "Nome completo" (dica "Como está no documento"); "CPF" (dica "000.000.000-00") e "Data de nascimento" (data); "Telefone" (dica "(11) 90000-0000") e "E-mail corporativo" (dica "nome.sobrenome@empresa.com.br").
- **"Contrato":** "Data de admissão" (no PNG, `10/13/2026`) e "Tipo de contrato" (`CLT`); "Cargo" ("Escolha o cargo") e "Setor" ("Escolha o setor"); "Gestor" (dica "O gestor do setor escolhido") e "Nível de acesso" (`Colaborador`).
- **"Benefícios":** seis caixas em duas colunas: Vale-refeição, Plano de saúde, Seguro de vida (as três **marcadas**), Vale-transporte, Plano odontológico, Auxílio-creche (desmarcadas).
- **"Depois de salvar"** (bloco de fundo claro): "Enviar convite de acesso ao portal", "Enviar o formulário de admissão" e "Incluir no treinamento Onboarding 2026" (as três **marcadas**).
- Rodapé: botões "Cancelar" e "Adicionar colaborador".
- **Não há** no print nenhuma alternância "Importar planilha" (li).

**O que fica**
- Os quatro blocos e os campos acima. Os campos de Dados pessoais e Contrato são **obrigatórios** (o contrato os põe em `required`).

**O que sai ou muda (e por quê)**

| Campo (rótulo) | Campo da API (`FuncionarioCriarComando`) | Regra (contrato) |
|---|---|---|
| Nome completo | `nome` | 1 a 150 caracteres |
| CPF | `cpf` | 11 dígitos, **dígitos verificadores corretos**; digite com máscara, envie só dígitos |
| Data de nascimento | `dataNascimento` | não pode ser no futuro |
| Telefone | `telefone` | 10 ou 11 dígitos, com DDD; digite com máscara, envie só dígitos |
| E-mail corporativo | `emailCorporativo` | e-mail válido, até 150, **único**; é o e-mail de login |
| Data de admissão | `dataAdmissao` | pode ser futura (a pessoa fica "Em admissão" até o dia) |
| Tipo de contrato | `tipoContrato` | `CLT` ou `ESTAGIO` (**provisório**, ver tela 01) |
| Cargo | `cargoId` | opções de `listarCargos` (`tamanhoPagina = 100`) |
| Setor | `setorId` | opções de `listarSetores` (`tamanhoPagina = 100`) |
| Gestor | (nenhum campo) | **só informativo** |
| Nível de acesso | `perfil` | `Funcionario` ("Colaborador", padrão), `Gestor` ou `RH` |
| Benefícios | `beneficioIds` | caixas de `listarBeneficiosDoCatalogo` (`tamanhoPagina = 100`); sem repetição; padrão `[]` |
| Enviar convite de acesso ao portal | `enviarConvite` | padrão `true` (marcada) |
| Enviar o formulário de admissão | `formularioAdmissaoId` | **desligada** nesta fatia (ver "Ligação com outras telas"); padrão `null` |
| Incluir no treinamento... | `incluirNoOnboarding` | **desligada** nesta fatia; padrão `false` |

- **"Gestor" não é campo:** a API usa sempre o gestor do setor escolhido (**provisório**, `x-provisorio` em `FuncionarioCriarComando`: "`FUNCIONARIOS.ID_GESTOR` pode ser diferente do gestor do setor?"; construído: sempre o do setor). A tela mostra o nome de `gestor` do setor escolhido em `listarSetores`; antes de escolher o setor, a dica do PNG ("O gestor do setor escolhido"); setor sem gestor: "Este setor não tem gestor" (texto meu; os pedidos de ponto e de férias dessa pessoa vão direto ao RH, contrato). Se o dono disser que o gestor pode ser outro, o comando ganha `gestorId` e o campo vira seletor.
- **Não há matrícula** no formulário: é gerada pelo sistema e só se lê (decisão de 2026-10-30). **Não há "Unidade"** (sem unidades).
- **Benefícios:** o contrato diz padrão `[]` e **nenhum documento diz que há benefícios que já vêm marcados**. O PNG mostra três marcados; trate como exemplo de preenchimento e **deixe todos desmarcados** (suponho). Ver a pergunta aberta 1. As caixas vêm do catálogo na ordem do `id`; o catálogo de demonstração tem **7** benefícios (o PNG mostra 6, sem "Vale-cultura").
- **"Depois de salvar":** só "Enviar convite de acesso ao portal" funciona nesta fatia (padrão marcada). As outras duas ficam **visíveis, desmarcadas e desabilitadas**, com a dica "Indisponível por enquanto" (texto meu). Quem liga cada uma: a fatia 5 (formulário de admissão) e a fatia 6 (onboarding), no cadastro e na importação de uma vez (e no diálogo "Readmitir"). Nesta fatia o cadastro envia sempre `formularioAdmissaoId: null` e `incluirNoOnboarding: false`. O rótulo do onboarding, nesta fatia, é "Incluir no treinamento de onboarding" (o nome "Onboarding 2026" do PNG vem do treinamento, só na fatia 6).
- **Alternância "Importar planilha" (fatia 2):** o spec (seção 6) diz que o botão de importar fica "na tela de cadastro". O lugar, definido em `telas\fatia-2.md` (desenho meu): **no cabeçalho do painel, logo abaixo do título**, um seletor de duas opções, "Cadastrar um colaborador" (ativa) e "Importar planilha". **Nesta fatia a segunda opção aparece desligada** (`disabled`, com a dica "Indisponível por enquanto") e a primeira é o formulário descrito aqui. A fatia 2 liga a opção e traz o conteúdo.
- **Texto de abertura:** "O restante a própria pessoa completa no formulário de admissão" depende de como as respostas do formulário de admissão chegam à ficha: **provisório** (`x-provisorio` em `components > schemas > FormularioTipo`, fatia 5): a hipótese do contrato é que **não chegam** e o RH lê as respostas e copia o que quiser. O texto fica como está no PNG; se o dono mudar a hipótese, só ele muda.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| "Adicionar colaborador" | `criarFuncionario` |
| Opções de Cargo, Setor e Benefícios | `listarCargos`, `listarSetores`, `listarBeneficiosDoCatalogo` |

**Estados**
- Carregando as opções: os selects mostram "Carregando…" e o botão de salvar fica desabilitado. Erro ao carregar as opções: mensagem com o `detail` e "Tentar de novo".
- Enviando: botão desabilitado, uma requisição só. Cancelar e "X" fecham o painel (sem confirmação; suponho).
- **Sucesso (201, devolve a ficha completa):** feche o painel, atualize a lista e os totais (`listarFuncionarios`) e mostre o aviso "{nome} foi cadastrado." com o botão "Abrir ficha" (texto meu). Com o convite marcado, acrescente "O convite foi enviado para {emailCorporativo}." (texto meu).
- Sem permissão: 403, acesso negado.

**Textos e mensagens de erro (contrato)**
- 400: cada mensagem junto do campo (`errors`).
- **409 `CPF_JA_CADASTRADO` / `EMAIL_JA_CADASTRADO`** (`cadastroExistente`: `funcionarioId`, `nome`, `ativo`): o `detail` junto de CPF ou E-mail. Com `ativo = false` o `detail` já manda usar "Readmitir" (ex.: "Este CPF pertence a um cadastro inativo (Henrique Alves). Se a pessoa está voltando à empresa, use "Readmitir" na ficha dela."): acrescente o botão "Abrir a ficha de {nome}", que leva à ficha 03 dele (onde está o "Readmitir"). Com `ativo = true` o contrato diz que o 409 "só avisa"; o botão também ajuda aqui (suponho).
- **422 `GESTOR_DO_SETOR_INATIVO`** ("O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar."): junto do Setor. `regraDoBanco` não aparece sozinha.
- 422 `SEM_TREINAMENTO_DE_ONBOARDING`, `FORMULARIO_ENCERRADO`, `FORMULARIO_NAO_E_POR_PESSOAS` e 400 em `formularioAdmissaoId`: não acontecem nesta fatia (as caixas estão desligadas); a fatia 5 e a 6 tratam.
- Erros de trigger (422 com `regraDoBanco`: "CPF invalido.", "O gestor informado e um funcionario inativo.", "Nao e possivel ativar beneficio para funcionario inativo."): mostre o `detail`.

**Observações para quem monta**
- O corpo tem `required: [nome, cpf, dataNascimento, telefone, emailCorporativo, dataAdmissao, tipoContrato, cargoId, setorId, perfil]`; o teste de contrato confere as chaves enviadas (uma chave a mais, como `matricula` ou `gestorId`, deve falhar).
- Máscara de CPF e de telefone: duas funções simples da fundação (sem biblioteca; `02-pilha.md`). O CPF é conferido no navegador por conforto (dígitos verificadores); quem decide é a API.

**Perguntas abertas (`x-provisorio`)**: `FuncionarioCriarComando` (gestor do setor), `FuncionarioTipoContrato`, `FormularioTipo` (texto de abertura). Pergunta ao dono (muda o que o usuário vê): benefícios que já vêm marcados (ver "Perguntas abertas", no fim).

---

## Tela 03: Ficha do colaborador, aba Dados (RH)

PNG: `Figma\telas\03-rh-colaboradores-ficha-aba-dados.png`.

**Li no print**
- Painel lateral de uns **864 px** (do x = 576 ao 1440) sobre a lista. Trilha "Colaboradores / Bruno Lima" ("Colaboradores" é link que fecha o painel) e botão fechar "X".
- Cabeçalho: avatar grande "BL", nome "Bruno Lima", "Analista pleno, setor Financeiro, matrícula 00482", etiqueta "Ativo". Botões: **"Editar dados"** (cheio), **"Inativar"** (contorno vermelho) e **"..."**.
- Abas: **Dados** (ativa), Ponto, Férias, Holerites, Documentos, Advertências.
- **"Dados pessoais"** (cartão em 3 colunas): Nome completo; E-mail corporativo; Telefone (`(11) 9****-4410`); CPF (`***.482.371-**`); Aniversário (`18 de junho`); Endereço (`[Rua, número, bairro e cidade]`, marcador).
- **"Contrato"**: Cargo; Setor; Gestor(a) (Marina Costa); Tipo de contrato (CLT); Jornada (`44 h semanais`); Admissão (`14/03/2022`); **Unidade (`[Matriz]`)**.
- **"Benefícios"**: etiquetas "Vale-refeição", "Plano de saúde + 1 dependente", "Vale-transporte", "Seguro de vida".
- Um quadro com quatro números: "Banco de horas" (`+6h20`), "Férias disponíveis" (`18 dias`), "Advertências" e "Próximo pagamento" (**o PNG corta o painel na altura de 997 px e os dois últimos valores não aparecem**: só li os rótulos).

**O que fica**
- Trilha, cabeçalho, as abas, os três blocos de dados, as etiquetas de benefícios e os quatro números.

**O que sai ou muda (e por quê)**
- **CPF e telefone completos** (decisão do dono, 2026-10-30; contrato `obterFichaDoFuncionario`: "o Figma mostra máscara, vale o documento"): `123.482.371-30` e `(11) 98765-4410`, com pontuação (padrão adotado, como `846.392.710-69` do exemplo de cadastro), na formatação das regras comuns. O dado vem de `cpf` e `telefone` (só dígitos).
- **Sai "Unidade"** (li `[Matriz]`): sem unidades (decisão do dono, 2026-10-28).
- **Campos que o contrato traz e o PNG não mostra:** `emailPessoal`, `rg` e `rgOrgaoEmissor` (o RH vê tudo) e `perfil` (nível de acesso). Acrescente **"E-mail pessoal"** e **"RG"** (`{rg}, {rgOrgaoEmissor}`) ao cartão "Dados pessoais" e **"Nível de acesso"** ao cartão "Contrato" (suponho; sem eles o "Editar dados" mudaria campos que a pessoa nunca vê). Todos com "—" quando `null`.
- **Matrícula** vem do campo `matricula` (texto de 5 dígitos, ex.: `00482`; gerada pelo sistema, só leitura). `null` não deve acontecer para quem existe; se vier, não escreva "matrícula" no cabeçalho.
- **Cabeçalho:** "{cargo.nome}, setor {setor.nome}, matrícula {matricula}"; `cargo` e `setor` podem vir `null` (omita a parte). A etiqueta segue a regra de "Rótulos que se repetem" (Inativo, Em admissão, Férias, Atestado ou Ativo).
- **Endereço:** o marcador do PNG vira o texto montado de `endereco` (todos os campos podem ser `null`): "{logradouro}, {numero}, {complemento}, {bairro}, {cidade}/{uf}, CEP {cep com hífen}", omitindo o que for `null` (suponho o formato). Tudo `null`: "—".
- **Aniversário:** "18 de junho" vem de `dataNascimento` (dia e mês por extenso, sem ano; `null`: "—").
- **Jornada:** "44 h semanais" é `jornadaSemanalHoras` (carga semanal do cargo; `null`: "—"). O horário (das 08:00 às 17:48) é da empresa e **não** aparece nesta aba.
- **Gestor(a):** `gestor.nome`; `null` mostra "Sem gestor" (o setor não tem gestor ou a pessoa é o gestor).
- **Etiquetas de benefícios:** uma por item de `beneficios` ("Plano de saúde + 1 dependente" quando `quantidadeDependentes` > 0; com mais de 1, "+ 2 dependentes"). Sem benefícios: "Nenhum benefício ativo." (texto meu).
- **Os quatro números** vêm de `obterIndicadoresDoFuncionario` (`bancoDeHorasMinutos`, `feriasDisponiveisDias`, `advertenciasQuantidade`, `proximoPagamento`). O contrato diz que cada um vem `null` **enquanto a fatia que o calcula não existir** (3, 7 e 4): mostre "—". Formatos: banco de horas com `formatarMinutosComoHoras(min, { sinal: true })` **da 1a** (`src\shared\formato\numeros.ts`; `+6h20`, `−1h30`, `0h00` sem sinal: se o zero com `sinal` não sair assim, ajuste o mesmo arquivo e o teste dele, **não crie outra função**); férias "18 dias" com `plural` da 1a ("1 dia" no singular); advertências o número; próximo pagamento "6 de novembro" com `formatarDiaEMes` (acrescentada à `datas.ts` pela Task 3 da 1b, `fatia-1b-telas.md`; como a tela 32, `telas\fatia-7.md`; suponho). Eles ficam **em quadro próprio abaixo dos Benefícios**, em duas colunas (li os rótulos nessa disposição).
- **Cores dos números** (li no PNG 52; a regra é suposição): banco de horas negativo em laranja, advertências maiores que zero em vermelho, o resto na cor padrão.
- **Abas:** nesta fatia só **Dados** e **Documentos** funcionam. As abas **Ponto** (fatia 3), **Férias** (fatia 7), **Holerites** e **Advertências** (fatia 4) chegam com as fatias delas: a moldura da ficha já nasce aceitando uma lista de abas e cada fatia acrescenta a sua. **Não** mostre uma aba que leva a uma página vazia.
- **"..." (o PNG só o mostra fechado):** o contrato dá **"Reenviar convite"** (`enviarConviteDeAcesso`: "Tela 29 e menu '...' da ficha"). O item aparece quando a pessoa **não está inativa** e `acesso.situacao` é diferente de `SENHA_DEFINIDA` (o convite só se reenvia a quem nunca definiu a senha); com `SEM_CONVITE` o rótulo é "Enviar convite de acesso". Sem itens, o botão "..." **não aparece** (suponho). Sem `emailCorporativo`, o item fica desabilitado com a dica "Esta pessoa não tem e-mail corporativo cadastrado." Sucesso: "Convite enviado para {emailCorporativo}." (texto meu). Erros: 422 `FUNCIONARIO_INATIVO`, `FUNCIONARIO_SEM_EMAIL_CORPORATIVO` e `SENHA_JA_DEFINIDA` (mostre o `detail`; o último não deve acontecer porque o item some).
- **Botões do cabeçalho conforme `situacao`** (contrato): **"Inativar"** só para quem **não** está `INATIVO`; **"Readmitir"** (novo, decisão do dono, 2026-10-30) **só** para `INATIVO`, **no lugar de** "Inativar". "Editar dados" aparece nos dois casos (suponho: o contrato não o proíbe para inativo).
- **Desligamento agendado** (`desligamento.situacao = AGENDADO`): faixa no alto do corpo, abaixo das abas, "Desligamento agendado: o último dia de trabalho é {dd/mm/aaaa}. A pessoa continua com acesso até o dia seguinte." com o botão "Cancelar desligamento" (texto meu; o contrato só diz que `cancelarDesligamentoAgendado` serve a isso). Com `EFETIVADO` a pessoa já é `INATIVO`.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Abrir a ficha (cabeçalho e dados) | `obterFichaDoFuncionario` |
| Quatro números (carrega **à parte**: se falhar, mostre "—" e "Tentar de novo" só nele; suponho) | `obterIndicadoresDoFuncionario` |
| "Editar dados" (fluxo abaixo) | `editarFuncionario` |
| "Inativar" e "Cancelar desligamento" (fluxo abaixo) | `inativarFuncionario`, `cancelarDesligamentoAgendado` |
| "Readmitir" (fluxo abaixo) | `readmitirFuncionario` |
| "Reenviar convite" | `enviarConviteDeAcesso` |

**Estados**
- Carregando: esqueleto do cabeçalho e dos blocos. 404 ("O registro não foi encontrado."): feche o painel e avise na lista. Erro: `detail` e "Tentar de novo". Sem permissão: 403.
- A ficha do **administrador do seed** pode vir sem dados pessoais: `cpf`, `telefone`, `dataNascimento`, `rg` e o endereço `null` (contrato). Mostre "—" e deixe o "Editar dados" completá-los (o CPF é exigido na edição).

**Observações para quem monta**
- Um endereço de painel (`/rh/colaboradores/:id`) deve abrir direto na ficha, com a lista atrás (suponho).
- A ficha de **quem acabou de ser cadastrado** vem na resposta de `criarFuncionario` (mesmo schema `FuncionarioFichaRh`): pode servir de cache inicial.

**Perguntas abertas (`x-provisorio`)**: `FuncionarioTipoContrato` (rótulo do tipo de contrato).

### Fluxo "Editar dados" (sem print; desenho meu)

O botão **"Editar dados"** troca o corpo da aba por um formulário (o painel continua o mesmo). `editarFuncionario` é uma **substituição completa**: o front carrega a ficha, a pessoa altera e o front devolve **tudo** (contrato).
- **Campos:** os mesmos de "Dados pessoais" e "Contrato" da tela 02 (nome, CPF, nascimento, telefone, e-mail corporativo, data de admissão, tipo de contrato, cargo, setor, gestor informativo, nível de acesso), mais **E-mail pessoal** (`emailPessoal`, opcional), **RG** (`rg`, até 20) e **Órgão emissor** (`rgOrgaoEmissor`, até 20), e o **Endereço** (`endereco`: CEP de 8 dígitos, logradouro até 150, número até 10, complemento até 100, bairro até 100, cidade até 100, UF de 2 letras; todos opcionais), e as caixas de **Benefícios** (os **ativos** desejados: os que entram são ativados e os que saem, desativados; pedidos de adesão pendentes e dependentes são da fatia 7 e não se tocam aqui). **Sem** "Depois de salvar", sem matrícula (só leitura) e sem situação.
- Campos que o contrato exige e a ficha pode ter `null` (seed): `cpf`, `telefone`, `dataNascimento`, `tipoContrato`, `cargoId`, `setorId`, `perfil`. O formulário abre com eles vazios e exige o preenchimento.
- **Avisos junto dos campos (texto meu, regra do contrato):** ao trocar o **e-mail corporativo**: "Trocar o e-mail corporativo troca o e-mail de login: a pessoa precisa usar o novo."; ao trocar o **setor**: "O gestor da pessoa passa a ser o do novo setor." (e, se o gestor do novo setor é a própria pessoa, ela fica sem gestor).
- **"Nível de acesso" da própria pessoa logada:** desabilitado quando `ficha.id` é igual ao `funcionarioId` do usuário logado, com a dica "Você não pode alterar o seu próprio nível de acesso." (o contrato dá 422 `NAO_PODE_ALTERAR_O_PROPRIO_PERFIL` com essa frase; o front evita o erro).
- **Botões:** "Cancelar" (volta à leitura sem gravar) e "Salvar alterações". Sucesso (200 devolve a ficha atualizada): volte à leitura, atualize a lista (`listarFuncionarios`) e mostre "Dados salvos." (texto meu).
- **Erros:** 400 por campo; **409** `CPF_JA_CADASTRADO` / `EMAIL_JA_CADASTRADO` (como na tela 02, com o botão "Abrir a ficha de {nome}"); 422 `GESTOR_DO_SETOR_INATIVO`; 422 `GESTOR_PRECISA_TER_PERFIL_GESTOR` ("Carlos Ribeiro gerencia o setor Comercial e precisa ter o nível Gestor. Tire-o do setor antes de mudar o nível."); 422 `NAO_PODE_ALTERAR_O_PROPRIO_PERFIL`; 422 de trigger com `regraDoBanco`. Sempre o `detail`.

### Fluxo "Inativar" (sem print; decisões do dono: contrato e roadmap)

O botão **"Inativar"** abre um **diálogo** "Inativar {nome}" (desenho meu):
1. **"Último dia de trabalho"** (data): padrão **hoje**; não pode ser anterior à `dataAdmissao` (a API responde 400 em `ultimoDiaDeTrabalho`); pode ser hoje, futuro ou passado.
2. **Explicação** (texto meu, conteúdo do contrato): "A pessoa perde o acesso ao portal, os benefícios dela são desativados e as férias futuras são canceladas, sem aviso a ela. Nada é apagado: o histórico continua na ficha." Quando `perfil` é `Gestor`, acrescente: "Se esta pessoa gerencia um setor, os pedidos que esperam por ela passam para o RH e o setor fica sem gestor ativo até você escolher outro em Cadastros, Setores." (o contrato descreve o efeito; o front não sabe se ela tem setor, por isso a frase é condicional).
3. **Trava de digitar "desativar" (decisão do dono):** um campo "Digite **desativar** para confirmar" e o botão **"Inativar"** **só habilitado** quando o texto digitado é `desativar` (sem diferenciar maiúsculas e sem espaços nas pontas; suponho). **É proteção só da interface:** a API **não** recebe nem confere essa palavra (contrato: "não há campo no corpo"); o corpo é só `{ ultimoDiaDeTrabalho }`.
4. Botões: "Cancelar" e "Inativar" (cor de perigo).
- **Resposta 200** (a ficha): `desligamento.situacao = AGENDADO` (o último dia é hoje ou futuro: a API grava a data no dia seguinte; a pessoa **continua ativa** até lá) ou `EFETIVADO` (já gravado: a pessoa está `INATIVO`). Mostre "Desligamento de {nome} registrado." e atualize a ficha, que passa a mostrar a faixa de agendamento ou o "Readmitir". Atualize a lista.
- **Erros:** 400 `ultimoDiaDeTrabalho`; **409 `DESLIGAMENTO_JA_REGISTRADO`** ("Este colaborador já está inativo ou já tem um desligamento registrado."); 404.
- **"Cancelar desligamento"** (faixa de agendamento): diálogo simples "Cancelar o desligamento agendado de {nome}?" com "Manter agendado" e "Cancelar desligamento" (sem a trava de digitar). 204: some a faixa. **422 `SEM_DESLIGAMENTO_AGENDADO`** ("Não há desligamento agendado para cancelar."): recarregue a ficha (ou já foi efetivado ou já foi cancelado).

### Fluxo "Readmitir" (sem print; decisão do dono, 2026-10-30)

O botão **"Readmitir"** (só em `INATIVO`) abre um diálogo "Readmitir {nome}" (desenho meu):
- **Explicação** (texto meu, conteúdo do contrato): "A pessoa volta com o mesmo cadastro, o mesmo CPF e a mesma matrícula, e o histórico continua na ficha. Os benefícios e as férias futuras cancelados na saída **não voltam**: dê os benefícios de novo em Editar dados. O acesso ao portal precisa ser liberado de novo."
- **Campos:** "Data de admissão" (obrigatória; **não pode ser anterior ao último dia de trabalho do vínculo anterior**, que vem em `desligamento.ultimoDiaDeTrabalho` da ficha: use-o como data mínima; pode ser futura, e então a pessoa fica "Em admissão"), "Tipo de contrato", "Cargo" e "Setor" (os três já preenchidos com os da ficha, suponho). **Sem** nível de acesso (é o que a pessoa tinha, exceto "Gestor", que volta como Colaborador), **sem** benefícios, **sem** CPF e e-mail (continuam os do cadastro).
- **Três caixas** (mesmo efeito do cadastro, tela 02): "Incluir no treinamento de onboarding" e "Enviar o formulário de admissão" **desligadas** nesta fatia (padrão `false` e `null`); e **"Enviar convite de acesso ao portal"** (`enviarConvite`, padrão **desmarcada**), que **só aparece quando `acesso.situacao` é diferente de `SENHA_DEFINIDA`** (o contrato: "o front só mostra a caixa quando faz sentido"). Marcada, ela libera o login e envia o convite de uma vez.
- Botões: "Cancelar" e "Readmitir".
- **Sucesso (200, a ficha):** "{nome} foi readmitido." e a ficha passa a "Ativo" ou "Em admissão". Se `acesso.ativo` é `false` (o login não volta sozinho), acrescente "O acesso ao portal continua desativado. Libere em Cadastros, Usuários do sistema (Ativar)." com o link para lá (texto meu; o contrato descreve o efeito). Atualize a lista e os totais.
- **Erros:** 400 em `dataAdmissao`; **409 `FUNCIONARIO_JA_ATIVO`** ("Este colaborador já está ativo. Só se readmite quem está inativo."): recarregue a ficha; 422 `GESTOR_DO_SETOR_INATIVO`; 422 `SENHA_JA_DEFINIDA` (a caixa de convite não deve ser marcável nesse caso); 422 `SEM_TREINAMENTO_DE_ONBOARDING` e `FORMULARIO_*` (só a partir das fatias 5 e 6).

---

## Tela 09: Ficha, aba Documentos (RH)

PNG: `Figma\telas\09-rh-colaboradores-ficha-aba-documentos.png`. A moldura (cabeçalho, abas) é a da tela 03; "Documentos" fica ativa.

**Li no print**
- Texto "Documentos guardados na ficha. Só o RH e o próprio colaborador conseguem ver." e o botão **"+ Adicionar documento"**.
- Tabela: **Documento**, **Enviado em**, **Situação** e uma coluna de ação. Seis linhas: "Documento de identidade (RG ou CNH)", "CPF", "Carteira de trabalho digital", "Contrato assinado" e "Exame admissional (ASO)", todas com `14/03/2022`, etiqueta **"Enviado"** e botão **"Ver"**; e "Comprovante de residência" com "Atualização pedida em 01/10", etiqueta **"Pendente"** (âmbar) e botão **"Lembrar"**.

**O que fica**
- O texto de abertura, o botão "Adicionar documento", as colunas, as etiquetas "Enviado" e "Pendente" e o botão "Ver".

**O que sai ou muda (e por quê)**
- **Sai o botão "Lembrar"** da linha pendente (decisão do dono, 2026-10-30; contrato `listarDocumentosDoFuncionario`: não existe lembrete de documento, nem rota nem e-mail, `CONVENCOES.md` seção 12). A linha pendente fica **sem botão** na coluna de ação.
- **Rótulos do tipo** (`tipo`; contrato `DocumentoColaboradorTipo`): `IDENTIDADE` "Documento de identidade (RG ou CNH)", `CPF` "CPF", `CARTEIRA_DE_TRABALHO` "Carteira de trabalho digital", `COMPROVANTE_DE_RESIDENCIA` "Comprovante de residência", `CONTRATO_ASSINADO` "Contrato assinado", `EXAME_ADMISSIONAL` "Exame admissional (ASO)", `OUTRO` "Outro". **Provisório** (`x-provisorio` em `DocumentoColaboradorTipo`): a lista de tipos ainda não foi decidida pelo dono (o banco guarda texto livre).
- **Coluna "Enviado em":** de `enviadoEmUtc` (`dd/mm/aaaa`, em São Paulo). Quando `situacao = PENDENTE`, o texto "Atualização pedida em {dd/mm}" vem de `pedidoEmUtc`.
- **`descricao` e `dataValidade`** existem no contrato e o PNG não os mostra: escreva, em corpo pequeno e cinza **embaixo do nome do tipo**, a descrição (quando houver) e "Válido até {dd/mm/aaaa}" (quando houver validade). Suponho (a validade é um campo do modal 10; seria estranho nunca aparecer).
- **Ordem:** mais recentes primeiro (a que a API manda). A ordem do PNG é de exemplo. **Sem paginação** (lista curta; só `itens`).
- **"Ver"** só em `ENVIADO`: chama `baixarDocumentoDoFuncionario` e abre o `url` do `LinkTemporario` pelo auxiliar de download da fundação (o link vale poucos minutos e baixa como anexo; nunca monte o caminho do arquivo). O download entra na auditoria. 422 `DOCUMENTO_SEM_ARQUIVO` ("Este documento ainda não foi enviado."): não acontece com o botão só em `ENVIADO`, mas mostre o `detail` se vier.
- **O gestor não vê esta aba** (não há rota para ele; a ficha da equipe, tela 52, nem tem a aba Documentos).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Lista | `listarDocumentosDoFuncionario` |
| "Ver" | `baixarDocumentoDoFuncionario` |
| "Adicionar documento" abre o modal da tela 10 | `adicionarDocumentoDoFuncionario` |

**Estados**
- Carregando: esqueleto da tabela. Vazio: "Nenhum documento na ficha ainda." (texto meu). Erro: `detail` e "Tentar de novo". 404: a ficha não existe (feche o painel). Sem permissão: 403.

**Perguntas abertas (`x-provisorio`)**: `DocumentoColaboradorTipo` (lista de tipos).

---

## Tela 10: Modal "Adicionar documento" (RH)

PNG: `Figma\telas\10-rh-colaboradores-ficha-modal-adicionar-documento.png`.

**Li no print**
- Janela de uns 640 px, título "Adicionar documento de Bruno" e botão fechar. "Como o documento vai chegar": dois cartões de escolha única, **"Anexar agora"** ("Você já tem o arquivo", escolhido) e **"Pedir para a pessoa enviar"** ("Ela recebe um aviso e envia pelo portal").
- "Tipo de documento" (select, "Escolha o tipo"), "Validade (opcional)" (data), "Descrição (opcional)" (dica "Ex.: comprovante de residência atualizado").
- Área tracejada: "Arraste o arquivo aqui", "PDF, JPG ou PNG, até 10 MB" e o botão "Escolher arquivo". Nota "Só o RH e Bruno conseguem ver os documentos da ficha."
- Rodapé: "Cancelar" e "Salvar documento".

**O que fica**
- Tudo acima. O nome na janela e na nota é o **primeiro nome** da pessoa (suponho).

**O que sai ou muda (e por quê)**
- **O segundo cartão não pode prometer aviso:** "Ela recebe um aviso e envia pelo portal" vira **"Ela envia pelo portal"** (decisão do dono, 2026-10-30; contrato `adicionarDocumentoDoFuncionario`: nenhum e-mail sai, `CONVENCOES.md` seção 12). A pessoa fica sabendo pela **bolinha** do item "Meus documentos" no menu (`documentosPendentes` de `obterContadoresDoMenuDoFuncionario`). Pode-se acrescentar, em corpo pequeno, "Ela vê o pedido em Meus documentos." (texto meu).
- **Os dois modos** (`modo`): `ANEXAR_AGORA` (padrão): o `arquivo` é **obrigatório** e a área de envio aparece; o documento nasce `ENVIADO`. `PEDIR_PARA_ENVIAR`: **não** manda arquivo, a **área de envio some** e o documento nasce `PENDENTE`.
- **Campos:** `tipo` (obrigatório; opções da tela 09, **provisório**), `dataValidade` (opcional), `descricao` (opcional, até 255; com `tipo = OUTRO` mostre a dica "Diga qual é o documento", porque a descrição é o que diz o que é; suponho).
- **Arquivo:** PDF, JPG ou PNG, **até 10 MB**. A tela confere extensão e tamanho **por conforto** (10 × 1024 × 1024 bytes) e mostra "Escolha um PDF, JPG ou PNG de até 10 MB." (texto meu); quem decide é a API (confere pelo conteúdo, não pela extensão). Erros de arquivo (tipo, tamanho, faltando ou sobrando) voltam como 400 em `arquivo`: mostre junto da área de envio. Cada chamada **cria um documento novo** (não substitui um pendente do mesmo tipo).
- O envio é `multipart/form-data` (campos `modo`, `tipo`, `descricao`, `dataValidade`, `arquivo`). O teste dessa tela começa com `// @vitest-environment node` (jsdom não monta multipart; `02-pilha.md` e `03-contrato-e-mocks.md`).
- **Sucesso (201, o documento):** feche o modal e recarregue a lista da tela 09 (a linha nova aparece como "Enviado" ou "Pendente").

**Rotas:** `adicionarDocumentoDoFuncionario` (`POST /api/rh/funcionarios/{funcionarioId}/documentos`).

**Estados**
- Enviando: botão desabilitado e "Enviando…" na área de envio. Erro de rede: o texto da fatia 0. 404 (ficha sumiu): feche e avise. 403.

**Perguntas abertas (`x-provisorio`)**: `DocumentoColaboradorTipo` (lista de tipos).

---

## Tela 29: Cadastros, moldura e cadastro "Cargos" (RH)

PNG: `Figma\telas\29-rh-cadastros.png`. Os outros cadastros (Setores, Gestores, Benefícios, Usuários do sistema e Empresa) **não têm print do conteúdo**: estão nas seções seguintes.

**Li no print**
- Título "Cadastros" e subtítulo "Tabelas de apoio que o RH mantém. Elas viram as opções que aparecem no resto do sistema."
- **Menu lateral do cadastro** (coluna à esquerda, cada item com um título, uma linha de apoio e um número; "Cargos" ativo): **Cargos** ("Nome e carga horária", 14), **Setores** ("Áreas e seus gestores", 6), **Gestores** ("Quem gerencia cada setor", 6), **Níveis de acesso** ("O que cada perfil vê", 3), **Benefícios** ("O que a empresa oferece", 6), **Usuários do sistema** ("Logins de acesso ao portal", 103).
- À direita, "Cargos": "Cada colaborador tem um cargo. A carga horária do cargo é usada para calcular o saldo do ponto e o banco de horas.", botão "+ Novo cargo", busca "Buscar neste cadastro", tabela **Cargo**, **Descrição**, **Carga semanal** (`44 h`), **Pessoas** e o botão "Editar" em cada linha; rodapé "Mostrando 7 de 14".

**O que fica**
- A moldura (título, subtítulo, menu lateral com números), o cadastro Cargos como está, e os cadastros Setores, Gestores, Benefícios e Usuários do sistema.

**O que sai ou muda (e por quê)**
- **Sai "Níveis de acesso"** (decisão do dono, 2026-10-30: três perfis fixos, nada para editar).
- **Entra "Empresa"** (item novo do menu lateral, só RH; contrato `salvarEmpresa`: "decidido (dono, 2026-10-30): fica em Cadastros, Empresa"). A fatia 7 acrescenta "Pagamento" (`telas\fatia-7.md`, tela 7.4). Ordem proposta (suponho): Cargos, Setores, Gestores, Benefícios, Usuários do sistema, Empresa, e depois Pagamento. Linhas de apoio dos itens novos (texto meu): Empresa "Nome, horário e dia de pagamento".
- **Os números do menu** são o `total` de cada lista (`listarCargos`, `listarSetores`, `listarBeneficiosDoCatalogo`, `listarUsuariosDoSistema`) e a quantidade de `itens` de `listarGestores`. Para os cadastros que não estão abertos, peça `tamanhoPagina = 1` e leia o `total`. No cenário de hoje: **Cargos 14, Setores 7** (o PNG diz 6; com Compras), **Gestores 5** (o PNG diz 6; três gestores de setor e duas pessoas do RH), **Benefícios 7** (o PNG diz 6) e **Usuários do sistema 104** (o PNG diz 103). Empresa não tem número.
- **Cargos:** colunas Cargo, Descrição, Carga semanal (`{cargaSemanalHoras} h`), Pessoas (`quantidadePessoas`: pessoas **ativas** com o cargo) e "Editar". **Não existe excluir cargo** (contrato; o Figma só tem "Editar"). A busca procura no nome e na descrição. Ordem: nome. O rodapé diz "Mostrando {n} de {total}" e, se `totalPaginas` for maior que 1, ganha "Anterior" e "Próxima" como na tela 01 (suponho; o PNG mostra 7 de 14 sem botões, com um tamanho de página menor que o do contrato).
- **O mesmo padrão** (título, texto de apoio, botão "Novo ...", busca "Buscar neste cadastro", tabela com "Editar", rodapé "Mostrando N de M") vale para Setores, Benefícios e Usuários do sistema.

**Rotas (Cargos)**

| O que a tela faz | `operationId` |
|---|---|
| Lista, busca (`busca`), páginas e o número do menu | `listarCargos` |
| "Novo cargo" e "Editar" abrem o modal da tela 30 | `criarCargo`, `editarCargo` |

**Estados:** carregando (esqueleto); vazio "Nenhum cargo cadastrado." ou, com busca, "Nenhum cargo encontrado." (texto meu); erro com `detail` e "Tentar de novo"; 403 acesso negado.

**Observações para quem monta**
- O mesmo modal de cargo serve a "Novo" e a "Editar" (ver a tela 30). As opções de "Cargo" do cadastro de colaborador usam `listarCargos` com `tamanhoPagina = 100`: invalide a lista quando um cargo é criado ou alterado.

---

## Tela 30: Modal "Novo cargo" (RH)

PNG: `Figma\telas\30-rh-cadastros-modal-novo-cargo.png`.

**Li no print**
- Janela de uns 640 px com o título "Novo cargo" **quebrado em duas linhas**, botão fechar. Campos: "Nome do cargo" (dica "Ex.: analista de compras"), "Carga horária semanal" (dica `44`, com a palavra "horas" ao lado) e "Descrição" (dica "O que a pessoa faz no dia a dia"). Nota "A carga horária vale para todas as pessoas do cargo." Botões **empilhados**: "Cancelar" sobre "Salvar cargo".

**O que fica**
- Os três campos, a nota e os dois botões.

**O que sai ou muda (e por quê)**
- O título em duas linhas e os botões empilhados parecem defeito de largura do desenho: use o título em **uma linha** ("Novo cargo") e os botões **lado a lado** (suponho; é o que as notas da fatia 5 fazem com os assistentes).
- **Regras (`CargoComando`):** `nome` obrigatório, 1 a 100, **único sem diferenciar maiúsculas**; `descricao` opcional, até 255; `cargaSemanalHoras` obrigatório, **maior que zero e até 168**, com até 2 casas decimais (a carga é **semanal**, ex.: 44).
- **"Editar"** abre o **mesmo modal** com o título "Editar cargo" e os campos preenchidos (`editarCargo` substitui nome, descrição e carga). Ao editar, mostre a nota "A nova carga vale para os dias de ponto criados depois; os que já foram gravados não mudam." (texto meu, regra do contrato).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| "Salvar cargo" (novo) | `criarCargo` (`POST /api/rh/cargos`) |
| "Salvar cargo" (edição) | `editarCargo` (`PUT /api/rh/cargos/{cargoId}`) |

**Estados e erros:** enviando (botão desabilitado). **409 `NOME_JA_CADASTRADO`** ("Já existe um cadastro com este nome."): junto do campo Nome. 400 por campo (`errors.nome`, `errors.cargaSemanalHoras`). 404 na edição (o cargo sumiu): feche e recarregue. Sucesso (201 ou 200 devolve o `CargoItem`): feche o modal e recarregue a lista e o número do menu.

---

## Tela 29: Cadastros, "Setores" (RH, sem print do conteúdo)

**Descrição em texto** (contrato `listarSetores`, `criarSetor`, `editarSetor`; desenho meu, no padrão do cadastro Cargos). Título "Setores", apoio "Áreas e seus gestores" (li no menu do PNG 29).

- **Tabela:** **Setor** (`nome`), **Descrição**, **Gestor** (`gestor.nome`; `null`: "Sem gestor"), **Pessoas** (`quantidadePessoas`: pessoas ativas do setor com a admissão já começada, **inclusive o gestor**; quem está em admissão não conta) e "Editar". Lista curta, **todos os setores numa página**; ordem: nome; busca no nome e na descrição. Sem excluir (contrato: "Não existe exclusão de setor").
- **Botão "+ Novo setor"** e **modal "Novo setor" / "Editar setor"** (o mesmo): **Nome** (obrigatório, 1 a 100, único), **Descrição** (opcional, até 255) e **Gestor** (`gestorFuncionarioId`; `null` = "Sem gestor").
  - O campo Gestor é um seletor com **busca** das pessoas ativas (`listarFuncionarios` com `situacao = ATIVO` e `busca`), mais a opção "Sem gestor". Mande o **`id` do colaborador** (não o `gestorId`).
  - Regras (contrato): a pessoa precisa estar **ativa**; **só quem tem o nível `Gestor` gerencia um setor**: quem tem `Funcionario` é **promovido a Gestor** pela API, e quem é do `RH` é recusado; se ela ainda não está em Gestores, a API a cadastra; um gestor responde por **um setor só**.
  - **Texto de apoio no modal** (texto meu, regras do contrato): "A pessoa escolhida passa a ter o nível Gestor. Quem deixa de ser gestor deste setor volta a Colaborador e continua no setor." Ao **trocar ou tirar** o gestor de um setor que já tinha: "Os pedidos de ponto e de férias que esperavam pelo gestor atual passam para o RH."
  - A lista de candidatos não informa o nível de acesso da pessoa (`FuncionarioListaItemRh` não tem `perfil`): **não tente filtrar** quem é do RH; a API recusa com 422 e o `detail` explica.
- **Erros:** **409 `NOME_JA_CADASTRADO`**; **409 `GESTOR_JA_RESPONDE_POR_OUTRO_SETOR`** ("Carlos Ribeiro já é o gestor do setor Comercial. Tire-o de lá antes de escolhê-lo aqui."); **422 `GESTOR_PRECISA_TER_PERFIL_GESTOR`** ("Ana Souza tem o nível RH. Só quem tem o nível Gestor pode gerenciar um setor."); **422 `FUNCIONARIO_INATIVO`**. Mostre o `detail` junto do campo Gestor. Sucesso: feche o modal, recarregue a lista de setores e o número do menu; a lista de **gestores** (cadastro Gestores) e as opções de Setor do cadastro de colaborador também mudam (invalide).
- **Estados:** carregando, vazio ("Nenhum setor cadastrado."), erro, 403.

**Rotas:** `listarSetores` (lista; também filtro da tela 01 e opções de Setor das telas 02, 03 e Readmitir), `criarSetor` (`POST /api/rh/setores`), `editarSetor` (`PUT /api/rh/setores/{setorId}`); a busca de pessoas para o gestor usa `listarFuncionarios`.

---

## Tela 29: Cadastros, "Gestores" e o interruptor "Gestor ausente" (RH, sem print do conteúdo)

**Descrição em texto** (contrato `listarGestores`, `cadastrarGestor`, `configurarAusenciaDoGestor`; decisão do dono, 2026-10-29, sem lugar nas telas do Figma, e a de 2026-10-30 que muda o nome para "Gestor ausente"; desenho meu). Título "Gestores", apoio "Quem gerencia cada setor" (li no menu).

- **Tabela** (sem paginação; ordem do `gestorId`): **Gestor** (`funcionario.nome` e, embaixo, `funcionario.cargo`), **Setor** (`setor.nome`; quando `soDecidePedidosDeGestores` é `true`, o texto **"Só decide pedidos de gestores"**, no lugar do setor), **Equipe** (`quantidadeEquipe`: "8 pessoas"; `0` para quem não tem setor) e **Gestor ausente** (o interruptor, abaixo). O número do menu é a quantidade de `itens` (hoje **5**: Carlos, Marina, Paulo e, só para decidir pedidos de gestores, Ana Souza e Sofia Ramos; o PNG 29 diz 6).
- **Dois tipos de linha** (contrato): `soDecidePedidosDeGestores = false` é quem **gerencia um setor** (nível Gestor); `true` é a pessoa do **RH** cadastrada **só para decidir os pedidos de gestores** (o trigger do banco exige uma linha em Gestores em quem decide). Essa pessoa não tem setor nem equipe e não usa as telas do gestor.
- **O interruptor "Gestor ausente"** (decisão do dono: marcar quando o gestor está ausente **por qualquer motivo, a qualquer momento**; vale para pedidos de **ponto e de férias** da equipe dele; antes se chamava "chave de aprovação automática", e o design, seção 7, ainda usa o nome antigo: **vale "Gestor ausente"**, `CONVENCOES.md` seção 9 e roadmap, atualização de 2026-10-30):
  - Cada linha de gestor de setor tem um interruptor rotulado **"Gestor ausente"**. Ele **reflete `ausenteHoje`** (a marcação vale **hoje**), não `ausente` (suponho: o contrato só diz que a tela mostra "Ausente" **só com `ausenteHoje = true`**): passada a data de volta, `ausente` continua `true` no cadastro, mas `ausenteHoje` vira `false` e o interruptor aparece **desligado**. Ligá-lo de novo é um `PUT` com `ausente = true` e uma data nova.
  - Ao lado do interruptor ligado, mostre a data de volta: "Volta em {dd/mm/aaaa}" (`ausenteAte`; a marcação vale **até o dia anterior** a ela) ou "Sem data de volta". Um link "Mudar a data de volta" reabre o diálogo preenchido (suponho; o `PUT` é o mesmo).
  - **Ligar** abre o diálogo **"Marcar {nome} como ausente"** com o campo **"Data de volta (opcional)"** (data **posterior a hoje**; a API responde 400 em `ausenteAte` se não for) e este texto (texto meu, conteúdo do contrato): "Enquanto estiver marcado, a etapa do gestor nos pedidos de ponto e de férias da equipe é aprovada sozinha e o RH ainda decide depois. Os pedidos que já esperam por ele também passam para o RH, menos pedidos de férias com sugestão de outra data em andamento." Botões "Cancelar" e "Marcar como ausente". Envia `{ ausente: true, ausenteAte: <data ou null> }`.
  - **Desligar** envia na hora `{ ausente: false, ausenteAte: null }`, sem diálogo (suponho: ao desmarcar, "só os pedidos novos voltam ao normal"). O contrato exige `ausenteAte` `null` com `ausente = false`.
  - **Resposta 200** (`GestorAusenciaResposta`, com `pedidosAprovadosAgora: { ajustesDePonto, ferias }`): atualize a linha e, se algum dos dois números for maior que zero, avise "{n} pedidos de ajuste de ponto e {m} pedidos de férias passaram para a etapa do RH." (texto meu). Com os dois em zero (o caso de Paulo Vieira no exemplo), nada a avisar além de "Marcação salva.".
  - **Linhas que não têm o interruptor:** quem só decide pedidos de gestores (`soDecidePedidosDeGestores = true`) **não tem etapa do gestor para aprovar sozinha**: o interruptor fica **desabilitado**, com a dica "Não tem equipe: não há etapa do gestor para aprovar sozinha." (o contrato dá 422 `GESTOR_SEM_EQUIPE`; o front evita o erro).
  - Enquanto a chamada está em andamento, o interruptor fica desabilitado. Erros: 400 `ausenteAte`, 404 (o gestor sumiu: recarregue), 422 `GESTOR_SEM_EQUIPE`.
- **Botão "+ Cadastrar gestor"** (`cadastrarGestor`): modal com **uma** escolha de pessoa (seletor com busca, como no cadastro de setor) e o texto (texto meu, regra do contrato): "Só quem é do RH é cadastrado aqui, para decidir os pedidos de gestores. Para pôr alguém como gestor de um setor, escolha a pessoa em Cadastros, Setores." Envia `{ funcionarioId }`. Erros: **409 `GESTOR_JA_CADASTRADO`**; **422 `GESTOR_SEM_SETOR_SO_PARA_RH`** (a pessoa não é do RH: "Só quem é do RH é cadastrado como gestor sem setor. Para gerenciar um setor, escolha a pessoa no cadastro de Setores."); **422 `FUNCIONARIO_INATIVO`**. Sucesso (201): a linha nova entra com `soDecidePedidosDeGestores = true`.
- **Não existe** "Níveis de acesso" nem excluir gestor. Quem deixou de gerenciar um setor sai da lista sozinho (contrato).
- **Estados:** carregando, vazio (não acontece com o seed, que traz gestores; "Nenhum gestor cadastrado." se acontecer; texto meu), erro, 403.

**Rotas:** `listarGestores`, `cadastrarGestor` (`POST /api/rh/gestores`), `configurarAusenciaDoGestor` (`PUT /api/rh/gestores/{gestorId}/ausencia`; o `gestorId` do caminho é o da linha em Gestores, **não** o `funcionarioId`).

---

## Tela 29: Cadastros, "Benefícios" e as três marcas do catálogo (RH, sem print do conteúdo)

**Descrição em texto** (contrato `listarBeneficiosDoCatalogo`, `criarBeneficioDoCatalogo`, `editarBeneficioDoCatalogo`, `excluirBeneficioDoCatalogo`; decisão do dono, 2026-10-30, sobre as marcas; desenho meu). Título "Benefícios", apoio "O que a empresa oferece" (li no menu).

- **Tabela:** **Benefício** (`nome` e, embaixo, `descricao`), **Quem pode pedir** (`opcional`: "A própria pessoa pode pedir" ou "Só o RH concede"; texto meu), **Dependentes** (`aceitaDependentes`: "Sim"/"Não"), **Trajeto** (`aceitaTrajeto`: "Sim"/"Não"), **Pessoas** (`quantidadePessoas`: pessoas com o benefício **ativo**) e os botões "Editar" e "Excluir". Busca no nome e na descrição; ordem do `id` (a ordem de cadastro). No cenário de hoje o catálogo tem **7** benefícios (1 Vale-refeição, 2 Plano de saúde, 3 Plano odontológico, 4 Vale-transporte, 5 Seguro de vida, 6 Auxílio-creche, 7 Vale-cultura); só o **3** e o **6** são `opcional`.
- **Modal "Novo benefício" / "Editar benefício"** (o mesmo): **Nome** (obrigatório, 1 a 100, único), **Descrição** (opcional, até 255) e as **três marcas**, três caixas de marcar com uma linha de apoio cada (rótulos meus, conteúdo do contrato):
  - **"Opcional"**: "A própria pessoa pode pedir este benefício (ele aparece em 'Disponíveis para você'). Desmarcado, só o RH o concede." (`opcional`)
  - **"Aceita dependentes"**: "O pedido deixa incluir dependentes, como no plano de saúde." (`aceitaDependentes`)
  - **"Pede o trajeto"**: "O pedido pede o trajeto, como no vale-transporte." (`aceitaTrajeto`)
  As três são obrigatórias no corpo (`BeneficioCatalogoComando`): envie sempre as três, `false` quando desmarcadas.
- **"Excluir"** (`excluirBeneficioDoCatalogo`): diálogo "Excluir o benefício {nome}?" com "Cancelar" e "Excluir", e o texto "Só é possível se ele nunca foi dado a ninguém." O botão aparece **sempre**: `quantidadePessoas` conta só os ativos, e a regra da API é "nenhum vínculo, nem como pedido", por isso **não** desabilite pelo número. 204: a linha sai. **409 `CADASTRO_EM_USO`** ("Este benefício já foi dado a colaboradores e não pode ser excluído."): mostre o `detail` no diálogo. O evento "Excluiu: Benefício ..." entra no log (tela 31, li "Benefício Vale-cultura" no PNG).
- **Erros:** **409 `NOME_JA_CADASTRADO`**; 400 por campo; 404. Sucesso: recarregue a lista e o número do menu; as caixas "Benefícios" do cadastro de colaborador usam esta lista (invalide).
- **Estados:** carregando, vazio ("Nenhum benefício cadastrado."; texto meu), erro, 403.

**Rotas:** `listarBeneficiosDoCatalogo`, `criarBeneficioDoCatalogo` (`POST /api/rh/beneficios`), `editarBeneficioDoCatalogo` (`PUT /api/rh/beneficios/{beneficioId}`), `excluirBeneficioDoCatalogo` (`DELETE /api/rh/beneficios/{beneficioId}`).

**Observação:** as marcas só têm efeito na tela "Benefícios" do funcionário (fatia 7, tela 44). Aqui o front só as grava e as mostra.

---

## Tela 29: Cadastros, "Usuários do sistema" (RH, sem print do conteúdo)

**Descrição em texto** (contrato `listarUsuariosDoSistema`, `enviarConviteDeAcesso`, `ativarUsuarioDoSistema`, `desativarUsuarioDoSistema`; decisão do dono, 2026-10-30: **mínimo, sem apagar**; desenho meu). Título "Usuários do sistema", apoio "Logins de acesso ao portal" (li no menu).

- **Quem aparece:** cada linha é um **colaborador ativo ou em admissão**, inclusive quem ainda não recebeu convite. **Inativos não aparecem** (perdem o login sozinhos). Quem foi **readmitido** volta a aparecer, com o login ainda desativado até o "Ativar". O número do menu é o `total` (hoje **104**).
- **Tabela:** **Pessoa** (`nome` e, embaixo, `emailCorporativo`), **Nível de acesso** (`perfil`, com "Colaborador" para `Funcionario`; `null`: "—"), **Situação** (etiqueta: "Sem convite", "Convite enviado", "Convite vencido", "Senha definida"), **Acesso** ("Liberado" quando `ativo` é `true`, "Desativado" quando `false`), **Último acesso** (`ultimoLoginEmUtc`, `dd/mm/aaaa hh:mm` em São Paulo; `null`: "Nunca entrou"), **Convite enviado em** (`conviteEnviadoEmUtc`; `null`: "—") e os botões da linha. Busca "Buscar neste cadastro" no nome e no e-mail corporativo; ordem: nome. Filtros opcionais (o contrato os tem; o desenho é meu): um seletor "Todas as situações" (`situacao`) e um "Todos os acessos" (`ativo`: Liberados, Desativados).
- **Botões da linha** (um só conjunto por linha, conforme o estado; os rótulos "Ativar" e "Desativar" são do dono):
  - **"Reenviar convite"** (`enviarConviteDeAcesso`): quando `situacao` é diferente de `SENHA_DEFINIDA`; com `SEM_CONVITE` o rótulo é "Enviar convite". Sem `emailCorporativo`: desabilitado, com a dica "Esta pessoa não tem e-mail corporativo cadastrado." Cria o login se não existe, **invalida o convite anterior** e coloca o e-mail na fila (o link vale **7 dias**). Sucesso (200 devolve a linha): atualize-a e diga "Convite enviado para {emailCorporativo}." (texto meu). 422 `FUNCIONARIO_INATIVO`, `FUNCIONARIO_SEM_EMAIL_CORPORATIVO`, `SENHA_JA_DEFINIDA`: o `detail`.
  - **"Ativar"** (`ativarUsuarioDoSistema`): quando `ativo` é `false` **e** `situacao` é diferente de `SEM_CONVITE` (sem login não há o que liberar; o caminho é "Enviar convite"; o contrato dá 422 `SEM_USUARIO`). Sem diálogo. O login passa a liberado. 422 `FUNCIONARIO_INATIVO`: o `detail`.
  - **"Desativar"** (`desativarUsuarioDoSistema`): quando `ativo` é `true`. **Diálogo de confirmação** (texto meu, conteúdo do contrato): "Desativar o acesso de {nome}? O login fica bloqueado e as sessões abertas dela são encerradas. Nada é apagado: 'Ativar' desfaz." Botões "Cancelar" e "Desativar". Na linha **da própria pessoa logada** (`funcionarioId` igual ao do usuário), o botão fica **desabilitado** com a dica "Você não pode desativar o seu próprio login." (o contrato dá 422 `NAO_PODE_DESATIVAR_A_SI_MESMO`).
- Cada ação devolve a linha atualizada (`UsuarioSistemaItem`): troque-a na tabela sem recarregar tudo.
- **O que a pessoa vê ao tentar entrar** com o login desativado: "Seu acesso está desativado. Fale com o RH." (tela de entrar, fatia 0).
- **Estados:** carregando, vazio ("Nenhum usuário encontrado."; texto meu), erro, 403.

**Rotas:** `listarUsuariosDoSistema`, `enviarConviteDeAcesso` (`POST /api/rh/usuarios/{funcionarioId}/convite`), `ativarUsuarioDoSistema` (`.../ativar`), `desativarUsuarioDoSistema` (`.../desativar`); o `funcionarioId` do caminho é o do colaborador.

---

## Tela sem print: Cadastros, "Empresa" (RH)

**Descrição em texto** (contrato `obterEmpresa` e `salvarEmpresa`; decisão do dono, 2026-10-30: "fica em Cadastros, Empresa, um item novo do menu lateral, só para o RH, com um formulário simples"; desenho meu). Título "Empresa", apoio (texto meu) "Nome, horário e dia de pagamento".

- **Formulário** (uma coluna, sem tabela; a empresa tem **uma única configuração**: o primeiro `PUT` a cria, os seguintes a substituem):
  - **Nome da empresa** (`nomeEmpresa`, obrigatório, até 150); é o nome que aparece no menu de todas as telas.
  - **UF** (`uf`, select com as 27 siglas de `EmpresaUf`; o contrato só traz as siglas) e **Município** (`municipio`, obrigatório, até 100). Apoio: "A UF escolhe os feriados estaduais e o município, os municipais. Valem para todos."
  - **Horário padrão de entrada** e **de saída** (`horarioEntrada`, `horarioSaida`, `HH:mm`, sem fuso). Apoio: "É o horário de todos. A saída pode ser menor que a entrada (turno que atravessa a meia-noite)."
  - **Tolerância de atraso, em minutos** (`toleranciaAtrasoMinutos`, inteiro de 0 a 120). Apoio: "O atraso é medido contra a entrada mais a tolerância."
  - **Dia do pagamento** (`diaPagamento`, inteiro de 1 a 31). Apoio: "Mês sem esse dia: vale o último dia do mês. Fim de semana ou feriado: antecipa para o dia útil anterior." (as exceções por mês são a tela 7.4 da fatia 7, que mostra esta regra e aponta para cá).
  - Botão **"Salvar"**.
- **Primeira vez:** `obterEmpresa` responde **404 `EMPRESA_NAO_CONFIGURADA`** ("A empresa ainda não foi configurada."): **não é erro**; mostre o formulário vazio com a faixa "A empresa ainda não foi configurada." (texto meu). A demonstração já vem configurada pelo seed (**Comercial Aurora Ltda**, SP, São Paulo, 08:00 a 17:48, tolerância 10, pagamento no dia 6; `ELENCO.md`).
- **Sucesso (200 devolve a configuração):** "Configuração salva." (texto meu) e **atualize o nome da empresa no menu lateral** (invalide a consulta de `obterEmpresa`, que o shell também usa). Entra na auditoria.
- **Erros:** 400 por campo (`errors.horarioEntrada`, `errors.toleranciaAtrasoMinutos`, `errors.diaPagamento`...). 403.
- **Estados:** carregando (campos desabilitados), erro de leitura diferente de 404 (`detail` e "Tentar de novo").

**Rotas:** `obterEmpresa` (`GET /api/eu/empresa`, qualquer perfil; é a mesma que o shell lê), `salvarEmpresa` (`PUT /api/rh/empresa`, só RH).

---

## Tela 31: Log de auditoria (RH)

PNG: `Figma\telas\31-rh-log-de-auditoria.png`.

**Li no print**
- Título "Log de auditoria"; subtítulo "Tudo o que foi criado, alterado ou excluído no portal: quem fez, quando e o que mudou. **Ninguém consegue editar nem apagar estes registros.**" Botão "Exportar CSV".
- Filtros: "Últimos 7 dias", busca "Buscar pessoa ou registro", "Todas as áreas" e "Todas as ações".
- Tabela: **Quando** ("Hoje, 09:38", "Ontem, 18:40", "05/10, 14:10"), **Quem** (nome e, embaixo, "RH", "Colaborador", "Gestor", "Automático" ou "Desligado"), **Área**, **Ação** (etiqueta cinza; **vermelha** em "Excluiu" e "Acesso negado") e **O que aconteceu**, com o botão "Detalhes" por linha.
- **Primeira linha expandida** (botão "Fechar"): "Registro afetado" ("Colaborador Bruno Lima, matrícula 00482"), "Data e hora exatas" (`07/10/2026 09:38:12`), "Origem" ("Portal no navegador, IP 10.0.4.23") e a tabela **"O que mudou"** com **Campo**, **Antes** (em vermelho) e **Depois** (em verde): "Cargo", "Analista", "Analista pleno".
- Rodapé: "Mostrando 12 de 1.284 registros dos últimos 7 dias", "Anterior" e "Próxima".

**O que fica**
- Tudo acima: filtros, as cinco colunas, a linha expansível, o rodapé e o "Exportar CSV".

**O que sai ou muda (e por quê)**
- **A frase do subtítulo muda** (decisão do dono, 2026-10-30; design, seção 7, "Tela 31"): em vez de "Ninguém consegue editar nem apagar estes registros", escreva **"Nenhuma tela do sistema edita ou apaga estes registros."** (a API não tem rota para editar nem apagar evento; a trava no banco é decisão da fatia 1a e não está garantida). O subtítulo fica: "Tudo o que foi criado, alterado ou excluído no portal: quem fez, quando e o que mudou. Nenhuma tela do sistema edita ou apaga estes registros."
- **Período** (li só o botão "Últimos 7 dias", fechado; o contrato tem `de` e `ate`, datas de calendário **inclusive**): proponho as opções "Hoje", "Últimos 7 dias" (padrão; **não envie** `de` nem `ate`, a API já assume "6 dias antes de `ate`" até hoje), "Últimos 30 dias" e "Período personalizado" (dois campos de data "De" e "Até"; `ate` não pode ser anterior a `de`) (desenho meu). As opções com datas enviam `de` e `ate` calculados no fuso de São Paulo.
- **Busca** ("Buscar pessoa ou registro"): `busca` procura no **nome de quem fez** e na **descrição** do registro.
- **Áreas** (`area`, "Todas as áreas" = sem o parâmetro) e **ações** (`acao`, "Todas as ações" = sem o parâmetro). Rótulos (contrato): áreas `ACESSO` "Acesso", `COLABORADORES` "Colaboradores", `DOCUMENTOS` "Documentos", `CADASTROS` "Cadastros", `IMPORTACAO` "Importação", `PONTO` "Ponto", `FERIADOS` "Feriados", `FERIAS` "Férias", `HOLERITES` "Holerites", `ADVERTENCIAS` "Advertências", `AVISOS` "Avisos", `FORMULARIOS` "Formulários", `TREINAMENTOS` "Treinamentos", `BENEFICIOS` "Benefícios", `ATESTADOS` "Atestados"; ações `ENTROU` "Entrou", `ACESSO_NEGADO` "Acesso negado", `CRIOU` "Criou", `ALTEROU` "Alterou", `EXCLUIU` "Excluiu", `INATIVOU` "Inativou", `REGISTROU` "Registrou", `PEDIU_AJUSTE` "Pediu ajuste", `APROVOU` "Aprovou", `RECUSOU` "Recusou", `PUBLICOU` "Publicou", `ENVIOU_EMAIL` "Enviou e-mail", `IMPORTOU` "Importou", `BAIXOU` "Baixou". **Vermelhas:** `EXCLUIU`, `ACESSO_NEGADO` e `INATIVOU` (contrato).
- **Coluna "Quem":** o nome (`ator.nome`) e, embaixo, **uma** destas, nesta precedência (contrato): `tipo = SISTEMA` mostra "Automático" (o nome vem "Sistema"); senão `desligado = true` mostra "Desligado"; senão o perfil que a pessoa **tinha na hora do evento** (`Funcionario` como "Colaborador").
- **Coluna "Quando":** de `quandoUtc` em São Paulo: "Hoje, HH:mm", "Ontem, HH:mm" e, para dias mais antigos, "dd/mm, HH:mm" (acrescente o ano se for de outro ano; suponho).
- **Linha expandida:** "Detalhes" chama `obterEventoDeAuditoria` **só ao abrir** (carrega, mostrando esqueleto) e vira "Fechar". Mostra "Registro afetado" (`registroAfetado`), "Data e hora exatas" (`quandoUtc` com segundos, em São Paulo), "Origem" (`origem.canal = PORTAL`: "Portal no navegador, IP {ip}"; `SISTEMA`: "Automático", sem IP) e a tabela "O que mudou" com `mudancas` (Campo, Antes, Depois). Com `valorOculto = true` (coluna sensível, o valor **não** é gravado) escreva **"valor não registrado"** no lugar de Antes e Depois. Com `mudancas` vazia (login, por exemplo) escreva "Este evento não altera dados." (texto meu). Uma linha aberta por vez (suponho).
- **Rodapé:** "Mostrando {itens.length} de {total} registros {período}", com o número no formato brasileiro (`1.284`) e o período por extenso ("dos últimos 7 dias"; com período personalizado, "de {dd/mm/aaaa} a {dd/mm/aaaa}"). "Anterior" e "Próxima" mudam `pagina`; use `tamanhoPagina = 20` (o PNG mostra 12 linhas só pela altura). **Ordem fixa:** do mais recente ao mais antigo (não há ordenação).
- **Não use `apenasAlteracoes` aqui:** é do painel do RH (tela 00, fatia 7). A tela 31 mostra tudo o que o período traz.
- **Anônimo:** o log **não** traz nome nem número de quem recebeu o lembrete de um formulário anônimo, nem eventos de resposta ou participação anônimas (contrato). O front mostra a `descricao` como vem e **não** tenta acrescentar nada.
- **"Exportar CSV"** (`exportarAuditoria`): baixa o log com os **mesmos filtros** da tela (`de`, `ate`, `busca`, `area`, `acao`) e **sem** paginação nem `apenasAlteracoes`. A resposta é **o próprio arquivo** (`text/csv`, UTF-8 com BOM, separador `;`), **não** um `LinkTemporario`: baixe com uma chamada autenticada (`Authorization`) e entregue o arquivo ao navegador por `baixarExportacao(chamada, nomePadrao)` da fundação (`src\shared\arquivos\baixar.ts`; `fatia-1a-fundacao.md`, Task 13): **já existe, use-o, não o recrie**; a fatia 2 o usa no "Baixar modelo". Nome do arquivo: o do `Content-Disposition` (`auditoria-2026-10-07.csv`); se o navegador não puder ler esse cabeçalho (a API precisa expô-lo no CORS; a conferir com o dono, na integração), nomeie `auditoria-{hoje}.csv` (suponho). O botão fica desabilitado enquanto baixa. A exportação entra na própria auditoria.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Lista, filtros e paginação (`pagina`, `tamanhoPagina`, `busca`, `de`, `ate`, `area`, `acao`) | `listarEventosDeAuditoria` |
| "Detalhes" (linha expandida) | `obterEventoDeAuditoria` |
| "Exportar CSV" | `exportarAuditoria` |

**Estados**
- Carregando (esqueleto da tabela). Vazio: "Nenhum registro neste período." (texto meu). Erro: `detail` e "Tentar de novo" (400 por `de` e `ate`: junto do campo). Sem permissão: 403, acesso negado. O detalhe com 404 (o evento não existe mais): feche a linha e avise.

**Observações para quem monta**
- O contrato só deixa o RH ler o log (`x-perfis: [RH]`). Não existe botão de editar nem de apagar em lugar nenhum desta tela.
- Os textos da coluna "O que aconteceu" e do detalhe são **texto simples**: renderize como texto, nunca como HTML (roadmap, decisão 6).

---

## Tela 52: Ficha da pessoa da equipe, aba Dados (gestor)

PNG: `Figma\telas\52-gestor-painel-da-equipe-ficha.png`.

**Li no print**
- Painel lateral sobre o "Painel da equipe" (a tela 51, fatia 7: título "Equipe Comercial", "Esperando sua aprovação", "Pessoas da equipe"). Trilha "Equipe Comercial / Diego Farias" ("Equipe Comercial" é link que fecha o painel) e botão fechar.
- Cabeçalho: avatar "DF", "Diego Farias", "Vendedor, setor Comercial", etiqueta "Ativo"; à direita o botão **"Registrar advertência"** (contorno).
- Abas: **Dados** (ativa), Ponto, Férias, Treinamentos, Advertências. (Sem Holerites e sem Documentos.)
- **Quatro números** em uma linha: "Banco de horas" (`-3h45`, em laranja), "Férias disponíveis" (`22 dias`), "Treinamentos pendentes" (`2`, em laranja) e "Advertências" (`2`, em vermelho).
- **"Contato e contrato"** (cartão): E-mail corporativo (`diego.farias@empresa.com.br`), Telefone (`(11) 9****-7783`), Cargo (Vendedor), Admissão (`08/08/2023`), Contrato (CLT), Jornada (`44 h semanais`).
- Nota no pé: "Só o RH altera dados cadastrais. CPF, endereço, holerites e documentos ficam visíveis apenas para o RH."
- Menu do gestor (ver a nota da fatia 0); rodapé "Carlos Ribeiro, Perfil gestor, setor Comercial".

**O que fica**
- Trilha, cabeçalho, as abas, os quatro números, o cartão "Contato e contrato" e a nota do pé. O gestor **não edita** o cadastro: sem "Editar dados", sem "Inativar", sem "...".

**O que sai ou muda (e por quê)**
- **Esta ficha é outro schema** (`FuncionarioFichaGestor`): **não existem** CPF, RG, endereço, e-mail pessoal, matrícula, benefícios nem documentos (decisão do dono). O telefone vem em `telefoneMascarado` (`(11) 9****-7783`: DDD e os 4 últimos dígitos): mostre como veio. A decisão de 2026-10-30 (CPF e telefone completos) é **só do RH**.
- **Cabeçalho:** "{cargo}, setor {setor}" (`cargo` e `setor` são texto; `null`: omita). A etiqueta segue a regra de "Rótulos que se repetem" com `situacaoHoje` (nunca `null` aqui, porque só a equipe ativa aparece): Diego, que está `SEM_REGISTRO`, mostra "Ativo", como o PNG.
- **Quatro números** de `obterIndicadoresDaEquipe`: `bancoDeHorasMinutos` (`-225` = "−3h45"), `feriasDisponiveisDias` (22), `treinamentosPendentes` (2) e `advertenciasQuantidade` (2). **O exemplo do contrato bate com o PNG.** Cada um vem `null` até a fatia que o calcula existir (3, 7, 6 e 4): mostre "—". A fatia 6 (`telas\fatia-6.md`) diz que `treinamentosPendentes` é de `resumo.pendentes` da aba Treinamentos: o front lê **só esta rota**, nunca soma por conta própria. Cores: como na tela 03 (suposição; li as cores no PNG).
- **"Contato e contrato":** `emailCorporativo`, `telefoneMascarado`, `cargo`, `dataAdmissao` (`dd/mm/aaaa`, sem converter fuso), `tipoContrato` (rótulo das regras comuns) e `jornadaSemanalHoras` ("44 h semanais").
- **A nota do pé** continua como no PNG: ela é verdadeira (o contrato diz que a tela dessa nota é "Só o RH altera dados cadastrais").
- **Abas Ponto, Férias, Treinamentos e Advertências** e o botão **"Registrar advertência"** são das fatias 3, 7, 6 e 4: a moldura aceita a lista de abas e o botão entra quando a fatia 4 o trouxer (`telas\fatia-4.md`). Nesta fatia a ficha mostra **só a aba Dados**.
- **Como o gestor chega aqui:** pela tabela "Pessoas da equipe" do Painel da equipe (tela 51, **fatia 7**). Esta fatia **não** cria a lista da equipe: a ficha abre pelo endereço `/gestor/equipe/:funcionarioId`, e os testes (MSW) a exercitam por ele. Até a fatia 7 o gestor só a alcança por esse endereço (suponho que isso basta, porque a demonstração do gestor é feita ao fim do plano).
- **Escopo:** só a **equipe dele** (pessoas ativas, com a admissão já começada, do setor de que é gestor, **sem ele mesmo**). Fora do escopo (outro setor, o próprio gestor, quem ainda está em admissão ou já inativo): **404**.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Cabeçalho e "Contato e contrato" | `obterFichaDaEquipe` |
| Quatro números (carrega à parte, como na tela 03) | `obterIndicadoresDaEquipe` |
| Entrada (a tabela "Pessoas da equipe" da tela 51; **não** é desta fatia) | `listarEquipeDoGestor` |

**Estados:** carregando (esqueleto); 404 ("O registro não foi encontrado."): feche o painel e avise; erro com `detail` e "Tentar de novo"; 403 (o perfil não é Gestor; o menu não leva até aqui).

**Observações para quem monta**
- A ficha do gestor e a do RH **não compartilham schema**: não escreva um componente que "esconda campos" conforme o perfil; use dois componentes, que dividem só a moldura (cabeçalho, abas, trilha).

---

## Tela sem print: Esqueci minha senha (público)

**Descrição em texto** (contrato `solicitarRedefinicaoDeSenha`: "um campo de e-mail e um botão, a partir do login"; desenho meu). Página pública (`/esqueci-senha`), sem menu, no mesmo cartão estreito da tela de entrar (`telas\fatia-0.md`), **fluida de 360 px em diante**.

- **Como se chega:** link **"Esqueci minha senha"** na tela de entrar, **abaixo do botão "Entrar"** (entra na fatia 1b; a tela de entrar da fatia 0 não o tem). Se o campo E-mail do login já estiver preenchido, traga o valor para cá (suponho).
- **Conteúdo:** título "Esqueci minha senha"; texto "Digite o e-mail do seu acesso. Se ele estiver cadastrado, enviaremos um link para definir uma nova senha. O link vale 1 hora." (a primeira parte é do contrato; o resto, texto meu); campo **"E-mail"** (`type="email"`, obrigatório, até 150); botão **"Enviar link"**; link "Voltar para entrar".
- **Resposta uniforme (contrato):** a API responde **sempre 202**, exista o e-mail ou não, esteja a pessoa ativa ou não. A tela, no 202, troca o formulário por "Se o e-mail estiver cadastrado, enviaremos um link para definir uma nova senha." (a frase do contrato). **Nunca** diga se o e-mail existe, se a pessoa está ativa nem "e-mail enviado para ...".
- **Limite:** 5 pedidos por minuto por IP: **429** mostra `Muitas tentativas. Aguarde um minuto e tente de novo.` (o contrato dá o `detail`).
- **Erros:** 400 (`errors.email`: e-mail em branco ou mal formado): junto do campo. 500 e sem conexão: os textos da fatia 0.
- **Estados:** inicial (vazio); enviando (botão desabilitado, uma requisição só); confirmação (acima); erro.
- **O e-mail que a pessoa recebe** (feito pela API, não pelo front): tem o botão "Definir nova senha" apontando para `/definir-senha?token=...`; o link vale **1 hora** e é de **uso único** (um pedido novo invalida o anterior). Pessoa inativa ou login desativado não recebe e-mail.

**Rotas:** `solicitarRedefinicaoDeSenha` (`POST /api/auth/senha/esqueci`; pública, `security: []`). Sem `Authorization`.

**Observações para quem monta:** a página **não usa a sessão** do site: fica fora da área logada e funciona igual se a pessoa já tem sessão neste navegador.

---

## Tela sem print: Definir senha, para o convite e para a redefinição (público)

**Descrição em texto** (contrato `validarLinkDeSenha` e `definirSenhaPeloLink`: "a tela 'Definir senha' (não existe no Figma): nova senha, confirmação feita só no front, botão 'Salvar senha'"; desenho meu). É a **tela do convite**: o RH cadastra a pessoa (tela 02), a API coloca o e-mail de convite na fila, e a pessoa abre o link **`/definir-senha?token=...`** (fixo pelo contrato), a mesma página serve à **redefinição** ("Esqueci minha senha"). Página pública, sem menu, no cartão estreito, **fluida de 360 px em diante** (suponho: o link do e-mail costuma ser aberto no celular).

**Fluxo**
1. **Ao abrir**, leia `token` do endereço. Sem `token`: vá direto ao estado "Link inválido". Com `token`: chame `validarLinkDeSenha` (`{ token }`), **antes de mostrar o formulário**. Isso **não gasta** o link. Enquanto espera: "Conferindo o link…" (texto meu, `role="status"`).
2. **200** (`ConviteLinkInfo`: `tipo`, `nome`, `emailCorporativo`, `expiraEmUtc`): mostre o formulário. O título depende de `tipo` (contrato):
   - `CONVITE`: **"Bem-vindo, defina a sua senha"**; abaixo, "Olá, {nome}. Esta será a senha do acesso {emailCorporativo}." (texto meu).
   - `REDEFINICAO`: **"Defina uma nova senha"**; abaixo, "Acesso {emailCorporativo}." (texto meu).
3. **Formulário:** **"Nova senha"** e **"Repita a nova senha"** (`type="password"`), apoio "De 8 a 128 caracteres." e botão **"Salvar senha"**. Regras: a nova senha tem de **8 a 128 caracteres** e **nenhuma exigência** de maiúscula, número ou símbolo (decisão do dono, 2026-10-30): **não** escreva um medidor de força nem regras que o contrato não tem. A confirmação é conferida **só no front** ("As senhas não conferem."; texto meu) e **não** vai para a API. **Nada de `trim`** (espaços nas pontas contam, como na tela de entrar). O campo de confirmação vazio ou diferente bloqueia o envio sem chamar a API.
4. **Salvar** (`definirSenhaPeloLink`, `{ token, novaSenha }`): **204**. A pessoa **não entra sozinha** (contrato: "não há login automático"). Mostre a confirmação: "Senha definida. Agora você já pode entrar." (para `CONVITE`) ou "Senha alterada. Entre com a nova senha." (para `REDEFINICAO`), com o botão **"Ir para entrar"** (texto meu), que leva à tela de entrar. Na redefinição, a API encerra as sessões abertas da pessoa.

**Erros e estados**
- **422 `LINK_INVALIDO_OU_VENCIDO`** (tanto ao abrir quanto ao salvar; "Link inválido ou vencido."): a API **não diz qual dos três** casos (não existe, já foi usado ou venceu) e o front também não. Mostre o `detail` ("Este link não vale mais. Peça um novo ao RH ou use "Esqueci minha senha"."), um botão **"Esqueci minha senha"** (leva à tela acima) e "Voltar para entrar". Vale também quando a pessoa está inativa ou o login, desativado.
- **429** ("Muitas tentativas. Aguarde um minuto e tente de novo."; 5 por minuto por IP; vale para validar e para salvar). 400: `errors.novaSenha` junto do campo; `errors.token`, trate como link inválido. 500 e sem conexão: os textos da fatia 0.
- Salvando: botão desabilitado, uma requisição só. **Use o link uma vez:** depois do 204, voltar à página dá o estado "Link inválido".

**Rotas:** `validarLinkDeSenha` (`POST /api/auth/senha/validar-link`), `definirSenhaPeloLink` (`POST /api/auth/senha/definir`); as duas públicas (`security: []`).

**Observações para quem monta**
- O token **não** vai para o console, `localStorage` nem para qualquer outra chamada. O `Referrer-Policy: no-referrer` do site já impede que ele vaze por referência.
- A página **não depende da sessão**: se já há uma sessão neste navegador, ela é ignorada.
- O convite vale **7 dias**; a redefinição, **1 hora** (contrato). O front não mostra a contagem; `expiraEmUtc` pode aparecer em corpo pequeno, "O link vale até {dd/mm/aaaa hh:mm}." (texto meu, opcional).

---

## Tela sem print: Trocar senha (os três perfis)

**Descrição em texto** (contrato `trocarMinhaSenha`: "senha atual, nova senha, confirmação só no front"; desenho meu). Modal aberto pelo botão **"Trocar senha"** do rodapé do menu (ver a nota da fatia 0, pergunta aberta 1), para **Funcionário, Gestor e RH**.

- **Campos:** **"Senha atual"**, **"Nova senha"** e **"Repita a nova senha"** (todos `type="password"`); botões "Cancelar" e **"Trocar senha"**. Apoio sob a nova senha: "De 8 a 128 caracteres." Sem `trim` em nenhum dos três.
- **Regras:** a nova senha de 8 a 128 caracteres, sem outra exigência; **tem de ser diferente da atual** ("A nova senha precisa ser diferente da atual."; texto meu; o contrato diz a regra e a API também a confere, com 400 em `errors.novaSenha`); a confirmação só no front ("As senhas não conferem.").
- **Sucesso (204):** feche o modal e mostre "Senha trocada. As suas outras sessões foram encerradas." (texto meu, contrato: "as outras sessões da pessoa são encerradas e a sessão atual continua"). **A sessão atual não cai**: o front não desloga.
- **Erros:** **422 `SENHA_ATUAL_INCORRETA`** ("A senha atual está incorreta."): **junto do campo "Senha atual"**, **sem deslogar** (contrato). **429** (5 tentativas de senha atual errada por minuto, por pessoa): `Muitas tentativas. Aguarde um minuto e tente de novo.` 400: por campo. 401: a fundação trata (renovar a sessão). 500 e sem conexão: textos da fatia 0.
- **Estados:** inicial, enviando (botão desabilitado, uma requisição só), erro.

**Rotas:** `trocarMinhaSenha` (`POST /api/eu/senha`).

**Observações para quem monta:** senhas e hashes **nunca** vão para o console nem para o `localStorage`; o corpo `{ senhaAtual, novaSenha }` não tem a confirmação.

---

## Tela sem print: Meus documentos (os três perfis)

**Descrição em texto** (contrato `listarMeusDocumentos`: "uma lista com tipo, validade, situação e, nos pedidos, o botão 'Enviar arquivo'"; roadmap, decisão 14; desenho meu). Página `/eu/meus-documentos`, no shell da pessoa (Funcionário, Gestor ou RH). Cada um vê **só os documentos dele** (as rotas são `/api/eu/...`).

- **Título e apoio:** "Meus documentos"; "Os documentos que o RH guarda na sua ficha e os que ele pediu para você enviar." (texto meu).
- **Lista** (sem paginação; na ordem da API, que traz os pendentes primeiro no exemplo): uma linha por documento com **Documento** (rótulo do tipo, como na tela 09, e a `descricao` embaixo, em corpo pequeno), **Validade** (`dataValidade`, `dd/mm/aaaa`; `null`: "—"), **Situação** (etiqueta "Pendente" âmbar ou "Enviado", como na tela 09), a data ("Pedido em {dd/mm}" de `pedidoEmUtc` quando `PENDENTE`; "Enviado em {dd/mm/aaaa}" de `enviadoEmUtc` quando `ENVIADO`) e a ação:
  - **`PENDENTE`: botão "Enviar arquivo"**. Abre um modal pequeno "Enviar {tipo}" com a área de envio da tela 10 (arrastar ou "Escolher arquivo"; PDF, JPG ou PNG, até 10 MB; confira extensão e tamanho por conforto) e os botões "Cancelar" e "Enviar documento". Envia por `multipart/form-data` (campo `arquivo`) para `enviarMeuDocumento`. 200 devolve o documento atualizado: a linha passa a "Enviado". **A pessoa não cria documento por conta própria** (não há botão "Adicionar" aqui).
  - **`ENVIADO`: botão "Ver"**: `baixarMeuDocumento` devolve o `LinkTemporario`; abra o `url` pelo auxiliar de download da fundação (baixa como anexo).
- **A bolinha do menu:** "Meus documentos" mostra `documentosPendentes` de `obterContadoresDoMenuDoFuncionario` (a fundação 1a já lê a consulta; o número é de documentos `PENDENTE`). **Depois de enviar um arquivo, chame `invalidarContadoresDoMenu`** para a bolinha baixar. (Como a pessoa fica sabendo que o RH pediu: só por essa bolinha, porque nenhum e-mail sai; contrato.)
- **Erros:** 400 em `arquivo` (junto da área de envio); **422 `DOCUMENTO_NAO_PEDIDO`** ("Este documento não está aguardando envio."): recarregue a lista; **422 `DOCUMENTO_SEM_ARQUIVO`**; 404 (documento de outra pessoa ou inexistente): recarregue.
- **Estados:** carregando (esqueleto); vazio "Você ainda não tem documentos na ficha." (texto meu); erro com `detail` e "Tentar de novo". Sem 403 (rota aberta aos três perfis).
- O teste de envio começa com `// @vitest-environment node` (multipart).

**Rotas:** `listarMeusDocumentos` (`GET /api/eu/documentos`), `enviarMeuDocumento` (`POST /api/eu/documentos/{documentoId}/arquivo`), `baixarMeuDocumento` (`GET /api/eu/documentos/{documentoId}/download`).

**Perguntas abertas (`x-provisorio`)**: `DocumentoColaboradorTipo` (lista de tipos). Pergunta ao dono: o RH tem este item no menu? (ver "Perguntas abertas" e a nota da fatia 0.)

---

## Início mínimo por perfil (fatia 1a; sem rota própria)

O roadmap pede, na 1a, "tela inicial mínima por perfil (saudação e menu)". Descrição curta (desenho meu, a partir dos PNG 32 e 00; as telas iniciais completas são da fatia 7, `telas\fatia-7.md`, e **substituem** esta):
- **Funcionário e gestor:** "Olá, {primeiro nome}" e, embaixo, a data de hoje por extenso ("Quarta-feira, 7 de outubro"; li no PNG 32), mais o shell com o menu do perfil. Nada mais.
- **RH:** "Bom dia, {primeiro nome}" (o PNG 00 mostra "Bom dia, Ana"; suponho que siga a hora: Bom dia, Boa tarde, Boa noite) e a data. Nada mais.
- O primeiro nome sai de `usuario.nome`; **nenhuma rota nova** (`obterUsuarioLogado` já vem na sessão).

---

## O que o gestor e o RH reaproveitam (decisão 14 do roadmap)

O gestor usa as telas do funcionário com o **menu do gestor** (PNG 52, 46 e 51); o contrato (`x-perfis: [Funcionario, Gestor, RH]`) deixa o RH usá-las também. Desta fatia:

| Tela | Funcionário | Gestor | RH |
|---|---|---|---|
| Meus documentos (`/eu/meus-documentos`) | sim | sim (mesma tela, menu do gestor) | sim (decidido pelo dono em 2026-11-02: "Meus documentos" continua nos três menus) |
| Trocar senha (modal do rodapé) | sim | sim | sim |
| Esqueci minha senha e Definir senha (públicas) | sim | sim | sim |
| Ficha da pessoa da equipe (tela 52) | não | **só ele** (a equipe dele) | não (o RH usa a tela 03) |
| Colaboradores, Cadastros e Log de auditoria (telas 01, 02, 03, 09, 10, 29, 30, 31) | não | não | **só ele** |

O gestor **não** vê CPF, endereço, holerites nem documentos da equipe e **não** edita cadastro (decisão do dono, 2026-10-21).

---

## Ligação com outras telas (caixas "Depois de salvar" e a alternância de importação)

- **"Enviar o formulário de admissão"** (telas 02 e "Readmitir"): **desligada** até a fatia 5. Ao fim dela (ficha 5, bloco 9: cadastro, importação e "Readmitir" de uma vez), a lista vem de `listarFormulariosRh` com `tipo=ADMISSAO` e `situacao=PUBLICADO` (`telas\fatia-5.md`, "Ligação com outras telas"); o cadastro passa a mandar `formularioAdmissaoId`. **Nenhum e-mail sai** por esta caixa.
- **"Incluir no treinamento de onboarding"** (telas 02 e "Readmitir"): **desligada** até a fatia 6. Ao fim dela (ficha 6, bloco 7: cadastro, importação e "Readmitir" de uma vez), a caixa só vale com treinamento de onboarding em vigor (`listarTreinamentosDeOnboarding`; a lista vazia desliga a caixa) e o texto usa o nome do treinamento (`Onboarding 2026`, no PNG 02). **Nenhum e-mail sai.** Provisório do contrato: como o RH marca qual treinamento é o de onboarding (`x-provisorio` em `listarTreinamentosDeOnboarding` e `incluirFuncionarioNoOnboarding`, fatia 6).
- **"Importar planilha"** (tela 02): o lugar está na tela 02, desligado; a fatia 2 o liga (`telas\fatia-2.md`).

---

## Conferência das 43 rotas

Cada linha liga uma rota do contrato a uma seção desta nota. **43 de 43.**

| # | `operationId` | Rota | Onde está |
|---|---|---|---|
| 1 | `listarFuncionarios` | `GET /api/rh/funcionarios` | tela 01 |
| 2 | `criarFuncionario` | `POST /api/rh/funcionarios` | tela 02 |
| 3 | `obterFichaDoFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}` | tela 03 |
| 4 | `editarFuncionario` | `PUT /api/rh/funcionarios/{funcionarioId}` | tela 03, fluxo "Editar dados" |
| 5 | `obterIndicadoresDoFuncionario` | `GET .../{funcionarioId}/indicadores` | tela 03 (quatro números) |
| 6 | `inativarFuncionario` | `POST .../{funcionarioId}/desligamento` | tela 03, fluxo "Inativar" |
| 7 | `cancelarDesligamentoAgendado` | `DELETE .../{funcionarioId}/desligamento` | tela 03, fluxo "Inativar" (faixa de agendamento) |
| 8 | `readmitirFuncionario` | `POST .../{funcionarioId}/readmissao` | tela 03, fluxo "Readmitir" |
| 9 | `listarEquipeDoGestor` | `GET /api/gestor/equipe` | entrada da tela 52; a chama a tela 51 (fatia 7) |
| 10 | `obterFichaDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}` | tela 52 |
| 11 | `obterIndicadoresDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}/indicadores` | tela 52 |
| 12 | `listarDocumentosDoFuncionario` | `GET .../{funcionarioId}/documentos` | tela 09 |
| 13 | `adicionarDocumentoDoFuncionario` | `POST .../{funcionarioId}/documentos` | tela 10 |
| 14 | `baixarDocumentoDoFuncionario` | `GET .../documentos/{documentoId}/download` | tela 09 ("Ver") |
| 15 | `listarMeusDocumentos` | `GET /api/eu/documentos` | Meus documentos |
| 16 | `enviarMeuDocumento` | `POST /api/eu/documentos/{documentoId}/arquivo` | Meus documentos |
| 17 | `baixarMeuDocumento` | `GET /api/eu/documentos/{documentoId}/download` | Meus documentos |
| 18 | `listarCargos` | `GET /api/rh/cargos` | tela 29, Cargos |
| 19 | `criarCargo` | `POST /api/rh/cargos` | tela 30 |
| 20 | `editarCargo` | `PUT /api/rh/cargos/{cargoId}` | tela 29 e tela 30 (modo edição) |
| 21 | `listarSetores` | `GET /api/rh/setores` | tela 29, Setores (e opções das telas 01, 02, 03) |
| 22 | `criarSetor` | `POST /api/rh/setores` | tela 29, Setores |
| 23 | `editarSetor` | `PUT /api/rh/setores/{setorId}` | tela 29, Setores |
| 24 | `listarGestores` | `GET /api/rh/gestores` | tela 29, Gestores |
| 25 | `cadastrarGestor` | `POST /api/rh/gestores` | tela 29, Gestores |
| 26 | `configurarAusenciaDoGestor` | `PUT /api/rh/gestores/{gestorId}/ausencia` | tela 29, Gestores (interruptor "Gestor ausente") |
| 27 | `listarBeneficiosDoCatalogo` | `GET /api/rh/beneficios` | tela 29, Benefícios (e caixas da tela 02) |
| 28 | `criarBeneficioDoCatalogo` | `POST /api/rh/beneficios` | tela 29, Benefícios |
| 29 | `editarBeneficioDoCatalogo` | `PUT /api/rh/beneficios/{beneficioId}` | tela 29, Benefícios |
| 30 | `excluirBeneficioDoCatalogo` | `DELETE /api/rh/beneficios/{beneficioId}` | tela 29, Benefícios |
| 31 | `obterEmpresa` | `GET /api/eu/empresa` | Cadastros, Empresa; shell (nome da empresa) |
| 32 | `salvarEmpresa` | `PUT /api/rh/empresa` | Cadastros, Empresa |
| 33 | `listarUsuariosDoSistema` | `GET /api/rh/usuarios` | tela 29, Usuários do sistema |
| 34 | `enviarConviteDeAcesso` | `POST /api/rh/usuarios/{funcionarioId}/convite` | tela 29, Usuários do sistema; menu "..." da tela 03 |
| 35 | `ativarUsuarioDoSistema` | `POST .../usuarios/{funcionarioId}/ativar` | tela 29, Usuários do sistema |
| 36 | `desativarUsuarioDoSistema` | `POST .../usuarios/{funcionarioId}/desativar` | tela 29, Usuários do sistema |
| 37 | `listarEventosDeAuditoria` | `GET /api/rh/auditoria` | tela 31 |
| 38 | `exportarAuditoria` | `GET /api/rh/auditoria/exportacao` | tela 31 ("Exportar CSV") |
| 39 | `obterEventoDeAuditoria` | `GET /api/rh/auditoria/{eventoId}` | tela 31 ("Detalhes") |
| 40 | `solicitarRedefinicaoDeSenha` | `POST /api/auth/senha/esqueci` | Esqueci minha senha |
| 41 | `validarLinkDeSenha` | `POST /api/auth/senha/validar-link` | Definir senha |
| 42 | `definirSenhaPeloLink` | `POST /api/auth/senha/definir` | Definir senha |
| 43 | `trocarMinhaSenha` | `POST /api/eu/senha` | Trocar senha |

A única rota sem tela **própria** desta fatia é a nº 9 (`listarEquipeDoGestor`): o contrato a descreve como "a lista da equipe" que várias fatias usam e quem a chama é a tela 51 (fatia 7).

---

## Perguntas abertas

Tudo o que depende de decisão do dono e que **já** está marcado no contrato (`x-provisorio`, `PROVISORIOS.md`) está citado em cada tela; construa normalmente em cima do que está escrito. Resumo:

| `x-provisorio` | O que a decisão pode mudar na tela | Telas |
|---|---|---|
| `components > schemas > FuncionarioTipoContrato` | os valores do filtro e do campo "Tipo de contrato" (hoje CLT e Estágio) | 01, 02, 03, "Readmitir" |
| `components > schemas > FuncionarioCriarComando` | "Gestor" hoje é só informativo (sempre o do setor); se puder ser outro, vira seletor | 02 |
| `components > schemas > DocumentoColaboradorTipo` | a lista de tipos do select e dos rótulos | 09, 10, Meus documentos |
| `components > schemas > FormularioTipo` (fatia 5) | se as respostas do formulário de admissão chegam à ficha; afeta o texto de abertura da tela 02 | 02 |

**Perguntas ao dono que mudam o que o usuário vê** (sem `x-provisorio`; a sessão do front as registra em `DUVIDAS.md` e segue com a resposta padrão; o item 2 já foi decidido pelo dono):
1. **Benefícios já marcados no cadastro (tela 02).** O PNG mostra três marcados (Vale-refeição, Plano de saúde, Seguro de vida); o contrato tem padrão `[]` e nenhum documento fala em benefícios padrão. *Resposta padrão:* todos desmarcados.
2. **"Meus documentos" e "Trocar senha" no menu.** O Figma não desenha nenhum dos dois nem "Sair". **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu` e `D-rodape`; **não se registram como dúvida**): "Meus documentos" continua nos três menus, e "Trocar senha" e "Sair" são dois botões de texto no rodapé do menu (`telas\fatia-0.md`, "Perguntas abertas e decisões do dono", 1 e 2).
