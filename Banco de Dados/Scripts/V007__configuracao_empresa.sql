-- ============================================================
-- V007 - Configuracao da empresa (horario padrao e dia de pagamento)
-- Itens 4 e 16 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   - O dono decidiu (2026-10-28) que existe UM horario padrao para
--     toda a empresa. O atraso e medido contra ele (o Figma mostra
--     "Atraso de 14 min" e "44 h semanais, das 08:00 as 17:48").
--     Isso precisa da entrada, da saida e da tolerancia de atraso.
--   - A UF escolhe os feriados estaduais, o municipio escolhe os
--     municipais, e o nome da empresa aparece no menu do sistema.
--   - O dia de pagamento (item 16) e o dia do mes da regra padrao
--     (ex.: 30). A aplicacao calcula as excecoes: se o mes nao tem o
--     dia, vale o ultimo, se cai em fim de semana ou feriado,
--     antecipa para o dia util anterior. As excecoes por mes ficam
--     em PAGAMENTOS_EXCECOES (script V027).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria CONFIGURACOES_EMPRESA, uma tabela de UMA UNICA LINHA.
--
-- COMO A "LINHA UNICA" FUNCIONA
--   A chave primaria so aceita o valor 1 (CHECK). Uma segunda linha
--   colide com a chave primaria. Assim nao existe "qual das duas
--   configuracoes vale?".
--
-- ATENCAO
--   - A tabela nasce VAZIA (o nome da empresa nao e conhecido aqui). A
--     linha e criada no primeiro acesso, pela tela de configuracao da
--     empresa (fatia 1), e a carga da demonstracao a preenche. Ate la, a
--     aplicacao precisa tratar "ainda sem configuracao": sem o horario
--     padrao nao ha calculo de atraso, e sem o dia de pagamento nao ha
--     "proximo pagamento".
--   - Nao ha CHECK impondo HORARIO_SAIDA > HORARIO_ENTRADA, para nao
--     travar uma empresa de turno que atravessa a meia-noite.
--   - UF fica em MAIUSCULAS (como a tabela FERIADOS, cujo trigger faz
--     UPPER) e so com letras. O CHECK compara em binario
--     (CAST ... AS BINARY, e nao o operador BINARY, obsoleto desde o
--     MySQL 8.0.27) porque a collation utf8mb4_unicode_ci nao diferencia
--     maiuscula de minuscula. Ele NAO confere se a sigla existe: "ZZ"
--     passa.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

CREATE TABLE CONFIGURACOES_EMPRESA (
    ID_CONFIGURACAO_EMPRESA TINYINT NOT NULL DEFAULT 1 PRIMARY KEY,
    NOME_EMPRESA VARCHAR(150) NOT NULL,
    UF CHAR(2) NOT NULL,
    MUNICIPIO VARCHAR(100) NOT NULL,
    HORARIO_ENTRADA TIME NOT NULL,
    HORARIO_SAIDA TIME NOT NULL,
    TOLERANCIA_ATRASO_MINUTOS SMALLINT NOT NULL DEFAULT 0,
    DIA_PAGAMENTO TINYINT NOT NULL,

    CONSTRAINT CHK_CONFIGURACOES_EMPRESA_LINHA_UNICA CHECK (ID_CONFIGURACAO_EMPRESA = 1),
    CONSTRAINT CHK_CONFIGURACOES_EMPRESA_UF
        CHECK (UF REGEXP '^[A-Za-z]{2}$' AND CAST(UF AS BINARY) = CAST(UPPER(UF) AS BINARY)),
    CONSTRAINT CHK_CONFIGURACOES_EMPRESA_TOLERANCIA CHECK (TOLERANCIA_ATRASO_MINUTOS BETWEEN 0 AND 120),
    CONSTRAINT CHK_CONFIGURACOES_EMPRESA_DIA_PAGAMENTO CHECK (DIA_PAGAMENTO BETWEEN 1 AND 31)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
