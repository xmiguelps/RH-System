# Fatia 1b (colaboradores, ficha e acesso) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **O método de execução (subagentes ou na própria sessão) ainda não foi escolhido pelo dono: ele decide depois do planejamento.** Entrada do plano do back-end: `docs/superpowers/plans/2026-10-30-plano-back-end.md`. Pré-requisito: a fatia 1a (`2026-10-30-back-end-fatia-1a-fundacao.md`) pronta.

**Goal:** Entregar, no que é API, o cadastro de colaboradores (lista, cadastro, ficha, edição, desligamento agendado e readmissão), o acesso (convite, redefinição e troca de senha, usuários do sistema) e a ficha e a lista da equipe do gestor, exatamente como o contrato descreve, deixando pronto o que a importação por Excel (fatia 2) e as fatias 3 a 7 reaproveitam.

**Architecture:** Mesma da 1a (Clean Architecture; casos de uso como classes simples; EF por Fluent API; MySQL real nos testes). Tudo o que depende de outra fatia entra por **interface pequena com uma versão padrão que diz a verdade do que existe hoje** (situação de hoje, indicadores da ficha, onboarding, formulário de admissão, condição para efetivar o desligamento): a fatia dona substitui a versão padrão sem mexer aqui. O cadastro tem **um só caminho** para criar uma ou 500 pessoas (`ICadastroDeFuncionarios`), que a importação reaproveita com a mesma validação.

**Tech Stack:** .NET 10, EF Core 9.x sem migrations, Pomelo 9.0.0, MySQL 8.4, xUnit, Testcontainers (MySQL e Mailpit), MailKit. **Nenhum pacote novo.**

**Spec:** `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seções 4 e 5); roadmap `docs/superpowers/plans/2026-10-28-roadmap-completo.md` (fatia 1, incremento 1b e "Regras", `:110-125`; "Decisões que atravessam fatias" 2, 3, 5 e 13); contrato `contrato/CONVENCOES.md` (seções 2, 3, 9, 10, 11, 12) e `contrato/fontes/20-fatia-1-colaboradores.yaml` (as 19 operações da seção "Autorrevisão"); scripts `Banco de Dados/Scripts/V001`; design dos planos `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md` (seção 4).

## Global Constraints

- .NET 10 com **EF Core 9.x** e `Pomelo.EntityFrameworkCore.MySql` **9.0.0**; **sem migrations**. Domain não depende de nada; Application depende só de Domain; Api e Infrastructure dependem de Application. Casos de uso são **classes simples**, sem biblioteca de mediação.
- **Scripts `V001` a `V005`: não se editam sem avisar o dono; depois da Task 14 da fatia 0 (primeiro deploy) só script novo.** Faltando coluna, índice ou tabela: **para e pergunta ao dono** (script novo, `V006` em diante, numerado por ele). Esta fatia **não** pede script novo.
- **O contrato é lei:** a API implementa `contrato/openapi.yaml`; não se inventa rota, campo nem `codigo` de erro. Cada rota nova entra no escopo do `ContratoDivergenciaTests` **no mesmo passo em que é implementada** e declara **todos** os códigos de resposta do contrato (`WithName`, `Produces<T>`, `ProduzProblemas`). **O escopo é a lista de `operationId` mantida no próprio teste, nunca prefixo de caminho** (`/api/rh/beneficios` é prefixo textual de `/api/rh/beneficios-adesoes`, e `/api/auth/senha/*` é desta fatia): **cada tarefa acrescenta os `operationId` das rotas dela no mesmo passo em que as implementa, nunca antes** (a regra "operação do contrato no escopo que a API não tem é divergência" faria o teste falhar no meio da fatia).
- **`contrato/STATUS.md`:** a tabela por fatia (a que o front lê) fica como está; abaixo dela fica a seção **"Rotas implementadas"** (uma linha por rota: rota, `operationId`, situação, observação; o formato está na Task 7 da 1a), editada pela **própria tarefa que entrega a rota**: a linha entra `em andamento`; o dono revisa no PR e passa a `pronta` no deploy. Os passos "Atualizar `contrato/STATUS.md`" abaixo fazem isso.
- **Fuso:** tabelas do grupo em `-03:00`; tabelas só da aplicação (`V001`) em UTC; a API fala UTC (`...Z`); `FusoBrasil` faz a conversão; "hoje" é sempre `FusoBrasil.Hoje(IRelogio.AgoraUtc)`.
- **Datas das tabelas só da aplicação** (`V001`: `CRIADO_EM`, `EXPIRA_EM`, `USADO_EM`, `EFETIVADO_EM`...): a aplicação as preenche **sempre**, a partir de `IRelogio` (UTC). O `DEFAULT (UTC_TIMESTAMP(6))` do banco é só rede de segurança: com relógio falso, uma `CRIADO_EM` gravada pelo banco quebraria o `CHECK` `EXPIRA_EM > CRIADO_EM` (tabela `USUARIOS_TOKENS_SENHA` do `V001`). Os testes com relógio falso esperam os valores do relógio falso.
- **Privacidade (roadmap, decisão 13):** senha, hash e token **nunca** vão para log, auditoria, mensagem de erro nem para o banco em claro (o token só existe no e-mail e na `CARGA` da fila até o envio). O gestor **nunca** recebe CPF, RG, e-mail pessoal, endereço, matrícula, benefícios, documentos nem telefone completo.
- **E-mails:** só os da lista fechada (`CONVENCOES.md:112-114`). Esta fatia envia **dois**: convite e redefinição de senha. Nenhuma outra ação desta fatia manda e-mail.
- **Commits e PRs: nunca pôr o Claude como coautor** (nem `Co-Authored-By`, nem "Generated with Claude Code"). Só commitar quando o dono pedir. Segredos nunca vão para o repositório.
- Nomes de domínio e de API em português sem acento; mensagens ao usuário em português do Brasil com acento. Comandos rodam **da raiz do repositório** no Windows PowerShell 5.1 (sem `&&`; `curl.exe`). `TreatWarningsAsErrors` e `dotnet format back-end --verify-no-changes` valem para tudo o que esta fatia escrever.
- **Os arquivos de `Banco de Dados/` são do grupo:** esta fatia não os toca.

## Decisões deste plano (o contrato e o roadmap não fixam)

Escolhas de engenharia da sessão que escreveu; o dono pode mudar na leitura. As oito primeiras mudam algo que ele, o RH ou o gestor veem; as demais são de engenharia:

1. **`situacaoHoje` antes da fatia 3.** O contrato não deixa `null` para quem está ativo (`20-fatia-1:2765-2771`, `:2981-2987`, `:3001`, `:3009`), mas ponto, atestado, feriado e horário padrão só existem nas fatias 3, 7 e 1c. A 1b aplica a regra de `CONVENCOES.md:88-101` por inteiro (Task 2) e lê os fatos por `IFonteDeFatosDoDia`; a versão que entrega **só conhece o fim de semana** (nenhuma entrada, férias, atestado, feriado nem horário configurado). Resultado até a fatia 3: dia útil = `AINDA_NAO_COMECOU` para todo ativo (**nunca** `SEM_REGISTRO`: sem horário padrão ninguém está atrasado), sábado e domingo = `SEM_EXPEDIENTE`. **O RH e o gestor veem "Ainda não começou" para todos até a fatia 3 trocar a fonte.**
2. **"A sessão atual continua" na troca de senha** (`20-fatia-1:2452-2454`). O JWT da fatia 0 só traz `sub`, `fid`, `email`, `nome` e `perfil` e `REFRESH_TOKENS` não liga o token à sessão, então a API **não sabe qual refresh token é o da sessão atual**. Decidido: o JWT ganha a claim **`rth`** (o hash SHA-256 do refresh token emitido junto; o hash de um segredo aleatório de 32 bytes não revela nada) e a troca revoga os refresh tokens **exceto** esse. Muda `IEmissorDeTokens.EmitirAcesso` da fatia 0 (parâmetro opcional novo). **Alternativa:** revogar todos (a sessão atual cairia na próxima renovação, em até 15 minutos), o que contradiz o contrato. **Limitação aceita (rara), documentada e testada na Task 5:** se outra aba renovou o refresh antes, o token de acesso ainda leva o `rth` antigo; a troca então revoga o refresh vigente e a sessão dessa aba cai na próxima renovação.
3. **O convite a um login desativado é recusado** (`enviarConviteDeAcesso`): 422 `USUARIO_DESATIVADO`, que o contrato já prevê na descrição da operação; o RH usa "Ativar" antes e o convite **não** reativa o login. O contrato dá o código mas **não o texto**: este plano usa o `detail` "O login desta pessoa está desativado. Libere o acesso em Usuários do sistema (Ativar) antes de enviar o convite." (o dono confirma). Só a **readmissão** com `enviarConvite = true` faz as duas coisas de uma vez: ela reativa o login e depois emite o convite (`20-fatia-1:657-664`). Login novo nasce ativo.
4. **O admin do seed ganha matrícula na primeira edição.** A ficha dele tem `matricula: null` (nota «(antigo V030)» do `V001`) e a edição é onde se completa (`20-fatia-1:344`, roadmap `:117`): quem salva a ficha sem matrícula recebe a próxima do contador.
5. **Auditoria dos dados pessoais:** CPF, RG, órgão emissor, e-mail pessoal, telefone, endereço e data de nascimento entram no log com **`valorOculto`** (o log diz "CPF alterado", nunca o valor; roadmap, decisão 13). Desligamento agendado e cancelado são eventos **explícitos**; a efetivação sai pelo interceptor (`Inativou`); o que o trigger do grupo faz ao inativar (login, benefícios, férias) **não tem registro próprio** (decisão do dono, 2026-10-28).
6. **`POST /api/auth/senha/esqueci`:** além do limite de 5 por minuto por IP (contrato), **um pedido por pessoa a cada 60 segundos**: o seguinte devolve o mesmo 202 e **não** enfileira nada (impede encher a caixa de alguém a partir de vários IPs). O contrato não o pede.
7. **`POST /api/eu/senha` mantém o 429 do contrato** (`20-fatia-1:2455-2457`): 5 tentativas de senha atual **errada** por minuto **por pessoa**, com `Retry-After`. Não é o limitador por IP das três rotas públicas (aquele conta toda chamada): a Task 5 traz um limitador em memória próprio (`ILimiteDeSenhaAtual`), consultado **antes** de conferir a senha, que só conta os erros de senha atual.
8. **Caixas "depois de salvar" até as fatias 5 e 6:** `incluirNoOnboarding = true` devolve o 422 `SEM_TREINAMENTO_DE_ONBOARDING` e `formularioAdmissaoId` preenchido devolve o 400 em `formularioAdmissaoId`, **ambos do próprio contrato** (`20-fatia-1:153-165`) e verdadeiros enquanto não existe treinamento nem formulário. Nada fica gravado.
9. **Tipo de contrato** é texto livre no banco (nota «(antigo V006)» do `V001`): valor fora de `CLT` e `ESTAGIO` sai `null` na ficha e na lista, em vez de derrubar a leitura.
10. **Benefícios do colaborador:** a 1b só cria linhas `APROVADO` e só desativa ou reativa linhas `APROVADO`. **Quando o RH concede** (cadastro ou edição) um benefício que **já tem linha** para a pessoa (`PENDENTE`, `RECUSADO` ou `CANCELADO`; a chave única `(ID_FUNCIONARIO, ID_BENEFICIO)`, `Criacao_Tabelas:562`, impede uma segunda), a API **atualiza essa linha** para ativa e aprovada em vez de inserir (é o que a nota «(antigo V021)» do `V001` manda para refazer um pedido: atualizar a mesma linha). Isso aprova, na prática, um pedido pendente do colaborador; as colunas do pedido (resposta, motivo) a 1b não as mapeia e a fatia 7 decide o que fazer com elas. Linha `PENDENTE`, `RECUSADO` ou `CANCELADO` de benefício que **não** está no conjunto desejado **não é tocada**.
11. **Ordem de travas** nas rotas de desligamento: sempre `FUNCIONARIOS` e depois `DESLIGAMENTOS_AGENDADOS` (evita impasse entre "Inativar", "Cancelar" e a tarefa).
12. **Limpeza de tokens de senha** (`Retencao:TokensDeSenhaDias`, 30): apaga o que tem mais de 30 dias **exceto o último convite de cada pessoa** (é dele que vem `conviteEnviadoEmUtc`).
13. **Último dia de trabalho** mais de 10 anos à frente: 400 em `ultimoDiaDeTrabalho` (o contrato é omisso; evita estouro de data).
14. **Eventos de auditoria de requisição anônima** (`esqueci`, `definir`): o fato de negócio **persiste** (token, senha), então o evento é gravado **na mesma conexão e na mesma transação do fato**, por `IAuditor.RegistrarComo` (ator informado: a 1a, Task 8.1, não deixa `Registrar` nem `RegistrarEGravarAsync` servirem ao ator `Anonimo`), e sai no `SalvarAsync` do fato: se o evento falhar, o fato desfaz, e nunca há evento sem fato nem fato sem evento. **Nunca `RegistrarEGravar*` aqui** (segunda conexão): essas transações seguram `FOR UPDATE` na linha da pessoa em `FUNCIONARIOS`, e o evento tem chave estrangeira para ela (`V001`), então a segunda conexão esperaria o limite do InnoDB (50 s) e a requisição viraria 500 (regra única da 1a, Task 8.1, "Qual conexão grava o evento"). O mesmo vale para o evento do convite (`IServicoDeConvite.EmitirAsync`): `Registrar`, dentro da transação.

## Review Focus

1. **Convite, redefinição e sessões** (Tasks 4, 5 e 6): dois usos simultâneos do mesmo link consomem um só; link vencido, usado e invalidado dão a **mesma** resposta; e-mail inexistente, inativo e sem login dão **o mesmo 202** e nada na fila; o token nunca aparece em claro no banco, no log nem na auditoria; trocar a senha encerra as **outras** sessões e mantém a atual, e redefinir encerra todas.
2. **Virada do dia** (Tasks 7 e 9): admissão em 08/10 vista às 22h do dia 07 (`-03:00`) é "Em admissão"; o desligamento com último dia 07/10 **não** é gravado às 23:30 do dia 07 e é gravado às 00:00 do dia 08 (`03:00Z`).
3. **O gestor nunca vê o que é do RH** (Tasks 7 e 11): CPF, matrícula, e-mail pessoal, endereço e benefícios ausentes das três rotas do gestor; telefone só mascarado; fora do escopo (outro setor, o próprio gestor, em admissão, inativo) é **404**, também nos indicadores; o RH recebe tudo completo.
4. **Corridas** (Tasks 4, 5, 8 e 9): dois cadastros do mesmo CPF, dois cliques em "Inativar", "Cancelar" contra a tarefa que efetiva, dois convites ao mesmo tempo, dois "esqueci a senha" da mesma pessoa (e o evento de auditoria, que não espera trava de outra conexão): ninguém termina `CANCELADO` e inativo, com matrícula repetida, com dois links em aberto nem com 500.
5. **Cadastro tudo ou nada** (Tasks 8 e 10): falha na caixa de onboarding ou de formulário, no convite ou no banco desfaz o colaborador, os dados pessoais, os benefícios, o login, o token, o e-mail na fila **e a matrícula (sem buraco)**.

---

## Antes de começar

**Pré-requisito:** a fatia 1a pronta (todas as tarefas dela passando: 1.1, 1.2, 2 a 7, 8.1, 8.2 e 9 a 13). Rode `dotnet test back-end` e `docker compose up -d --wait` e confirme verdes antes da Task 1. A fatia 1 pode começar com as Tasks 1 a 9 da fatia 0 passando (sem esperar o front nem o deploy da Task 14).

**O que li** (arquivo e linha de 2026-10-30; se o arquivo mudou, vale o arquivo. Dos planos da 1a e da fatia 0, que mudam, cito só o título da tarefa ou da seção, não a linha):

- **Plano da 1a:** a seção "Para as fatias 1b e 1c" e as Tasks 1 a 13. Uso os nomes exatos de lá: `IAtorAtual`, `IEscopoDoUsuario`/`EquipeDoGestor`/`DaEquipe`, `Politicas`/`FamiliasDeRota`, `IAuditor`/`[Auditado]`/`[Auditar]`/`IAuditavel`, `IFilaDeEmails`/`EmailParaEnfileirar`/`IRenderizadorDeEmail`/`ModeloDeEmail`, `IAgendador`/`NovaTarefa`/`ITarefaAgendada`/`TarefaParaExecutar`/`IExecutorDeTarefas`, `IGeradorDeMatriculas`, `ParametrosDeLista`/`RespostaPaginada<T>`/`MapaDeOrdenacao<T>`/`BuscaLike`/`ToRespostaPaginadaAsync`, os erros e `ColetorDeErros`, `TextosDeErro`, `FusoBrasil`, `ProduzProblemas`, `MatrizDePerfisTests`, `CamposDoRh.AfirmarSemCamposDoRh`, `AfirmarConformeAoContratoAsync`, `ElencoDeTeste` e `ApiFactory`. A 1a cita `FuncionarioListaRespostaRh(..., Totais) : RespostaPaginada<Item>` (Task 4, `RespostaPaginada<T>`), nome que este plano usa. As entidades que a 1a mapeia (`UsuarioTokenSenha`, `Gestor`, `DesligamentoAgendado`, `Funcionario.Matricula/CargoId/SetorId`, `Usuario.SenhaProvisoria/DefinirSenha`, `Setor.GestorId`) estão na Task 1 dela; o interceptor e as regras de auditoria, na Task 8; a fila, na Task 10; o agendador, na Task 11. **Peças da 1a que esta fatia só consome:** a porta `IPedidosParadosNoGestor` (Task 13: o composto padrão, vazio, e `PedidosAprovados`; a seção "Para as fatias 1b e 1c" diz onde estão) e `EnumsDeConsulta` (Task 4).
- **Fatia 0** (pelos títulos): `Funcionario`, `Usuario`, `Cargo`, `Setor` e `Perfil` (Task 5), as interfaces e os casos de uso de autenticação (Task 6), a infraestrutura (Task 7), os erros e o comparador de contrato (Task 8), o limitador de tentativas e o seed do admin (Task 9). O admin do seed não tem cargo, setor nem matrícula (Task 9, seed).
- **Contrato** (`20-fatia-1-colaboradores.yaml`; o dono pode mover as linhas: vale o `operationId`): as 19 operações em `:28`, `:122`, `:262`, `:332`, `:467`, `:504`, `:603`, `:633`, `:778`, `:833`, `:877`, `:1927`, `:1998`, `:2067`, `:2101`, `:2355`, `:2383`, `:2419`, `:2449`; schemas em `:2654-3047` (colaboradores), `:3286-3319` (usuários) e `:3409-3452` (senha). `CONVENCOES.md:70-86` (duas etapas e gestor; a etapa é fixada na criação do pedido), `:88-101` (situação de hoje), `:112-114` (e-mails).
- **Banco (`V001`):** `SEQUENCIAS` e `FUNCIONARIOS.MATRICULA` (matrícula; nota «(antigo V030)»), `USUARIOS.SENHA_PROVISORIA` (nota «(antigo V032)», que também diz como a situação se calcula), `USUARIOS_TOKENS_SENHA` (nota «(antigo V033)», como a aplicação usa os tokens), `DESLIGAMENTOS_AGENDADOS` (nota «(antigo V034)»), `FUNCIONARIOS.MATRICULA` e `TIPO_CONTRATO` (nota «(antigo V006)») e `BENEFICIOS_FUNCIONARIOS` (nota «(antigo V021)»: `STATUS_PEDIDO` sem padrão; refazer pedido é atualizar a mesma linha; `BENEFICIO_ATIVO` só com `APROVADO`). Do grupo: `Criacao_Tabelas:81-105` (`FUNCIONARIOS`), `:165-189` (`FUNCIONARIOS_DETALHES`, `CPF` único), `:197-209` (`USUARIOS`), `:548-562` (`BENEFICIOS_FUNCIONARIOS`, chave única); `Criacao_Triggers:57-140` (gestor igual a si mesmo, gestor inativo, `ATIVO = FALSE` com data de desligamento, e o `AFTER UPDATE` que desativa `USUARIOS` e `BENEFICIOS_FUNCIONARIOS` e cancela `FERIAS`), `:263-316` (usuário só para funcionário ativo com e-mail e `SENHA` de 40+ caracteres), `:886-900`.
- **Medi** com um script Node descartável em 2026-10-30: dos CPFs que aparecem nos exemplos do contrato, `29384756012` (Henrique Alves, resposta de `readmitirFuncionario`) **não passa** na conta do trigger do banco (relatório).

**O que suponho** (o teste indicado confirma ou derruba):

- Que os nomes da 1a e da fatia 0 são os dos planos. **Antes da Task 1, abra** `Funcionario`, `Cargo`, `Setor`, `Usuario`, `EmissorDeTokensJwt`, `EntrarCasoDeUso`, `RenovarTokenCasoDeUso`, `IRefreshTokenRepositorio`, `LimiteDeTentativas.cs`, `TratamentoDeErros.cs` e `ApiFactory`, anote o que difere e **use o nome real mantendo o significado**. Em especial: o nome da propriedade-chave de `ID_NIVEL_ACESSO` em `Funcionario` (este plano diz `NivelDeAcessoId`; se não existir, acrescente) e se `Perfil` já tem a conversão inversa (`NomesDeNivel.DoPerfil`).
- Que o ASP.NET liga enum de **consulta** por `Enum.TryParse` (nomes C# como `EmAdmissao`), **sem** a política de nomes do JSON: por isso `situacao`, `tipoContrato` e `ordenarPor` chegam como texto e `EnumsDeConsulta` (da 1a, Task 4) converte; o teste `Situacao_EM_ADMISSAO_...` (Task 7) prova.
- Que `JsonNamingPolicy.SnakeCaseUpper` dá `CLT`, `ESTAGIO`, `EM_ADMISSAO`, `SEM_REGISTRO`, `SENHA_DEFINIDA` (suposição também da 1a, na seção de suposições dela).
- Que o gerador de OpenAPI marca `required` e escreve `oneOf` com `null` como o contrato (as respostas têm muitos campos `cargo`, `setor`, `gestor` anuláveis) e que o comparador trata o `allOf` com `Paginacao` das listas. Se `ContratoDivergenciaTests` acusar só a **forma**, o erro é do comparador ou do transformador da Task 8 da fatia 0: ajuste ali, **não** o contrato.
- Que o EF, numa mesma gravação, pode mandar o `INSERT` antes do `UPDATE` (a chave única `(ID_USUARIO, TIPO, EM_ABERTO)` do `V001` bateria): por isso a emissão de convite grava em **duas** etapas (Task 4).
- Que `SELECT ... FOR UPDATE` em `FUNCIONARIOS`/`USUARIOS` serializa as corridas (os testes de corrida provam). **Sobre o evento de auditoria não se supõe mais que ele não bloqueia:** a chave estrangeira do evento para `FUNCIONARIOS` (`V001`) faz o InnoDB pedir trava compartilhada na linha pai (comportamento lido, não medido), então nenhum evento sai por segunda conexão enquanto a transação segura a linha da pessoa (decisão 14); o teste `Esqueci_com_a_linha_da_pessoa_travada...` da Task 5 mede.
- Que a resposta de `esqueci` não se distingue pelo **tempo** entre e-mail existente e inexistente (a fila tira o SMTP da requisição; **não medi**).

**Fatos a conferir antes (achados da leitura):**

1. **Escopo do teste de divergência da fatia 0.** O plano da fatia 0 (Task 8) lista o escopo por **`operationId`** no próprio teste (na fatia 0: `entrar`, `renovarSessao`, `sair`, `obterUsuarioLogado`; a Task 9 acrescenta `verificarSaude`), e **cada fatia acrescenta os dela**. Abra `ComparadorDeContrato` e `ContratoDivergenciaTests` e confira que o comparador recebe o **conjunto de `operationId`**, não um predicado de caminho. Se a fatia 0 foi feita com o predicado de caminho, troque a assinatura **antes da Task 1** (mudança mecânica) e liste os `operationId` já implementados (fatia 0 e 1a). O motivo: o contrato tem **quatro** operações sob `/api/auth/senha/` que só esta fatia implementa e várias fatias sob o mesmo prefixo de caminho.
2. **Não há escopo por prefixo nesta fatia.** `/api/rh/funcionarios` e `/api/gestor/equipe` têm sob eles rotas das fatias 1c, 3, 4, 5, 6 e 7 (documentos, ponto, férias...). O escopo é a lista de `operationId` da seção "Autorrevisão", acrescentada tarefa a tarefa.

## Estrutura de arquivos (o que esta fatia cria; caminhos sob `back-end/`)

- `src/RHSystem.Domain/`: `Colaboradores/FuncionarioDetalhes.cs`, `Cpf.cs`, `TipoDeContrato.cs`; `Beneficios/BeneficioDoFuncionario.cs`, `Autenticacao/NomesDeNivel.cs`; muda `Autenticacao/Funcionario.cs`, `Cargo.cs`, `Setor.cs`, `Usuario.cs`, `UsuarioTokenSenha.cs`, `NivelDeAcesso.cs`, `Perfil.cs`, `Colaboradores/DesligamentoAgendado.cs`.
- `src/RHSystem.Application/`: `Comum/ProblemaDeApiException.cs`; `Colaboradores/` (situação de hoje, telefone, DTOs, cadastro, gestor, desligamento, consultas, indicadores e os casos de uso); `Acesso/` (token de senha, hash provisório, convite, usuários, senha, limite da senha atual e os casos de uso).
- `src/RHSystem.Infrastructure/`: `Persistencia/Configuracoes/` (detalhes, benefício do funcionário; muda funcionário, cargo, setor); `Colaboradores/` (consultas, cadastro, gestor, desligamento, tarefa, fontes padrão); `Acesso/` (gerador de token, convite, consulta de usuários, limite da senha atual, limpeza); `Emails/` (dois renderizadores).
- `src/RHSystem.Api/`: `Colaboradores/ColaboradoresEndpoints.cs`, `EquipeEndpoints.cs`; `Acesso/SenhaEndpoints.cs`, `UsuariosDoSistemaEndpoints.cs`; muda `TratamentoDeErros.cs`, `LimiteDeTentativas.cs`, `Program.cs`. **Não cria** `EnumsDeConsulta` (é da 1a, Task 4).
- `tests/`: `Domain.Tests/Colaboradores/`, `Application.Tests/Colaboradores/` e `Acesso/`, `Api.IntegrationTests/Colaboradores/`, `Acesso/`, `Equipe/`; `Infra/SementeDoCadastro.cs`, `CpfsDeTeste.cs`.

---

### Task 1: Mapeamento da 1b e o Domain do cadastro

**Files:**
- Create (Domain): `Colaboradores/FuncionarioDetalhes.cs`, `Colaboradores/Cpf.cs`, `Colaboradores/TipoDeContrato.cs`, `Beneficios/BeneficioDoFuncionario.cs`, `Autenticacao/NomesDeNivel.cs`
- Modify (Domain): `Autenticacao/Funcionario.cs`, `Cargo.cs`, `Setor.cs`, `Usuario.cs`, `NivelDeAcesso.cs`, `Perfil.cs`
- Create (Infrastructure): `Persistencia/Configuracoes/FuncionarioDetalhesConfiguracao.cs`, `BeneficioDoFuncionarioConfiguracao.cs`; modify `FuncionarioConfiguracao.cs`, `CargoConfiguracao.cs`, `SetorConfiguracao.cs`, `AppDbContext.cs`
- Test: `tests/RHSystem.Domain.Tests/Colaboradores/CpfTests.cs`, `FuncionarioCadastroTests.cs`, `BeneficioDoFuncionarioTests.cs`, `Autenticacao/NomesDeNivelTests.cs`; `tests/RHSystem.Api.IntegrationTests/Persistencia/MapeamentoDaFatia1bTests.cs`, `tests/RHSystem.Api.IntegrationTests/Auditoria/AuditoriaDoCadastroTests.cs`, `tabelas-sem-mapeamento.txt` (tira `FUNCIONARIOS_DETALHES` e `BENEFICIOS_FUNCIONARIOS`); `Infra/SementeDoCadastro.cs`, `Infra/CpfsDeTeste.cs`

**Interfaces:**
- Consumes: `Funcionario`, `Usuario`, `Cargo`, `Setor`, `Gestor`, `Beneficio`, `Perfil`, `AppDbContext`, `EnumComoTextoMaiusculo<T>`, `ConversoresDeTempo`, `[Auditar]`/`IAuditavel` (1a, Tasks 1 e 8).
- Produces (Domain):

```csharp
// RHSystem.Domain.Colaboradores
static class Cpf { static string Normalizar(string? texto);          // só os dígitos
                   static bool EhValido(string cpf); }                // 11 dígitos, não todos iguais, os dois dígitos verificadores (a conta do TRG_FUNCIONARIOS_DETALHES_BEFORE_INSERT)
enum TipoDeContrato { Clt, Estagio }                                  // banco: "CLT", "ESTAGIO"
static class TiposDeContrato { static string ParaTexto(TipoDeContrato t); static TipoDeContrato? DoTexto(string? texto); }   // desconhecido ou nulo => null
enum SituacaoCadastral { Ativo, EmAdmissao, Inativo }
sealed record DadosPessoais(string Cpf, string? Telefone, string? EmailPessoal, string? Rg, string? RgOrgaoEmissor,
    string? Cep, string? Logradouro, string? Numero, string? Complemento, string? Bairro, string? Cidade, string? Uf);
class FuncionarioDetalhes { int Id; int FuncionarioId; Funcionario? Funcionario; /* + as colunas de DadosPessoais, setter privado */
  static FuncionarioDetalhes Criar(Funcionario f, DadosPessoais d); void Atualizar(DadosPessoais d); DadosPessoais ParaDados(); }
// RHSystem.Domain.Autenticacao: Funcionario ganha
DateOnly? DataNascimento  [Auditar("Data de nascimento", Sensivel = true)]
DateOnly? DataDesligamento [Auditar("Último dia de trabalho")]
int? GestorId   /* ID_GESTOR; derivado do setor: sem [Auditar] */      string? TipoContrato [Auditar("Tipo de contrato")]
FuncionarioDetalhes? Detalhes                                          int? NivelDeAcessoId [Auditar("Nível de acesso")]   // nome real da chave de ID_NIVEL_ACESSO
void AtualizarCadastro(string nome, string emailCorporativo, DateOnly dataNascimento, DateOnly dataAdmissao, TipoDeContrato tipoContrato,
                       int cargoId, int setorId, int? gestorId, NivelDeAcesso nivel);     // usada no cadastro (depois de Criar) e na edição; e-mail com Trim e minúsculas
void Desligar(DateOnly ultimoDiaDeTrabalho);          // DataDesligamento = o dia; Ativo = false
void Readmitir(DateOnly dataAdmissao, int cargoId, int setorId, TipoDeContrato tipoContrato, int? gestorId, NivelDeAcesso nivel);   // InvalidOperationException se Ativo; Ativo = true; DataDesligamento = null
SituacaoCadastral Situacao(DateOnly hoje);            // Inativo se !Ativo; EmAdmissao se DataAdmissao > hoje; senão Ativo
// ESTA FATIA É A DONA das peças abaixo; a 1c e a fatia 7 só as consomem, nunca as redefinem:
// Cargo ganha:  decimal? CargaSemanalHoras { get; }   // CARGA_HORARIA, só leitura aqui (o método que a escreve é acréscimo da 1c em `Cargo.cs`)
// Setor ganha:  void DefinirGestor(int? idGestor)     // idGestor = ID_GESTOR de GESTORES (não o do colaborador); null limpa
// Usuario ganha: void Reativar()              (Inativar() já existe)
// NivelDeAcesso passa a implementar IAuditavel (NomeParaAuditoria = "RH", "Colaborador" ou "Gestor"; RegistroAfetado = "Nível de acesso {nome}")
static class NomesDeNivel { static string DoPerfil(Perfil p); }    // RH => "RH"; Funcionario => "Funcionario"; Gestor => "Funcionario Gestor" (inverso de PerfilPorNome)
// RHSystem.Domain.Beneficios
enum StatusDoPedidoDeBeneficio { Pendente, Aprovado, Recusado, Cancelado }   // banco: PENDENTE, APROVADO, RECUSADO, CANCELADO (V001)
class BeneficioDoFuncionario { int Id; int FuncionarioId; int BeneficioId; Beneficio? Beneficio; bool Ativo; StatusDoPedidoDeBeneficio Status;
  static BeneficioDoFuncionario Conceder(int funcionarioId, int beneficioId);   // linha NOVA: Ativo, Aprovado
  void Reconceder();                    // linha que JÁ EXISTE (PENDENTE, RECUSADO, CANCELADO ou APROVADO desativada): Status = Aprovado, Ativo = true; a chave única (ID_FUNCIONARIO, ID_BENEFICIO) impede inserir outra (decisão 10)
  void Desativar(); void Reativar(); }                                           // Reativar: InvalidOperationException se Status != Aprovado
```

- Produces (Infrastructure): `AppDbContext` ganha `DbSet<FuncionarioDetalhes> DetalhesDosFuncionarios` e `DbSet<BeneficioDoFuncionario> BeneficiosDosFuncionarios`.
- Produces (testes): `CpfsDeTeste.Valido(int n)` (CPF com dígitos verificadores corretos a partir de `n`, de 1 a 999999999) e `SementeDoCadastro` (por SQL, sobre o `ElencoDeTeste`): `CargoAsync(string nome, decimal cargaSemanal) -> int`, `BeneficioAsync(string nome) -> int`, `DetalhesAsync(int funcionarioId, string cpf, string? telefone = "11987654410")`, `DefinirTipoDeContratoAsync(int funcionarioId, string texto)`, `DefinirSenhaAsync(PessoaDeTeste p, string senha)` (hash do `ISenhaHasher` real).

**Cuidados de mapeamento** (armadilhas de `LEIA-ME.md` e da decisão 12; cada linha tem teste):

| Coluna | Como mapear | Por quê |
|---|---|---|
| `BENEFICIOS_FUNCIONARIOS.BENEFICIO_ATIVO` (padrão `TRUE` no grupo) e `STATUS_PEDIDO` (sem padrão desde o `V001`) | `ValueGenerated.Never`, **sem** declarar padrão; `STATUS_PEDIDO` com `EnumComoTextoMaiusculo` | o EF tem de enviar sempre o valor (nota «(antigo V021)» do `V001`); o `CHECK` recusa ativo sem `APROVADO` (`CHK_BENEFICIOS_FUNCIONARIOS_ATIVO_SO_APROVADO` do `V001`) |
| Colunas do `V001` que a 1b não mapeia (`DATA_PEDIDO`, `AUTORIZA_DESCONTO_FOLHA`...) | ficam fora do modelo: aceitam `NULL` ou têm padrão | a fatia 7 as acrescenta à mesma entidade |
| `FUNCIONARIOS.DATA_NASCIMENTO` e `DATA_DESLIGAMENTO` (`DATE`) | `DateOnly?`, **sem** conversor de fuso (dia de calendário) | `CONVENCOES.md:37` |
| `FUNCIONARIOS.TIPO_CONTRATO` | `string?` | texto livre sem `CHECK` (nota «(antigo V006)» do `V001`) |
| `FUNCIONARIOS.ID_GESTOR` | chave estrangeira opcional para `Gestor` | `GESTORES.ID_GESTOR` |
| `FUNCIONARIOS_DETALHES.CPF`, `CEP`, `UF` (`CHAR`) | `HasColumnType("char(11)")`, `char(8)`, `char(2)`; relação 1:1 por `ID_FUNCIONARIO` | `Criacao_Tabelas:165-189` |
| `CARGOS.CARGA_HORARIA` | `decimal?` com `HasPrecision(5, 2)` (propriedade `Cargo.CargaSemanalHoras`) | `Criacao_Tabelas:62-70` |
| `SETORES.ID_GESTOR` | já mapeado pela 1a (`Setor.GestorId`); `Setor.DefinirGestor(int? idGestor)` só o grava | `Criacao_Tabelas:113-120`; a chave estrangeira é para `GESTORES.ID_GESTOR` |

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Domain.Tests
Cpf_Normalizar_tira_pontos_e_traco()                                          // "529.982.247-25" => "52998224725"
Cpf_EhValido_aceita_52998224725_e_11144477735_e_recusa_12345678900_11111111111_10_digitos_e_letras()
Funcionario_AtualizarCadastro_normaliza_o_email_e_grava_cargo_setor_gestor_e_nivel()
Funcionario_Desligar_grava_a_data_e_inativa_e_Readmitir_ativa_zera_a_data_e_troca_o_vinculo()
Funcionario_Readmitir_de_quem_esta_ativo_lanca_InvalidOperationException()
Funcionario_Situacao_e_Ativo_EmAdmissao_ou_Inativo_pelo_dia()                 // admissão 2026-10-08, hoje 2026-10-07 => EmAdmissao; hoje 2026-10-08 => Ativo
NomesDeNivel_DoPerfil_e_PerfilPorNome_sao_inversos_para_os_tres_perfis()
BeneficioDoFuncionario_Conceder_nasce_ativo_e_aprovado_e_Reativar_de_recusado_lanca()
BeneficioDoFuncionario_Reconceder_leva_PENDENTE_RECUSADO_e_CANCELADO_a_ativo_e_aprovado()       // decisão 10
Marcas_de_auditoria_da_1b()                                                   // DataDesligamento "Último dia de trabalho"; DataNascimento Sensivel; TipoContrato; NivelDeAcessoId; GestorId SEM [Auditar]; FuncionarioDetalhes sem [Auditado]
// Api.IntegrationTests/Persistencia/MapeamentoDaFatia1bTests (releia com OUTRO AppDbContext)
FuncionarioDetalhes_ida_e_volta_com_CPF_char_11_e_UF_em_maiusculas()
BeneficioDoFuncionario_grava_Ativo_true_e_false_e_Status_APROVADO_como_enviados()
Funcionario_grava_DataNascimento_DataDesligamento_TipoContrato_e_GestorId_e_o_gestor_do_setor_ida_e_volta()
Cargo_le_CargaSemanalHoras_44_00_como_decimal()
Setor_DefinirGestor_grava_e_limpa_o_gestor()
Trigger_recusa_gestor_igual_ao_funcionario_e_gestor_inativo_com_SQLSTATE_45000()   // documenta a rede de segurança: "Um funcionario nao pode ser gestor de si mesmo." e "O gestor informado e um funcionario inativo."
Toda_tabela_do_banco_esta_mapeada_ou_listada_em_tabelas_sem_mapeamento()           // da 1a: continua verde com as duas tabelas fora da lista
// Api.IntegrationTests/Auditoria/AuditoriaDoCadastroTests (DbContext do DI com o interceptor e o relógio falso; veio da Task 8.1 da 1a, onde o CargoId ainda não muda)
Alterar_cargo_do_funcionario_gera_Alterou_com_antes_e_depois_pelo_NOME_do_cargo()   // AtualizarCadastro troca o cargo: "Analista" -> "Analista pleno" (cargos de SementeDoCadastro.CargoAsync), descrição "Cargo de Bruno Lima"
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Colaboradores|FullyQualifiedName~NomesDeNivelTests|FullyQualifiedName~MapeamentoDaFatia1bTests|FullyQualifiedName~AuditoriaDoCadastroTests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as entidades, as configurações e os métodos acima; criar `CpfsDeTeste` e `SementeDoCadastro`; tirar as duas tabelas de `tabelas-sem-mapeamento.txt`. `Funcionario.Detalhes` é a navegação 1:1; as projeções das consultas trazem `f.NivelDeAcesso.Nome` e convertem para `Perfil` **em memória**.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end --filter "FullyQualifiedName~Persistencia|FullyQualifiedName~AtributosDeAuditoriaTests"` (a 1a continua verde); Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: mapeamento do cadastro de colaboradores (detalhes, beneficios, gestor)"`

---

### Task 2: Regras de leitura: situação de hoje, telefone mascarado e as fontes padrão

**Files:**
- Create (Application): `Colaboradores/SituacaoDeHoje.cs`, `Colaboradores/TelefoneMascarado.cs`, `Colaboradores/IFonteDeIndicadorDaFicha.cs`
- Create (Infrastructure): `Colaboradores/FonteDeFatosDoDiaSemRegistros.cs`, `Colaboradores/SituacaoDeHojeServico.cs`; modify `DependencyInjection.cs`
- Test: `tests/RHSystem.Application.Tests/Colaboradores/RegraDeSituacaoDeHojeTests.cs`, `PresencaContagensTests.cs`, `TelefoneMascaradoTests.cs`; `tests/RHSystem.Api.IntegrationTests/Colaboradores/SituacaoDeHojeServicoTests.cs`

**Interfaces:**
- Consumes: `IRelogio`, `FusoBrasil` (1a Task 4).
- Produces (`RHSystem.Application.Colaboradores`; **as fatias 3 e 7 reaproveitam estes tipos**):

```csharp
enum SituacaoHoje { SemExpediente, Ferias, Atestado, Trabalhando, SemRegistro, AindaNaoComecou }   // JSON: SEM_EXPEDIENTE, FERIAS, ATESTADO, TRABALHANDO, SEM_REGISTRO, AINDA_NAO_COMECOU
sealed record PresencaContagens(int Trabalhando, int Ferias, int Atestado, int SemRegistro, int AindaNaoComecou, int SemExpediente) {
  static PresencaContagens De(IEnumerable<SituacaoHoje> situacoes); int Total { get; } }          // Total não é propriedade do JSON: [JsonIgnore]
sealed record CalendarioDoDia(bool DiaComExpediente, TimeOnly? LimiteDeEntrada);    // limite = horário padrão de entrada + tolerância, hora local; null = empresa sem horário configurado
sealed record FatosDaPessoaNoDia(bool JaRegistrouEntrada, bool EmFerias, bool ComAtestadoDeDiaInteiro);
static class RegraDeSituacaoDeHoje { static SituacaoHoje Calcular(CalendarioDoDia calendario, FatosDaPessoaNoDia fatos, TimeOnly agoraLocal); }
interface IFonteDeFatosDoDia {          // quem sabe: ponto, atestado e feriado (fatia 3), férias (7), horário da empresa (1c); a fatia 3 substitui a versão padrão
  Task<CalendarioDoDia> ObterCalendarioAsync(DateOnly dia, CancellationToken ct);
  Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> ObterPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly dia, CancellationToken ct);   // quem não está no dicionário não tem nenhum fato
}
sealed record DiaDeHoje(DateOnly Dia, bool DiaUtil, IReadOnlyDictionary<int, SituacaoHoje> Situacoes);
interface ISituacaoDeHoje { Task<DiaDeHoje> ObterAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct); }   // hoje = FusoBrasil.Hoje(IRelogio.AgoraUtc); agora local = FusoBrasil.ParaLocal(...)
static class TelefoneMascarado { static string? Mascarar(string? telefone); static string? SoDigitos(string? telefone); }
enum IndicadorDaFicha { BancoDeHorasMinutos, FeriasDisponiveisDias, AdvertenciasQuantidade, TreinamentosPendentes }
interface IFonteDeIndicadorDaFicha { IndicadorDaFicha Indicador { get; } Task<int?> ObterAsync(int funcionarioId, CancellationToken ct); }   // o MESMO valor da rota de origem (contrato, tabela de `obterIndicadoresDoFuncionario`); 1b não registra nenhuma
interface IFonteDoProximoPagamento { Task<DateOnly?> ObterAsync(CancellationToken ct); }                                                  // fatia 7; 1b não registra
```

**Regra** (`CONVENCOES.md:94-97`): quem **já registrou entrada** é `TRABALHANDO`; sem entrada vale a primeira que se aplica: `SEM_EXPEDIENTE` (dia sem expediente), `FERIAS`, `ATESTADO` (só de dia inteiro), depois `SEM_REGISTRO` se `agoraLocal > LimiteDeEntrada` (**estritamente depois**), senão `AINDA_NAO_COMECOU`; **sem `LimiteDeEntrada` (null) ninguém está atrasado**: `AINDA_NAO_COMECOU`. A versão padrão da fonte: dia útil = segunda a sexta; `LimiteDeEntrada` = null; nenhuma pessoa com fato.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// RegraDeSituacaoDeHojeTests ([Theory] com a tabela abaixo)
Entrada_registrada_vence_tudo_inclusive_sabado_e_ferias()                       // TRABALHANDO
Sabado_domingo_ou_feriado_sem_entrada_e_SEM_EXPEDIENTE()
Ferias_vencem_atestado_e_atestado_de_dia_inteiro_e_ATESTADO()
Sem_entrada_as_09h42_com_limite_08h10_e_SEM_REGISTRO_e_as_08h10_ainda_e_AINDA_NAO_COMECOU_e_as_08h11_e_SEM_REGISTRO()   // o exemplo de ELENCO.md (entrada 08:00, tolerância 10)
Sem_limite_de_entrada_ninguem_esta_atrasado()                                   // null => AINDA_NAO_COMECOU
// PresencaContagensTests
De_conta_cada_situacao_e_a_soma_dos_seis_e_o_total()
// TelefoneMascaradoTests
Mascarar_11_digitos_da_DDD_o_primeiro_digito_e_os_4_ultimos()                   // "11987657783" => "(11) 9****-7783"
Mascarar_10_digitos_da_DDD_e_os_4_ultimos()                                     // "1133447783" => "(11) ****-7783"
Mascarar_aceita_telefone_ja_com_mascara_e_devolve_null_para_nulo_vazio_ou_tamanho_estranho()   // "(11) 98765-7783" => "(11) 9****-7783"; "123" => null
SoDigitos_tira_tudo_que_nao_e_digito()
// SituacaoDeHojeServicoTests (relógio falso; fonte falsa)
Quarta_2026_10_07_12h42Z_e_dia_util_e_sabado_2026_10_10_nao_e()
No_sabado_so_quem_tem_entrada_registrada_e_TRABALHANDO_e_diaUtil_e_false()
Fonte_padrao_deixa_todo_ativo_AINDA_NAO_COMECOU_num_dia_util_e_nunca_SEM_REGISTRO()          // decisão 1
Virada_do_dia_2026_10_08T02h30Z_ainda_e_quarta_07_10_em_Brasilia()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~RegraDeSituacaoDeHojeTests|FullyQualifiedName~PresencaContagensTests|FullyQualifiedName~TelefoneMascaradoTests|FullyQualifiedName~SituacaoDeHojeServicoTests"`
Expected: FAIL.

- [ ] **Step 3: Implementar** com as assinaturas e a regra acima; registrar `FonteDeFatosDoDiaSemRegistros` como `IFonteDeFatosDoDia` e `SituacaoDeHojeServico` como `ISituacaoDeHoje` (escopo da requisição). A fatia que substituir a fonte registra a dela **depois**, para valer.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: regra da situacao de hoje, telefone mascarado e fontes padrao"`

---

### Task 3: Erro com título e extensões do contrato (`ProblemaDeApiException`)

**Files:**
- Create: `src/RHSystem.Application/Comum/ProblemaDeApiException.cs`
- Modify: `src/RHSystem.Api/Configuracao/TratamentoDeErros.cs` (da fatia 0 e da 1a, Task 3)
- Test: `tests/RHSystem.Api.IntegrationTests/Erros/ProblemaDeApiTests.cs`

**Por quê:** as exceções da 1a fixam o `title` (`ErroDeNegocioException` é sempre "Operação recusada.") e não levam campos extras. O contrato precisa de **título próprio** (`LINK_INVALIDO_OU_VENCIDO`: "Link inválido ou vencido.", resposta `Erro422LinkDeSenhaInvalido`), de **campo extra** (`cadastroExistente` do 409 de CPF e e-mail, schema `ColaboradorDuplicadoProblema`; a fatia 2 também pede `linha`) e de **cabeçalho** (`Retry-After` do 429 de `trocarMinhaSenha`, Task 5).

**Interfaces:**
- Consumes: `TratamentoDeErros`, `TextosDeErro` (1a, Task 3).
- Produces:

```csharp
// RHSystem.Application.Comum
sealed class ProblemaDeApiException : Exception {
  ProblemaDeApiException(int status, string titulo, string detalhe, string? codigo = null, IReadOnlyDictionary<string, object?>? extensoes = null,
                         IReadOnlyDictionary<string, string>? cabecalhos = null);
  // a Api devolve application/problem+json com title, status, detail, traceId, codigo (se houver) e as extensões em camelCase; chave de extensão repetida de title/status/detail/traceId/codigo é ignorada;
  // `cabecalhos` vão para a resposta como estão (o 429 leva `Retry-After`, em segundos)
}
```

- [ ] **Step 1: Escrever os testes que falham** (rotas `/__teste/erros/problema*`, `AllowAnonymous`, `ExcludeFromDescription`)

```csharp
ProblemaDeApi_vira_o_status_title_detail_e_codigo_pedidos_com_application_problem_json()
ProblemaDeApi_leva_as_extensoes_como_objeto_em_camelCase()                  // { "cadastroExistente": { "funcionarioId": 12, "nome": "Bruno Lima", "ativo": false } }
ProblemaDeApi_nao_deixa_extensao_sobrescrever_title_status_detail_traceId_nem_codigo()
ProblemaDeApi_sempre_tem_traceId_e_nunca_stack_trace()
ProblemaDeApi_escreve_os_cabecalhos_pedidos_como_Retry_After_no_429()
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter ProblemaDeApiTests`; Expected: FAIL.
- [ ] **Step 3: Implementar** a exceção e o mapeamento no tratamento global.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end --filter ErrosPadraoTests` (a 1a continua verde); Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: erro de api com titulo e extensoes do contrato"`

---

### Task 4: Convite e redefinição: emissão do link (serviço, e-mails e login com senha provisória)

**Files:**
- Create (Application): `Acesso/IGeradorDeTokenDeSenha.cs`, `Acesso/HashProvisorio.cs`, `Acesso/IServicoDeConvite.cs`
- Create (Infrastructure): `Acesso/GeradorDeTokenDeSenha.cs`, `Acesso/ServicoDeConvite.cs`, `Emails/ConviteRenderizador.cs`, `Emails/RedefinicaoSenhaRenderizador.cs`
- Modify: `Domain/Autenticacao/UsuarioTokenSenha.cs` (`Usar`, `Invalidar`), `Application/Autenticacao/EntrarCasoDeUso.cs`, `Infrastructure/DependencyInjection.cs`, `Api/appsettings.json`
- Test: `tests/RHSystem.Application.Tests/Acesso/HashProvisorioTests.cs`, `Autenticacao/EntrarCasoDeUsoSenhaProvisoriaTests.cs`; `tests/RHSystem.Api.IntegrationTests/Acesso/GeradorDeTokenDeSenhaTests.cs`, `ServicoDeConviteTests.cs`, `RenderizadoresDeSenhaTests.cs`, `LoginComSenhaProvisoriaTests.cs`

**Interfaces:**
- Consumes: `IFilaDeEmails`, `EmailParaEnfileirar`, `IRenderizadorDeEmail`, `ModeloDeEmail`, `ChaveDeIdempotencia.Montar` (1a, Task 10); `IAuditor` (1a, Task 8); `IUnidadeDeTrabalho`, `IRelogio`, `ISenhaHasher` (fatia 0); `Usuario.Criar(f, hash, senhaProvisoria)`, `UsuarioTokenSenha.Criar`, `TipoDeTokenDeSenha` (1a, Task 1); `ErroDeNegocioException`, `NaoEncontradoException` (1a, Task 3).
- Produces:

```csharp
// RHSystem.Application.Acesso
sealed record TokenDeSenhaGerado(string Token, string Hash);
interface IGeradorDeTokenDeSenha { TokenDeSenhaGerado Novo(); string Hash(string token); }   // Token = 32 bytes aleatórios em 64 hexadecimais minúsculos; Hash = SHA-256 do TEXTO do token em 64 hexadecimais (o que o CHECK do V001 confere)
static class HashProvisorio { static string Gerar(); }   // >= 40 caracteres (o trigger de `USUARIOS`, no `V002`, exige), aleatório, primeiro byte 0xFF: nunca confere com senha alguma
sealed record ConviteEmitido(int FuncionarioId, int UsuarioId, bool LoginCriado, DateTime ExpiraEmUtc);
interface IServicoDeConvite {
  // EXIGE transação aberta (como IGeradorDeMatriculas). 1 a 500 pessoas. Trava as linhas de FUNCIONARIOS (em ordem de id), cria o login que falta
  // (SenhaProvisoria = true, hash provisório, ativo), invalida o convite em aberto e grava o novo (só o hash) em DUAS gravações (invalidar/criar logins, depois inserir tokens),
  // enfileira o e-mail CONVITE (carga = nome, token, expiraEmUtc) e registra o evento Acesso/Criou (login novo) ou Acesso/Alterou (reenvio) por IAuditor.Registrar, na mesma transação (nunca RegistrarEGravar*: decisão 14). NÃO ativa login desativado: recusa.
  // 404 se o id não existe; 422, nesta ordem (a do contrato, `enviarConviteDeAcesso`): FUNCIONARIO_INATIVO, FUNCIONARIO_SEM_EMAIL_CORPORATIVO, SENHA_JA_DEFINIDA (detalhes dos exemplos `inativo`, `semEmailCorporativo` e `senhaDefinida`)
  // e USUARIO_DESATIVADO (login que existe com ATIVO = false; o contrato não dá o texto: o `detail` está na decisão 3). Quem reativa o login antes é "Ativar" (Task 6) ou a readmissão (Task 10).
  Task<IReadOnlyList<ConviteEmitido>> EmitirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct);
  // Abre a própria transação. Nunca revela se o e-mail existe nem lança por ele. Só para colaborador ATIVO com login ATIVO. Dentro de Senha:IntervaloMinimoEntreRedefinicoesSegundos
  // do último pedido da pessoa não faz nada. Senão invalida o link anterior, grava o token REDEFINICAO, enfileira o e-mail REDEFINICAO_SENHA e registra o evento Acesso/Alterou por IAuditor.RegistrarComo (a pessoa como ator), tudo na mesma transação e com a linha da pessoa travada (FOR UPDATE): nunca RegistrarEGravar*, decisão 14.
  Task SolicitarRedefinicaoAsync(string emailNormalizado, CancellationToken ct);
}
// RHSystem.Domain.Autenticacao: UsuarioTokenSenha ganha  void Usar(DateTime agoraUtc);  void Invalidar(DateTime agoraUtc);   // Usar de quem já foi usado ou invalidado: InvalidOperationException
```

**Configuração nova** (padrões): `Senha:ConviteDias` (7), `Senha:RedefinicaoMinutos` (60), `Senha:IntervaloMinimoEntreRedefinicoesSegundos` (60). **Datas do token:** `UsuarioTokenSenha.Criar(..., agoraUtc, validade)` (1a) grava `CRIADO_EM` e `EXPIRA_EM` a partir de `IRelogio`; nunca o padrão do banco. **E-mails** (tipos sugeridos na nota «(antigo V035)» do `V001`): `CONVITE` e `REDEFINICAO_SENHA`; chave `ChaveDeIdempotencia.Montar(tipo, ("func", funcionarioId), ("token", idDoToken))`; `EmailParaEnfileirar.FuncionarioId` = a pessoa, `ReferenciaId` = id do token; `Carga` = `{ nome, token, expiraEmUtc }` (o token **só** na carga, que a fila apaga ao terminar). Renderizador: `IOptions` com `App:UrlDoFront`; link `{UrlDoFront}/definir-senha?token={token}` (`20-fatia-1:2363`); todo valor interpolado no `Html` passa por `HtmlEncode`. Assuntos: "Seu acesso ao RH-System" e "Redefinição de senha do RH-System"; o texto diz a validade (7 dias; 1 hora) e que o link é de uso único. `ModeloDeEmail`: área `Acesso`; descrição "E-mail de convite enviado" / "E-mail de redefinição de senha enviado"; registro afetado "Colaborador {nome}". **A descrição e o registro nunca levam o token.**

**`EntrarCasoDeUso`:** login com `SenhaProvisoria = true` é tratado como e-mail inexistente (verifica a senha contra o hash fictício, mesmo custo, e devolve `CredenciaisInvalidas`): o hash provisório nunca é verificado e o tempo não denuncia o estado. **A auditoria e a resposta são as da senha errada de usuário que existe** (1a, Task 8.2): o login com senha provisória grava o **mesmo evento** (`Acesso/AcessoNegado`, "Tentativa de login com senha incorreta", a pessoa como ator, no máximo **um por pessoa por minuto**, por `RegistrarEGravarComLimiteAsync`, depois de verificar a senha e fora de transação) e devolve **a mesma resposta 401 genérica**; o texto não revela que o convite está pendente.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests
HashProvisorio_tem_40_ou_mais_caracteres_difere_a_cada_chamada_e_comeca_pelo_byte_0xFF()
Entrar_com_login_de_senha_provisoria_devolve_CredenciaisInvalidas_e_ainda_chama_Verificar_do_hasher()   // contra enumeração por tempo
Entrar_com_senha_provisoria_e_senha_certa_para_o_hash_provisorio_nao_entra()
Entrar_com_login_de_senha_provisoria_registra_AcessoNegado_pelo_metodo_com_limite_de_1_minuto_e_devolve_a_mesma_falha_da_senha_errada()   // o mesmo evento e a mesma FalhaDeEntrada.CredenciaisInvalidas de "senha errada de usuário que existe" (1a, Task 8.2)
// LoginComSenhaProvisoriaTests (ApiFactory, relógio falso, convite emitido e não aceito)
Login_de_convite_nao_aceito_devolve_o_mesmo_401_da_senha_errada_e_grava_um_AcessoNegado_por_minuto_com_ip_e_sem_senha()   // 3 tentativas no mesmo minuto = 1 evento; o relógio avança 61 s e a quarta grava o segundo; status, title e detail iguais aos da senha errada de quem já definiu a senha
// Api.IntegrationTests/Acesso/GeradorDeTokenDeSenhaTests
Token_tem_64_hexadecimais_minusculos_e_dois_tokens_diferem()
Hash_e_o_SHA_256_do_texto_do_token_e_nao_e_o_proprio_token()
HashProvisorio_nunca_confere_no_SenhaHasher_real_e_Verificar_nao_lanca()     // prova a suposição do hash provisório
// ServicoDeConviteTests (banco real, relógio 2026-10-07T12:42:00Z, EnviadorDeEmailEmMemoria)
Emitir_cria_login_ativo_com_SenhaProvisoria_true_e_hash_provisorio()
Emitir_grava_so_o_hash_e_o_token_nao_aparece_em_nenhuma_coluna_do_banco_fora_da_CARGA_da_fila()   // varre USUARIOS_TOKENS_SENHA, USUARIOS, AUDITORIA_*; Review Focus 1
Emitir_enfileira_CONVITE_com_chave_ASCII_carga_com_o_token_e_expira_em_7_dias()                   // ExpiraEmUtc = 2026-10-14T12:42:00Z
Emitir_grava_CRIADO_EM_e_EXPIRA_EM_do_relogio_falso_e_nao_do_UTC_do_banco()                       // CriadoEm = 2026-10-07T12:42:00Z (relógio falso), sem violar o CHECK EXPIRA_EM > CRIADO_EM
Reemitir_invalida_o_convite_anterior_e_deixa_um_so_em_aberto()                                    // EM_ABERTO = 1 em exatamente uma linha
Dois_convites_simultaneos_para_a_mesma_pessoa_deixam_um_so_em_aberto_e_nenhum_lanca()             // 20 tarefas, Barrier; Review Focus 4
Emitir_para_inativo_sem_email_corporativo_e_com_senha_definida_devolve_os_tres_422_do_contrato()  // FUNCIONARIO_INATIVO, FUNCIONARIO_SEM_EMAIL_CORPORATIVO, SENHA_JA_DEFINIDA
Emitir_para_login_desativado_devolve_422_USUARIO_DESATIVADO_nao_reativa_o_login_e_nao_grava_token_nem_email()   // decisão 3; vale também no lote (o lote todo desfaz)
Emitir_para_id_inexistente_lanca_NaoEncontrado()
Emitir_500_pessoas_grava_500_logins_500_tokens_e_500_emails()                                     // limite do lote da fatia 2
Emitir_dentro_de_transacao_que_desfaz_nao_deixa_login_token_nem_email()                           // Review Focus 5
Auditoria_do_convite_e_Acesso_Criou_para_login_novo_e_Acesso_Alterou_para_reenvio_sem_token()
Redefinicao_de_ativo_com_login_cria_token_de_1_hora_e_enfileira_REDEFINICAO_SENHA()              // ExpiraEmUtc = 2026-10-07T13:42:00Z
Redefinicao_de_inexistente_inativo_ou_sem_login_ou_com_login_desativado_nao_grava_nada()
Redefinicao_repetida_em_menos_de_60_s_nao_cria_outro_token_nem_outro_email_e_depois_de_60_s_invalida_o_anterior()   // decisão 6
Redefinicao_normaliza_o_email_maiusculas_e_espacos()
Redefinicao_grava_Acesso_Alterou_com_a_pessoa_como_ator_e_sem_token()
// RenderizadoresDeSenhaTests
Convite_renderiza_assunto_link_com_o_token_validade_de_7_dias_e_auditoria_sem_token()
Redefinicao_renderiza_assunto_link_validade_de_1_hora_e_codifica_HTML_do_nome()                  // nome "<b>Ana</b>" sai codificado no Html
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter "FullyQualifiedName~HashProvisorioTests|FullyQualifiedName~EntrarCasoDeUsoSenhaProvisoriaTests|FullyQualifiedName~Acesso"`; Expected: FAIL.
- [ ] **Step 3: Implementar** as interfaces, o serviço (`FOR UPDATE` pela `FromSql` em `FUNCIONARIOS`, ordenado por id), os dois renderizadores (registrados por DI), a mudança em `EntrarCasoDeUso` e `UsuarioTokenSenha.Usar/Invalidar`; acrescentar `Senha:*` ao `appsettings.json`. `EnfileirarLoteAsync` para o lote.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end --filter "FullyQualifiedName~AutenticacaoEndpointsTests|FullyQualifiedName~Emails"` (regressão da fatia 0 e da fila); Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: convite e redefinicao de senha (emissao do link, hash provisorio)"`

---

### Task 5: Senha por link e troca de senha (`esqueci`, `validar-link`, `definir`, `POST /api/eu/senha`)

**Files:**
- Create (Application): `Acesso/AcessoDtos.cs` (`ConviteLinkInfo`, `ConviteLinkTipo`, os quatro comandos), `Acesso/SolicitarRedefinicaoDeSenhaCasoDeUso.cs`, `ValidarLinkDeSenhaCasoDeUso.cs`, `DefinirSenhaPeloLinkCasoDeUso.cs`, `TrocarMinhaSenhaCasoDeUso.cs`, `ILimiteDeSenhaAtual.cs`
- Create (Infrastructure): `Acesso/LimiteDeSenhaAtual.cs`; modify `DependencyInjection.cs`
- Create (Api): `Acesso/SenhaEndpoints.cs`; modify `Configuracao/LimiteDeTentativas.cs`, `Program.cs`, `appsettings.json`
- Modify: `Application/Autenticacao/Contratos.cs` (`IEmissorDeTokens.EmitirAcesso(Usuario u, string? hashDoRefreshDaSessao = null)`; `IRefreshTokenRepositorio.RevogarOutrosDoUsuarioAsync`), `Infrastructure/Autenticacao/EmissorDeTokensJwt.cs`, `RefreshTokenRepositorio.cs`, `Application/Autenticacao/EntrarCasoDeUso.cs`, `RenovarTokenCasoDeUso.cs`, `tests/.../Infra/ApiFactory.cs`, `tests/.../Contrato/ContratoDivergenciaTests.cs` (escopo), `contrato/STATUS.md`
- Test: `tests/RHSystem.Application.Tests/Acesso/SenhaCasosDeUsoTests.cs`; `tests/RHSystem.Api.IntegrationTests/Acesso/SenhaPublicaTests.cs`, `TrocarSenhaTests.cs`, `LimiteDeSenhaTests.cs`

**Interfaces:**
- Consumes: `IServicoDeConvite`, `IGeradorDeTokenDeSenha`, `HashProvisorio` (Task 4); `ISenhaHasher`, `IRefreshTokenRepositorio`, `IEmissorDeTokens`, `IUnidadeDeTrabalho` (fatia 0); `IAuditor` (1a); `ColetorDeErros`, `ErroDeNegocioException` (1a); `ProblemaDeApiException` (Task 3); `FamiliasDeRota.GrupoEu()`, `ProduzProblemas`.
- Produces (rotas; `operationId`, códigos e textos **do contrato**):
  - `POST /api/auth/senha/esqueci` (`solicitarRedefinicaoDeSenha`, anônima) → `202` (sem corpo) | `400` | `429` | `500`
  - `POST /api/auth/senha/validar-link` (`validarLinkDeSenha`, anônima) → `200 ConviteLinkInfo` | `400` | `422` | `429` | `500`
  - `POST /api/auth/senha/definir` (`definirSenhaPeloLink`, anônima) → `204` | `400` | `422` | `429` | `500`
  - `POST /api/eu/senha` (`trocarMinhaSenha`, `GrupoEu`) → `204` | `400` | `401` | `422` | `429` | `500`
- Produces (código):

```csharp
// RHSystem.Application.Acesso (tipos de borda: propriedades NÃO anuláveis, validadas em tempo de execução para dar 400 com `errors`, não erro de binding)
enum ConviteLinkTipo { Convite, Redefinicao }                      // JSON CONVITE, REDEFINICAO
sealed record ConviteLinkInfo(ConviteLinkTipo Tipo, string Nome, string EmailCorporativo, DateTime ExpiraEmUtc);
sealed record ConviteEsqueciSenhaComando(string Email);   sealed record ConviteLinkValidarComando(string Token);
sealed record ConviteDefinirSenhaComando(string Token, string NovaSenha);   sealed record SenhaTrocarComando(string SenhaAtual, string NovaSenha);
interface ILimiteDeSenhaAtual {            // só para `trocarMinhaSenha`; singleton em memória, por instância (a mesma limitação do limitador por IP da fatia 0); usa IRelogio
  TimeSpan? Bloqueio(int usuarioId);       // null = pode tentar; senão, quanto falta para o erro mais antigo da janela de 60 s sair (vira o `Retry-After`, em segundos, arredondado para cima)
  void RegistrarErro(int usuarioId); }     // anota um erro de senha atual; `Autenticacao:LimiteSenhaAtualPorMinuto` (5) erros nos últimos 60 s bloqueiam
// Application.Autenticacao (fatia 0) ganha:
//   IEmissorDeTokens.EmitirAcesso(Usuario u, string? hashDoRefreshDaSessao = null)   // com valor, o JWT leva a claim `rth` (decisão 2); Entrar e Renovar passam o hash do refresh que acabam de gerar
//   IRefreshTokenRepositorio.RevogarOutrosDoUsuarioAsync(int usuarioId, string? hashDaSessaoAtual, DateTime agoraUtc, CancellationToken ct)  -> int   // null revoga todos; ExecuteUpdate com // AUDITORIA-OK: tabela técnica, sem registro por decisão
```

**Regras:**
- **Senha** (`CONVITE`/`definir`/`trocar`): de 8 a 128 caracteres, **sem** exigir maiúscula, número ou símbolo (`20-fatia-1:3437`); fora disso 400 em `novaSenha`. Campo vazio ou nulo: 400 com `errors` por campo (`token`, `email`, `senhaAtual`...), texto "O {campo} é obrigatório."; o `title` e o `detail` do 400 são os de `TextosDeErro` da 1a, iguais ao `Erro400Validacao` do contrato ("Dados inválidos." e "Confira os campos indicados."); só o `entrar` usa "Preencha o e-mail e a senha." (`Erro400Login`).
- **Link válido** (`validar-link` e `definir`): o hash do token existe, `EM_ABERTO`, `EXPIRA_EM > agora` (**no instante exato é inválido**), o login está **ativo**, o colaborador está **ativo**, e, se o tipo é `CONVITE`, o login ainda tem `SenhaProvisoria`. Qualquer falha: **o mesmo** `ProblemaDeApiException(422, "Link inválido ou vencido.", "Este link não vale mais. Peça um novo ao RH ou use \"Esqueci minha senha\".", "LINK_INVALIDO_OU_VENCIDO")`.
- **`definir`**, numa transação: consome o link com `UPDATE ... SET USADO_EM = agora WHERE TOKEN_HASH = ? AND EM_ABERTO = 1 AND EXPIRA_EM > agora` e exige **uma linha afetada** (duas pessoas com o mesmo link: só uma conta 1; `// AUDITORIA-OK: tabela técnica, o evento é o de senha definida`); `Usuario.DefinirSenha(hash)` (zera a marca de provisória); **invalida os outros links em aberto** da pessoa; **revoga todos os refresh tokens** da pessoa; grava `Acesso/Alterou` "Senha definida (convite)" ou "(redefinição)" por `RegistrarComo` (a pessoa como ator; a requisição é anônima), **na mesma conexão e na mesma transação** do fato, no mesmo `SalvarAsync` (se o evento falhar, o fato desfaz; decisão 14). Devolve 204; **não** há login automático.
- **`esqueci`:** normaliza o e-mail; formato inválido ou vazio é 400 (não enumera nada); fora isso **sempre** `Results.Accepted()` com a mesma resposta; chama `SolicitarRedefinicaoAsync`.
- **`trocar`** (`IAtorAtual.UsuarioId`), nesta ordem: (1) 400 dos campos (não conta como tentativa); (2) `ILimiteDeSenhaAtual.Bloqueio(usuarioId)` diferente de `null` = **429** (abaixo), **antes** de conferir a senha; (3) senha atual errada = 422 `SENHA_ATUAL_INCORRETA` ("A senha atual está incorreta."), **conta um erro** (`RegistrarErro`), nada muda e o front não desloga; (4) nova igual à atual = 400 em `novaSenha` ("A nova senha precisa ser diferente da atual."; o contrato só declara 400, 422 e 429 aqui: `SenhaTrocarComando.novaSenha`, `:3452`); (5) sucesso: `DefinirSenha`, `RevogarOutrosDoUsuarioAsync(usuarioId, claim rth)`, `Registrar(Acesso/Alterou, "Senha trocada")`; token **sem** a claim `rth` (emitido antes desta fatia) revoga **todos**. **Limitação do `rth` (decisão 2, aceita, testada):** se o refresh da sessão já foi renovado por outra aba, o token de acesso desta aba ainda leva o `rth` do refresh antigo; `RevogarOutrosDoUsuarioAsync` mantém só esse (já gasto) e revoga o vigente, e a sessão dessa aba cai na próxima renovação. Raro (o token de acesso vale 15 minutos); o `README` o diz.
- **Limites.** (a) **Três rotas públicas** (`LimiteDeTentativas.cs`): política `senha`, `Autenticacao:LimiteSenhaPorMinuto` (5), partição **IP + rota** (cada rota tem balde próprio); 429 com `Retry-After` e os textos do contrato; o `ApiFactory` põe 1000 (como no login). (b) **`trocarMinhaSenha`** **não** passa por esse limitador: `ILimiteDeSenhaAtual` (`Autenticacao:LimiteSenhaAtualPorMinuto`, 5; o `ApiFactory` põe 1000 e o teste do limite sobe uma fábrica com 5) conta só os **erros de senha atual da pessoa** nos últimos 60 s. O 429 é `ProblemaDeApiException(429, "Muitas tentativas.", "Muitas tentativas. Aguarde um minuto e tente de novo.", cabecalhos: { "Retry-After": segundos })` (os textos do 429 do contrato, os mesmos de `TextosDeErro` da 1a; o CORS expõe `Retry-After`, fatia 0). A pessoa bloqueada recebe 429 **mesmo com a senha certa** (senão o limite não protegeria nada); a senha certa e o 400 não contam como tentativa.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests
Senha_de_7_ou_129_caracteres_e_vazia_dao_400_em_novaSenha_e_8_e_128_passam()
Trocar_para_a_mesma_senha_da_400_em_novaSenha()
// SenhaPublicaTests (ApiFactory, relógio 2026-10-07T12:42:00Z; emails por EmitirAsync/SolicitarRedefinicaoAsync + extração do token da carga da fila)
Esqueci_devolve_o_mesmo_202_vazio_para_email_existente_inexistente_inativo_e_sem_login()          // status, cabeçalhos relevantes e corpo iguais; Review Focus 1
Esqueci_de_inexistente_nao_enfileira_nada_e_o_de_existente_enfileira_um_REDEFINICAO_SENHA()
Esqueci_com_a_linha_da_pessoa_travada_pela_propria_transacao_responde_202_em_poucos_segundos_e_o_evento_sai_na_mesma_transacao()   // CONCORRÊNCIA e sonda da decisão 14: a transação do esqueci segura FOR UPDATE na linha da pessoa (Task 4) enquanto o evento é gravado; responde 202 em menos de 10 s (o limite do InnoDB é 50 s: com o evento em segunda conexão a chamada esperaria e viraria 500) e deixa o evento Acesso/Alterou com a pessoa como ator. Controle positivo, uma vez e descartável: com o evento gravado por segunda conexão (e a guarda da 1a desligada só nesse experimento) o teste tem de falhar por tempo; sem isso, "passou" admitiria "a trava nunca foi disputada"
Esqueci_simultaneo_da_mesma_pessoa_responde_202_em_todas_as_chamadas_em_poucos_segundos_grava_um_so_token_e_nunca_500()   // 20 tarefas, Barrier, IPs diferentes (o limite por IP não interfere): tudo termina em menos de 10 s, um token em aberto, um evento
Esqueci_com_email_vazio_ou_sem_formato_devolve_400_errors_email()                                  // title "Dados inválidos." e detail "Confira os campos indicados."
Validar_devolve_tipo_nome_email_e_expiracao_e_nao_gasta_o_link()                                   // chamar duas vezes: 200 e 200
Validar_link_inexistente_usado_invalidado_vencido_e_de_login_ou_colaborador_inativo_devolvem_o_mesmo_422_com_title_detail_e_codigo_do_contrato()
Link_de_convite_vence_em_7_dias_e_o_de_redefinicao_em_1_hora_e_no_instante_exato_ja_e_invalido()
Definir_com_convite_grava_hash_de_verdade_zera_SenhaProvisoria_e_entrar_funciona_com_a_senha_nova()
Definir_usa_o_link_uma_so_vez_e_o_segundo_uso_devolve_422()
Dois_usos_simultaneos_do_mesmo_link_dao_um_204_e_um_422_e_nenhum_500()                             // 20 tarefas, Barrier; Review Focus 1
Definir_por_redefinicao_revoga_todos_os_refresh_tokens_da_pessoa()
Definir_invalida_os_outros_links_em_aberto_da_pessoa()
Definir_de_convite_para_quem_ja_tinha_senha_definida_e_invalido()                                   // CONVITE com SenhaProvisoria false
Definir_com_campos_vazios_ou_senha_fora_de_8_a_128_devolve_400_com_errors_por_campo()
Definir_grava_Acesso_Alterou_com_a_pessoa_como_ator_e_nem_senha_nem_hash_nem_token_aparecem_no_banco_fora_de_USUARIOS_SENHA()
Se_o_evento_de_auditoria_falha_a_senha_nao_muda_e_o_link_continua_valendo()                         // IAuditor falso que lança; atomicidade (decisão 14)
// TrocarSenhaTests
Trocar_com_a_senha_atual_certa_devolve_204_e_o_login_com_a_nova_funciona_e_o_com_a_antiga_nao()
Trocar_com_a_senha_atual_errada_devolve_422_SENHA_ATUAL_INCORRETA_e_nada_muda()
Trocar_revoga_os_outros_refresh_tokens_e_mantem_o_da_sessao_atual()                                  // dois logins; o token de acesso do primeiro leva `rth`; o refresh do primeiro ainda renova, o do segundo não
Trocar_com_token_de_acesso_sem_a_claim_rth_revoga_todos_os_refresh_tokens()
Trocar_com_o_rth_de_um_refresh_ja_renovado_revoga_o_refresh_vigente_e_a_sessao_cai_na_proxima_renovacao()   // LIMITAÇÃO ACEITA (decisão 2): entrar (R1, acesso A1 com rth de R1); renovar com R1 (R2, A2); trocar com A1 devolve 204; renovar com R2 devolve 401
Jwt_do_entrar_e_do_renovar_leva_a_claim_rth_igual_ao_hash_do_refresh_emitido_junto()
Trocar_registra_Acesso_Alterou_sem_senha_nem_hash()
Anonimo_em_trocar_recebe_401_e_os_tres_perfis_passam()
// LimiteDeSenhaTests (limite 5; fábrica própria com `Autenticacao:LimiteSenhaPorMinuto` e `Autenticacao:LimiteSenhaAtualPorMinuto` em 5; relógio falso)
Sexta_chamada_do_mesmo_IP_a_cada_rota_de_senha_devolve_429_com_Retry_After_e_as_rotas_tem_baldes_separados()
Trocar_senha_com_5_senhas_atuais_erradas_em_um_minuto_devolve_429_na_sexta_com_Retry_After_mesmo_com_a_senha_certa()   // Retry-After = segundos até o erro mais antigo sair da janela; title "Muitas tentativas." e detail do contrato
Trocar_senha_so_conta_o_erro_de_senha_atual_da_propria_pessoa()          // 400, senha certa e erros de OUTRA pessoa não contam
Trocar_senha_volta_a_aceitar_60_segundos_depois_do_erro_mais_antigo()    // relógio falso avança
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter "FullyQualifiedName~Senha|FullyQualifiedName~TrocarSenhaTests"`; Expected: FAIL (404 nas rotas).
- [ ] **Step 3: Implementar** os casos de uso, as quatro rotas (`WithName(operationId)`, `Produces<T>`, `ProduzProblemas` com **exatamente** os códigos acima; as três públicas com `AllowAnonymous` por rota, a de troca em `GrupoEu()`), o limitador por IP das três rotas, `LimiteDeSenhaAtual` (singleton, registrado no `DependencyInjection`), `rth` no emissor, o método do repositório, `ApiFactory.ClienteComo(PessoaDeTeste p, string? hashDaSessao = null)`; acrescentar ao escopo de `ContratoDivergenciaTests` os `operationId` `solicitarRedefinicaoDeSenha`, `validarLinkDeSenha`, `definirSenhaPeloLink` e `trocarMinhaSenha` (agora, junto das rotas, nunca antes); `Autenticacao:LimiteSenhaPorMinuto` e `Autenticacao:LimiteSenhaAtualPorMinuto` ao `appsettings.json`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", quatro linhas com a situação `em andamento`: `POST /api/auth/senha/esqueci` (`solicitarRedefinicaoDeSenha`), `POST /api/auth/senha/validar-link` (`validarLinkDeSenha`), `POST /api/auth/senha/definir` (`definirSenhaPeloLink`) e `POST /api/eu/senha` (`trocarMinhaSenha`; observação "troca de senha com a sessão atual mantida; limitação do `rth` na decisão 2 do plano"). A linha da fatia 1 da tabela por fatia já está `em andamento` (1a).
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Senha|FullyQualifiedName~TrocarSenhaTests|FullyQualifiedName~AutenticacaoEndpointsTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~EmissorDeTokensJwtTests"`
Expected: PASS, inclusive a divergência nas quatro operações (o 429 de `trocarMinhaSenha` declarado), a matriz (as três públicas aceitam os quatro atores; a de troca aceita os três perfis e recusa anônimo) e o `STATUS`.

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: definir, redefinir e trocar a senha (links de uso unico e sessao atual)"`

---

### Task 6: Usuários do sistema (lista, convite, ativar e desativar)

**Files:**
- Create (Application): `Acesso/IConsultaDeUsuariosDoSistema.cs`, `Acesso/UsuarioSistemaDtos.cs`, `ListarUsuariosDoSistemaCasoDeUso.cs`, `EnviarConviteDeAcessoCasoDeUso.cs`, `AtivarUsuarioDoSistemaCasoDeUso.cs`, `DesativarUsuarioDoSistemaCasoDeUso.cs`
- Create (Infrastructure): `Acesso/ConsultaDeUsuariosDoSistema.cs`
- Create (Api): `Acesso/UsuariosDoSistemaEndpoints.cs`; modify `Program.cs`, `tests/.../Contrato/ContratoDivergenciaTests.cs`, `contrato/STATUS.md`
- Test: `tests/RHSystem.Api.IntegrationTests/Acesso/UsuariosDoSistemaTests.cs`

**Interfaces:**
- Consumes: `IServicoDeConvite` (Task 4); `Usuario.Inativar` (fatia 0) e `Usuario.Reativar` (Task 1); `ParametrosDeLista`, `RespostaPaginada<T>`, `BuscaLike`, `ToRespostaPaginadaAsync`, `MapaDeOrdenacao`, `EnumsDeConsulta` (1a, Task 4); `IAtorAtual` (1a, Task 5); `IRefreshTokenRepositorio.RevogarTodosDoUsuarioAsync` (fatia 0); `IAuditor`; `GrupoRh()`; `ProduzProblemas`.
- Produces:

```csharp
// RHSystem.Application.Acesso
enum UsuarioSistemaSituacao { SemConvite, ConviteEnviado, ConviteExpirado, SenhaDefinida }   // JSON SEM_CONVITE, CONVITE_ENVIADO, CONVITE_EXPIRADO, SENHA_DEFINIDA
sealed record UsuarioSistemaItem(int FuncionarioId, string Nome, string? EmailCorporativo, Perfil? Perfil, UsuarioSistemaSituacao Situacao, bool Ativo,
                                 DateTime? UltimoLoginEmUtc, DateTime? ConviteEnviadoEmUtc);
interface IConsultaDeUsuariosDoSistema {
  // linhas = colaboradores ATIVOS (inclui em admissão; inativos não aparecem), ordem: nome, desempate: id; busca no nome e no e-mail corporativo; `p` chega com OrdenarPor e Direcao nulos (a rota não os recebe)
  Task<RespostaPaginada<UsuarioSistemaItem>> ListarAsync(ParametrosDeLista p, UsuarioSistemaSituacao? situacao, bool? ativo, CancellationToken ct);
  Task<UsuarioSistemaItem?> ObterAsync(int funcionarioId, CancellationToken ct);    // qualquer colaborador que exista, ativo ou não (a ficha e as respostas de convite/ativar/desativar usam)
}
```

- Rotas (só `RH`; o `ordenarPor` e a `direcao` **não** são parâmetros destas rotas: a consulta tem um record próprio com `pagina`, `tamanhoPagina`, `busca`, `situacao` e `ativo`; `situacao` chega como texto e `EnumsDeConsulta`, da 1a, a converte; **não** se usa `[AsParameters]` de `ParametrosDeLista` aqui):
  - `GET /api/rh/usuarios` (`listarUsuariosDoSistema`) → `200` (`RespostaPaginada<UsuarioSistemaItem>`) | `400` | `401` | `403` | `500`
  - `POST /api/rh/usuarios/{funcionarioId}/convite` (`enviarConviteDeAcesso`) → `200 UsuarioSistemaItem` | `401` | `403` | `404` | `422` | `500`
  - `POST /api/rh/usuarios/{funcionarioId}/ativar` (`ativarUsuarioDoSistema`) → `200` | `401` | `403` | `404` | `422` | `500`
  - `POST /api/rh/usuarios/{funcionarioId}/desativar` (`desativarUsuarioDoSistema`) → `200` | `401` | `403` | `404` | `422` | `500`

**Regras:** `situacao` calculada, nunca gravada (nota «(antigo V032)» do `V001`): sem `USUARIOS` = `SEM_CONVITE`; `SenhaProvisoria = false` = `SENHA_DEFINIDA`; provisória com token `CONVITE` em aberto (`USADO_EM` e `INVALIDADO_EM` nulos) e `EXPIRA_EM > agora` = `CONVITE_ENVIADO`; provisória sem ele = `CONVITE_EXPIRADO`. `ativo` = `USUARIOS.ATIVO` (falso em `SEM_CONVITE`). `ultimoLoginEmUtc` = `USUARIOS.DATA_HORA_ULTIMO_LOGIN` (convertido); `conviteEnviadoEmUtc` = `CRIADO_EM` do **último** token `CONVITE`. O filtro por `situacao` e a projeção usam **a mesma expressão**. **`ativar`:** 404 se o colaborador não existe; 422 `SEM_USUARIO` ("Esta pessoa ainda não tem login. Envie o convite primeiro.") sem login; 422 `FUNCIONARIO_INATIVO` ("A pessoa escolhida está inativa.") se o colaborador está inativo; já liberado devolve o login como está; evento `Acesso/Alterou` "Acesso de {nome} liberado". **`desativar`:** 404; 422 `SEM_USUARIO`; 422 `NAO_PODE_DESATIVAR_A_SI_MESMO` ("Você não pode desativar o seu próprio login.") quando `funcionarioId` é o do ator; `Usuario.Inativar()`, **revoga todos os refresh tokens**; já desativado devolve como está; evento `Acesso/Alterou` "Acesso de {nome} bloqueado". **`convite`:** 404; abre a transação, chama `EmitirAsync([id])` (os 422 vêm dele, inclusive `USUARIO_DESATIVADO`: login desativado não recebe convite, o RH usa "Ativar" antes), devolve o item.

- [ ] **Step 1: Escrever os testes que falham** (cenário: `Rh`, `MembroA` sem login, `MembroB` com login de senha definida, `EmAdmissao` com convite enviado, `Inativo`; relógio fixo)

```csharp
Lista_so_colaboradores_ativos_e_em_admissao_com_a_situacao_e_o_total_do_menu()          // Inativo não aparece; EmAdmissao aparece
Situacao_de_cada_login_SEM_CONVITE_CONVITE_ENVIADO_CONVITE_EXPIRADO_e_SENHA_DEFINIDA()   // expirado: relógio + 7 dias e 1 s; reemitir volta a ENVIADO
Filtros_situacao_e_ativo_batem_com_a_situacao_mostrada_e_busca_procura_no_nome_e_no_email()
Lista_ordena_por_nome_com_desempate_por_id_e_paginacao_alem_do_fim_devolve_vazia_com_total_certo()
Situacao_desconhecida_na_consulta_devolve_400()                                          // situacao=XYZ
Convite_cria_login_e_devolve_o_item_CONVITE_ENVIADO_com_conviteEnviadoEmUtc_do_relogio()
Convite_para_inativo_sem_email_ou_com_senha_definida_devolve_os_422_e_para_id_inexistente_404()
Convite_para_login_desativado_devolve_422_USUARIO_DESATIVADO_e_depois_de_Ativar_o_convite_passa()   // decisão 3
Ativar_sem_login_422_SEM_USUARIO_de_inativo_422_FUNCIONARIO_INATIVO_ja_ativo_devolve_como_esta_e_inexistente_404()
Desativar_bloqueia_o_login_revoga_os_refresh_tokens_e_o_login_com_senha_certa_vira_403_Acesso_desativado()
Desativar_o_proprio_login_devolve_422_NAO_PODE_DESATIVAR_A_SI_MESMO_e_sem_login_422_SEM_USUARIO()
Ativar_e_desativar_gravam_Acesso_Alterou_com_o_ator_RH()
Gestor_e_Funcionario_recebem_403_nas_quatro_rotas_e_anonimo_401()
Respostas_batem_com_o_schema_do_contrato()                                                // AfirmarConformeAoContratoAsync nas quatro
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter UsuariosDoSistemaTests`; Expected: FAIL (404 nas rotas).
- [ ] **Step 3: Implementar** consulta, casos de uso e as quatro rotas (`GrupoRh()`, `WithName`, `Produces<T>`, `ProduzProblemas` com os códigos acima); acrescentar ao escopo de `ContratoDivergenciaTests` os `operationId` `listarUsuariosDoSistema`, `enviarConviteDeAcesso`, `ativarUsuarioDoSistema` e `desativarUsuarioDoSistema` (agora, junto das rotas, nunca antes).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", quatro linhas com a situação `em andamento` (`GET /api/rh/usuarios`, `POST /api/rh/usuarios/{funcionarioId}/convite`, `.../ativar`, `.../desativar`, cada uma com o seu `operationId`).
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~UsuariosDoSistemaTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS, inclusive a divergência nas quatro operações, a matriz (as quatro só aceitam `RH`) e o `STATUS`.

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: usuarios do sistema (lista, convite, ativar e desativar)"`

---

### Task 7: Leitura do cadastro pelo RH (lista, ficha e indicadores)

**Files:**
- Create (Application): `Colaboradores/FuncionarioDtos.cs`, `IConsultaDeFuncionarios.cs`, `ListarFuncionariosCasoDeUso.cs`, `ObterFichaDoFuncionarioCasoDeUso.cs`, `ObterIndicadoresDoFuncionarioCasoDeUso.cs`
- Create (Infrastructure): `Colaboradores/ConsultaDeFuncionarios.cs`
- Create (Api): `Colaboradores/ColaboradoresEndpoints.cs`; modify `Program.cs`, `tests/.../Contrato/ContratoDivergenciaTests.cs`, `contrato/STATUS.md`
- Test: `tests/RHSystem.Api.IntegrationTests/Colaboradores/ListaDeFuncionariosTests.cs`, `FichaDoFuncionarioTests.cs`, `IndicadoresDaFichaTests.cs`

**Interfaces:**
- Consumes: `ISituacaoDeHoje`, `IFonteDeIndicadorDaFicha`, `IFonteDoProximoPagamento`, `TelefoneMascarado` (Task 2); `IConsultaDeUsuariosDoSistema` (Task 6); `FuncionarioDetalhes`, `BeneficioDoFuncionario`, `Cargo.CargaSemanalHoras`, `TiposDeContrato`, `SituacaoCadastral` (Task 1); `ParametrosDeLista`, `RespostaPaginada<T>`, `MapaDeOrdenacao<T>`, `BuscaLike`, `ToRespostaPaginadaAsync`, `OuNaoEncontrado`, **`EnumsDeConsulta`** (1a, Task 4: liga o texto do contrato, como `EM_ADMISSAO`, ao enum; texto desconhecido vira 400 em `{campo}`, "Valor inválido."; **use a assinatura real da 1a**: esta fatia **não** a define); `GrupoRh()`, `ProduzProblemas`.
- Produces (`RHSystem.Application.Colaboradores`; nomes **dos schemas do contrato** (`20-fatia-1:2654-3047`), propriedades em PascalCase, JSON camelCase; os demais records desta seção seguem o schema homônimo):

```csharp
enum FuncionarioSituacao { Ativo, EmAdmissao, Inativo }          // JSON ATIVO, EM_ADMISSAO, INATIVO
enum FuncionarioDesligamentoSituacao { Agendado, Efetivado }
sealed record CargoReferencia(int Id, string Nome);  sealed record SetorReferencia(int Id, string Nome);  sealed record GestorReferencia(int GestorId, int FuncionarioId, string Nome);
sealed record FuncionarioEndereco(string? Cep, string? Logradouro, string? Numero, string? Complemento, string? Bairro, string? Cidade, string? Uf);
sealed record FuncionarioBeneficioItem(int BeneficioId, string Nome, int QuantidadeDependentes);        // dependentes: 0 até a fatia 7
sealed record FuncionarioDesligamento(DateOnly UltimoDiaDeTrabalho, FuncionarioDesligamentoSituacao Situacao);
sealed record FuncionarioAcesso(UsuarioSistemaSituacao Situacao, bool Ativo);
sealed record FuncionarioFichaRh(int Id, string? Matricula, string Nome, string? EmailCorporativo, string? Cpf, string? Telefone, DateOnly? DataNascimento, string? Rg, string? RgOrgaoEmissor,
    string? EmailPessoal, FuncionarioEndereco Endereco, CargoReferencia? Cargo, SetorReferencia? Setor, GestorReferencia? Gestor, TipoDeContrato? TipoContrato, decimal? JornadaSemanalHoras,
    DateOnly DataAdmissao, Perfil? Perfil, bool Ativo, FuncionarioSituacao Situacao, SituacaoHoje? SituacaoHoje, FuncionarioDesligamento? Desligamento,
    IReadOnlyList<FuncionarioBeneficioItem> Beneficios, FuncionarioAcesso Acesso);
sealed record FuncionarioListaItemRh(int Id, string Nome, string? EmailCorporativo, string? Cargo, string? Setor, DateOnly DataAdmissao, FuncionarioSituacao Situacao, SituacaoHoje? SituacaoHoje);
sealed record FuncionarioListaTotais(int Ativos, int EmAdmissao, int Inativos);
sealed record FuncionarioListaRespostaRh(IReadOnlyList<FuncionarioListaItemRh> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, FuncionarioListaTotais Totais)
    : RespostaPaginada<FuncionarioListaItemRh>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);
sealed record FuncionarioIndicadoresRh(int? BancoDeHorasMinutos, int? FeriasDisponiveisDias, int? AdvertenciasQuantidade, DateOnly? ProximoPagamento);
interface IConsultaDeFuncionarios {
  Task<FuncionarioListaRespostaRh> ListarAsync(ParametrosDeLista p, FuncionarioSituacao situacao, int? setorId, TipoDeContrato? tipoContrato, CancellationToken ct);
  Task<FuncionarioFichaRh?> ObterFichaRhAsync(int funcionarioId, CancellationToken ct);       // qualquer colaborador, ativo ou não
}
```

`GestorReferencia(int GestorId, int FuncionarioId, string Nome)` (acima) é **definido aqui, uma vez**: a 1c o consome (o schema `GestorReferencia` do contrato).

- Rotas (só `RH`):
  - `GET /api/rh/funcionarios` (`listarFuncionarios`) → `200 FuncionarioListaRespostaRh` | `400` | `401` | `403` | `500`; parâmetros `pagina`, `tamanhoPagina`, `busca`, `situacao` (padrão `ATIVO`), `setorId`, `tipoContrato`, `ordenarPor` (`NOME`, `DATA_ADMISSAO`; padrão `NOME`), `direcao`
  - `GET /api/rh/funcionarios/{funcionarioId}` (`obterFichaDoFuncionario`) → `200 FuncionarioFichaRh` | `401` | `403` | `404` | `500`
  - `GET /api/rh/funcionarios/{funcionarioId}/indicadores` (`obterIndicadoresDoFuncionario`) → `200 FuncionarioIndicadoresRh` | `401` | `403` | `404` | `500`

**Regras** (`20-fatia-1:30-56`): `situacao` de cada linha: `INATIVO` = `Ativo` falso; `EM_ADMISSAO` = ativo com `DataAdmissao > hoje` (**hoje em `-03:00`**); `ATIVO` = o resto (quem tem desligamento só **agendado** ainda é `ATIVO`). `situacaoHoje` (via `ISituacaoDeHoje`, só dos ids da página) vem `null` se `situacao != ATIVO`. **Busca:** nome (parte, sem maiúsculas e sem acentos, `BuscaLike`); se o termo só tem dígitos e `.-/` e espaço, procura também no CPF (só os dígitos contam) e na matrícula (contém: `482` acha `00482`); termo com letras procura **só no nome**. **Totais** da empresa inteira, ignoram todos os filtros. Ordem: `NOME` (padrão) ou `DATA_ADMISSAO`, desempate por id. **Ficha:** `gestor` vem de `FUNCIONARIOS.ID_GESTOR` (`null` se o setor não tem gestor ou a pessoa é o gestor); `jornadaSemanalHoras` = `Cargo.CargaSemanalHoras`; `beneficios` = linhas **ativas e aprovadas**, por id; `acesso` = `IConsultaDeUsuariosDoSistema.ObterAsync`; `perfil` = `null` se não há nível; quem foi criado antes da fatia 1 (admin do seed) sem dados pessoais devolve `cpf`, `telefone` etc. `null` e `endereco` com os sete campos `null`. **Indicadores:** cada campo é o primeiro valor das `IFonteDeIndicadorDaFicha` registradas para ele (e `IFonteDoProximoPagamento`); **sem fonte, `null`**; 404 se o colaborador não existe.

- [ ] **Step 1: Escrever os testes que falham** (`ApiFactory` com o elenco, `SementeDoCadastro`; relógio `2026-10-07T12:42:00Z`)

```csharp
// ListaDeFuncionariosTests
Lista_padrao_e_ATIVO_ordenada_por_nome_com_pagina_1_e_tamanho_20_e_o_total_respeita_os_filtros()
Totais_ignoram_todos_os_filtros_e_contam_ativos_em_admissao_e_inativos_da_empresa_inteira()
Situacao_EM_ADMISSAO_chega_com_sublinhado_e_valor_desconhecido_devolve_400()                        // prova a suposição de binding de enum
Quem_tem_desligamento_so_agendado_continua_ATIVO_e_inativo_aparece_so_na_aba_INATIVO()
Admissao_em_08_10_vista_as_22h_de_07_10_em_Brasilia_e_EM_ADMISSAO_e_vira_ATIVO_as_00h_de_08_10()    // relógio 2026-10-08T01:00Z => EM_ADMISSAO; 03:00Z => ATIVO (Review Focus 2)
Busca_por_nome_ignora_maiusculas_e_acentos_e_trata_percentual_como_literal()
Busca_por_CPF_com_ou_sem_mascara_e_por_matricula_com_ou_sem_zeros_a_esquerda()                      // "529.982.247-25", "52998224725", "00482", "482"
Busca_com_letras_e_numeros_procura_so_no_nome()
Filtra_por_setorId_e_tipoContrato_e_combina_com_a_busca_e_a_situacao()
OrdenarPor_DATA_ADMISSAO_com_direcao_e_desempate_nao_repete_nem_pula_item_entre_paginas_e_ordenarPor_desconhecido_devolve_400()
SituacaoHoje_so_vem_para_ATIVO_e_null_para_EM_ADMISSAO_e_INATIVO()
Tipo_de_contrato_fora_de_CLT_e_ESTAGIO_no_banco_sai_null_e_nao_quebra_a_lista()                     // decisão 9
// FichaDoFuncionarioTests
Ficha_traz_cpf_e_telefone_completos_endereco_cargo_setor_gestor_jornada_beneficios_e_acesso()
Ficha_do_gestor_do_setor_tem_gestor_null_e_a_de_setor_sem_gestor_tambem()
Ficha_do_admin_do_seed_devolve_cpf_telefone_e_endereco_nulos_cargo_setor_e_matricula_null()          // `endereco` presente com sete nulos
Ficha_de_inativo_mostra_ativo_false_situacao_INATIVO_e_desligamento_EFETIVADO()
Ficha_de_id_inexistente_devolve_404_e_de_outro_perfil_403_e_anonimo_401()
Respostas_da_lista_e_da_ficha_batem_com_o_schema_do_contrato()
// IndicadoresDaFichaTests
Indicadores_sem_nenhuma_fonte_devolvem_os_quatro_campos_null()
Indicadores_usam_o_valor_das_fontes_registradas_e_a_data_do_proximo_pagamento()                      // fontes falsas registradas pelo teste
Indicadores_de_id_inexistente_devolvem_404_e_o_gestor_recebe_403()
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter "FullyQualifiedName~ListaDeFuncionariosTests|FullyQualifiedName~FichaDoFuncionarioTests|FullyQualifiedName~IndicadoresDaFichaTests"`; Expected: FAIL.
- [ ] **Step 3: Implementar** DTOs, consulta, casos de uso e as três rotas (cada uma com o record de consulta próprio e `[AsParameters]`: `listarFuncionarios` declara `pagina`, `tamanhoPagina`, `busca`, `ordenarPor` e `direcao`, e mais os filtros dela; `situacao`, `tipoContrato` e `ordenarPor` passam por `EnumsDeConsulta` da 1a; `WithName`, `Produces<T>`, `ProduzProblemas` com os códigos acima); acrescentar ao escopo de `ContratoDivergenciaTests` **só** os `operationId` desta tarefa, `listarFuncionarios`, `obterFichaDoFuncionario` e `obterIndicadoresDoFuncionario`, agora, junto das rotas (`criarFuncionario` e `editarFuncionario`, que dividem o caminho `/api/rh/funcionarios` com a lista, entram nas Tasks 8 e 8b).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", três linhas com a situação `em andamento` (cada uma com o seu `operationId`) e a observação **"parcial: `situacaoHoje` só conhece fim de semana até a fatia 3; indicadores `null` até as fatias 3, 4 e 7; sem benefícios de dependentes até a 7"**.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Colaboradores|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS, inclusive a divergência nas três operações, a matriz e o `STATUS`.

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: lista, ficha e indicadores de colaboradores para o RH"`

---

### Task 8: Cadastrar colaboradores (o caminho único de criação)

**Files:**
- Create (Application): `Colaboradores/ICadastroDeFuncionarios.cs`, `IGestorDoFuncionario.cs`, `IPassosDepoisDeSalvar.cs` (onboarding e formulário), `CriarFuncionarioCasoDeUso.cs`
- Create (Infrastructure): `Colaboradores/CadastroDeFuncionarios.cs`, `GestorDoFuncionario.cs`, `OnboardingIndisponivel.cs`, `FormularioDeAdmissaoIndisponivel.cs`; modify `DependencyInjection.cs`
- Modify (Api): `Colaboradores/ColaboradoresEndpoints.cs` (o `POST`), `tests/.../Contrato/ContratoDivergenciaTests.cs` (acrescenta `criarFuncionario`), `contrato/STATUS.md`
- Test: `tests/RHSystem.Application.Tests/Colaboradores/ValidacaoDoCadastroTests.cs`; `tests/RHSystem.Api.IntegrationTests/Colaboradores/CadastroDeFuncionariosTests.cs`, `CriarFuncionarioTests.cs`, `GestorDoFuncionarioTests.cs`

**Interfaces:**
- Consumes: `IServicoDeConvite` (Task 4); `ProblemaDeApiException` (Task 3); `IConsultaDeFuncionarios` (Task 7); `IGeradorDeMatriculas` (1a, Task 2); `IAuditor` (1a); `Cpf`, `NomesDeNivel`, `FuncionarioDetalhes`, `BeneficioDoFuncionario` (Task 1); `ColetorDeErros`, `ErroDeNegocioException` (1a); `IUnidadeDeTrabalho`.
- Produces (**a fatia 2 reaproveita**; a edição, Task 8b, também):

```csharp
// RHSystem.Application.Colaboradores
sealed record NovoFuncionario(string Nome, string Cpf, DateOnly DataNascimento, string Telefone, string EmailCorporativo, DateOnly DataAdmissao,
    TipoDeContrato TipoContrato, int CargoId, int SetorId, Perfil Perfil, IReadOnlyList<int> BeneficioIds);   // CPF e telefone SÓ com dígitos (quem chama usa Cpf.Normalizar e TelefoneMascarado.SoDigitos)
sealed record OpcoesDoCadastro(bool EnviarConvite, bool IncluirNoOnboarding, int? FormularioAdmissaoId);
enum TipoDeProblemaDeCadastro { Campo, Duplicado, Regra }                 // 400, 409, 422
sealed record CadastroExistente(int FuncionarioId, string Nome, bool Ativo);
sealed record ProblemaDeCadastro(int Indice, TipoDeProblemaDeCadastro Tipo, string Campo, string Codigo, string Mensagem, CadastroExistente? Existente = null, int? OutroIndice = null);
sealed record FuncionarioCadastrado(int Id, string Matricula);
sealed record ResultadoDoCadastro(IReadOnlyList<FuncionarioCadastrado> Cadastrados, int ConvitesEnviados, int IncluidosNoOnboarding, int IncluidosNoFormularioDeAdmissao);   // "enviados" = na fila
sealed class CadastroInvalidoException(IReadOnlyList<ProblemaDeCadastro> problemas) : Exception { IReadOnlyList<ProblemaDeCadastro> Problemas { get; } }   // nada foi gravado
sealed class LinhaRecusadaException(int indice, bool porDuplicidade, string? regraDoBanco) : Exception { int Indice; bool PorDuplicidade; string? RegraDoBanco; }  // o banco recusou a linha `indice` durante a gravação (a transação do chamador deve desfazer)
interface ICadastroDeFuncionarios {
  // SEM gravar. As MESMAS regras do cadastro, em lote (a prévia da importação usa).
  Task<IReadOnlyList<ProblemaDeCadastro>> ValidarAsync(IReadOnlyList<NovoFuncionario> novos, CancellationToken ct);
  // EXIGE transação aberta (como IGeradorDeMatriculas); 1 a 500; NÃO abre nem confirma transação. Valida tudo (CadastroInvalidoException) e confere as caixas ANTES de gravar;
  // reserva as matrículas em bloco, grava linha por linha (assim o banco aponta a linha: LinhaRecusadaException), depois convites, onboarding e formulário. O chamador da importação envolve com IAuditor.SuspenderRegistroAutomatico().
  Task<ResultadoDoCadastro> CadastrarAsync(IReadOnlyList<NovoFuncionario> novos, OpcoesDoCadastro opcoes, CancellationToken ct);
}
// ESTA FATIA É A DONA de IGestorDoFuncionario; a 1c só chama RefazerDosMembrosAsync (em criarSetor e editarSetor), não o reimplementa
sealed record ResolucaoDoGestor(int? GestorId, bool GestorInativo, string? NomeDoGestor);
interface IGestorDoFuncionario {
  Task<ResolucaoDoGestor> ResolverAsync(int setorId, int? funcionarioId, CancellationToken ct);   // o ID_GESTOR que a pessoa deve ter: o gestor do setor; null se o setor não tem gestor OU o gestor é a própria pessoa
  Task<int> RefazerDosMembrosAsync(int setorId, CancellationToken ct);                            // PARA A 1c: refaz o ID_GESTOR dos membros ATIVOS do setor (o próprio gestor fica null); devolve quantos mudaram; 422 GESTOR_DO_SETOR_INATIVO se o gestor novo está inativo.
                                                                                                    // Lê o gestor do setor NO BANCO: quem chama grava o setor antes (SaveChanges), na mesma transação
}
// Passos que dependem das fatias 5 e 6; a versão padrão é "indisponível" (decisão 8); as fatias dona substituem
interface IInclusaoNoOnboarding { Task ConferirAsync(CancellationToken ct);                                  // sem treinamento em vigor: 422 SEM_TREINAMENTO_DE_ONBOARDING
                                  Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct); }          // quantos foram incluídos (mesmo efeito de incluirFuncionarioNoOnboarding, fatia 6)
interface IEnvioDoFormularioDeAdmissao { Task ConferirAsync(int formularioId, CancellationToken ct);         // 400 em formularioAdmissaoId; 422 FORMULARIO_NAO_E_POR_PESSOAS ou FORMULARIO_ENCERRADO
                                         Task<int> AdicionarDestinatariosAsync(int formularioId, IReadOnlyList<int> funcionarioIds, CancellationToken ct); }   // mesmo efeito de adicionarDestinatariosFormulario (fatia 5)
```

- Produces (comando de borda, schema `FuncionarioCriarComando`; opcionais com o padrão do contrato):

```csharp
sealed record FuncionarioCriarComando(string Nome, string Cpf, DateOnly DataNascimento, string Telefone, string EmailCorporativo, DateOnly DataAdmissao, TipoDeContrato TipoContrato,
    int CargoId, int SetorId, Perfil Perfil, IReadOnlyList<int>? BeneficioIds = null, bool EnviarConvite = true, bool IncluirNoOnboarding = false, int? FormularioAdmissaoId = null);
```

- Rota (só `RH`): `POST /api/rh/funcionarios` (`criarFuncionario`) → `201 FuncionarioFichaRh` | `400` | `401` | `403` | `409` (CPF ou e-mail: `ColaboradorDuplicadoProblema`) | `422` | `500`; corpo `FuncionarioCriarComando` (`beneficioIds`, `enviarConvite` padrão `true`, `incluirNoOnboarding` `false`, `formularioAdmissaoId` `null`: **opcionais**)

**Regras de validação** (ordem fixa: **400** coletados por campo, depois **409**, depois **422**; a edição, Task 8b, usa as mesmas e acrescenta as dela):
- **400** (`errors` por campo, camelCase): `nome` 1 a 150 ("O nome é obrigatório."); `cpf` 11 dígitos com dígitos verificadores ("CPF inválido."); `dataNascimento` não futura ("A data de nascimento não pode ser no futuro."); `telefone` 10 ou 11 dígitos ("Informe o telefone com DDD."); `emailCorporativo` com formato e até 150; `cargoId`, `setorId` e `beneficioIds` **existem** ("O cargo não existe." etc.); `dataAdmissao` obrigatória.
- **409** `CPF_JA_CADASTRADO` / `EMAIL_JA_CADASTRADO` (CPF primeiro), título "Conflito.", detalhes do contrato (exemplos `cpf`, `email`, `cpfInativo` e `emailInativo` da resposta 409 de `criarFuncionario`: ativo "Já existe um colaborador com este CPF." / "...com este e-mail corporativo."; inativo "Este CPF pertence a um cadastro inativo ({nome}). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela." e o mesmo para o e-mail), extensão `cadastroExistente { funcionarioId, nome, ativo }` (`ProblemaDeApiException`). A comparação de e-mail é a do banco (sem maiúsculas e sem acentos). Duplicidade **dentro do lote** (`ValidarAsync`): `Duplicado` com `OutroIndice`.
- **422** `GESTOR_DO_SETOR_INATIVO` ("O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar."); `SEM_TREINAMENTO_DE_ONBOARDING`, `FORMULARIO_NAO_E_POR_PESSOAS`, `FORMULARIO_ENCERRADO` (pelas caixas). Trigger do banco que escapar: 422 com `regraDoBanco` pela tradução da 1a.
- **Efeitos do cadastro:** `Funcionario.Criar` + `AtualizarCadastro` (gestor = `ResolverAsync(setorId, null)`), `FuncionarioDetalhes`, `BeneficioDoFuncionario.Conceder` por id (pessoa nova: não há linha anterior; a regra de **atualizar a linha que já existe**, decisão 10, aparece na edição, Task 8b), matrícula reservada **no fim** (uma reserva do tamanho do lote), convite (`EmitirAsync`) se `EnviarConvite`; `201` com a ficha; **nenhum e-mail além do convite**.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests/ValidacaoDoCadastroTests (falsos em memória para a parte pura)
Validacao_coleta_todos_os_400_de_uma_vez_com_o_nome_do_campo_em_camelCase()
// CadastroDeFuncionariosTests (banco real; o serviço direto, dentro de ExecutarEmTransacaoAsync)
ValidarAsync_nao_grava_nada_e_acusa_CPF_invalido_e_cargo_inexistente_e_gestor_inativo_por_linha()
ValidarAsync_acusa_CPF_e_email_repetidos_no_lote_com_OutroIndice_e_os_ja_cadastrados_com_Existente()
Cadastrar_3_reserva_3_matriculas_consecutivas_de_5_digitos_e_grava_funcionario_detalhes_beneficios_e_gestor_do_setor()
Cadastrar_500_em_uma_transacao_reserva_500_matriculas_sem_buraco()
Linha_recusada_pelo_banco_desfaz_o_lote_inteiro_e_a_matricula_volta()                        // ULTIMO_VALOR = 0 por SQL com a matrícula 00003 já em uso: a 3a linha dá LinhaRecusada(indice 2, porDuplicidade)
Falha_na_caixa_de_onboarding_ou_de_formulario_desfaz_colaborador_dados_pessoais_beneficios_login_token_e_email()   // Review Focus 5; fakes que lançam depois das linhas salvas
Caixas_marcadas_sem_fonte_devolvem_o_422_SEM_TREINAMENTO_DE_ONBOARDING_e_o_400_em_formularioAdmissaoId_e_nada_e_gravado()   // decisão 8
Cadastrar_com_as_caixas_atendidas_por_fakes_nao_enfileira_outro_email_alem_do_convite()      // lista fechada
Dois_cadastros_simultaneos_do_mesmo_CPF_gravam_um_so_e_o_outro_recebe_409_sem_matricula_repetida()   // 20 tarefas, Barrier; Review Focus 4
// GestorDoFuncionarioTests
Resolver_devolve_o_gestor_do_setor_e_null_sem_gestor_e_null_quando_o_gestor_e_a_propria_pessoa_e_GestorInativo_true_quando_inativo()
RefazerDosMembros_atualiza_so_os_ativos_do_setor_o_gestor_novo_fica_com_null_e_inativos_nao_mudam()
// CriarFuncionarioTests (ApiFactory)
Criar_devolve_201_com_a_ficha_matricula_gerada_gestor_do_setor_beneficios_e_acesso_CONVITE_ENVIADO()
Criar_sem_convite_deixa_acesso_SEM_CONVITE_e_nao_enfileira_email()
Criar_com_CPF_ou_email_de_cadastro_ativo_devolve_409_com_cadastroExistente_ativo_true()
Criar_com_CPF_ou_email_de_cadastro_inativo_devolve_409_que_manda_usar_Readmitir_com_ativo_false()
Criar_com_gestor_do_setor_inativo_devolve_422_GESTOR_DO_SETOR_INATIVO_e_nada_e_gravado()
Criar_com_dataAdmissao_amanha_as_22h_de_Brasilia_fica_EM_ADMISSAO()
Criar_com_perfil_Gestor_e_com_perfil_RH_grava_o_nivel_certo()
Criar_grava_Colaboradores_Criou_pelo_interceptor_sem_CPF_e_o_convite_como_Acesso_Criou()
Criar_com_corpo_vazio_ou_enum_invalido_devolve_400_e_nome_de_150_e_151_caracteres_passam_e_falham()
Criar_com_perfil_errado_403_e_anonimo_401_e_a_resposta_bate_com_o_schema_do_contrato()   // 403 para Gestor e Funcionario, 401 para anônimo; AfirmarConformeAoContratoAsync
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter "FullyQualifiedName~CadastroDeFuncionariosTests|FullyQualifiedName~CriarFuncionarioTests|FullyQualifiedName~GestorDoFuncionarioTests|FullyQualifiedName~ValidacaoDoCadastroTests"`; Expected: FAIL.
- [ ] **Step 3: Implementar** o serviço (a validação é **uma só** e serve a `ValidarAsync` e a `CadastrarAsync`), `GestorDoFuncionario` (`RefazerDosMembrosAsync` com `ExecuteUpdate` e `// AUDITORIA-OK: o gestor do colaborador é derivado do setor; o evento é o da troca de gestor do setor (fatia 1c)`), as duas versões padrão das caixas, `CriarFuncionarioCasoDeUso` (abre `ExecutarEmTransacaoAsync`; os `ProblemaDeCadastro` viram 400, 409 e 422 do contrato; `LinhaRecusadaException` vira 409 genérico ou 422 com `regraDoBanco`) e a rota `POST`; acrescentar `criarFuncionario` ao escopo de `ContratoDivergenciaTests` (agora, junto da rota, nunca antes). **Registre as duas versões padrão no `DependencyInjection` antes de qualquer outra**, para as fatias 5 e 6 substituírem.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", uma linha (`POST /api/rh/funcionarios`, `criarFuncionario`), `em andamento`, observação **"as caixas 'incluir no onboarding' e 'formulário de admissão' recusam com o erro do contrato até as fatias 6 e 5"**.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Colaboradores|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~GeradorDeMatriculasTests"`
Expected: PASS.

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: cadastrar colaboradores (caminho unico de criacao)"`

---

### Task 8b: Editar colaboradores

**Files:**
- Create (Application): `Colaboradores/EditarFuncionarioCasoDeUso.cs`
- Modify: `Application/Colaboradores/ICadastroDeFuncionarios.cs` e `Infrastructure/Colaboradores/CadastroDeFuncionarios.cs` (acrescentam `ValidarEdicaoAsync`), `Api/Colaboradores/ColaboradoresEndpoints.cs` (o `PUT`), `tests/.../Contrato/ContratoDivergenciaTests.cs` (acrescenta `editarFuncionario`), `contrato/STATUS.md`
- Test: `tests/RHSystem.Api.IntegrationTests/Colaboradores/EditarFuncionarioTests.cs`

**Interfaces:**
- Consumes: `ICadastroDeFuncionarios`, `NovoFuncionario`, `ProblemaDeCadastro`, `IGestorDoFuncionario.ResolverAsync` (Task 8); `IConsultaDeFuncionarios.ObterFichaRhAsync` (Task 7); `BeneficioDoFuncionario` (`Conceder`, `Reconceder`, `Reativar`, `Desativar`), `FuncionarioDetalhes`, `Cpf`, `NomesDeNivel` (Task 1); `ProblemaDeApiException` (Task 3); `IGeradorDeMatriculas` (1a, Task 2); `IAuditor`, `IAtorAtual`, `ColetorDeErros`, `ErroDeNegocioException` (1a); `IUnidadeDeTrabalho`.
- Produces:

```csharp
// RHSystem.Application.Colaboradores: ICadastroDeFuncionarios ganha (implementada no mesmo serviço, com a MESMA validação da Task 8)
// SEM gravar. Mesmas regras de campo, de existência (cargo, setor, benefícios) e de duplicidade de CPF e e-mail, sem contar o próprio colaborador (`funcionarioId`);
// o gestor do setor inativo só é problema se o setor mudou (o trigger só olha o ID_GESTOR quando ele muda: `Criacao_Triggers:98`). Não confere as caixas "depois de salvar".
Task<IReadOnlyList<ProblemaDeCadastro>> ValidarEdicaoAsync(int funcionarioId, NovoFuncionario dados, CancellationToken ct);
// comando de borda, schema `FuncionarioEditarComando` (substituição completa)
sealed record FuncionarioEditarComando(string Nome, string Cpf, DateOnly DataNascimento, string Telefone, string EmailCorporativo, string? EmailPessoal, string? Rg, string? RgOrgaoEmissor,
    FuncionarioEndereco Endereco, DateOnly DataAdmissao, TipoDeContrato TipoContrato, int CargoId, int SetorId, Perfil Perfil, IReadOnlyList<int> BeneficioIds);   // tudo sempre; os anuláveis aceitam null
```

- Rota (só `RH`): `PUT /api/rh/funcionarios/{funcionarioId}` (`editarFuncionario`) → `200 FuncionarioFichaRh` | `400` | `401` | `403` | `404` | `409` | `422` | `500`; corpo `FuncionarioEditarComando`

**Regras da edição** (as de validação da Task 8, na mesma ordem 400, 409, 422, mais estas):
- **400:** `emailPessoal` com formato e até 150; `endereco.uf` sigla válida; `endereco.cep` 8 dígitos; `dataAdmissao` não posterior ao último dia de trabalho vigente (agendado ou efetivado: o `CHECK CHK_FUNCIONARIOS_DATAS` daria 500).
- **409:** na edição o próprio colaborador não conta (`ValidarEdicaoAsync`).
- **422:** `GESTOR_DO_SETOR_INATIVO` só se o setor mudou; `NAO_PODE_ALTERAR_O_PROPRIO_PERFIL` ("Você não pode alterar o seu próprio nível de acesso."), `GESTOR_PRECISA_TER_PERFIL_GESTOR` ("{nome} gerencia o setor {setor} e precisa ter o nível Gestor. Tire-o do setor antes de mudar o nível.") e `FUNCIONARIO_INATIVO` ao **dar** benefício a quem está inativo ("Não é possível dar benefício a um colaborador inativo."). Trigger do banco que escapar: 422 com `regraDoBanco` pela tradução da 1a.
- **Efeitos:** troca de e-mail troca o login; **troca de setor refaz o gestor da pessoa** (só se o setor mudou; `ResolverAsync(novoSetorId, funcionarioId)`: a pessoa fica sem gestor se ela é o gestor do novo setor); quem não tinha `FuncionarioDetalhes` passa a ter; **quem não tem matrícula recebe a próxima** (decisão 4); dados pessoais que mudaram entram por `IAuditor.Registrar(Colaboradores/Alterou, "Dados pessoais de {nome}", mudanças com ValorOculto = true)` (decisão 5); o restante sai pelo interceptor.
- **Benefícios** (`beneficioIds` é o conjunto **ativo** desejado; decisão 10): para cada id desejado, **sem linha** = `BeneficioDoFuncionario.Conceder` (insere); linha `APROVADO` desativada = `Reativar`; linha `PENDENTE`, `RECUSADO` ou `CANCELADO` = **`Reconceder` na mesma linha** (nunca um segundo `INSERT`: a chave única `(ID_FUNCIONARIO, ID_BENEFICIO)` o recusaria); linha `APROVADO` ativa não muda. Para cada benefício ativo e `APROVADO` que **saiu** do conjunto = `Desativar`. Linha `PENDENTE`, `RECUSADO` ou `CANCELADO` de benefício **fora** do conjunto não é tocada. Os métodos de domínio é que gravam: nada de `ExecuteUpdate` por cima do interceptor.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// EditarFuncionarioTests
Editar_devolve_200_com_a_ficha_atualizada_e_troca_do_email_troca_o_login()
Editar_troca_de_setor_refaz_o_gestor_e_se_o_gestor_do_novo_setor_e_a_pessoa_ela_fica_sem_gestor()
Editar_beneficioIds_ativa_os_que_entram_e_desativa_so_os_APROVADO_ativos_que_saem()
Editar_dando_beneficio_que_ja_tem_linha_PENDENTE_RECUSADO_ou_CANCELADO_atualiza_a_mesma_linha_para_ativa_e_aprovada_sem_inserir_outra()   // [Theory] com os três status; o id da linha não muda e continua uma linha por (pessoa, benefício); decisão 10
Editar_nao_toca_em_linha_PENDENTE_RECUSADO_ou_CANCELADO_de_beneficio_que_fica_fora_do_conjunto()
Editar_dando_beneficio_com_linha_APROVADO_desativada_reativa_a_mesma_linha()
Editar_o_admin_do_seed_cria_os_dados_pessoais_e_recebe_a_proxima_matricula()                 // decisão 4
Editar_dados_pessoais_grava_evento_com_valorOculto_e_sem_antes_nem_depois_e_cargo_pelo_nome()   // "Cargo de Bruno Lima": "Analista" => "Analista pleno" (1a)
Editar_o_proprio_perfil_devolve_422_NAO_PODE_ALTERAR_O_PROPRIO_PERFIL()
Editar_o_perfil_de_quem_gerencia_setor_devolve_422_GESTOR_PRECISA_TER_PERFIL_GESTOR()
Editar_dataAdmissao_depois_do_ultimo_dia_de_trabalho_devolve_400_e_nao_500()
Editar_dar_beneficio_a_inativo_devolve_422_FUNCIONARIO_INATIVO()
Editar_com_CPF_de_outra_pessoa_devolve_409_e_com_o_proprio_CPF_passa()
Editar_inexistente_404_perfil_errado_403_e_anonimo_401()
Respostas_batem_com_o_schema_do_contrato()
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter FullyQualifiedName~EditarFuncionarioTests`; Expected: FAIL (404 na rota).
- [ ] **Step 3: Implementar** `ValidarEdicaoAsync` (a mesma validação da Task 8), `EditarFuncionarioCasoDeUso` (abre `ExecutarEmTransacaoAsync`; mesmos problemas de cadastro viram 400, 409 e 422) e a rota `PUT`; acrescentar `editarFuncionario` ao escopo de `ContratoDivergenciaTests` (agora, junto da rota, nunca antes).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", uma linha (`PUT /api/rh/funcionarios/{funcionarioId}`, `editarFuncionario`), `em andamento`.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Colaboradores|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~GeradorDeMatriculasTests"`
Expected: PASS.

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: editar colaboradores (beneficios, gestor do setor e dados pessoais)"`

---

### Task 9: Desligamento agendado, a tarefa que o efetiva e a limpeza de tokens de senha

**Files:**
- Create (Application): `Colaboradores/IDesligamentoDeFuncionarios.cs`, `InativarFuncionarioCasoDeUso.cs`, `CancelarDesligamentoCasoDeUso.cs`
- Create (Infrastructure): `Colaboradores/DesligamentoDeFuncionarios.cs`, `EfetivarDesligamentoTarefa.cs`, `Acesso/LimparTokensDeSenhaTarefa.cs`; modify `DependencyInjection.cs`, `Api/appsettings.json`
- Modify: `Domain/Colaboradores/DesligamentoAgendado.cs` (`Efetivar`, `Cancelar`, `Reverter`), `Api/Colaboradores/ColaboradoresEndpoints.cs`, `tests/.../Contrato/ContratoDivergenciaTests.cs`, `contrato/STATUS.md`
- Test: `tests/RHSystem.Domain.Tests/Colaboradores/DesligamentoAgendadoTests.cs`; `tests/RHSystem.Api.IntegrationTests/Colaboradores/DesligamentoDeFuncionariosTests.cs`, `InativarFuncionarioTests.cs`, `EfetivarDesligamentoTarefaTests.cs`, `LimparTokensDeSenhaTarefaTests.cs`

**Interfaces:**
- Consumes: `IAgendador`, `NovaTarefa`, `ITarefaAgendada`, `TarefaParaExecutar`, `IExecutorDeTarefas` (1a, Task 11); `ChaveDeIdempotencia.Montar` (1a); `FusoBrasil.InicioDoDiaEmUtc`, `Hoje`; `IAuditor`; `IConsultaDeFuncionarios` (Task 7); `DesligamentoAgendado`, `SituacaoDoDesligamento`, `Gestor` (1a, Task 1); `Funcionario.Desligar` (Task 1); `ProblemaDeApiException`; **`IPedidosParadosNoGestor`** (1a, Task 13: `Task PassarParaORhAsync(int gestorId, CancellationToken)`; **injete só `IPedidosParadosNoGestor`**: a 1a registra o composto, vazio até as fatias 3 e 7 registrarem a implementação delas de `IPedidosParadosNoGestorDaFatia`; **nunca** `IEnumerable<...>` nem uma implementação própria da porta).
- Produces:

```csharp
// RHSystem.Domain.Colaboradores: DesligamentoAgendado ganha
void Efetivar(DateTime agoraUtc);     // só de Agendado (senão InvalidOperationException); EfetivadoEm
void Cancelar(DateTime agoraUtc);     // só de Agendado; CanceladoEm
void Reverter(DateTime agoraUtc);     // só de Efetivado; RevertidoEm (mantém EfetivadoEm: o CHECK do V001 exige)
// RHSystem.Application.Colaboradores
sealed record FuncionarioInativarComando(DateOnly UltimoDiaDeTrabalho);     // schema `FuncionarioInativarComando` (`20-fatia-1:2929-2938`)
enum ResultadoDaEfetivacao { Efetivado, JaEstavaEfetivado, NaoEstavaAgendado, AindaNaoPode }
sealed record ResultadoDoAgendamento(int DesligamentoId, bool EfetivadoNaHora);
interface IDesligamentoDeFuncionarios {
  // Abre a própria transação. 404; 400 `ultimoDiaDeTrabalho` (anterior à admissão, ou mais de 10 anos à frente); 409 DESLIGAMENTO_JA_REGISTRADO (inativo ou já com desligamento vigente).
  // Cria AGENDADO; se o INÍCIO do dia seguinte ao último dia (00:00 de -03:00, em UTC) já passou E nenhuma condição recusa, efetiva na mesma transação; senão agenda a tarefa.
  Task<ResultadoDoAgendamento> AgendarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct);
  Task CancelarAsync(int funcionarioId, CancellationToken ct);                       // 404; 422 SEM_DESLIGAMENTO_AGENDADO se não há AGENDADO vigente (inclusive se já EFETIVADO); cancela a tarefa pela chave
  Task<ResultadoDaEfetivacao> EfetivarAsync(int desligamentoId, CancellationToken ct);   // idempotente; a tarefa e a recuperação chamam
}
// A fatia 3 acrescenta "depois de a consolidação do banco de horas daquele dia fechar" (roadmap, `:115`) registrando uma implementação; a 1b não registra nenhuma (vale true)
interface ICondicaoParaEfetivarDesligamento { Task<bool> PodeEfetivarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct); }
// Tarefas (1a: registradas como AddScoped<ITarefaAgendada, ...>)
//   Tipo "EFETIVAR_DESLIGAMENTO": chave ChaveDeIdempotencia.Montar("EFETIVAR_DESLIGAMENTO", ("desligamento", id)); ExecutarApos = InicioDoDiaEmUtc(ultimoDia.AddDays(1)); ReferenciaId = id
//   Tipo "LIMPAR_TOKENS_DE_SENHA": chave ...Montar("LIMPAR_TOKENS_DE_SENHA", ("dia", AAAA-MM-DD))
```

- Rotas (só `RH`):
  - `POST /api/rh/funcionarios/{funcionarioId}/desligamento` (`inativarFuncionario`) → `200 FuncionarioFichaRh` (com `desligamento` `AGENDADO` ou `EFETIVADO`) | `400` | `401` | `403` | `404` | `409` | `500`; corpo `FuncionarioInativarComando { ultimoDiaDeTrabalho }` (a API **não** recebe a palavra "desativar" da tela)
  - `DELETE /api/rh/funcionarios/{funcionarioId}/desligamento` (`cancelarDesligamentoAgendado`) → `204` | `401` | `403` | `404` | `422` | `500`

**Regras** (nota «(antigo V034)» do `V001`, roadmap `:115`): **ordem de travas** `FUNCIONARIOS` e depois `DESLIGAMENTOS_AGENDADOS` (`FOR UPDATE`), em `AgendarAsync`, `CancelarAsync` e `EfetivarAsync` (esta lê o `FuncionarioId` sem trava, trava na ordem e **relê o estado**). **Efetivar:** `Funcionario.Desligar(ultimoDia)` e `Efetivar` na mesma transação; **o trigger do grupo desativa o login e os benefícios e cancela as férias futuras, sem aviso e sem registro próprio** (decisão do dono); `DATA_DESLIGAMENTO` recebe o **último dia de trabalho**, que pode ser do passado. Se o colaborador **é gestor** (tem linha em `GESTORES`: `Gestor.FuncionarioId`), o miolo da efetivação chama `IPedidosParadosNoGestor.PassarParaORhAsync(gestor.Id, ct)` (a porta única da 1a; o composto chama cada fatia), **na mesma transação, depois de `Desligar`**: a etapa do pedido é **fixada na criação** (`CONVENCOES.md`, seção 9), então "passar ao RH" é uma **ação explícita** de quem tira o gestor, não um cálculo na leitura. O setor continua apontando para ele (só a 1c troca o gestor do setor). A implementação padrão da 1a é vazia: **dívida registrada aqui, em "Para as fatias 3, 4, 5, 6 e 7" e no `STATUS.md`**: até as fatias 3 e 7 a implementarem sobre as tabelas delas, os pedidos parados num gestor desligado ficam onde estão. Agendar sem efetivar **não** chama. **`EfetivarDesligamentoTarefa`:** `ExecutarAsync` chama `EfetivarAsync(ReferenciaId)`; `AindaNaoPode` **lança** `InvalidOperationException("Desligamento ainda não pode ser efetivado.")` (sem dado pessoal; o agendador tenta de novo); `JaEstavaEfetivado` e `NaoEstavaAgendado` voltam normalmente. **`RecuperarAsync` (roda na subida e a cada hora) chama `EfetivarAsync` direto para todo `AGENDADO` vencido** (assim uma tarefa `FALHOU` ou perdida não deixa ninguém para trás) e `AgendarAsync` (idempotente) para os que ainda não venceram e não têm tarefa. **Datas:** `CRIADO_EM`, `EFETIVADO_EM`, `CANCELADO_EM` e `REVERTIDO_EM` vêm de `IRelogio` (métodos de domínio com `agoraUtc`), nunca do padrão do banco. **Auditoria:** quando **não** efetiva na hora, evento explícito `Colaboradores/Inativou` "Desligamento de {nome} agendado" com a mudança `Último dia de trabalho` (`null` => `dd/MM/yyyy`); quando efetiva (na hora ou pela tarefa), **só** o `Inativou` do interceptor (que leva o último dia pela marca `[Auditar]`): nunca dois eventos para o mesmo clique; o cancelamento grava `Colaboradores/Alterou` "Cancelamento do desligamento de {nome}". **`LimparTokensDeSenhaTarefa`:** `RecuperarAsync` agenda uma por dia; `ExecutarAsync` apaga tokens com mais de `Retencao:TokensDeSenhaDias` (30), **exceto o último `CONVITE` de cada pessoa** (decisão 12), com `// AUDITORIA-OK: tabela técnica, sem registro por decisão`; **nunca** apaga token `EM_ABERTO` ainda válido.

- [ ] **Step 1: Escrever os testes que falham** (relógio `2026-10-07T12:42:00Z`; ator `Rh`)

```csharp
// Domain.Tests
DesligamentoAgendado_Efetivar_Cancelar_e_Reverter_so_valem_nas_transicoes_do_V001_e_Reverter_mantem_EfetivadoEm()
// DesligamentoDeFuncionariosTests (serviço direto, banco real)
Agendar_para_13_11_deixa_AGENDADO_a_pessoa_ativa_e_cria_a_tarefa_para_2026_11_14T03_00Z()
Ultimo_dia_hoje_nao_efetiva_porque_o_dia_seguinte_so_comeca_em_2026_10_08T03_00Z()
Ultimo_dia_no_passado_efetiva_na_hora_com_DATA_DESLIGAMENTO_do_passado_e_o_trigger_desativa_login_beneficios_e_ferias_futuras()
Virada_do_dia_ultimo_dia_07_10_as_23h30_de_Brasilia_nao_efetiva_e_as_00h00_de_08_10_sim()       // relógio 2026-10-08T02:30Z e 03:00Z (Review Focus 2)
Efetivar_gera_um_unico_evento_Inativou_com_o_ultimo_dia_e_nenhum_evento_do_login_dos_beneficios_nem_das_ferias()   // decisão do dono
Agendar_sem_efetivar_grava_so_o_evento_explicito_Desligamento_agendado_e_nenhum_Inativou()
Ultimo_dia_antes_da_admissao_ou_9999_12_31_devolve_400_em_ultimoDiaDeTrabalho_e_nao_500()      // decisão 13
Agendar_de_inativo_ou_de_quem_ja_tem_desligamento_vigente_devolve_409_DESLIGAMENTO_JA_REGISTRADO()
Dois_cliques_em_Inativar_dao_um_200_e_um_409_com_o_codigo_e_nenhum_500()                       // 20 tarefas, Barrier; Review Focus 4
Cancelar_AGENDADO_vira_CANCELADO_cancela_a_tarefa_e_a_pessoa_continua_ativa_e_pode_agendar_de_novo()
Cancelar_EFETIVADO_ou_inexistente_devolve_422_SEM_DESLIGAMENTO_AGENDADO()
Cancelar_e_efetivar_ao_mesmo_tempo_terminam_coerentes_nunca_CANCELADO_com_a_pessoa_inativa()    // 20 repetições, Barrier; Review Focus 4
Condicao_que_recusa_mantem_AGENDADO_e_a_pessoa_ativa_e_quando_libera_efetiva()                  // fake de ICondicaoParaEfetivarDesligamento
Efetivar_o_gestor_de_um_setor_chama_PassarParaORhAsync_com_o_id_do_gestor_uma_vez_e_nao_mexe_no_setor()   // falso registrado pelo teste, que anota as chamadas; decisão 5
Efetivar_quem_nao_e_gestor_e_agendar_sem_efetivar_nao_chamam_PassarParaORhAsync()
Se_PassarParaORhAsync_lanca_a_efetivacao_inteira_desfaz_e_a_pessoa_continua_ativa_e_AGENDADO()          // atomicidade
Agendar_e_efetivar_gravam_CriadoEm_e_EfetivadoEm_do_relogio_falso()                                    // decisão 7: 2026-10-07T12:42:00Z, não o UTC do banco
// EfetivarDesligamentoTarefaTests (IExecutorDeTarefas com relógio falso)
Tarefa_efetiva_no_horario_e_nao_antes()
Tarefa_com_condicao_recusando_lanca_e_volta_a_PENDENTE_para_nova_tentativa()
Recuperacao_efetiva_o_vencido_mesmo_com_a_tarefa_FALHOU_ou_inexistente()
Parada_de_3_dias_a_recuperacao_efetiva_todos_os_vencidos_sem_duplicar_e_agenda_os_futuros_sem_tarefa()
Tarefa_executa_como_Sistema_e_o_Inativou_sai_com_ator_SISTEMA_e_canal_SISTEMA()
// InativarFuncionarioTests (ApiFactory)
Inativar_devolve_200_com_a_ficha_e_desligamento_AGENDADO_ou_EFETIVADO()
Cancelar_devolve_204_e_depois_a_ficha_volta_a_desligamento_null()
Gestor_e_Funcionario_recebem_403_nas_duas_rotas_anonimo_401_e_id_inexistente_404()
Respostas_batem_com_o_schema_do_contrato()
// LimparTokensDeSenhaTarefaTests
Apaga_tokens_velhos_usados_ou_invalidados_e_o_convite_antigo_que_tem_outro_mais_novo_e_nunca_o_ultimo_convite_nem_token_em_aberto_valido()
RecuperarAsync_agenda_uma_limpeza_por_dia_sem_duplicar()
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter "FullyQualifiedName~DesligamentoAgendadoTests|FullyQualifiedName~DesligamentoDeFuncionariosTests|FullyQualifiedName~EfetivarDesligamentoTarefaTests|FullyQualifiedName~InativarFuncionarioTests|FullyQualifiedName~LimparTokensDeSenhaTarefaTests"`; Expected: FAIL.
- [ ] **Step 3: Implementar** o serviço (o miolo da efetivação é um método comum, sem abrir transação, chamado por `AgendarAsync` e por `EfetivarAsync`), as duas tarefas, os casos de uso e as duas rotas; `Retencao:TokensDeSenhaDias` ao `appsettings.json`; o miolo da efetivação chama a porta `IPedidosParadosNoGestor` quando a pessoa é gestor; acrescentar ao escopo de `ContratoDivergenciaTests` os `operationId` `inativarFuncionario` e `cancelarDesligamentoAgendado` (agora, junto das rotas, nunca antes).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", duas linhas (`POST` e `DELETE /api/rh/funcionarios/{funcionarioId}/desligamento`, com os `operationId` `inativarFuncionario` e `cancelarDesligamentoAgendado`), `em andamento`, observação **"a condição 'depois de a consolidação do banco de horas' entra com a fatia 3; os pedidos parados num gestor desligado só passam ao RH quando as fatias 3 e 7 registrarem a implementação delas da porta `IPedidosParadosNoGestor` (o composto padrão é vazio)"**.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Desligamento|FullyQualifiedName~InativarFuncionarioTests|FullyQualifiedName~LimparTokens|FullyQualifiedName~GuardaDeExecuteUpdateTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~Agendamento"`
Expected: PASS (inclusive o guarda: as chamadas novas têm o comentário `AUDITORIA-OK`).

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: desligamento agendado com tarefa de efetivacao e limpeza de tokens"`

---

### Task 10: Readmissão

**Files:**
- Create (Application): `Colaboradores/IReadmissaoDeFuncionarios.cs`, `ReadmitirFuncionarioCasoDeUso.cs`
- Create (Infrastructure): `Colaboradores/ReadmissaoDeFuncionarios.cs`; modify `DependencyInjection.cs`
- Modify (Api): `Colaboradores/ColaboradoresEndpoints.cs`, `tests/.../Contrato/ContratoDivergenciaTests.cs`, `contrato/STATUS.md`
- Test: `tests/RHSystem.Api.IntegrationTests/Colaboradores/ReadmissaoDeFuncionariosTests.cs`

**Interfaces:**
- Consumes: `IServicoDeConvite` (Task 4), `IGestorDoFuncionario`, `IInclusaoNoOnboarding`, `IEnvioDoFormularioDeAdmissao` (Task 8), `DesligamentoAgendado.Reverter` (Task 9), `Funcionario.Readmitir` (Task 1), `IConsultaDeFuncionarios` (Task 7), `Usuario.Reativar` (Task 1), `Setor.DefinirGestor(int? idGestor)` (Task 1), `IAuditor`, `ProblemaDeApiException`; `Gestor` e `IPedidosParadosNoGestor` (1a; a mesma porta única da Task 9, que o composto da 1a atende: injete só `IPedidosParadosNoGestor`).
- Produces:

```csharp
// RHSystem.Application.Colaboradores
sealed record FuncionarioReadmitirComando(DateOnly DataAdmissao, int CargoId, int SetorId, TipoDeContrato TipoContrato,   // schema `FuncionarioReadmitirComando` (`20-fatia-1:2902-2928`)
    bool IncluirNoOnboarding = false, int? FormularioAdmissaoId = null, bool EnviarConvite = false);
sealed record DadosDaReadmissao(DateOnly DataAdmissao, int CargoId, int SetorId, TipoDeContrato TipoContrato, bool IncluirNoOnboarding, int? FormularioAdmissaoId, bool EnviarConvite);
interface IReadmissaoDeFuncionarios { Task ReadmitirAsync(int funcionarioId, DadosDaReadmissao dados, CancellationToken ct); }   // abre a própria transação; tudo ou nada
```

- Rota (só `RH`): `POST /api/rh/funcionarios/{funcionarioId}/readmissao` (`readmitirFuncionario`) → `200 FuncionarioFichaRh` | `400` | `401` | `403` | `404` | `409` | `422` | `500`; corpo `FuncionarioReadmitirComando` (`incluirNoOnboarding`, `formularioAdmissaoId`, `enviarConvite` **opcionais**, padrão `false`, `null`, `false`).

**Regras** (`20-fatia-1:635-669`): trava `FUNCIONARIOS` (dois cliques: o segundo vê o cadastro ativo e recebe 409); **409 `FUNCIONARIO_JA_ATIVO`** ("Este colaborador já está ativo. Só se readmite quem está inativo.") para quem está ativo, **inclusive com desligamento só agendado**; 400 em `dataAdmissao` se for anterior ao último dia de trabalho do vínculo anterior; 422 `GESTOR_DO_SETOR_INATIVO` e `SEM_TREINAMENTO_DE_ONBOARDING` (e os 400 e 422 do formulário) como no cadastro. **Faz, tudo ou nada:** `Funcionario.Readmitir` (nova data, cargo, setor, tipo de contrato; gestor = o do setor; nível = o que tinha, **`Gestor` volta como `Funcionario`**); o desligamento `EFETIVADO` vai a `REVERTIDO`; o setor que ainda apontava para ela como gestora passa a **sem gestor** (`Setor.DefinirGestor(null)`, gravado antes) e o `ID_GESTOR` dos membros desse setor também (`IGestorDoFuncionario.RefazerDosMembrosAsync`), **e a readmissão chama `PassarParaORhAsync(gestor.Id, ct)`** (os pedidos que esperavam por ela passam ao RH: a etapa é fixada na criação do pedido, então é uma ação explícita; `CONVENCOES.md`, seção 9). Se o setor **já não apontava** para ela (a 1c o trocou depois do desligamento, e a 1c já chamou a porta ao trocar), nada disso é feito e a porta **não** é chamada; **não restaura** benefícios nem férias; **salva o colaborador antes de mexer no login** (o trigger de `USUARIOS` consulta o banco). **Login:** só com `EnviarConvite`; para quem **nunca definiu a senha** (sem login ou `SenhaProvisoria`), **reativa o login** (`Usuario.Reativar`; cria se faltar) **e só depois** emite o convite, porque `EmitirAsync` recusa login desativado (**o mesmo que "Ativar" mais "Reenviar convite"**); quem já definiu a senha recebe 422 `SENHA_JA_DEFINIDA` com o detalhe da readmissão ("Esta pessoa já definiu a senha. Desmarque o convite e libere o acesso em Usuários do sistema (Ativar)."); sem convite o login **continua desativado** (`acesso.ativo = false`). Data de admissão futura: `situacao = EM_ADMISSAO` e `situacaoHoje = null`. Auditoria: pelo interceptor, `Alterou` da área `COLABORADORES` com "o que mudou" (situação, data de admissão **antes e depois**, último dia de trabalho **antes e `null`**, cargo, setor): é onde ficam as datas do vínculo anterior.

- [ ] **Step 1: Escrever os testes que falham** (cenário: `Inativo` efetivado com benefício, férias futuras e login, gestor de setor desligado)

```csharp
Readmitir_ativa_zera_o_desligamento_marca_REVERTIDO_e_devolve_a_ficha_com_a_nova_admissao()
Readmitir_nao_restaura_beneficios_nem_ferias_e_a_ficha_vem_com_beneficios_vazio()
Readmitir_grava_o_evento_Alterou_com_data_de_admissao_antes_e_depois_e_ultimo_dia_de_trabalho_antes_e_null()
Readmitir_ex_gestor_volta_como_Funcionario_e_o_setor_que_apontava_para_ele_e_os_membros_dele_ficam_sem_gestor()
Readmitir_ex_gestor_chama_PassarParaORhAsync_uma_vez_com_o_id_do_gestor_e_se_o_setor_ja_nao_apontava_para_ele_nao_chama()   // falso que anota as chamadas; decisão 5
Se_PassarParaORhAsync_lanca_a_readmissao_inteira_desfaz()
Readmitir_com_gestor_do_setor_inativo_devolve_422_GESTOR_DO_SETOR_INATIVO_e_nada_muda()
Readmitir_ativo_ou_com_desligamento_so_agendado_devolve_409_FUNCIONARIO_JA_ATIVO()
Dois_cliques_em_Readmitir_dao_um_200_e_um_409()
Readmitir_com_dataAdmissao_antes_do_ultimo_dia_de_trabalho_devolve_400_em_dataAdmissao()
Readmitir_com_data_futura_fica_EM_ADMISSAO_e_situacaoHoje_null()
Sem_convite_o_login_continua_desativado_e_a_ficha_mostra_acesso_ativo_false()
Convite_para_quem_nunca_definiu_a_senha_ativa_o_login_e_enfileira_o_CONVITE()
Convite_para_quem_ja_definiu_a_senha_devolve_422_SENHA_JA_DEFINIDA_e_nada_muda()
Caixas_sem_fonte_devolvem_o_422_e_o_400_e_nada_muda()
Falha_depois_de_salvar_o_colaborador_desfaz_tudo_inclusive_o_REVERTIDO_e_o_login()              // fake que lança; Review Focus 5
Readmitir_nao_enfileira_nenhum_email_alem_do_convite()
Readmitir_inexistente_404_perfil_errado_403_e_anonimo_401_e_respostas_batem_com_o_schema_do_contrato()
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter ReadmissaoDeFuncionariosTests`; Expected: FAIL (404 na rota).
- [ ] **Step 3: Implementar** o serviço, o caso de uso e a rota; acrescentar `readmitirFuncionario` ao escopo de `ContratoDivergenciaTests` (agora, junto da rota, nunca antes).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", uma linha (`POST /api/rh/funcionarios/{funcionarioId}/readmissao`, `readmitirFuncionario`), `em andamento`, observação **"os pedidos parados no gestor readmitido só passam ao RH quando as fatias 3 e 7 registrarem a implementação delas da porta `IPedidosParadosNoGestor`"**.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~ReadmissaoDeFuncionariosTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS.

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: readmissao de colaborador inativo"`

---

### Task 11: A equipe do gestor (lista, ficha e indicadores)

**Files:**
- Create (Application): `Colaboradores/IConsultaDaEquipe.cs`, `FuncionarioEquipeDtos.cs`, `ListarEquipeDoGestorCasoDeUso.cs`, `ObterFichaDaEquipeCasoDeUso.cs`, `ObterIndicadoresDaEquipeCasoDeUso.cs`
- Create (Infrastructure): `Colaboradores/ConsultaDaEquipe.cs`
- Create (Api): `Colaboradores/EquipeEndpoints.cs`; modify `Program.cs`, `tests/.../Contrato/ContratoDivergenciaTests.cs`, `contrato/STATUS.md`
- Test: `tests/RHSystem.Api.IntegrationTests/Equipe/EquipeDoGestorTests.cs`, `FichaDaEquipeTests.cs`, `IndicadoresDaEquipeTests.cs`

**Interfaces:**
- Consumes: `IEscopoDoUsuario`, `EquipeDoGestor`, `DaEquipe` (1a, Task 6); `ISituacaoDeHoje`, `PresencaContagens`, `TelefoneMascarado`, `IFonteDeIndicadorDaFicha` (Task 2); `OuNaoEncontrado` (1a); `GrupoGestor()`; `CamposDoRh.AfirmarSemCamposDoRh` (1a, Task 7).
- Produces:

```csharp
// RHSystem.Application.Colaboradores (schemas do contrato; DTO PRÓPRIO do gestor: sem CPF, RG, e-mail pessoal, endereço, matrícula, benefícios nem `telefone`)
sealed record FuncionarioEquipeItem(int Id, string Nome, string? Cargo, string? Setor, bool Ativo, SituacaoHoje SituacaoHoje);
sealed record FuncionarioEquipeResposta(SetorReferencia? Setor, int Total, bool DiaUtil, PresencaContagens ContagensHoje, IReadOnlyList<FuncionarioEquipeItem> Itens);
sealed record FuncionarioFichaGestor(int Id, string Nome, string? Cargo, string? Setor, bool Ativo, SituacaoHoje SituacaoHoje, string? EmailCorporativo,
    string? TelefoneMascarado, DateOnly DataAdmissao, TipoDeContrato? TipoContrato, decimal? JornadaSemanalHoras);
sealed record FuncionarioIndicadoresGestor(int? BancoDeHorasMinutos, int? FeriasDisponiveisDias, int? TreinamentosPendentes, int? AdvertenciasQuantidade);
interface IConsultaDaEquipe {
  Task<FuncionarioEquipeResposta> ListarAsync(EquipeDoGestor? equipe, CancellationToken ct);            // null = gestor sem setor: setor null, total 0, contagens 0, itens []
  Task<FuncionarioFichaGestor?> ObterFichaAsync(EquipeDoGestor? equipe, int funcionarioId, CancellationToken ct);   // null fora da equipe
}
```

- Rotas (só `Gestor`; todas **404 quando fora do escopo**, nunca 403 nem 200 vazio):
  - `GET /api/gestor/equipe` (`listarEquipeDoGestor`) → `200 FuncionarioEquipeResposta` | `401` | `403` | `500`
  - `GET /api/gestor/equipe/{funcionarioId}` (`obterFichaDaEquipe`) → `200 FuncionarioFichaGestor` | `401` | `403` | `404` | `500`
  - `GET /api/gestor/equipe/{funcionarioId}/indicadores` (`obterIndicadoresDaEquipe`) → `200 FuncionarioIndicadoresGestor` | `401` | `403` | `404` | `500`

**Regras:** a equipe é a **definição única** da 1a (`DaEquipe`: ativos, admissão já começada, do setor dele, sem ele; `20-fatia-1:785-788`), **a consulta já exclui o que está fora** (nunca "buscar e depois conferir"); lista sem paginação, nome de A a Z, desempate por id; `contagensHoje` calculadas **desta lista** (soma dos seis = `total`); `diaUtil` vem do calendário de `ISituacaoDeHoje` (também para equipe vazia). Ficha: `cargo` e `setor` em texto; `telefoneMascarado` = `TelefoneMascarado.Mascarar(telefone)`; `situacaoHoje` nunca `null`. Indicadores: mesmo mecanismo da Task 7 (`null` sem fonte; **sem `proximoPagamento`**, que o gestor não tem).

- [ ] **Step 1: Escrever os testes que falham** (`ElencoDeTeste`: `Gestor`, `MembroA`, `MembroB`, `EmAdmissao`, `Inativo`, `DeOutroSetor`, `GestorSemSetor`; `SementeDoCadastro.DetalhesAsync` para os telefones)

```csharp
Lista_so_a_propria_equipe_em_ordem_de_nome_sem_o_gestor_sem_em_admissao_sem_inativo_e_sem_outro_setor()
Contagens_de_hoje_somam_o_total_e_a_fonte_padrao_deixa_todos_AINDA_NAO_COMECOU_num_dia_util()   // decisão 1; relógio 2026-10-07T12:42Z
Sabado_diaUtil_false_e_todos_SEM_EXPEDIENTE_exceto_quem_tem_entrada_registrada_pela_fonte_falsa()
Gestor_sem_setor_devolve_equipe_vazia_com_setor_null_total_0_e_contagens_zeradas_e_nao_erro()
Ficha_da_equipe_traz_so_os_campos_do_gestor_e_o_telefone_mascarado()                              // "(11) 9****-7783"
Ficha_da_equipe_nao_tem_nenhum_campo_do_RH_em_nenhuma_profundidade()                              // CamposDoRh.AfirmarSemCamposDoRh nas três respostas; Review Focus 3
RH_ve_o_telefone_completo_e_o_gestor_ve_o_mascarado_da_mesma_pessoa()
Pessoa_de_outro_setor_o_proprio_gestor_quem_esta_em_admissao_e_quem_esta_inativo_devolvem_404_na_ficha_e_nos_indicadores()   // Review Focus 3
Admissao_em_08_10_vista_as_22h_de_07_10_em_Brasilia_nao_esta_na_equipe_e_as_00h_de_08_10_esta()   // relógio 2026-10-08T01:00Z e 03:00Z
Id_inexistente_devolve_exatamente_o_mesmo_404_de_fora_do_escopo()
Indicadores_da_equipe_sem_fonte_vem_com_os_quatro_campos_null_e_com_fontes_registradas_traz_os_valores()
Gestor_que_mudou_de_setor_passa_a_ver_a_equipe_nova_sem_novo_login()
RH_e_Funcionario_recebem_403_nas_tres_rotas_e_anonimo_401()                                       // RH recebe 403, não 404
Respostas_batem_com_o_schema_do_contrato()
```

- [ ] **Step 2: Rodar e ver falhar.** Run: `dotnet test back-end --filter "FullyQualifiedName~EquipeDoGestorTests|FullyQualifiedName~FichaDaEquipeTests|FullyQualifiedName~IndicadoresDaEquipeTests"`; Expected: FAIL (404 nas rotas).
- [ ] **Step 3: Implementar** consulta, casos de uso e as três rotas (`GrupoGestor()`, `WithName`, `Produces<T>`, `ProduzProblemas` com os códigos acima); acrescentar ao escopo de `ContratoDivergenciaTests` os `operationId` `listarEquipeDoGestor`, `obterFichaDaEquipe` e `obterIndicadoresDaEquipe` (agora, junto das rotas, nunca antes). **Por `operationId`, nunca por prefixo**: o contrato tem sob `/api/gestor/equipe/{funcionarioId}/` rotas de ponto, férias, treinamentos e advertências.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** na seção "Rotas implementadas", três linhas (cada uma com o seu `operationId`), `em andamento`, observação **"parcial: `situacaoHoje` e `contagensHoje` conhecem só o fim de semana até a fatia 3; indicadores `null` até as fatias 3, 4, 6 e 7"**.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Equipe|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS, inclusive a divergência nas três operações, a matriz (as três só aceitam `Gestor`; `Rh` e `Funcionario` recebem 403) e `Respostas_2xx_das_rotas_do_gestor_no_contrato_nao_tem_campos_do_rh` (`ContratoEStatusTests`, da 1a).

- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: equipe do gestor (lista, ficha sem CPF e indicadores)"`

---

### Task 12: Verificação da fatia, fluxo de acesso de ponta a ponta e README

**Files:**
- Create (testes): `tests/RHSystem.Api.IntegrationTests/Acesso/FluxoDeAcessoDePontaAPontaTests.cs`
- Modify: `README.md`, `contrato/STATUS.md`

- [ ] **Step 1: Escrever o teste de ponta a ponta da API** (sem navegador; `ApiFactory`, `EnviadorDeEmailEmMemoria`, `IProcessadorDeEmails.ProcessarCicloAsync` chamado direto)

```csharp
RH_cadastra_a_pessoa_recebe_o_convite_define_a_senha_e_entra()
// 1) o RH faz POST /api/rh/funcionarios (enviarConvite true) e recebe 201; 2) o ciclo da fila envia o e-mail CONVITE e a carga do banco fica nula;
// 3) o teste extrai o token do link da mensagem; 4) validar-link devolve tipo CONVITE; 5) entrar antes de definir a senha devolve 401;
// 6) definir devolve 204; 7) entrar devolve 200 com o perfil certo; 8) o link não vale mais (422); 9) nem token, nem senha, nem hash em nenhum evento de auditoria nem no log capturado
Esqueci_minha_senha_redefine_encerra_as_sessoes_e_a_senha_antiga_deixa_de_valer()
Inativar_com_ultimo_dia_no_passado_derruba_o_login_na_proxima_renovacao_e_Readmitir_com_convite_traz_a_pessoa_de_volta()
```

- [ ] **Step 2: Rodar.** Run: `dotnet test back-end --filter FluxoDeAcessoDePontaAPontaTests`; Expected: PASS (as Tasks 4 a 10 já o satisfazem). **Se falhar, o defeito é da Task que falhou** (a falha aponta o passo 1 a 9 do fluxo): corrija nela, não aqui.
- [ ] **Step 3: Verificar a fatia inteira**

Run: `dotnet build back-end; dotnet test back-end; dotnet format back-end --verify-no-changes`
Expected: build sem warnings; **todos** os testes passando (fatia 0, 1a, matriz, divergência, conformidade, `STATUS`, as Tasks 1 a 11 e 8b); `dotnet format` sem alterações. Conferir que a lista de `operationId` do escopo de `ContratoDivergenciaTests` tem os **19** da seção "Autorrevisão" e que a seção "Rotas implementadas" do `STATUS.md` lista as **19 rotas** desta fatia.

- [ ] **Step 4: Verificação manual** (Docker): `docker compose up -d --wait`; API com o seed do admin; com `curl.exe` (**todo corpo JSON vai de arquivo**: no Windows PowerShell 5.1 as aspas duplas de um JSON passado em linha chegam quebradas ao programa nativo, suponho, não medido, como na nota da Task 3 da fatia 0; crie `$env:TEMP\corpo.json` **fora do repositório** a cada chamada, com o JSON do exemplo do contrato da rota, e passe `-H "Content-Type: application/json" --data-binary "@$env:TEMP\corpo.json"`; ou faça o passo no Git Bash): entrar como admin; `POST /api/rh/funcionarios` com convite; abrir `http://localhost:8025` (Mailpit), copiar o link; `POST /api/auth/senha/validar-link` e `.../definir`; entrar com a pessoa nova; `POST /api/eu/senha`; `POST /api/rh/funcionarios/{id}/desligamento` com último dia ontem (efetiva na hora) e conferir `GET /api/rh/funcionarios/{id}` (`situacao: INATIVO`); `POST .../readmissao`. Registrar o resultado no `README.md` (seção "Cadastro, ficha e acesso": as configurações novas `Senha:*`, `Autenticacao:LimiteSenhaPorMinuto`, `Autenticacao:LimiteSenhaAtualPorMinuto`, `Retencao:TokensDeSenhaDias`, o que cada fonte padrão ainda não sabe, a dívida da porta `IPedidosParadosNoGestor` vazia e a limitação do `rth` na troca de senha).
- [ ] **Step 5: Commit** (com autorização): `git add back-end contrato/STATUS.md README.md; git commit -m "test: fluxo de acesso de ponta a ponta e verificacao da fatia 1b"`

**Fora deste plano:** o teste de ponta a ponta com navegador é um **passo de integração da fatia 1**, feito **depois** de o front da fatia 1 estar na `main`, num diretório `e2e\` na raiz criado pelo dono; o detalhe só se escreve nessa hora (a entrada do plano do back-end o diz e aponta para a ficha 8 do front). Nenhuma das Tasks desta fatia instala ferramenta de navegador; o Step 1 acima cobre o fluxo **na API**.

---

## Para as fatias 1c e 2

O que esta fatia produz e que as seguintes **consomem** (caminhos sob `back-end/src/`). **Os nomes e as assinaturas não mudam depois** (o `Interfaces → Produces` de cada tarefa é a fonte); precisando de outra coisa, acrescenta-se, não se renomeia.

**Para a fatia 1c (cadastros de apoio, empresa, documentos)** — o plano dela é escrito ao mesmo tempo que este; **conferir o que segue antes de executar a 1c**:
- **Peças que a 1b define, uma vez, e a 1c só consome (não remapear nem redefinir):** `Funcionario.DataNascimento`, `DataDesligamento`, `GestorId`, `TipoContrato`, `Detalhes`, `NivelDeAcessoId`; `FuncionarioDetalhes`; **`BeneficioDoFuncionario`** (`BENEFICIOS_FUNCIONARIOS`) e `StatusDoPedidoDeBeneficio` (`RHSystem.Domain.Beneficios/`; com `Conceder`, `Reconceder`, `Desativar` e `Reativar`; é dele que a 1c tira `quantidadePessoas` do benefício e a regra "exclui só o que nunca foi usado"); **`Cargo.CargaSemanalHoras`** (`decimal?`, só leitura aqui: o método que a escreve é acréscimo da 1c em `Cargo.cs`); **`Setor.DefinirGestor(int? idGestor)`** (`idGestor` é o `ID_GESTOR` de `GESTORES`); **`NivelDeAcesso : IAuditavel`** e **`NomesDeNivel.DoPerfil(Perfil)`** (`Domain/Autenticacao/`); o record **`GestorReferencia(int GestorId, int FuncionarioId, string Nome)`** (`RHSystem.Application/Colaboradores/`, Task 7). `tabelas-sem-mapeamento.txt` já não lista `FUNCIONARIOS_DETALHES` nem `BENEFICIOS_FUNCIONARIOS`. A 1b **não** define `Funcionario.DefinirNivel` nem `Funcionario.DefinirGestor`: o gestor do colaborador muda por `AtualizarCadastro` (Task 8b) e por `RefazerDosMembrosAsync`, e o nível por `AtualizarCadastro` e `Readmitir`; se a 1c precisar de `DefinirNivel(NivelDeAcesso)` para "quem deixa de ser gestor volta a `Funcionario`", é acréscimo dela em `Funcionario.cs`.
- **`IGestorDoFuncionario.RefazerDosMembrosAsync(setorId)`** (`RHSystem.Application/Colaboradores/IGestorDoFuncionario.cs`, Task 8): **`criarSetor` e `editarSetor` o chamam, na mesma transação, sempre que o gestor do setor mudar, depois de gravar o setor** (ele lê o gestor do setor no banco); a 1c **não** o reimplementa (roadmap `:111`: "quando o gestor do setor muda é preciso refazer o `ID_GESTOR` dos membros"). Sem isso a equipe nova fica com o gestor velho.
- **`IPedidosParadosNoGestor`** (da **1a**; o composto padrão é vazio; as fatias 3 e 7 registram a implementação delas, `IPedidosParadosNoGestorDaFatia`): a 1b chama `PassarParaORhAsync(gestorId)` ao **efetivar o desligamento** de quem é gestor (Task 9) e na **readmissão** que o tira do setor (Task 10); a 1c a chama ao **trocar ou tirar o gestor de um setor** e chama `AprovarEtapaDoGestorAsync(gestorId)` ao ligar "Gestor ausente". A 1c **não** limpa `ID_GESTOR` de pedido nenhum: a etapa do pedido é fixada na criação (`CONVENCOES.md`, seção 9).
- **Regras que a 1c herda do contrato** e que dependem da 1b: um gestor responde por **um** setor; só quem tem nível `Gestor` gerencia setor (`GESTOR_PRECISA_TER_PERFIL_GESTOR`); quem deixa de ser gestor volta a `Funcionario`; a 1b **não** cria linha em `GESTORES` (a 1c cria ao escolher o gestor do setor e ao `cadastrarGestor`).
- `Cpf` e `TelefoneMascarado` (`Domain/Colaboradores/`, `Application/Colaboradores/`), `ProblemaDeApiException` (`Application/Comum/`), `CpfsDeTeste` e `SementeDoCadastro` (`tests/.../Infra/`: a 1c os reaproveita e acrescenta os métodos dela).
- **Escopo do teste de divergência:** lista de `operationId` no próprio teste (a 1c acrescenta os dela, cada tarefa no passo em que implementa as rotas; **nunca** prefixo de caminho: `/api/rh/funcionarios` e `/api/rh/beneficios` são prefixos textuais de rotas de outras fatias).
- Sem efeito da 1b sobre a empresa: o horário padrão **não** é lido por esta fatia; quem o lê é a fonte de fatos do dia que a fatia 3 registra.

**Para a fatia 2 (importação por Excel)**:
- **`ICadastroDeFuncionarios`** (`RHSystem.Application/Colaboradores/ICadastroDeFuncionarios.cs`):
  - Prévia: converte cada linha da planilha em `NovoFuncionario` (CPF e telefone só com dígitos: `Cpf.Normalizar`, `TelefoneMascarado.SoDigitos`; nomes de cargo, setor, tipo de contrato, nível e benefícios resolvidos para ids pela 2) e chama `ValidarAsync`. Cada `ProblemaDeCadastro` traz `Indice` (a linha), `Campo`, `Codigo`, `Mensagem` e, para CPF e e-mail de cadastro **inativo**, `Existente.Ativo = false` (a mensagem manda usar "Readmitir"; a 2 reescreve o texto por coluna). Repetição dentro do arquivo vem em `OutroIndice`.
  - Confirmar: dentro de `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync` e de `using IAuditor.SuspenderRegistroAutomatico()` (um evento `IMPORTOU` só, que a 2 registra com `IAuditor.Registrar`), chama `CadastrarAsync(novos, new OpcoesDoCadastro(enviarConvite, incluirNoOnboarding, formularioAdmissaoId), ct)`. Reserva as matrículas **em bloco**, grava linha por linha, **tudo ou nada**.
  - `CadastroInvalidoException` (nada gravado; reconferência no confirmar) e `LinhaRecusadaException(Indice, PorDuplicidade, RegraDoBanco)` viram o 422 `IMPORTACAO_LINHA_RECUSADA` com `ProblemaDeApiException(422, "Operação recusada.", "A linha {n} foi recusada: ...", "IMPORTACAO_LINHA_RECUSADA", { linha, regraDoBanco })` (`25-fatia-2:304-310`).
  - `ResultadoDoCadastro` dá `totalCriados`, `convitesEnviados` ("na fila"), `incluidosNoOnboarding` e `incluidosNoFormularioDeAdmissao` e os ids para a lista `funcionarios`.
- `IServicoDeConvite.EmitirAsync(IReadOnlyList<int>)`: lote de 500 em três gravações; a vazão é do processador da fila (1a).
- As caixas "depois de salvar" valem para **todas** as linhas e seguem as mesmas versões padrão: `IInclusaoNoOnboarding` e `IEnvioDoFormularioDeAdmissao` (a 2 não as reescreve).

**Para as fatias 3, 4, 5, 6 e 7 (substituir as versões padrão; cada uma registra a dela **depois** da registrada pela Infrastructure)**:
- `IFonteDeFatosDoDia` (fatia 3, com férias da 7 e o horário padrão da empresa da 1c): dá `TRABALHANDO`, `SEM_REGISTRO`, `FERIAS`, `ATESTADO` e feriado; os tipos `SituacaoHoje`, `PresencaContagens`, `CalendarioDoDia`, `FatosDaPessoaNoDia` são **os mesmos** que as fatias 3 e 7 usam (`RHSystem.Application/Colaboradores/SituacaoDeHoje.cs`).
- `IFonteDeIndicadorDaFicha` (um por indicador: `BancoDeHorasMinutos` fatia 3; `FeriasDisponiveisDias` fatia 7; `AdvertenciasQuantidade` fatia 4; `TreinamentosPendentes` fatia 6) e `IFonteDoProximoPagamento` (fatia 7): devolvem **o mesmo valor** da rota de origem (tabela de `obterIndicadoresDoFuncionario`).
- `ICondicaoParaEfetivarDesligamento` (fatia 3): "o banco de horas daquele dia já fechou". Enquanto não houver, a 1b efetiva no dia seguinte ao último dia de trabalho.
- `IInclusaoNoOnboarding` (fatia 6) e `IEnvioDoFormularioDeAdmissao` (fatia 5): trocar as versões "indisponível"; os erros do contrato que a versão padrão devolve (`SEM_TREINAMENTO_DE_ONBOARDING`; 400 em `formularioAdmissaoId`) passam a vir das regras reais.
- **`IPedidosParadosNoGestor` (dívida das fatias 3 e 7):** a etapa do pedido é **fixada na criação** (`CONVENCOES.md`, seção 9), então "passar ao RH" é uma **ação explícita** de quem tira o gestor. A 1b chama `PassarParaORhAsync(gestorId, ct)` ao efetivar o desligamento de um gestor e na readmissão que o tira do setor; a 1c o chama ao trocar ou tirar o gestor do setor e chama `AprovarEtapaDoGestorAsync` ao ligar "Gestor ausente". **A 1a registra o composto padrão (vazio); as fatias 3 e 7 implementam `IPedidosParadosNoGestorDaFatia` sobre as tabelas de pedido delas e a registram por DI** (quem chama, a 1b e a 1c, usa só `IPedidosParadosNoGestor`). Até lá, os pedidos parados num gestor desligado, trocado ou readmitido ficam onde estão: está na seção "Rotas implementadas" do `STATUS.md` (observação das rotas de desligamento e readmissão) e no `README`.
- `Funcionario.GestorId`, `FuncionarioDetalhes`, `Perfil` por `NomesDeNivel` e a regra "o gestor nunca recebe os campos do RH" (`CamposDoRh.AfirmarSemCamposDoRh` em toda rota `/api/gestor`).
- `IEmissorDeTokens.EmitirAcesso(Usuario, string? hashDoRefreshDaSessao = null)` e a claim `rth`: rotas que precisem saber "a sessão atual" leem a claim.

---

## Autorrevisão (cobertura do contrato, da 1b e do roadmap)

| `operationId` | Rota | Task |
|---|---|---|
| `listarFuncionarios` | `GET /api/rh/funcionarios` | 7 |
| `criarFuncionario` | `POST /api/rh/funcionarios` | 8 |
| `obterFichaDoFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}` | 7 |
| `editarFuncionario` | `PUT /api/rh/funcionarios/{funcionarioId}` | 8b |
| `obterIndicadoresDoFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}/indicadores` | 7 |
| `inativarFuncionario` | `POST /api/rh/funcionarios/{funcionarioId}/desligamento` | 9 |
| `cancelarDesligamentoAgendado` | `DELETE /api/rh/funcionarios/{funcionarioId}/desligamento` | 9 |
| `readmitirFuncionario` | `POST /api/rh/funcionarios/{funcionarioId}/readmissao` | 10 |
| `listarEquipeDoGestor` | `GET /api/gestor/equipe` | 11 |
| `obterFichaDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}` | 11 |
| `obterIndicadoresDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}/indicadores` | 11 |
| `listarUsuariosDoSistema` | `GET /api/rh/usuarios` | 6 |
| `enviarConviteDeAcesso` | `POST /api/rh/usuarios/{funcionarioId}/convite` | 6 |
| `ativarUsuarioDoSistema` | `POST /api/rh/usuarios/{funcionarioId}/ativar` | 6 |
| `desativarUsuarioDoSistema` | `POST /api/rh/usuarios/{funcionarioId}/desativar` | 6 |
| `solicitarRedefinicaoDeSenha` | `POST /api/auth/senha/esqueci` | 5 |
| `validarLinkDeSenha` | `POST /api/auth/senha/validar-link` | 5 |
| `definirSenhaPeloLink` | `POST /api/auth/senha/definir` | 5 |
| `trocarMinhaSenha` | `POST /api/eu/senha` | 5 |

**Os 19 `operationId` do escopo de `ContratoDivergenciaTests` desta fatia** (a lista fica no próprio teste; cada tarefa acrescenta os dela no passo em que implementa as rotas): `listarFuncionarios`, `obterFichaDoFuncionario`, `obterIndicadoresDoFuncionario` (Task 7); `criarFuncionario` (8); `editarFuncionario` (8b); `inativarFuncionario`, `cancelarDesligamentoAgendado` (9); `readmitirFuncionario` (10); `listarEquipeDoGestor`, `obterFichaDaEquipe`, `obterIndicadoresDaEquipe` (11); `listarUsuariosDoSistema`, `enviarConviteDeAcesso`, `ativarUsuarioDoSistema`, `desativarUsuarioDoSistema` (6); `solicitarRedefinicaoDeSenha`, `validarLinkDeSenha`, `definirSenhaPeloLink`, `trocarMinhaSenha` (5).

**Fora da 1b por serem da 1c:** documentos (6 rotas), cargos, setores, gestores (inclusive "Gestor ausente"), benefícios do catálogo, empresa; **da 1a:** auditoria (3 rotas) e contadores do menu. **Cobertas aqui e não são rotas:** matriz de perfis (as 19 rotas entram sozinhas em `MatrizDePerfisTests`; Tasks 5 a 11 conferem), privacidade e retenção (Tasks 4, 5, 9 e 11), efeitos de trigger sem registro (Task 9), e-mails da lista fechada (Tasks 4, 8 e 10), `ID_GESTOR` do gestor do setor e os triggers que recusam gestor igual ao colaborador e gestor inativo (Tasks 1, 8 e 8b), admin do seed completando a ficha (Tasks 7 e 8b), pedidos parados no gestor que sai (Tasks 9 e 10, pela porta da 1a).

**Regras da fatia 1 (roadmap `:110-125`) e onde são provadas:** auditoria de convite, login, acesso negado, alterações e efeitos de trigger: Tasks 4, 5, 6, 8, 8b, 9 e 10 (login e acesso negado são da 1a); e-mail do convite pela fila: Tasks 4 e 12; nenhum e-mail fora da lista: Tasks 8 e 10; inativar cancela benefícios e férias sem aviso nem registro: Task 9; desligamento no dia seguinte ao último dia de trabalho e a nota da fatia 3: Task 9; hash provisório inutilizável: Tasks 4 e 5; DTO próprio do gestor e telefone mascarado: Task 11; CPF e telefone completos para o RH: Task 7.

**Tipos coerentes entre tarefas:** `SituacaoHoje`/`PresencaContagens` nascem na Task 2 e são usados nas 7 e 11; `UsuarioSistemaSituacao` e `IConsultaDeUsuariosDoSistema` nascem na 6 e são usados na ficha (7); `ProblemaDeApiException` nasce na 3 e é usada nas 5, 8, 8b e 10; `ILimiteDeSenhaAtual` nasce na 5; `IServicoDeConvite` nasce na 4 e é usado nas 6, 8 e 10; `IGestorDoFuncionario`, `IInclusaoNoOnboarding` e `IEnvioDoFormularioDeAdmissao` nascem na 8 e são usados nas 8b e 10; `ICadastroDeFuncionarios.ValidarEdicaoAsync` nasce na 8b (a mesma validação da 8); `DesligamentoAgendado.Reverter` nasce na 9 e é usado na 10; `FuncionarioFichaRh` nasce na 7 e é devolvida pelas 8, 8b, 9 e 10; `TipoDeContrato`, `Cpf`, `FuncionarioDetalhes`, `NomesDeNivel`, `BeneficioDoFuncionario` (com `Reconceder`, usado na 8b) e `Setor.DefinirGestor` nascem na 1; `GestorReferencia` nasce na 7; `IPedidosParadosNoGestor` e `EnumsDeConsulta` são da 1a e só são consumidos (Tasks 7, 6, 9 e 10).

**Proporção:** 13 tarefas (a Task 8 virou 8 e 8b) para 19 operações, quase todas com regra própria (cada rota de leitura tem escopo, filtro e campo por perfil); a conta é a da 1a (12 tarefas para 6 rotas e 3 serviços). Código só onde o plano decide: assinaturas, textos e códigos do contrato, nomes de teste e as asserções com valores exatos.

**Pontos deixados em aberto de propósito** (decisão do dono ou de outra fatia, listados no relatório): `situacaoHoje` provisório (decisão 1); claim `rth` e a limitação aceita da troca de senha (decisão 2); o texto do 422 `USUARIO_DESATIVADO`, que o contrato não traz (decisão 3); conceder benefício que tem linha `PENDENTE` aprova na prática o pedido (decisão 10); o CPF `29384756012` do exemplo de `readmitirFuncionario` (inválido pelo trigger do banco: a fatia 8 não pode semear por ele).
