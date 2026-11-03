-- ============================================================
-- V008 - Origem da batida em PONTOS_MARCACOES
-- Item 5 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O Figma mostra "Saida corrigida pelo RH em 07/10" na linha do
--   dia. Para o sistema saber isso, a batida precisa dizer se foi o
--   proprio funcionario que bateu ou se o RH a corrigiu/incluiu
--   (por um pedido de ajuste aprovado ou direto pela ficha).
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta ORIGEM em PONTOS_MARCACOES, com dois valores:
--     SISTEMA       - batida feita pelo funcionario (padrao)
--     CORRIGIDA_RH  - batida incluida ou corrigida pelo RH
--   As linhas que ja existem viram SISTEMA (e o que elas sao).
--
-- O QUE ESTE SCRIPT NAO FAZ (de proposito)
--   Nao guarda QUEM corrigiu nem QUANDO. Quem e quando saem do log de
--   auditoria da aplicacao (tabela propria, criada a parte), que ja
--   registra toda alteracao. Se voce preferir ter isso na propria
--   batida, e so acrescentar duas colunas depois.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT. PONTOS_MARCACOES nao
-- tem trigger (o script de triggers do grupo nao cita a tabela).
-- ============================================================

ALTER TABLE PONTOS_MARCACOES
    ADD COLUMN ORIGEM VARCHAR(20) NOT NULL DEFAULT 'SISTEMA',
    ADD CONSTRAINT CHK_PONTOS_MARCACOES_ORIGEM CHECK (ORIGEM IN ('SISTEMA', 'CORRIGIDA_RH'));
