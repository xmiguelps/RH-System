# Fatia 8: fechamento (o sistema pronto para a banca) Implementation Plan

> **Para quem executa:** esta fatia não tem rota nova: é DER/MER, seed fictício, ensaio automático do roteiro e documentos. Foi escrita **depois** da execução, que seguiu o roadmap (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, "Fatia 8: fechamento"); o ledger `.superpowers/sdd/2026-11-13-back-end-fatia-8-fechamento/progress.md` (git-ignorado) tem um `Ruling:` por decisão.

**Goal:** deixar o sistema pronto para a banca: o modelo do banco em texto, uma demo com dados fictícios que sobe com um comando, o roteiro ensaiado por testes e a lista honesta do que o sistema não faz e do que custa.

**Architecture:** o seed é SQL puro (`Banco de Dados/Demo/seed_demo.sql`), fora de `Scripts/` (não é versão), parametrizado por `@hoje` e `@dominio`; os testes o rodam contra um MySQL em contêiner e usam a API inteira por cima, entrando pelo login de verdade. O DER sai de um script Python que lê `V001` a `V005`.

**Tech Stack:** MySQL 8.4, xUnit, Testcontainers, Python 3 (só o gerador do DER).

**Spec:** roadmap, seção "Fatia 8: fechamento".

## Global Constraints

- Nenhum dado de pessoa real: nomes, CPFs (dígitos válidos, de ninguém), telefones e e-mails inventados; e-mails em domínio reservado (`.example`), com o envio desligado por padrão.
- O seed não é um script de versão e não muda o esquema; `V001` a `V005` ficam como estão.
- Nada de preço inventado nos custos; nada de afirmação de conformidade legal do ponto.

## Review Focus

1. **O seed não quebra com outro dia, outra collation nem uma segunda rodada:** `@hoje` fixo no passado e no fim de semana, `@dominio` diferente, segunda rodada falha cedo sem duplicar.
2. **O que os documentos afirmam existe:** cada passo do roteiro tem o teste que o ensaia; as limitações e os custos não dizem nada que o código ou o plano não sustente.
3. **Dados pessoais e segredos:** nada real no seed; a senha pública é só de demonstração e está dita como tal.

---

## Task 1: DER/MER gerado dos scripts

`Banco de Dados/Modelagem/gerar_der.py` lê os `CREATE TABLE` e as chaves estrangeiras (dentro do `CREATE TABLE` e em `ALTER TABLE ... ADD CONSTRAINT`) e escreve `DER_gerado.md` (Mermaid `erDiagram`, tabelas em ordem alfabética, determinístico; `--conferir` sai com 1 se o arquivo estiver velho). Teste `Esquema/DerGeradoTests`: as tabelas e o número de chaves do arquivo são os dos scripts embutidos (RED visto: tirar uma relação do arquivo derruba o teste).

## Task 2: seed fictício da demo

`Banco de Dados/Demo/seed_demo.sql` + `LEIA-ME.md`. Uma empresa inventada: configuração, 8 cargos, 3 setores, 12 pessoas (RH, 2 gestores, 8 funcionários e 1 que começa em 5 dias) com login e senha de demonstração, ponto dos últimos dias úteis (atraso, falta e férias de mentira), banco de horas, avisos, férias (em gozo, esperando o gestor, esperando o RH, concluída), benefícios com dependente e trajeto, documentos pedidos, advertência, exceção de pagamento, 3 formulários e 2 treinamentos (um com certificado). Testes `Demo/SeedDaDemoTests`: o arquivo roda inteiro, cada pessoa entra, os painéis do RH, do gestor e da pessoa mostram o que a demo promete, as regras do banco aceitam tudo, roda em 4 dias diferentes e a segunda rodada falha no primeiro comando.

## Task 3: ensaio automático do roteiro

`Demo/FluxosDaDemoTests`: um teste por passo do roteiro, pela API de verdade sobre o seed (entrar e bater as quatro batidas; pedir, aprovar e confirmar férias com um e-mail por decisão; RH envia o holerite e a pessoa recebe o link; atestado enviado e validado; importação da planilha com prévia e confirmação; concluir o treinamento e conferir o certificado público; responder o formulário de admissão; o log nascendo das ações).

## Task 4: documentos

`docs/demo/roteiro-da-demo.md`, `limitacoes.md`, `custos-e-como-desligar.md` e a seção do `README.md`, com as perguntas ao dono.

## Task 5: fechamento

Build limpo, `dotnet format --verify-no-changes`, suíte inteira em três ondas (`suite-gestao.sh` passa a incluir o namespace `Demo`), revisor fresco, uma rodada de correção, PR com `--squash --admin`, memória e relatório final.
