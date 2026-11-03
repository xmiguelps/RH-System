-- ============================================================
-- V006 - Matricula e tipo de contrato em FUNCIONARIOS
-- Item 3 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O Figma mostra a matricula (ex.: 00482) na ficha e na busca da
--   lista de colaboradores, e filtra a lista por tipo de contrato
--   (CLT, estagio etc.). Hoje FUNCIONARIOS nao tem nenhuma das duas.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta duas colunas em FUNCIONARIOS:
--   - MATRICULA: texto, e nao numero, para nao perder o zero a
--     esquerda ('00482'). Unica por funcionario.
--   - TIPO_CONTRATO: texto livre por enquanto.
--
-- DECISOES QUE VOCE PODE REVER
--   1) As duas colunas aceitam NULL. Motivo: ja pode haver
--      funcionarios cadastrados (dados de teste) e a matricula so
--      existe depois que o RH cadastra a pessoa. A aplicacao
--      preenche, o banco nao exige.
--   2) NAO ha CHECK com os valores de TIPO_CONTRATO. A lista de
--      valores (CLT, ESTAGIO, ...) ainda nao foi decidida pelo dono.
--      Quando for, o jeito e um script novo com:
--        ALTER TABLE FUNCIONARIOS ADD CONSTRAINT
--          CHK_FUNCIONARIOS_TIPO_CONTRATO CHECK
--          (TIPO_CONTRATO IS NULL OR TIPO_CONTRATO IN (...))
--   3) A regra de formacao da matricula (so digitos? quantos?) tambem
--      e decisao do dono, por isso nao ha CHECK de formato.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE FUNCIONARIOS
    ADD COLUMN MATRICULA VARCHAR(20) NULL,
    ADD COLUMN TIPO_CONTRATO VARCHAR(30) NULL,
    ADD CONSTRAINT UQ_FUNCIONARIOS_MATRICULA UNIQUE (MATRICULA);
