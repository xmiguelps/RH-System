-- ============================================================
-- V019 - Registro de participacao em formulario anonimo
-- Item 24 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   Num formulario ANONIMO a resposta nao guarda quem respondeu
--   (V017). Mesmo assim o sistema precisa saber:
--   - quem JA respondeu, para nao pedir de novo e para a lista de
--     "pendentes" do funcionario,
--   - quantas pessoas responderam (o RH ve a contagem, as respostas so
--     aparecem depois de um minimo, comecando em 3),
--   - impedir a mesma pessoa de responder duas vezes.
--   Para isso existe um registro SEPARADO das respostas: "a pessoa X
--   participou do formulario Y". Ele nao aponta para nenhuma resposta.
--
-- COMO ESTE DESENHO PROTEGE O ANONIMATO
--   O risco e alguem cruzar a ORDEM ou a DATA: "a 5a pessoa a participar
--   deve ser dona da 5a resposta", ou "so a Ana participou em 05/11, entao
--   as respostas de 05/11 sao dela". Para nao ser possivel:
--   - a tabela NAO tem data nem hora, so a presenca ("participou"),
--   - a chave primaria NAO e automatica: e um UUID ALEATORIO (versao 4)
--     gerado pela aplicacao. O InnoDB guarda as linhas ordenadas pela
--     chave primaria, entao a ordem fisica e aleatoria, nao a de chegada,
--   - nao tem ligacao nenhuma com FORMULARIOS_RESPOSTAS.
--   POR QUE O CHECK DA VERSAO DO UUID: a funcao UUID() do MySQL gera UUID
--   de VERSAO 1, que contem o horario e e quase sequencial, o que
--   desfaria a protecao. O CHECK recusa tudo que nao seja versao 4 (o
--   caractere 15 e "4"). NAO use DEFAULT (UUID()) nesta coluna.
--   Cuidado da aplicacao: nao registrar participacao e resposta no log
--   de auditoria de um jeito que reconstrua a ordem.
--
-- ID_AVALIADO (avaliacoes anonimas)
--   Numa avaliacao, a mesma pessoa responde uma vez POR AVALIADO. A
--   chave unica (formulario, funcionario, avaliado) impede repetir, mas o
--   MySQL aceita varios NULL numa chave unica. A coluna gerada
--   CHAVE_AVALIADO troca o NULL por 0 e entra na chave unica. Nao grave
--   nela: ela se calcula sozinha.
--
-- PARA QUEM MAPEIA NO EF CORE
--   CHAVE_AVALIADO e coluna calculada (nao entra no INSERT). A chave
--   primaria e so ID_PARTICIPACAO (nao ha chave composta com coluna
--   gerada, que o EF e o Pomelo poderiam nao aceitar).
--
-- FORMULARIO RECORRENTE
--   Ao reabrir, a aplicacao apaga as respostas da rodada anterior (sem
--   historico, decisao do dono) E APAGA tambem as participacoes dele,
--   senao ninguem conseguiria responder de novo.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE FORMULARIOS_PARTICIPACOES (
    ID_PARTICIPACAO CHAR(36) NOT NULL PRIMARY KEY,
    ID_FORMULARIO INT NOT NULL,
    ID_FUNCIONARIO INT NOT NULL,
    ID_AVALIADO INT NULL,
    CHAVE_AVALIADO INT GENERATED ALWAYS AS (IFNULL(ID_AVALIADO, 0)) STORED NOT NULL,

    CONSTRAINT UQ_FORMULARIOS_PARTICIPACOES_PESSOA UNIQUE (ID_FORMULARIO, ID_FUNCIONARIO, CHAVE_AVALIADO),
    -- UUID versao 4 (aleatorio). Ver o comentario no topo.
    CONSTRAINT CHK_FORMULARIOS_PARTICIPACOES_UUID_V4 CHECK (SUBSTRING(ID_PARTICIPACAO, 15, 1) = '4'),

    CONSTRAINT FK_FORMULARIOS_PARTICIPACOES_FORMULARIOS
        FOREIGN KEY (ID_FORMULARIO)
        REFERENCES FORMULARIOS(ID_FORMULARIO),

    CONSTRAINT FK_FORMULARIOS_PARTICIPACOES_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT FK_FORMULARIOS_PARTICIPACOES_AVALIADOS
        FOREIGN KEY (ID_AVALIADO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
