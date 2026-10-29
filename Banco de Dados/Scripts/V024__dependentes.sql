-- ============================================================
-- V024 - Dependentes (DEPENDENTES)
-- Item 19 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   Alguns beneficios (plano de saude, odontologico) incluem dependentes
--   (filho, conjuge). O Figma (Beneficios) mostra "Quem vai entrar no
--   plano: Voce, [dependente], Incluir outro dependente" e, em "Seus
--   pedidos", "Inclusao de dependente no plano de saude" com situacao.
--
-- O QUE ESTE SCRIPT FAZ
--   Cria DEPENDENTES. Dados aprovados pelo dono em 2026-10-29: nome,
--   parentesco e data de nascimento (se faltar campo, vira um script
--   novo).
--
-- POR QUE O DEPENDENTE SE LIGA A ADESAO (BENEFICIOS_FUNCIONARIOS) E NAO
-- AO CATALOGO (BENEFICIOS)
--   Decisao do dono. Assim:
--   - a cobertura do dependente depende da adesao estar ativa: a
--     inativacao do funcionario ja desliga as adesoes
--     (TRG_FUNCIONARIOS_AFTER_UPDATE), e a aplicacao trata como sem
--     cobertura os dependentes de uma adesao inativa, sem trigger novo.
--     ATENCAO: o trigger NAO muda o STATUS_PEDIDO do dependente (nem do
--     trajeto): um dependente PENDENTE sobrevive a inativacao, e a
--     aplicacao o cancela,
--   - um dependente pode estar num beneficio e nao em outro (cada adesao
--     tem os seus, a mesma pessoa pode aparecer em duas adesoes).
--
-- INCLUSAO DE DEPENDENTE E PEDIDO TAMBEM
--   O Figma lista a inclusao de dependente em "Seus pedidos", com
--   situacao (Aprovado). Por isso a tabela tem STATUS_PEDIDO (PENDENTE,
--   APROVADO, RECUSADO, CANCELADO), DATA_PEDIDO, quem do RH respondeu,
--   quando e o motivo da recusa, iguais aos de V021. O dependente que o
--   RH cadastra direto entra ja APROVADO (a aplicacao grava).
--   Nao inventei CPF do dependente: nao foi pedido.
--
-- PARENTESCO
--   Texto livre por enquanto (sem CHECK), os valores (FILHO, CONJUGE...)
--   ainda nao foram decididos.
--
-- PRIVACIDADE
--   So o RH e o proprio funcionario veem (a aplicacao filtra).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE DEPENDENTES (
    ID_DEPENDENTE INT AUTO_INCREMENT PRIMARY KEY,
    ID_BENEFICIO_FUNCIONARIOS INT NOT NULL,
    NOME VARCHAR(150) NOT NULL,
    PARENTESCO VARCHAR(30) NOT NULL,
    DATA_NASCIMENTO DATE NOT NULL,
    STATUS_PEDIDO VARCHAR(20) NOT NULL DEFAULT 'PENDENTE',
    DATA_PEDIDO DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ID_FUNCIONARIO_RH INT NULL,
    DATA_RESPOSTA DATETIME NULL,
    MOTIVO_RECUSA TEXT NULL,

    CONSTRAINT FK_DEPENDENTES_BENEFICIOS_FUNCIONARIOS
        FOREIGN KEY (ID_BENEFICIO_FUNCIONARIOS)
        REFERENCES BENEFICIOS_FUNCIONARIOS(ID_BENEFICIO_FUNCIONARIOS),

    CONSTRAINT FK_DEPENDENTES_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT CHK_DEPENDENTES_STATUS_PEDIDO
        CHECK (STATUS_PEDIDO IN ('PENDENTE', 'APROVADO', 'RECUSADO', 'CANCELADO')),
    CONSTRAINT CHK_DEPENDENTES_RESPOSTA_JUNTAS
        CHECK ((ID_FUNCIONARIO_RH IS NULL) = (DATA_RESPOSTA IS NULL)),
    CONSTRAINT CHK_DEPENDENTES_RECUSA_COM_MOTIVO
        CHECK (STATUS_PEDIDO <> 'RECUSADO' OR (MOTIVO_RECUSA IS NOT NULL AND TRIM(MOTIVO_RECUSA) <> ''))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
