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

Sobe o MySQL 8.4 em `localhost:3306` (troque `MYSQL_PORTA` no `.env` se a porta estiver ocupada), com o banco **vazio** `rh_system`. Quem cria as tabelas é a API, ao iniciar. O servidor usa `utf8mb4`, o fuso `-03:00` e `log_bin_trust_function_creators=1`, os mesmos valores dos testes e do Azure.

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

## Front-end

Será descrito aqui quando o front existir (plano em `docs/plano-front-end/`).
