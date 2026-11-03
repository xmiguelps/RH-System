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

Será descrito aqui quando a API existir (fatia 0, Task 9).

## Front-end

Será descrito aqui quando o front existir (plano em `docs/plano-front-end/`).
