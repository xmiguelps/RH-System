-- ============================================================
-- V009 - Batidas pedidas no ajuste de ponto (PONTOS_MANUTENCOES_ITENS)
-- Item 6 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O pedido de ajuste de ponto hoje guarda so a justificativa em
--   texto e, no maximo, UMA batida (PONTOS_MANUTENCOES.ID_MARCACAO).
--   O Figma (telas 13 e 55) mostra o que o funcionario PEDE:
--     "Incluir a saida as 17:52"            (uma batida), ou
--     "Incluir o periodo 13:05 - 17:50"     (duas batidas: entrada e
--                                            saida, num pedido so).
--   O dono decidiu (2026-10-29) que um pedido pode trazer varias
--   batidas. Por isso a correcao pedida vai para uma tabela de
--   itens, uma linha por batida proposta.
--
-- O QUE ESTE SCRIPT FAZ
--   1) Cria uma chave unica extra em PONTOS_MANUTENCOES
--      (ID_MANUTENCAO_PONTO, ID_PONTO). Ela nao muda nada nos dados:
--      a chave primaria ja e unica. Serve so de "alvo" da chave
--      estrangeira composta do item 2 (a mesma tecnica que o grupo
--      ja usou em UQ_PONTOS_MARCACOES_ID_PONTO).
--   2) Cria PONTOS_MANUTENCOES_ITENS:
--      - o pedido a que o item pertence,
--      - ID_PONTO (o dia): a FK composta com o pedido garante que o
--        item e do MESMO DIA do pedido, sem precisar de trigger,
--      - ID_MARCACAO: a batida existente que o item corrige. NULL
--        quando a batida e nova ("incluir a saida"),
--        a FK composta (ID_MARCACAO, ID_PONTO) garante que a batida
--        corrigida e do mesmo dia,
--      - DATA_HORA_PROPOSTA e TIPO_MARCACAO (ENTRADA ou SAIDA).
--
-- REGRAS QUE FICAM COM A APLICACAO (nao da para o CHECK ver outra tabela)
--   - A data/hora proposta cair no dia do ponto (ou na madrugada
--     seguinte, se a jornada atravessa a meia-noite).
--   - Gravar o pedido e seus itens na mesma transacao.
--   - Na aprovacao final, aplicar os itens em PONTOS_MARCACOES com
--     ORIGEM = 'CORRIGIDA_RH' (V008). A chave unica
--     (ID_PONTO, DATA_HORA_MARCACAO) de PONTOS_MARCACOES pode colidir
--     se o horario ja existir, a aplicacao trata.
--
-- LIMITACAO CONHECIDA
--   Um pedido cobre UM DIA (o ID_PONTO obrigatorio de
--   PONTOS_MANUTENCOES). E o que o Figma mostra ("Dia do ponto").
--   Pedir varios dias de uma vez exigiria mexer no pedido e no
--   trigger TRG_PONTOS_MANUTENCOES_BEFORE_INSERT, nao foi pedido.
--
-- PONTOS_MANUTENCOES.ID_MARCACAO deixa de ser usada. Nao removi a
-- coluna para nao quebrar nada, pode ficar vazia.
--
-- CORRIGIR UMA BATIDA = UPDATE NO LUGAR (para saber)
--   As chaves estrangeiras impedem apagar uma batida que algum item cita.
--   Entao a correcao aprovada e um UPDATE da propria batida, e o horario
--   ORIGINAL fica so no log de auditoria da aplicacao. Tambem nao existe
--   item "remover batida" (nao esta no Figma), por exemplo para uma batida
--   duplicada. Se o dono quiser, e um tipo de item a mais.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

ALTER TABLE PONTOS_MANUTENCOES
    ADD CONSTRAINT UQ_PONTOS_MANUTENCOES_ID_PONTO UNIQUE (ID_MANUTENCAO_PONTO, ID_PONTO);

CREATE TABLE PONTOS_MANUTENCOES_ITENS (
    ID_MANUTENCAO_PONTO_ITEM INT AUTO_INCREMENT PRIMARY KEY,
    ID_MANUTENCAO_PONTO INT NOT NULL,
    ID_PONTO INT NOT NULL,
    ID_MARCACAO INT NULL,
    DATA_HORA_PROPOSTA DATETIME NOT NULL,
    TIPO_MARCACAO VARCHAR(10) NOT NULL,

    CONSTRAINT FK_PONTOS_MANUTENCOES_ITENS_PEDIDOS
        FOREIGN KEY (ID_MANUTENCAO_PONTO, ID_PONTO)
        REFERENCES PONTOS_MANUTENCOES(ID_MANUTENCAO_PONTO, ID_PONTO),

    CONSTRAINT FK_PONTOS_MANUTENCOES_ITENS_MARCACOES
        FOREIGN KEY (ID_MARCACAO, ID_PONTO)
        REFERENCES PONTOS_MARCACOES(ID_MARCACAO, ID_PONTO),

    CONSTRAINT UQ_PONTOS_MANUTENCOES_ITENS_PEDIDO_DATAHORA UNIQUE (ID_MANUTENCAO_PONTO, DATA_HORA_PROPOSTA),
    CONSTRAINT CHK_PONTOS_MANUTENCOES_ITENS_TIPO CHECK (TIPO_MARCACAO IN ('ENTRADA', 'SAIDA'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
