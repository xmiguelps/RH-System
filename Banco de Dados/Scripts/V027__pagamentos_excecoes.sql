-- ============================================================
-- V027 - Excecoes do dia de pagamento (PAGAMENTOS_EXCECOES)
-- Item 25 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   A regra PADRAO do pagamento e um dia do mes (DIA_PAGAMENTO, em
--   CONFIGURACOES_EMPRESA, V007). Se o mes nao tem o dia, vale o ultimo,
--   se cai em fim de semana ou feriado, antecipa para o dia util anterior
--   (a aplicacao calcula). Mas de vez em quando o pagamento de um mes
--   cai em OUTRO dia, e o dono decidiu (2026-10-29) GUARDAR essa excecao.
--   Ela alimenta o "proximo pagamento" do funcionario e o holerite
--   agendado "para o dia do pagamento".
--
-- O QUE ESTE SCRIPT FAZ
--   Cria PAGAMENTOS_EXCECOES: ano, mes e a data de pagamento daquele mes.
--
-- POR QUE ANO E MES (e nao so o mes)
--   A excecao vale para UM mes de UM ano. A chave unica e (ANO, MES), para
--   uma excecao de marco de 2027 nao se repetir sozinha em marco de 2028.
--   (ANO usa o tipo YEAR, como HOLERITES.ANO.)
--
-- O QUE O BANCO GARANTE (CHECK)
--   A data de pagamento cai DENTRO do ano e mes da excecao.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE PAGAMENTOS_EXCECOES (
    ID_PAGAMENTO_EXCECAO INT AUTO_INCREMENT PRIMARY KEY,
    ANO YEAR NOT NULL,
    MES TINYINT NOT NULL,
    DATA_PAGAMENTO DATE NOT NULL,

    CONSTRAINT UQ_PAGAMENTOS_EXCECOES_ANO_MES UNIQUE (ANO, MES),
    CONSTRAINT CHK_PAGAMENTOS_EXCECOES_MES CHECK (MES BETWEEN 1 AND 12),
    CONSTRAINT CHK_PAGAMENTOS_EXCECOES_DATA_NO_MES
        CHECK (YEAR(DATA_PAGAMENTO) = ANO AND MONTH(DATA_PAGAMENTO) = MES)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
