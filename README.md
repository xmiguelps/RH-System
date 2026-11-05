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
| `App:UrlDoFront` | base dos links dos e-mails (cada renderizador monta o seu link) | vazio (`http://localhost:5173` em `Development`) |
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
- **Propriedade com conversor de JSON próprio perde o `type` no documento.** Medido: com o conversor de `DateTime` do contrato (`JsonDoContrato`), `acessoExpiraEmUtc` saiu sem tipo (`[]`, e o contrato diz `[string]`). Por isso `EsquemasDoContrato` (um transformador de esquema ligado em `AdicionarDocumentacao`) devolve `string` + `date-time` ao instante e `string` + o padrão `HH:mm` ao horário do dia. Conversor novo, esquema novo no mesmo passo.
- **Quem muda o JSON roda `ContratoDivergenciaTests`.** O conserto acima só apareceu porque a Task 5 rodou esse teste; a Task 4, que criou o conversor, não o rodava.

### Decisões do back-end (medidas)

- **Reserva da fila de e-mails e do agendador: `FromSql` com `SELECT ... FOR UPDATE SKIP LOCKED`, dentro de `ExecutarEmTransacaoAsync`** (sonda `SkipLockedPeloFromSqlTests`, que fica como teste de regressão). Medido no MySQL 8.4 do contêiner: roda e devolve as n primeiras linhas na ordem; com a transação da primeira conexão aberta, um `SELECT ... NOWAIT` na segunda falha com o erro **3572** (controle positivo: a primeira conexão de fato trava as linhas); duas conexões com a MESMA consulta recebem linhas diferentes, sem esperar; e um `1062` dentro da transação não a aborta (o `INSERT` seguinte e o `COMMIT` funcionam). O `EnfileirarAsync` depende do último: chave repetida vira `false`, não perde o resto da transação.
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
