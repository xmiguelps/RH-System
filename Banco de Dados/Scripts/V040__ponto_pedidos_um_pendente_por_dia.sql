-- ============================================================
-- V040 - No maximo um pedido de ajuste pendente por dia de ponto
-- Fatia 3 (ponto). Lacuna achada na analise do contrato.
-- ============================================================
-- POR QUE
--   O contrato recusa um segundo pedido de ajuste enquanto o dia ja tem um em
--   analise (codigo PONTO_PEDIDO_JA_EM_ANALISE em
--   contrato/fontes/30-fatia-3-ponto.yaml, rota criarPedidoDeAjuste, e
--   PONTO_DIA_COM_PEDIDO_EM_ANALISE na correcao direta do RH). Hoje so a
--   aplicacao confere isso. Dois cliques no botao "Enviar pedido", ou duas
--   abas, podem passar na conferencia ao mesmo tempo e gravar dois pedidos
--   PENDENTE do mesmo dia, e o RH decidiria o mesmo dia duas vezes. E o mesmo
--   risco do duplo clique na batida, que o grupo ja resolveu com uma chave
--   unica (UQ_PONTOS_MARCACOES_PONTO_DATAHORA).
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta CHAVE_PENDENTE, uma coluna CALCULADA: vale o ID_PONTO enquanto
--   o pedido esta PENDENTE e vale NULL depois da decisao (APROVADA ou
--   REJEITADA). A chave unica e sobre ela. O MySQL aceita varios NULL numa
--   chave unica, entao os pedidos ja decididos nao atrapalham, e so os
--   pendentes disputam o mesmo ID_PONTO. Nao grave nela: ela se calcula sozinha
--   (mesma tecnica de CHAVE_DESTINO em V018).
--
-- ORDEM DOS EFEITOS
--   O trigger TRG_PONTOS_MANUTENCOES_BEFORE_INSERT forca STATUS_SOLICITACAO =
--   'PENDENTE' antes de gravar, e a coluna calculada usa o valor ja ajustado.
--
-- SE O SCRIPT FALHAR (erro de chave duplicada)
--   Ja existem dois pedidos PENDENTE no mesmo dia. Veja quais com
--     SELECT ID_PONTO, COUNT(*) FROM PONTOS_MANUTENCOES
--      WHERE STATUS_SOLICITACAO = 'PENDENTE' GROUP BY ID_PONTO HAVING COUNT(*) > 1
--   e decida um deles antes de rodar de novo. Nada e alterado se a chave falha.
--
-- PARA QUEM MAPEIA NO EF CORE
--   CHAVE_PENDENTE e calculada: mapeie como calculada (fora do INSERT), como
--   CHAVE_DESTINO e CHAVE_AVALIADO.
--
-- O QUE FICA COM A APLICACAO
--   Responder com a mensagem amigavel (409) quando o banco recusar por esta
--   chave, e continuar conferindo antes de gravar para o caso comum.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE PONTOS_MANUTENCOES
    ADD COLUMN CHAVE_PENDENTE INT
        GENERATED ALWAYS AS (IF(STATUS_SOLICITACAO = 'PENDENTE', ID_PONTO, NULL)) STORED NULL,
    ADD CONSTRAINT UQ_PONTOS_MANUTENCOES_UM_PENDENTE UNIQUE (CHAVE_PENDENTE);
