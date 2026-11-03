-- ============================================================
-- V018 - Quem deve responder cada formulario (FORMULARIOS_DESTINATARIOS)
-- Item 23 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O Figma (etapa 4) pergunta "Quem responde": setores ("todos de um ou
--   mais setores") ou pessoas escolhidas (alem de candidatos por link,
--   que nao precisam de destinatario). Sem uma tabela que diga quem deve
--   responder, nao existem os "formularios pendentes" do funcionario
--   nem o "lembrar quem falta" do RH.
--   Nas AVALIACOES, o destinatario tambem precisa dizer SOBRE QUEM ele
--   responde (o gestor responde uma vez por pessoa da equipe).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria FORMULARIOS_DESTINATARIOS: uma linha por destinatario. Cada
--   linha e UMA PESSOA (ID_FUNCIONARIO) ou UM SETOR (ID_SETOR), nunca os
--   dois (CHECK). ID_AVALIADO e opcional (avaliacoes).
--   O prazo NAO esta aqui: fica em FORMULARIOS.DATA_PRAZO (V015).
--
-- A COLUNA CHAVE_DESTINO (leia)
--   Queremos impedir a mesma linha repetida (mesmo formulario, mesma
--   pessoa ou setor, mesmo avaliado). Mas o MySQL aceita VARIOS NULL
--   numa chave unica, entao UNIQUE (ID_FORMULARIO, ID_FUNCIONARIO,
--   ID_SETOR, ID_AVALIADO) nao protegeria nada, porque sempre ha um
--   NULL. A solucao e uma coluna gerada que troca NULL por 0 e junta os
--   tres ids, a chave unica e sobre ela. Nao grave nela: ela se calcula
--   sozinha.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Resolver "setor" em pessoas na hora (quem esta no setor hoje) e
--     incluir quem for admitido depois, se for a regra do formulario.
--   - Quem ja respondeu: olhar FORMULARIOS_RESPOSTAS (formulario comum)
--     ou FORMULARIOS_PARTICIPACOES (anonimo, V019).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE FORMULARIOS_DESTINATARIOS (
    ID_FORMULARIO_DESTINATARIO INT AUTO_INCREMENT PRIMARY KEY,
    ID_FORMULARIO INT NOT NULL,
    ID_FUNCIONARIO INT NULL,
    ID_SETOR INT NULL,
    ID_AVALIADO INT NULL,
    CHAVE_DESTINO VARCHAR(40) GENERATED ALWAYS AS
        (CONCAT_WS(':', IFNULL(ID_FUNCIONARIO, 0), IFNULL(ID_SETOR, 0), IFNULL(ID_AVALIADO, 0))) STORED,

    CONSTRAINT FK_FORMULARIOS_DESTINATARIOS_FORMULARIOS
        FOREIGN KEY (ID_FORMULARIO)
        REFERENCES FORMULARIOS(ID_FORMULARIO),

    CONSTRAINT FK_FORMULARIOS_DESTINATARIOS_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT FK_FORMULARIOS_DESTINATARIOS_SETORES
        FOREIGN KEY (ID_SETOR)
        REFERENCES SETORES(ID_SETOR),

    CONSTRAINT FK_FORMULARIOS_DESTINATARIOS_AVALIADOS
        FOREIGN KEY (ID_AVALIADO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT UQ_FORMULARIOS_DESTINATARIOS_DESTINO UNIQUE (ID_FORMULARIO, CHAVE_DESTINO),
    -- pessoa OU setor, nunca os dois e nunca nenhum
    CONSTRAINT CHK_FORMULARIOS_DESTINATARIOS_PESSOA_OU_SETOR
        CHECK ((ID_FUNCIONARIO IS NULL) <> (ID_SETOR IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
