# RH-System

Sistema de RH feito como TCC: API em .NET 10 em `back-end/`, front-end em React em `front-end/` e banco MySQL mantido por scripts SQL em `Banco de Dados/Scripts/`.

- Plano do back-end: [`docs/superpowers/plans/2026-10-30-plano-back-end.md`](docs/superpowers/plans/2026-10-30-plano-back-end.md)
- Plano do front-end: [`docs/plano-front-end/LEIA-PRIMEIRO.md`](docs/plano-front-end/LEIA-PRIMEIRO.md)
- Contrato da API: [`contrato/`](contrato/)

## Pré-requisitos

- **Git**.
- **Docker Desktop**, com o daemon ligado (o MySQL local e os testes de integração rodam em contêiner).
- **.NET SDK 10** (o `global.json` da raiz fixa o SDK 10).
- **Node 24 LTS** (`^24.15.0`), para o contrato e para o front-end.
- Os comandos abaixo rodam **da raiz do repositório**.

## Banco de dados

O esquema vem dos scripts `Banco de Dados/Scripts/Vnnn__descricao.sql`. A API os aplica ao iniciar (controlado por configuração), guarda o hash de cada um e **recusa subir se um script já aplicado foi editado**.

- **Nunca edite um script já aplicado.** Até o primeiro deploy no Azure, um script só se edita avisando o dono, e os bancos locais são recriados; depois do primeiro deploy, todo script aplicado fica congelado e qualquer mudança é um script novo (`ALTER` para tabela; `DROP` e `CREATE` para trigger e função).
- Os números de versão (`Vnnn`) são do dono: o grupo envia o conteúdo e o dono numera, para não haver duas versões com o mesmo número.
- Para recriar o banco local do zero: `docker compose down -v` e depois `docker compose up -d --wait`.

### MySQL local

```
cp .env.example .env        # no PowerShell: Copy-Item .env.example .env
docker compose up -d --wait
```

Sobe o MySQL 8.4 e o **Mailpit** (captador de e-mail de desenvolvimento). **Ver os e-mails de desenvolvimento: `http://localhost:8025`** (SMTP na porta 1025; troque `MAILPIT_SMTP_PORTA` e `MAILPIT_UI_PORTA` no `.env` se estiverem ocupadas). Nada sai para a internet. O MySQL fica em `localhost:3306` (troque `MYSQL_PORTA` no `.env` se a porta estiver ocupada), com o banco **vazio** `rh_system`. Quem cria as tabelas é a API, ao iniciar. O servidor usa `utf8mb4`, o fuso `-03:00` e `log_bin_trust_function_creators=1`, os mesmos valores dos testes e do Azure.

## Back-end

### Rodar a API

1. Suba o MySQL (acima).
2. Guarde a chave do JWT localmente, **fora do repositório** (user-secrets; 32 bytes ou mais), e, se quiser um administrador inicial, o e-mail e a senha dele:

```
dotnet user-secrets init --project back-end/src/RHSystem.Api
dotnet user-secrets set "Jwt:Chave" "<uma-chave-com-32-ou-mais-bytes>" --project back-end/src/RHSystem.Api
dotnet user-secrets set "Seed:AdminEmail" "admin@empresa.com.br" --project back-end/src/RHSystem.Api
dotnet user-secrets set "Seed:AdminSenha" "<uma-senha>" --project back-end/src/RHSystem.Api
```

3. Rode: `dotnet run --project back-end/src/RHSystem.Api`. A API sobe em `http://localhost:5000` e, em `Development`, aplica os scripts pendentes ao iniciar (`Database:AplicarScriptsAoIniciar`).

- **A API não sobe sem o banco** quando `AplicarScriptsAoIniciar` está ligado (ela precisa aplicar os scripts) e **não sobe se um script já aplicado foi editado**: a mensagem diz qual. Sem a opção ligada, ela sobe e o `GET /health` responde `503` enquanto o banco não responde.
- O **seed** cria o administrador (funcionário de nível `RH`, sem cargo, setor nem matrícula) uma única vez, só se os dois valores estiverem preenchidos e ainda não existir nenhum usuário. A senha nunca vai para o log.
- **Documentação da API** (só com `Documentacao:Habilitada=true`, ligada em `Development`): o documento OpenAPI em `/openapi/v1.json` e a interface em `/scalar/v1`, sem login. O **contrato** (`contrato/openapi.yaml`) é a fonte de verdade; um teste compara o documento gerado com ele.

### Configuração

| Chave | Para quê | Padrão |
|---|---|---|
| `ConnectionStrings:Padrao` | conexão com o MySQL | só em `Development` (aponta para o compose) |
| `Jwt:Chave` | chave de assinatura do JWT (32 bytes ou mais) | **sem padrão**: user-secrets ou variável de ambiente |
| `Jwt:Emissor`, `Jwt:Audiencia` | emissor e audiência do JWT | `rh-system`, `rh-system-web` |
| `Jwt:AcessoMinutos`, `Jwt:RefreshDias` | vida do acesso e do refresh | 15 e 7 |
| `Database:ServerVersion` | versão do MySQL para o provider | `8.4.0` |
| `Database:AplicarScriptsAoIniciar` | aplica os scripts ao subir | `false` (`true` em `Development`) |
| `Autenticacao:LimiteLoginPorMinuto` | tentativas de login por IP por minuto (`429` acima) | 5 |
| `Cors:OrigensPermitidas` | origens do front que podem chamar a API | nenhuma (`http://localhost:5173` em `Development`) |
| `ProxyReverso:Habilitado`, `ProxyReverso:ProxiesConfiaveis` | confiar em `X-Forwarded-For` de proxies da lista (IPs ou CIDR) | `false` |
| `Documentacao:Habilitada` | publica o documento OpenAPI e a interface | `false` |
| `Seed:AdminNome`, `Seed:AdminEmail`, `Seed:AdminSenha` | administrador inicial | vazios (sem seed) |
| `Email:EnvioAtivo` | liga o serviço em segundo plano que envia a fila de e-mails | `false` (`true` em `Development`): sem provedor configurado a fila só acumula e nada vira `FALHOU` por engano |
| `Email:IntervaloSegundos`, `Email:LoteMaximoPorCiclo` | um ciclo de envio a cada N segundos, com até M mensagens | 10 e 20 (500 convites saem em 25 ciclos) |
| `Email:MaximoDeTentativas`, `Email:ReservaMinutos` | desiste (`FALHOU`) na tentativa N; a mensagem reservada por um processador que caiu volta a valer depois de M minutos | 5 e 5 (esperas de 1, 5, 30 e 120 min entre as tentativas) |
| `Email:Remetente`, `Email:NomeDoRemetente` | de quem o e-mail sai | vazio e `RH System` (`Remetente` é obrigatório com o envio ligado) |
| `Email:Smtp:Servidor`, `Email:Smtp:Porta`, `Email:Smtp:Seguranca` | o servidor SMTP (`Nenhuma`, `StartTls` ou `Ssl`) | vazio, 587, `StartTls` (`localhost`, 1025, `Nenhuma` em `Development`, o Mailpit) |
| `Email:Smtp:Usuario`, `Email:Smtp:Senha` | credencial do provedor | vazios; **a senha só por user-secrets ou configuração do App Service** |
| `App:UrlDoFront` | base dos links dos e-mails (cada renderizador monta o seu link). **Obrigatória, como URL http ou https, com `Email:EnvioAtivo=true`: a API não sobe sem ela** (sem ela cada convite e cada redefinição falhariam em silêncio na fila) | vazio (`http://localhost:5173` em `Development`) |
| `Agendador:Ativo` | liga o serviço em segundo plano do agendador (recuperação na subida e a cada hora, um ciclo a cada N segundos) | `true` (`false` nos testes, que chamam o ciclo direto) |
| `Agendador:IntervaloSegundos`, `Agendador:RecuperacaoMinutos`, `Agendador:LoteMaximoPorCiclo` | um ciclo a cada N segundos, a recuperação a cada M minutos, até L tarefas por ciclo | 30, 60 e 10 |
| `Agendador:ReservaMinutos`, `Agendador:MaximoDeTentativas` | a tarefa reservada por um executor que caiu volta a valer depois de N minutos; desiste (`FALHOU`) na tentativa M | 10 e 5 (esperas de 1, 5, 30 e 60 min entre as tentativas) |
| `Retencao:TarefasDias`, `Retencao:EmailsDias` | por quantos dias o que já terminou fica em `TAREFAS_AGENDADAS` e em `FILA_EMAILS` (o endereço é dado pessoal) antes de a limpeza apagar | 30 e 90 |

**Atrás de um proxy (produção):** ligue `ProxyReverso:Habilitado` **e** informe `ProxyReverso:ProxiesConfiaveis` (IPs ou redes CIDR do proxy da plataforma; a lista certa do Azure é definida no deploy, Task 14). Sem a lista só o `127.0.0.1` é confiável, o `X-Forwarded-For` é ignorado e todas as pessoas dividem o mesmo limite de tentativas de login: a API avisa no log (`Warning`) quando isso acontece.

Nos serviços do Azure, os nomes viram variáveis de ambiente com `__` no lugar de `:` (por exemplo `Jwt__Chave`). Segredo nunca vai para o repositório.

### Testes

```
dotnet test back-end
```

Precisa do Docker ligado (os testes de integração usam um MySQL 8.4 real em contêiner e os **mesmos scripts**). Cada teste que cria banco aplica todos os scripts (cerca de 15 segundos), então a suíte inteira leva vários minutos; durante o desenvolvimento rode por filtro, por exemplo `dotnet test back-end --filter "FullyQualifiedName~Autenticacao"`. A formatação é conferida com `dotnet format back-end --verify-no-changes`.

### O que o gerador de OpenAPI do .NET 10 escreve (medido pela sonda dos testes)

O teste de divergência compara o contrato com o documento nativo da API, então estas medidas importam (`SondaDoOpenApiNativoTests`):

- A versão do documento é a **3.1.1** (o contrato é 3.1.0: mesma família).
- Propriedades não anuláveis dos `record` saem em `required` (e `cargo` e `setor`, que aceitam `null`, também, como no contrato). Anulável sai como `type: ["null", "string"]`.
- Enum em texto sai como `enum` de strings, **sem** `type` (o comparador entende como texto).
- Respostas de erro saem como `application/problem+json`. A rota do documento é `/openapi/v1.json`.
- O .NET aceita número escrito como texto por padrão e então escreve `["integer","string"]`; a API configura `JsonNumberHandling.Strict`, e o documento sai `integer`. Um parâmetro de corpo anulável saía como "null ou objeto" e sem `required`; os endpoints declaram o corpo obrigatório, como o contrato.
- Quando uma rota nova entra, o `operationId` dela entra em `ContratoDivergenciaTests.OperationIdsNoEscopo` **no mesmo passo**.
- **Parâmetro de construtor de `record` sem valor padrão sai em `required`, mesmo anulável.** Medido na Task 11: o contrato declara `cargo` e `setor` do item da equipe como anuláveis e **não** obrigatórios, e a API os marcava `required` até os dois ganharem `= null` no construtor (o JSON enviado é o mesmo). Campo opcional do contrato: padrão no parâmetro (ou propriedade `init` com padrão).
- **Propriedade com conversor de JSON próprio perde o `type` no documento.** Medido: com o conversor de `DateTime` do contrato (`JsonDoContrato`), `acessoExpiraEmUtc` saiu sem tipo (`[]`, e o contrato diz `[string]`). Por isso `EsquemasDoContrato` (um transformador de esquema ligado em `AdicionarDocumentacao`) devolve `string` + `date-time` ao instante e `string` + o padrão `HH:mm` ao horário do dia. Conversor novo, esquema novo no mesmo passo.
- **Quem muda o JSON roda `ContratoDivergenciaTests`.** O conserto acima só apareceu porque a Task 5 rodou esse teste; a Task 4, que criou o conversor, não o rodava.

### Decisões do back-end (medidas)

- **Reserva da fila de e-mails e do agendador: `FromSql` com `SELECT ... FOR UPDATE SKIP LOCKED`, dentro de `ExecutarEmTransacaoAsync`** (sonda `SkipLockedPeloFromSqlTests`, que fica como teste de regressão). Medido no MySQL 8.4 do contêiner: roda e devolve as n primeiras linhas na ordem; com a transação da primeira conexão aberta, um `SELECT ... NOWAIT` na segunda falha com o erro **3572** (controle positivo: a primeira conexão de fato trava as linhas); duas conexões com a MESMA consulta recebem linhas diferentes, sem esperar; e um `1062` dentro da transação não a aborta (o `INSERT` seguinte e o `COMMIT` funcionam). O `EnfileirarAsync` depende do último: chave repetida vira `false`, não perde o resto da transação.
- **A reserva das filas roda em `READ COMMITTED`, não no isolamento padrão.** Medido (testes `DeadlockNaReservaDeEmailsTests` e `DeadlockNaReservaDeTarefasTests`: 4 processadores ao mesmo tempo, 120 itens por rodada, 15 rodadas): com a transação no padrão do MySQL (`REPEATABLE READ`), a reserva (`SELECT ... FOR UPDATE SKIP LOCKED` seguido do `UPDATE` que move a linha de `PENDENTE` para `EM_ENVIO`/`EM_EXECUCAO`) deu **54 a 77 deadlocks** (erro 1213) na fila de e-mails e **73 a 79** no agendador, em todas as rodadas de teste; nenhum item saía duas vezes (a transação desfaz e o ciclo seguinte repete), mas o ciclo falhava. Com `READ COMMITTED` só nessa transação (`IUnidadeDeTrabalho.ExecutarEmTransacaoAsync(..., IsolationLevel.ReadCommitted, ...)`, que vale só para ela), **0 deadlocks** nas três rodadas de cada um. A explicação provável é o bloqueio de intervalo do `REPEATABLE READ` no índice `(SITUACAO, PROXIMA_TENTATIVA_EM)`; não li o `SHOW ENGINE INNODB STATUS`, então o que está medido é o efeito do isolamento, não o tipo de bloqueio. Pelo manual do MySQL (não medi), `READ COMMITTED` com log binário exige `binlog_format=ROW`: o contêiner `mysql:8.4` dos testes e do compose o aceitou (as sondas rodaram); o MySQL do Azure não foi conferido.
- **O EF registrava o VALOR de uma chave repetida no log (medido; corrigido).** Chave de idempotência repetida (erro 1062, "Duplicate entry '<valor>'") vale como sucesso e é engolida pelo código, mas o EF registra a falha de gravação (`CoreEventId.SaveChangesFailed`) em nível `Error`, com a exceção, e a mensagem traz o valor da chave: o teste `ChaveRepetidaNoLogTests` (coletor no log da API inteira, com controle positivo) falhou na fila de e-mails, no lote e no agendador. Quando a chave tiver dado pessoal (na 1b, CPF ou e-mail; no lembrete anônimo, a pessoa), o log o guardaria. Esse evento desce para `Debug` no `AddDbContext`. Medido pelo controle de mutação: só o `SaveChangesFailed` leva o valor; "Failed executing DbCommand" (`RelationalEventId.CommandError`) **não** o levou nesses testes e continua em `Error`, com o SQL. Não medi o texto do `CommandError` para outros tipos de erro do banco.
- **Remetente recusado pelo SMTP é falha temporária.** `550 5.7.1` no `MAIL FROM` (domínio não verificado, remetente não autorizado) vale para toda mensagem; dá-lo como permanente apagava a carga (e o token do convite) de tudo o que o ciclo tentasse. Caixa do destinatário recusada e conteúdo recusado seguem permanentes. A fila repete 5 vezes (1, 5, 30 e 120 minutos); passado esse tempo, a mensagem falha de vez. Não medi contra um provedor real.
- **Pacote com falha conhecida puxado pelo MailKit.** O MailKit/MimeKit pedem `System.Security.Cryptography.Pkcs 10.0.0`, que tem duas falhas de CPU ao interpretar CMS/S-MIME de origem externa (GHSA-846f-f764-76mx, alta; GHSA-2fmv-rv2c-59x7, moderada; corrigidas na 10.0.13). Aqui só se ENVIA e-mail comum, então a exposição é pequena, mas a versão corrigida (10.0.14) está fixada por referência direta em `Infrastructure` e em `Directory.Packages.props`. Versões escolhidas em 2026-11-04: MailKit 4.18.1 (27/09/2026; a 4.18.2 tinha 12 dias) e Mailpit v1.31.6 (15/10/2026).
- **Regra de reserva da autorização** (rota sem política e sem `AllowAnonymous` exige login). Medido com a reserva escrita como `RequireAuthenticatedUser` puro: o anônimo recebia **401** em `/nao-existe`, `/api/nao-existe`, `/api/rh/nao-existe` e em `/openapi/v1.json` e `/scalar/v1` com `Documentacao:Habilitada=false`, e as respostas de rejeição do roteamento (405 de método errado e 415 de tipo de mídia errado) também viravam 401. Hoje a reserva exige login **só de rota real** (`RouteEndpoint`): o que não casou com rota e as rejeições do roteamento seguem como antes (404, 405, 415, todos `application/problem+json`). Rota real sem política segue 401 para o anônimo. Os testes `SondaDaPoliticaDeReservaTests` e `FamiliasDeRotaTests` fixam os dois lados. Não medi o `MapFallback` (a saída que o plano previa); escolhi esta porque mantém o 405 e o 415 da fatia 0.
- **Perfil no token.** `/api/rh`, `/api/gestor` e `/api/eu` olham o claim `perfil` como texto exato (`RH`, `Gestor`, `Funcionario`): token válido sem o claim, com `rh` minúsculo ou com um número recebe 403, inclusive em `/api/auth/eu`.

### Agendador e Always On

O que roda depois, sem ninguém pedir, é uma **tarefa agendada** (`TAREFAS_AGENDADAS`): uma linha com tipo, chave de idempotência e a hora a partir da qual pode rodar. Um serviço em segundo plano (`AgendadorHostedService`) faz, a cada `Agendador:IntervaloSegundos`, um ciclo: reserva até `LoteMaximoPorCiclo` tarefas vencidas com `SELECT ... FOR UPDATE SKIP LOCKED` (a técnica medida na sonda `SkipLockedPeloFromSqlTests`), executa cada uma em escopo próprio (sem `HttpContext`, então o ator da auditoria é o **Sistema**) e a marca `CONCLUIDA`; se lançar exceção, ela volta a `PENDENTE` com espera de 1, 5, 30 e 60 minutos e, na 5ª tentativa, vira `FALHOU`.

- **Cada tarefa é idempotente e recupera o que ficou para trás.** O processo pode cair entre agir e marcar `CONCLUIDA`, então a tarefa confere o estado real antes de agir. Na subida e a cada `RecuperacaoMinutos` o serviço chama `RecuperarAsync` de cada tipo, que cria (pelo `IAgendador`, sem duplicar) o que a parada deixou de fazer.
- **Duas limpezas já existem:** `LIMPAR_TAREFAS` apaga `CONCLUIDA` e `CANCELADA` com mais de `Retencao:TarefasDias` (nunca `PENDENTE`, `EM_EXECUCAO` nem `FALHOU`) e `LIMPAR_EMAILS` apaga `ENVIADO`, `FALHOU`, `DEVOLVIDO` e `CANCELADO` com mais de `Retencao:EmailsDias` (nunca `PENDENTE` nem `EM_ENVIO`). Cada fatia acrescenta as suas (`AddScoped<ITarefaAgendada, ...>`).
- **Exige Always On.** Com o App Service dormindo (plano sem Always On) **nada roda**; ao acordar, a recuperação refaz o que passou, mas um desligamento agendado, por exemplo, só se efetiva depois disso. Always On é recurso do plano pago do App Service: a Task 14 da fatia 0 (Step 2) é o passo em que o dono escolhe o plano.

### Verificação ponta a ponta da fundação (Task 12, 2026-11-04)

Conferência manual com o Docker, em **banco novo**, para o seed do administrador e o evento `CRIOU` do `Sistema` nascerem com a auditoria já ligada. O plano mandava `docker compose down -v`, que apaga o volume do MySQL local de quem desenvolve; em vez disso a conferência rodou num **projeto compose separado**, com volume próprio e portas diferentes, e só esse volume foi removido no fim:

```
MYSQL_PORTA=3307 MAILPIT_SMTP_PORTA=1026 MAILPIT_UI_PORTA=8026 docker compose -p rh-verif up -d --wait
# API em http://localhost:5055 com ConnectionStrings__Padrao (Port=3307), Email__Smtp__Porta=1026, Jwt__Chave e Seed__AdminEmail/Seed__AdminSenha
# efêmeros (variáveis de ambiente do processo; o corpo do login ficou num arquivo fora do repositório, apagado ao fim)
MYSQL_PORTA=3307 MAILPIT_SMTP_PORTA=1026 MAILPIT_UI_PORTA=8026 docker compose -p rh-verif down -v
```

Resultado medido (a API subiu aplicando V001 a V004 num banco vazio):

- **(a)** no log: `Agendador iniciado: 2 tipo(s) recuperado(s); um ciclo a cada 30 s e recuperação a cada 60 min.`, sem nenhuma linha de aviso ou erro; as duas limpezas (`LIMPAR_TAREFAS` e `LIMPAR_EMAILS`) rodaram sozinhas (no log, o `DELETE` de cada uma e, depois dele, o `UPDATE` que grava `CONCLUIDA_EM`; não consultei a tabela para ler a situação).
- **(b)** `POST /api/auth/entrar` com o administrador do seed devolveu `200`; `GET /api/rh/auditoria` listou exatamente 2 eventos: `ENTROU` (ator: o administrador, `PESSOA`) e `CRIOU` (ator: `Sistema`, `SISTEMA`).
- **(c)** `GET /api/eu/contadores-do-menu` devolveu `{"avisosNaoLidos":0,"formulariosPendentes":0,"documentosPendentes":0}`; para o administrador (RH), `/api/rh/contadores-do-menu` devolveu `{"pedidosDeAjusteDePonto":0}` e `/api/gestor/contadores-do-menu`, `403`.
- **(d)** o Mailpit abre (`HTTP 200` na interface) e a caixa está vazia (0 mensagens).

### Cadastro, ficha e acesso (fatia 1b)

O RH cadastra, edita, desliga e readmite colaboradores (`/api/rh/funcionarios`), o gestor lê a própria equipe (`/api/gestor/equipe`) e a pessoa define e troca a senha (`/api/auth/senha/*`, `/api/eu/senha`). **Não houve verificação manual** (Docker, `curl` e Mailpit) desta fatia: o dono decidiu não fazer teste de fumaça. O fluxo é provado na API por `FluxoDeAcessoDePontaAPontaTests` (cadastro com convite, e-mail pela fila, link, senha, entrada, "esqueci minha senha", desligamento com o último dia no passado e readmissão com convite), com o e-mail em memória e o token lido do link da mensagem.

**Configurações novas** (as da tabela acima continuam valendo):

| Chave | Para quê | Padrão |
|---|---|---|
| `Senha:ConviteDias` | validade do link de convite | 7 |
| `Senha:RedefinicaoMinutos` | validade do link de "esqueci minha senha" | 60 |
| `Senha:IntervaloMinimoEntreRedefinicoesSegundos` | dentro deste intervalo do último pedido da mesma pessoa, o seguinte devolve o mesmo 202 e não enfileira nada (zero desliga) | 60 |
| `Autenticacao:LimiteSenhaPorMinuto` | tentativas por minuto, por IP e rota, nas três rotas públicas de senha (`429` acima) | 5 |
| `Autenticacao:LimiteSenhaAtualPorMinuto` | erros da senha atual por minuto, por pessoa, na troca de senha (`429` acima; no mínimo 1) | 5 |
| `Retencao:TokensDeSenhaDias` | por quantos dias um link de senha que já não serve (usado, invalidado ou vencido) fica em `USUARIOS_TOKENS_SENHA`; o último convite de cada pessoa e o link ainda válido nunca saem | 30 |

**O que cada fonte padrão ainda não sabe.** Cada uma é uma peça que a fatia dona registra por DI (`AddScoped`) **depois** da que está registrada hoje; sem ela, a API diz a verdade em vez de inventar:

- `IFonteDeFatosDoDia` (fatia 3, com as férias da 7 e o horário da 1c): só conhece o fim de semana. `situacaoHoje` e `contagensHoje` valem `AINDA_NAO_COMECOU` num dia útil e `SEM_EXPEDIENTE` no fim de semana; nada de ponto, atestado, feriado, férias nem hora-limite.
- `IFonteDeIndicadorDaFicha` (banco de horas: fatia 3; advertências: 4; treinamentos: 6; férias: 7) e `IFonteDoProximoPagamento` (7): não há nenhuma; os indicadores saem `null` (nunca zero inventado).
- `IInclusaoNoOnboarding` (fatia 6) e `IEnvioDoFormularioDeAdmissao` (fatia 5): as versões "indisponível" recusam a caixa marcada com o erro do contrato (422 `SEM_TREINAMENTO_DE_ONBOARDING`; 400 em `formularioAdmissaoId`) e nada fica gravado.
- `ICondicaoParaEfetivarDesligamento` (fatia 3: "o banco de horas daquele dia já fechou"): não há nenhuma e a efetivação vale. O desligamento se efetiva no dia seguinte ao último dia de trabalho (00:00 de -03:00), pela tarefa `EFETIVAR_DESLIGAMENTO`; se esse momento já passou, na hora.

**Dívida registrada: a porta `IPedidosParadosNoGestor` está vazia.** A etapa de um pedido (ponto, férias) é fixada na criação, então "passar ao RH" os pedidos que esperavam por um gestor é uma ação explícita: a 1b a chama ao **efetivar o desligamento** de quem é gestor e na **readmissão** que o tira do setor. O composto padrão não tem nenhuma fatia por trás (as fatias 3 e 7 implementam `IPedidosParadosNoGestorDaFatia` sobre as tabelas delas), então, até lá, **os pedidos parados num gestor desligado, trocado ou readmitido ficam onde estão**. Está também no `contrato/STATUS.md` (rotas de desligamento e readmissão).

**Limitação aceita da troca de senha (claim `rth`).** O token de acesso leva a claim `rth` (o hash do refresh token emitido junto) para a troca manter a sessão atual e revogar as outras. Token emitido antes da claim, ou sem ela, faz a troca revogar **todas** as sessões. E, se outra aba renovou o refresh antes, o token de acesso desta aba ainda leva o `rth` antigo: a troca então revoga o refresh vigente e a sessão dessa aba cai na próxima renovação (em até 15 minutos). É rara, está documentada e testada (`TrocarSenhaTests`).

**Outros comportamentos que a revisão geral da 1b fixou.** Trocar o e-mail corporativo na edição **invalida os links de convite e de redefinição em aberto** (eles foram para o endereço antigo; o RH reenvia o convite ao novo). Editar os benefícios de um colaborador grava o evento "Benefícios de {nome}" com os nomes antes e depois; o **cadastro e a importação ainda não gravam esse evento** (o registro automático é suspenso na importação, e um evento por pessoa contrariaria o evento único de importação: a fatia 2 resolve). Os campos do endereço respeitam os tamanhos do contrato (logradouro 150; número 10; complemento, bairro e cidade 100).

**Ambiente (para o deploy).** O isolamento `READ COMMITTED` das filas e das operações do vínculo (desligar, cancelar, efetivar e readmitir) pede binlog em formato `ROW` no servidor (com `STATEMENT` o MySQL recusa gravar sob `READ COMMITTED`; é o que se sabe do MySQL, não foi medido aqui: conferir no servidor do Azure); o provedor de e-mail e a lista de proxies confiáveis do Azure são definidos no deploy.

### Cadastros de apoio, empresa e documentos (fatia 1c)

**"Gestor ausente" (`PUT /api/rh/gestores/{gestorId}/ausencia`).** Marcar grava a marca em `GESTORES` e chama `IPedidosParadosNoGestor.AprovarEtapaDoGestorAsync` **na mesma transação**: se a porta falha, a marcação é desfeita e a resposta é 500. A resposta repete em `pedidosAprovadosAgora` o `PedidosAprovados` que a porta devolveu, então vale `{ ajustesDePonto: 0, ferias: 0 }` enquanto a porta for o composto vazio da 1a; as fatias 3 e 7 passam a preencher os números ao registrarem a implementação delas (`IPedidosParadosNoGestorDaFatia`). Desmarcar não chama a porta (só os pedidos novos voltam ao normal). Não há tarefa agendada: passada a data de volta, `ausente` continua `true` e `ausenteHoje` vira `false`; a marca vale até o dia anterior à volta, no dia de Brasília. `IGestorAusente`, que as fatias 3 e 7 chamam ao criar um pedido, só responde pelos pedidos novos.

**Setores e a troca de gestor (`/api/rh/setores`).** Trocar ou tirar o gestor roda numa transação só: promove o novo a `Gestor` (cria a linha em `GESTORES` se faltar), rebaixa o antigo a `Funcionario` (a linha dele fica), grava o setor, chama `IGestorDoFuncionario.RefazerDosMembrosAsync` (1b) e `IPedidosParadosNoGestor.PassarParaORhAsync` (1a); qualquer falha desfaz tudo. Os pedidos parados do gestor antigo só passam ao RH quando as fatias 3 e 7 registrarem a implementação delas da porta (o composto padrão é vazio). O contrato diz que um gestor responde por um setor só: a regra é da API (o banco não a impõe) e uma corrida entre duas edições que escolhem o mesmo gestor para setores diferentes não é impedida.

**Arquivos (Blob Storage).** O envio de documento é o primeiro uso de arquivos. `IArmazenamentoDeArquivos` (gravar sem sobrescrever, link temporário de leitura por SAS que baixa como anexo, apagar) é a peça que as fatias seguintes reaproveitam; o tipo do arquivo vem do conteúdo (`IdentificadorDeTipoDeArquivo`), nunca do nome. Em desenvolvimento o armazenamento é o **Azurite**, no `docker compose`: `docker compose up -d --wait azurite` sobe o emulador em `http://127.0.0.1:10000` (conta `devstoreaccount1`, a chave pública do emulador, que `UseDevelopmentStorage=true` já descreve) e os arquivos somem com `docker compose down -v`. A imagem tem versão fixada (3.37.0, conferida em 2026-11-10; nunca `latest`) e roda com `--skipApiVersionCheck`: o SDK `Azure.Storage.Blobs` 12.30.1 fala a versão de API 2026-10-06, que esse Azurite (o mais novo até agora) ainda recusa; tirar o flag quando um Azurite novo a aceitar. Só `ArmazenamentoBlobTests` fala com o Azurite (em contêiner pelo `AzuriteFixture`); o resto dos testes usa o armazenamento em memória.

| Chave (`Armazenamento:`) | Para quê | Padrão |
|---|---|---|
| `ConexaoBlob` | a connection string do Storage Account (traz a chave da conta). **Só por user-secrets ou pela configuração do App Service; nunca no repositório, no log nem na resposta.** Em Development: `UseDevelopmentStorage=true` | vazia |
| `Container` | o contêiner de blobs (privado, criado se não existir) | `arquivos` |
| `LinkValidoMinutos` | quanto tempo vale o link de download | 5 |
| `SomenteHttps` | o link só funciona por HTTPS (`false` em Development: o Azurite fala HTTP) | `true` |
| `SobrasMinimoHoras` | a limpeza `LIMPAR_ARQUIVOS_ORFAOS` só apaga arquivo mais velho que isto (protege o envio que já gravou o blob e ainda não inseriu a linha) | 24 |

Sem `ConexaoBlob` a API **sobe** e a primeira rota que usa arquivo responde 500, com o erro claro no log (nunca com a connection string). A limpeza só toca o prefixo que tem uma `IFonteDeReferenciasDeArquivo` registrada pela fatia dona dos arquivos; se a fonte falha, nada do prefixo é apagado e a tarefa lança para o agendador tentar de novo.

**Documentos do colaborador (`/api/rh/funcionarios/{funcionarioId}/documentos`).** O envio segue a receita do arquivo: o caso de uso lê a pessoa (404) e valida tudo (400, todos os erros juntos, antes de qualquer efeito); grava no armazenamento SEM transação aberta; abre uma transação curta que insere a linha e o evento `Criou`; e, se o blob ou a transação falhar, apaga o blob (o erro original é o que sai; se a limpeza também falhar, a sobra fica para a `LIMPAR_ARQUIVOS_ORFAOS`, que só apaga o que tem mais de 24 horas e nenhuma linha referencia). O tipo vem dos primeiros bytes (PDF, JPG ou PNG, até 10 MiB: 10 MB são 10 MiB); o nome que a pessoa deu é limpo (`NomeParaDownload`) e nunca vai para o log de auditoria, que mostra só que o arquivo mudou. O download devolve um link temporário e grava o evento `Baixou` ANTES de criá-lo (se o registro falhar, não há link). **O formulário é lido de `HttpRequest`, não por `[FromForm]`**, e o corpo `multipart/form-data` do contrato é escrito à mão no OpenAPI (`FormulariosDoContrato`): com `[FromForm]` ou `Accepts` o roteamento recusaria o tipo de mídia (415) antes da autorização, e a matriz de perfis exige 401 e 403 primeiro. Corpo que não é multipart responde 415 e multipart quebrado responde 400. O teto de 16 MiB da requisição vai em `RequestSizeLimit` na rota; **o servidor de teste (`TestServer`) não aplica esse limite**, então ele só vale (e só foi conferido por inspeção) no Kestrel.

**Meus documentos (`/api/eu/documentos`).** Qualquer pessoa logada (Funcionario, Gestor, RH; sem 403) lista, envia e baixa SÓ os documentos dela: o id da pessoa é o do token e documento de outra pessoa dá o mesmo 404 do inexistente. Enviar só vale para documento `PENDENTE` (que o RH pediu) e segue a mesma receita do arquivo, na ordem 404, 400 (arquivo), 422 `DOCUMENTO_NAO_PEDIDO`; a validação e o "gravar o blob, efetivar, apagar o blob se falhar" são um código só (`ValidacaoDeArquivoDeDocumento` e `EnvioDeArquivoDeDocumento`), usado também pelo RH. **Dois envios ao mesmo tempo:** a `Situacao` é o token de concorrência do `UPDATE`; o segundo a salvar não acha a linha pendente, vira 422 `DOCUMENTO_NAO_PEDIDO` e apaga o blob dele (sobra um arquivo só, o do vencedor). Baixar o próprio documento não grava evento (só o RH baixando o de outra pessoa é auditado). A bolinha `documentosPendentes` de `GET /api/eu/contadores-do-menu` vem de `FonteDeDocumentosPendentes`: uma consulta `COUNT` dos `PENDENTE` da própria pessoa (as rotas do gestor e do RH não têm o campo).

**Pergunta aberta ao dono (decisão 1 do plano da 1c).** Ao escolher como gestor de um setor uma pessoa **de outro setor**, a API (a) deixa o setor dela como está, (b) recusa, ou (c) muda o setor dela. Está **implementada a (a)**: ele gerencia o setor sem fazer parte dele (`quantidadePessoas` do setor não o conta) e o `ID_GESTOR` dele é refeito como o de qualquer gestor do setor. Enquanto gerencia o setor ele fica sem `ID_GESTOR` (regra da 1b: o gestor do setor não tem gestor) e, ao ser trocado ou tirado, volta a ter o gestor do setor de origem dele. Só (b) ou (c) pedem contrato novo (um erro novo, ou um efeito novo descrito).

### Importação de colaboradores por Excel (fatia 2)

**Rotas (todas só do RH).** `GET /api/rh/importacoes/modelo` baixa o `.xlsx` modelo, gerado na hora com os cargos, setores e benefícios de hoje (aba "Valores válidos"); `POST /api/rh/importacoes` (`multipart/form-data`, campo `arquivo`) lê a planilha, valida **todas** as linhas **sem cadastrar ninguém** e devolve a prévia (201, sem `Location`); `POST /api/rh/importacoes/{importacaoId}/confirmar` cadastra todo mundo ou ninguém. **Limites:** só `.xlsx`, até **5 MiB** (5 MB são 5 MiB, como os 10 MB dos documentos) e **500 linhas de dados**; o zip descompactado até 50 MiB, 100 entradas, 10 planilhas e 200 000 células (estourar qualquer um é 422 `ARQUIVO_MUITO_GRANDE`). O corpo da requisição tem teto de 6 MiB (`RequestSizeLimit`; de 5 a 6 MiB responde o 422 do contrato). **O servidor de teste não aplica esse teto** (só o Kestrel): a rota o declara e o teste confere o que ela declara. Problema no arquivo todo é 422 com `codigo` (`ARQUIVO_INVALIDO`, `ARQUIVO_MUITO_GRANDE`, `MODELO_ALTERADO`, `PLANILHA_VAZIA`, `LIMITE_DE_LINHAS_EXCEDIDO`), sem prévia e sem rastro.

**Como o Excel é lido.** O leitor usa `System.Xml.XmlReader` direto sobre o `ZipArchive` (`DtdProcessing.Prohibit`, sem resolvedor de entidades): um DOCTYPE no XML da planilha morre na hora (nada de XXE nem "billion laughs"), e o tamanho descompactado é conferido pelo declarado e pelos bytes realmente lidos. O `DocumentFormat.OpenXml` 3.4.1 (MIT) fica só no gerador do modelo e nos testes. Só a aba "Colaboradores" é lida; a linha 1 tem de trazer os 11 títulos do modelo, na ordem (comparados sem diferenciar maiúsculas, acentos nem espaços nas pontas), senão `MODELO_ALTERADO`. CPF digitado como número ganha os zeros à esquerda até 11 dígitos; a data vem de célula de data (a planilha no sistema 1904 é respeitada) ou do texto `d/M/aaaa`. Cargo, setor, benefício (separados por `;`), tipo de contrato e nível casam **sem diferenciar maiúsculas, acentos nem espaços nas pontas** (mais frouxo que o contrato; o banco já proíbe dois nomes iguais ignorando acentos, então nunca há ambiguidade).

**A prévia.** O montador converte cada linha e chama `ICadastroDeFuncionarios.ValidarAsync` **uma vez** com todas: são as mesmas regras do cadastro da tela 02 (campos, CPF e e-mail repetidos no arquivo e no sistema, cargo, setor, gestor do setor inativo), com as mensagens ligadas à coluna e à linha do Excel (CPF ou e-mail de cadastro **inativo** manda usar "Readmitir"; a mensagem não diz o nome de quem é). A importação vira uma linha de `IMPORTACOES` (`PREVIA` em JSON e o arquivo em `importacoes/`) que vale **1 hora**; a linha e o evento `Importou` (só os totais: nunca o nome do arquivo nem as linhas) vão numa transação curta, e o arquivo é gravado antes, **fora de qualquer transação**, e apagado se algo falhar (a receita da 1c, com a mesma classe `EnvioDeArquivoDeDocumento`).

**A confirmação.** Ordem das conferências: 404, 409 `IMPORTACAO_JA_CONFIRMADA`, 422 `IMPORTACAO_EXPIRADA` (a prévia vence **no instante** de `EXPIRA_EM`), 422 `IMPORTACAO_COM_ERROS`, e só então as linhas são convertidas **de novo** com o catálogo de agora (cargo apagado depois da prévia recusa a linha dele). Uma transação só: a importação é **reivindicada primeiro** (`UPDATE ... WHERE SITUACAO = 'PRONTA' AND EXPIRA_EM > agora`, que também limpa a prévia e o caminho do arquivo), depois `CadastrarAsync` cadastra, pelo mesmo caminho da tela 02, com o registro automático da auditoria suspenso (**um** evento `Importou` com os totais, nenhum por pessoa; o convite continua tendo o evento de acesso dele, "Login criado e convite enviado", como no cadastro da tela). Duas confirmações juntas dão um 200 e um 409. Qualquer recusa, inclusive a do banco na linha 500, desfaz tudo (nenhum colaborador, nenhum convite na fila, a importação continua `PRONTA`) e responde 422 `IMPORTACAO_LINHA_RECUSADA` com `linha` (a do Excel) e, se foi o banco, `regraDoBanco`. O arquivo é apagado **depois** do commit; se isso falhar a importação continua confirmada e a `LIMPAR_ARQUIVOS_ORFAOS` o apaga. Qualquer RH confirma a importação de qualquer RH (o contrato não limita).

**Enquanto as fatias 5 e 6 não existem.** As caixas "depois de salvar" usam as versões "indisponível" da 1b: `incluirNoOnboarding: true` dá 422 `SEM_TREINAMENTO_DE_ONBOARDING` e `formularioAdmissaoId` informado dá 400 em `formularioAdmissaoId`, e **nada é cadastrado**. A importação **não** marca "Em admissão": a situação cadastral vem da data de admissão de cada linha, como no cadastro.

**Limpeza: `LIMPAR_IMPORTACOES_VENCIDAS`, por hora.** A prévia e o arquivo têm nome, CPF, nascimento, telefone e e-mail de gente que talvez nem seja cadastrada, então somem quando a importação vence, sem esperar o dia seguinte. A tarefa é recuperada na subida e a cada hora (chave estável dentro da hora) e **cada execução agenda a da hora seguinte** (a recuperação conta 60 minutos do FIM da anterior, então o espaçamento deriva e poderia pular uma hora), e limpa até 100 importações por execução, as mais antigas primeiro; a linha fica com os totais. O arquivo é apagado antes; se ele não puder ser apagado a **prévia some mesmo assim** e o caminho fica para a próxima execução tentar de novo (a tarefa lança, sem o texto da exceção).

**Privacidade no log (achado desta fatia).** As consultas de CPF e e-mail repetidos do cadastro usam `Contains` sobre uma lista, e o provedor MySQL escreve os **valores dentro do texto do SQL** (a tentativa de mandar a lista como parâmetro, `TranslateParameterizedCollectionsToParameters`, deu 500 com o Pomelo 9.0.0). O EF Core registra o texto do comando (`Executed DbCommand`) em nível `Information`, então CPF e e-mail de quem estava sendo cadastrado iam para o log. Agora o evento `RelationalEventId.CommandExecuted` desce para `Debug` **por código** (`DependencyInjection.cs`, ao lado do `SaveChangesFailed`), e não por uma chave do `appsettings.json` que um operador possa mudar: mesmo com a categoria `Microsoft.EntityFrameworkCore.Database.Command` ligada em `Information`, o log não leva o valor (o teste `Os_valores_da_previa_nao_aparecem_no_log` prova os dois casos). Só quem liga o nível `Debug` dessa categoria vê o SQL (e os valores). **Resíduo conhecido:** o erro de comando (`Failed executing DbCommand`, nível `Error`) continua saindo com o SQL para diagnosticar falha de verdade, e por isso **pode trazer esses valores se uma dessas consultas falhar** (timeout, deadlock).

**Perguntas em aberto ao dono.** (1) Quais são as colunas do modelo? Foram as do cadastro mínimo da tela 02 sem o gestor (que vem do setor); mudar colunas muda `ColunaDaImportacao` e o contrato, só. (2) A importação deve marcar todo mundo "Em admissão"? Hoje não. (3) O limite de 500 linhas e 5 MiB serve? (4) Um RH poder confirmar a importação de outro RH serve?

### Ponto (fatia 3)

**Rotas (34).** O dia da própria pessoa (`obterPontoHoje`, `registrarMarcacaoDePonto`, espelho do mês, extrato do banco de horas, pedidos de ajuste e atestados dela), a equipe do gestor (ponto do dia, espelho, pedidos para a etapa dele, atestados, PDF) e o RH (espelho e PDF de qualquer pessoa, correção direta de um dia, decisão final dos pedidos, atestados, feriados, extrato e exportação para a folha). `contrato/STATUS.md` lista cada uma; o comparador de contrato cobre as 34.

**Tempo e contas.** Todo instante dentro da aplicação é UTC (`IRelogio`); o banco guarda `DATETIME` em horário de Brasília (-03:00) e o EF converte. O dia do ponto é o da ENTRADA: a jornada que atravessa a meia-noite vale até **12 horas** depois da entrada (decisão provisória do contrato). As horas são contadas em **minutos inteiros** com os segundos de cada batida descartados (o que a tela mostra); as funções de horas do banco não são usadas (duas casas de hora erram até 0,3 minuto por dia).

**Pedido de ajuste, em duas etapas.** A etapa não tem coluna: sai de `STATUS`, `ID_GESTOR` e `RESULTADO_GESTOR` (`EtapaDoPedidoDeAjuste`). O gestor aprova a etapa dele (o pedido segue `PENDENTE` para o RH) ou recusa (encerra); o RH decide de forma final e **aprovar aplica os itens tudo ou nada** (a sequência é conferida em memória antes de gravar). A decisão é um `UPDATE` condicional, então duas abas, o gestor ausente e o RH nunca decidem duas vezes (o 409 certo sai pelo estado de agora). Gestor ausente faz o pedido nascer com a etapa dele aprovada sozinha; trocar, tirar ou desligar o gestor passa os pedidos parados ao RH (`IPedidosParadosNoGestorDaFatia`). O e-mail `RESULTADO_AJUSTE_PONTO` sai a cada decisão, na mesma transação. O RH também corrige um dia direto (`corrigirMarcacoesDoDia`: a lista substitui o dia, sem segunda etapa nem e-mail; a batida antiga fica no log).

**Atestados.** PDF, JPG ou PNG pelos primeiros bytes, até 5 MiB; é dado de saúde: o evento nunca leva o nome do arquivo nem o motivo, e o `ATESTADOS`/`BAIXOU` é gravado **antes** de o link existir. Decisão do RH final por `UPDATE` condicional (409 `ATESTADO_JA_DECIDIDO`); validar recalcula os dias já passados do atestado.

**Feriados.** A lista traz os que valem para a empresa (nacionais, estaduais da UF, municipais do município e os da empresa) e não dá erro sem a empresa configurada; o RH cadastra e edita só o municipal (409 na mesma data e nome, que o banco não diferencia por caixa nem acento). `IMPORTAR_FERIADOS:ano=` traz os nacionais **calculados** (Sexta-feira Santa pela Páscoa; Carnaval e Corpus Christi ficam de fora, como na carga do `V001`) e os estaduais da UF da empresa, sem duplicar por data e tipo (a carga de 2026 tem nomes sem acento); `REPETIR_FERIADO:ano=` copia o municipal marcado para repetir (o 29/02 só em ano bissexto). As duas se recuperam na subida e a cada hora para o ano de hoje e o seguinte.

**Banco de horas.** A linha do dia (`CREDITO` "Hora extra", `DEBITO` "Saldo negativo do dia" ou "Falta") nasce de `CONSOLIDAR_BANCO_HORAS:dia=`, que roda a partir das **12:00 (-03:00) do dia seguinte** e se recupera até 60 dias para trás; o dia incompleto ou com pedido em análise não lança (a linha antiga espera) e saldo zero apaga a linha. O recálculo do dia também roda depois de um pedido aprovado, de uma correção do RH e de um atestado validado; inativo só atualiza a linha que já tem. O `saldoMinutos` é a soma dos minutos **de cada linha** (nunca `FN_CALCULAR_SALDO_BANCO_HORAS`) e `atualizadoAte` é o último dia consolidado. **O desligamento só efetiva depois de o último dia da pessoa estar consolidado.**

**Exportação para a folha e PDF.** O CSV (UTF-8 com BOM, `;`, CRLF, uma linha por pessoa ativa em algum dia do mês, nenhum número negativo, texto neutralizado contra fórmula) usa os mesmos números do espelho. O PDF do espelho usa **PDFsharp-MigraDoc 6.2.4 (MIT)** com a fonte **Noto Sans embutida** (SIL OFL 1.1; o texto da licença está em `back-end/src/RHSystem.Infrastructure/Ponto/Fontes/`), então nenhuma fonte do sistema é usada e o PDF sai igual num contêiner sem fontes; é gravado em `espelhos/` e entregue por link temporário (a limpeza de sobras o apaga depois das horas mínimas). A versão do pacote fica fixada: conferir a licença no nuspec antes de subir.

**Perguntas em aberto ao dono.** (1) **Pedido de ajuste parado quando a pessoa muda de setor** fica com o gestor antigo, que deixa de vê-lo, e ninguém decide a etapa dele (dívida: não há porta na edição do colaborador). (2) **Hora extra e tolerância** (contrato `x-provisorio`): todo saldo positivo vira crédito, sem adicional, e a tolerância só marca o atraso. (3) **Formato da folha** (colunas, minutos, uma linha por pessoa) é o do contrato, provisório. (4) A **janela de 12 horas** da jornada que atravessa a meia-noite. (5) A **tabela de feriados estaduais** é uma primeira versão de datas fixas (ES, GO, MG e MT sem linha): conferir antes de produção. (6) Na **primeira subida** a consolidação cria 61 tarefas (60 dias para trás) e o lote do agendador (10 por ciclo) as processa em alguns ciclos. (7) Feriado criado, editado ou desativado em data já consolidada **não** recalcula o banco de horas.

### Comunicação e documentos (fatia 4)

**Rotas (35).** Avisos do RH (lista, resumo, criar, editar, excluir, publicar, encerrar, leituras e "lembrar quem não viu") e da própria pessoa (lista, abrir, marcar como lido); holerites (o RH envia, substitui e envia em lote; lista e baixa; a pessoa lista e baixa os dela); advertências (o RH registra, lista, resume, abre e responde à contestação; o gestor faz o mesmo só com a equipe dele, sem ver a contestação; a pessoa lista, confirma a ciência e contesta); aniversariantes; e o próximo pagamento, que sustenta o "no dia do pagamento" do holerite. `contrato/STATUS.md` lista cada rota; o comparador de contrato cobre as 35.

**E-mail e tarefas.** Nenhum e-mail de aviso ou de holerite sai dentro do pedido HTTP: a publicação grava a tarefa `PUBLICAR_AVISO:aviso={id}:em={instante}` / `PUBLICAR_HOLERITE:holerite={id}` (com `ExecutarApos` no instante em que fica visível) e é ela que enfileira a mensagem, uma por pessoa, idempotente pela chave. O "lembrar quem não viu" enfileira **fora de transação** (um teste de cliques em paralelo achou um deadlock) e a chave leva o minuto. As advertências enfileiram dentro da transação do registro: `ADVERTENCIA_REGISTRADA` à pessoa (sempre) e, quando o gestor registra, **uma cópia a cada pessoa ativa do RH com e-mail** (o gestor nunca recebe cópia); `ADVERTENCIA_RESPONDIDA` com o texto do RH. A contestação não manda e-mail. O e-mail da advertência diz só o tipo e o dia: o motivo e a descrição ficam no RH-System.

**Arquivos.** O holerite é só PDF (pelo conteúdo), até 5 MiB; o pedido aceita até 6 MiB para o erro de arquivo grande ser o 400 do contrato e não o 413. O lote aceita até 200 PDFs e 100 MiB (lidos como MiB), cada item é independente (sempre 200, com o resultado de cada um) e um PDF acima de 5 MiB é erro do item. O banco nunca apaga holerite: a correção é uma **versão nova**; a pessoa vê só a maior versão **já publicada** de cada competência e tipo (com a versão 2 agendada, a 1 continua à vista). O download é por link temporário e grava o evento `BAIXOU` **antes** de o link existir.

**Contas.** Todo instante gravado vai cortado ao segundo e em UTC na aplicação (-03:00 no banco). O dia do ocorrido, a publicação agendada (começo do dia -03:00), a competência do holerite e a janela dos aniversários usam o dia de hoje **no horário do Brasil**. Cada mudança de situação da advertência é um `UPDATE` condicional na situação de partida (ciência e contestação em paralelo têm um efeito só; a primeira contestação nunca é sobrescrita; o desfecho da contestação não tem coluna e é derivado). A contagem de advertências é uma só em todas as telas: as **não canceladas do ano**.

**Perguntas em aberto ao dono.** (1) **Pagamento:** a regra do dia do pagamento (antecipa para o dia útil anterior; exceção por mês; 36 meses de busca) e quem a configura (decisões 2, 7, 8, 9 e 10 do plano). (2) **Cópia ao RH** vai para todas as pessoas ativas com nível RH e e-mail: confirmar se é isso ou um endereço único. (3) O texto do erro `ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO` (o contrato não traz exemplo; quase inalcançável, porque o gestor do setor fica sem gestor). (4) A **retenção do holerite de quem saiu**: hoje ele fica para sempre e o RH baixa quando quiser. (5) Se a fatia 7 liga o `proximoPagamento` da ficha (a 4 só entrega a porta). (6) **100 MB do lote** lidos como 100 MiB. (7) **413** para um holerite individual acima de 6 MiB. (8) A **data do ocorrido** da advertência pode ser anterior à admissão da pessoa (o contrato não diz). (9) O RH que registra aparece como "RH" sem nome (o banco não guarda quem foi).

### Formulários (fatia 5)

**Rotas (32).** O construtor do RH (criar, editar, perguntas e alternativas, destinatários, salvar a publicação, publicar, encerrar, excluir, lista e detalhe); "quem respondeu", ver respostas, resultado, exportação em CSV e "lembrar quem falta"; os candidatos do formulário por link (lista, respostas e currículo) e os avaliados de uma avaliação; as telas da própria pessoa (meus formulários, abrir, responder e minhas respostas); os formulários da equipe do gestor (lista, quem respondeu e lembrete, sem ver conteúdo); e as duas rotas **públicas** do candidato (a página e a candidatura, sem login). `contrato/STATUS.md` lista cada rota; o comparador de contrato cobre as 32.

**Tipos e público.** Pesquisa, avaliação (a equipe avalia o gestor ou o gestor avalia a equipe), admissão, entrevista (a do link) e Sugestão (vários envios por pessoa); o público é por pessoas, por setores ou por **link** (só entrevista). Quem deve responder é calculado **na hora** (pessoa ativa com admissão começada); só o formulário **anônimo** congela uma fotografia na publicação. A avaliação da equipe anônima é **proibida** (cada pessoa recebe uma só avaliação e o resultado nunca apareceria).

**Anonimato.** A resposta anônima é gravada sem autor, sem arquivo e com a data grossa do **dia da publicação da rodada**; quem participou fica numa tabela à parte, sem ligação com as respostas (uma participação por pessoa e por avaliado, também com dois envios ao mesmo tempo). Resultado, exportação e lista de avaliados só existem **depois de encerrado de verdade** (o prazo vencido com a tarefa parada conta), com o mínimo de 3 respostas por pergunta e, nas avaliações, 3 por avaliado. Nada de auditoria, log ou e-mail identifica quem respondeu: o lembrete do anônimo sai por uma fila sigilosa (sem funcionário, apagada ao enviar).

**Tarefas agendadas.** `PUBLICAR_FORMULARIO` e `ENCERRAR_FORMULARIO` (data e prazo, com recuperação), `LEMBRETE_FORMULARIO` (lembrete automático e o geral, sigilosos no anônimo), `REABRIR_FORMULARIO` (o formulário recorrente: só reabre o que está **encerrado**, apaga respostas, participações e arquivos da rodada anterior, avança o prazo e o anônimo exige 5 pessoas ativas) e `APAGAR_CANDIDATOS_VENCIDOS` (a retenção: diária, apaga o candidato com mais de 180 dias, as respostas dele e, depois da transação, o currículo e os arquivos). Todas idempotentes pela chave e recuperáveis na subida e a cada hora.

**Link público e candidatos.** `GET /api/publico/formularios/{token}` (30 consultas por minuto por IP) e `POST .../respostas` (10 envios por hora por IP, `multipart/form-data`) respondem **antes de qualquer outra regra** com 429 e `Retry-After`; código inexistente, de rascunho ou fora do padrão é sempre o **mesmo 404**. A candidatura valida nome, e-mail (aparado, minúsculas, único por formulário em qualquer caixa e em corrida), telefone, aceite do aviso, respostas e arquivos (currículo e anexos: PDF ou Word até 5 MB pelo **conteúdo**), é tudo ou nada com a receita do blob e **nunca** grava auditoria, log ou erro com dado pessoal. O RH vê a lista (mais recentes primeiro, busca em nome e e-mail), as respostas e baixa o currículo por link temporário (`curriculo-<nome>.<ext>`, evento `BAIXOU` sem dado pessoal); candidato de outro formulário é 404. Configuração (`appsettings.json`, seção `Formularios`): `LimiteConsultaPorMinuto` (30), `LimiteEnvioPorHora` (10) e `RetencaoDeCandidatosDias` (180).

**Arquivos.** PDF ou Word (`.docx` = ZIP com `word/document.xml`; `.doc` = assinatura OLE2) até 5 MB, o tipo sempre pelo conteúdo, o nome limpo (sem diretório nem aspas) e baixado só como anexo por link temporário; o caminho do armazenamento nunca sai em resposta nenhuma. Arquivo em formulário anônimo não existe.

**Contas.** Todo instante gravado vai cortado ao segundo e em UTC na aplicação (-03:00 no banco); a resposta com nome e a Sugestão gravam todas as linhas de um envio com o **mesmo instante**, e é assim que o envio se separa de outro da mesma pessoa. Publicar, encerrar e responder se excluem pela linha do formulário (`UPDATE` condicional): responder contra encerrar na mesma hora dá 422 e nada gravado.

**Perguntas em aberto ao dono.** (1) **Proteção do link público:** hoje só limite por IP (30/min e 10/h), sem captcha. (2) **Retenção de candidato:** 180 dias é um valor de partida; o aceite do aviso de privacidade é exigido mas **não fica registrado** (o banco não tem coluna: pedir ao responsável pelo esquema). (3) **Recorrência:** a reabertura só acontece com o formulário encerrado e o por link não pode ser recorrente. (4) **Sem rota** para mudar o prazo de um publicado nem devolver um agendado a rascunho (o RH exclui e refaz). (5) **Avaliação da equipe anônima** proibida. (6) Um **OLE2 que não é Word** (`.xls`, `.ppt` renomeados) passa como `.doc`. (7) Os textos de erro sem exemplo no contrato foram escritos pelo executor (lista no ledger da fatia). (8) O gestor de uma equipe de **uma só pessoa** num anônimo encerrado vê se ela participou (a regra do contrato fala em "quantas pessoas da equipe" e não trata esse caso). (9) O código 422 `FORMULARIO_JA_RESPONDIDO` (pessoa que já respondeu) não existe no contrato: o front precisa tratar o código desconhecido pelo texto `detail`.

## Front-end

O front-end (React 19, TypeScript, Vite) está em `front-end/`, feito por Lucas num repositório à parte e trazido para cá (fatias 0 a 6 prontas; a fatia 7 começou e não está completa). O plano está em `docs/plano-front-end/` (comece por `LEIA-PRIMEIRO.md`) e o andamento, em `PROGRESSO.md` e `DUVIDAS.md` da mesma pasta.

### Rodar o front para ver as telas

Precisa de **Node** na faixa de `package.json` (`^22.22.2` ou `^24.15.0`; confira com `node -v`). Da raiz do repositório:

```
cd front-end
npm ci
npm run dev
```

Abra **http://localhost:5173**. O `npm run dev` primeiro gera os tipos e os dados simulados a partir de `contrato/openapi.yaml` (`predev`) e depois sobe o Vite.

- **Os dados são simulados (MSW), e a API não precisa estar de pé.** O front intercepta as chamadas `/api/...` no navegador e responde com os exemplos do contrato. É o padrão em `npm run dev`; não precisa de arquivo `.env`.
- **O login simulado ignora o que você digita** (use um e-mail com formato válido e qualquer senha) e **entra sempre como Ana Souza, perfil RH**. Os dados de exemplo são os mesmos para qualquer id (a tela de cada pessoa mostra os mesmos dados). **Não há seletor de perfil hoje**: com os dados simulados, as telas de gestor e de funcionário não aparecem (o exemplo do login vem do contrato e a pasta `src/shared/mocks/gerado/` é gerada de novo a cada `npm run dev`, então não adianta editá-la).
- **Falar com a API de verdade:** copie `front-end/.env.example` para `front-end/.env.local` e ponha `VITE_MOCKS=nao` (e `VITE_API_URL=http://localhost:5000`, a API rodando como em "Rodar a API"). **Hoje só existem na API o login, a sessão, a saúde e a auditoria** (ver `contrato/STATUS.md`): as outras telas dão erro nesse modo até as rotas ficarem prontas.
- **Só os componentes:** `VITE_VITRINE=sim npm run dev` (no PowerShell: `$env:VITE_VITRINE="sim"; npm run dev`) habilita a rota `/eu/vitrine`, uma página de prova dos componentes.

Outros comandos de `front-end/`: `npm test` (Vitest), `npm run lint`, `npm run build` (gera `dist/`), `npm run csp` (confere a política de segurança do site, precisa do Edge ou do Chrome instalado) e `npm run format:check`. **Hoje o `format:check` falha** em 6 arquivos de `src/features/ferias/` (a fatia 7 começada e sem formatar).
