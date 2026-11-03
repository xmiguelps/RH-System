# Mudanças do contrato

Regra de versão: ver `CONVENCOES.md`, seção 14. A versão fica em `fontes/00-base.yaml` (`info.version`).

## 0.2.2 — 2026-11-03 (rascunho)

- Só texto (versão de correção, `CONVENCOES.md`, seção 14): as citações aos scripts de banco `V006` a `V048` foram reescritas depois que o dono juntou os 42 scripts da aplicação dentro do `V001` e do `V002` (2026-11-03). Onde o contrato dizia "script `V042`", agora diz `V001`; as citações com número de linha passaram a apontar para a tabela ou para a nota «(antigo Vnnn)» do `V001`. Nenhuma rota, campo, exemplo ou código de erro mudou.

## 0.2.1 — 2026-11-02 (rascunho)

- Texto do 400: o `detail` de `Erro400Validacao` passa a ser "Confira os campos indicados." (o padrão que a API usa em todo 400). O texto "Preencha o e-mail e a senha." ficou só na rota `entrar`, que usa a nova resposta `Erro400Login`. O exemplo do 400 de `publicarTreinamento` também usa o texto padrão (o que falta vem em `errors`). Nenhuma rota nem campo mudou. Decisão do dono (2026-11-02): os botões "Lembrar quem não viu" (aviso, `lembrarQuemNaoViuAviso`) e "Lembrar quem falta" do gestor (`lembrarQuemFaltaNaEquipe`) ficam; os dois e-mails entram na lista fechada (`CONVENCOES.md`, seção 12) e as duas rotas deixam de ser provisórias. O número do certificado fica `CERT-{ano}-{id}` (decisão do dono, 2026-11-02): `CertificadoResumo.numero` deixa de ser provisório.

## 0.2.0 — 2026-10-30 (rascunho, todas as fatias)

- Contrato das fatias 0 a 7 (223 operações), escrito por fatia e revisado uma vez; aplicadas as decisões do dono de 2026-10-30 (anonimato, presença agora, matrícula, "Gestor ausente", treinamentos sem prazo, e-mails, benefícios, lote de holerites) e as correções do revisor (modelo único de aprovação em duas etapas, `SituacaoHoje`, códigos de erro unificados, elenco único).
- Segunda revisão feita e terceira passada aplicada: cenário único em `ELENCO.md`, schemas `PresencaContagens` e `DecisaoDeEtapa`, rota `readmitirFuncionario`, e-mail de resultado de pedido de ponto e de férias, treinamento com certificado só se encerra, motivo obrigatório ao recusar férias, regras de anonimato endurecidas (perguntas congeladas, sem contagem antes de encerrar, mínimo por avaliado).
- Códigos de erro renomeados desde 0.1.0: `TREINAMENTO_PUBLICADO` → `TREINAMENTO_MODULOS_TRAVADOS`, `PONTO_MARCACAO_DUPLICADA` → `PONTO_BATIDA_DUPLICADA`, `CANDIDATO_JA_RESPONDEU` → `JA_RESPONDIDO`, `PONTO_PEDIDO_AGUARDANDO_GESTOR` → `PEDIDO_ETAPA_INCORRETA`, `PEDIDO_JA_DECIDIDO` de benefício → `BENEFICIO_PEDIDO_JA_DECIDIDO`.
- Falta ainda: o dono responder o mínimo de respostas por avaliado e vetar as escolhas de engenharia; a versão **1.0.0** é a da publicação na `main`.

## 0.1.0 — 2026-10-30 (rascunho)

- Convenções, base compartilhada e a fatia 0 (login e sessão), conforme o plano da fatia 0.
- As demais fatias estão sendo escritas. A versão **1.0.0** é a da publicação na `main`.
