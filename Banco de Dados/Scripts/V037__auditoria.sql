-- ============================================================
-- V037 - Log de auditoria (AUDITORIA_EVENTOS e AUDITORIA_MUDANCAS)
-- Fatia 1 e todas as outras (contrato: GET /api/rh/auditoria, enums
--         AuditoriaArea e AuditoriaAcao)
-- ============================================================
-- POR QUE
--   O RH precisa ver "tudo o que acontece no portal" (tela 31, so leitura,
--   com busca, periodo, area e acao, e exportacao em CSV), e o painel do RH
--   mostra as "Ultimas alteracoes" a partir dele. Cada fatia registra os
--   seus eventos. Nada disso existe no banco de hoje.
--   Mais de uma fatia LE o log, nao so escreve: o painel do RH (fatia 7)
--   mostra as "Ultimas alteracoes". O instante em que o RH corrigiu uma
--   batida ("corrigida pelo RH em 07/10") fica na propria batida, em
--   PONTOS_MARCACOES.DATA_CORRECAO (V039), e nao neste log. Para achar o
--   evento certo de um registro (qualquer "historico deste registro"), o
--   texto livre de "registro afetado" nao serve: por isso o evento guarda
--   tambem QUAL registro (ENTIDADE e ID_REGISTRO), com indice.
--
-- O QUE ESTE SCRIPT FAZ
--   AUDITORIA_EVENTOS, uma linha por evento (o que a lista mostra):
--   - QUANDO (UTC), ATOR (quem fez), AREA, ACAO, DESCRICAO ("o que
--     aconteceu", texto simples), REGISTRO_AFETADO (texto da linha
--     expandida), CANAL e IP (origem).
--   - ATOR_TIPO PESSOA ou SISTEMA (tarefa automatica). ID_FUNCIONARIO_ATOR
--     aponta para a pessoa e fica NULL para o SISTEMA. ATOR_NOME e
--     ATOR_PERFIL sao uma COPIA do nome e do perfil NA HORA DO EVENTO: o
--     log diz quem era a pessoa naquele dia, mesmo que o nome ou o perfil
--     mudem depois (a "Desligado" da tela vem de FUNCIONARIOS.ATIVO hoje).
--   - AREA e ACAO: os enums do contrato (AuditoriaArea, AuditoriaAcao), com
--     CHECK. Valor novo no contrato e um CHECK novo aqui.
--   - ENTIDADE e ID_REGISTRO (opcionais, andam juntos): o nome da tabela e o
--     id do registro afetado, por exemplo PONTOS_MARCACOES e o id da
--     batida. Eventos sem um registro unico (uma exportacao, um lote de
--     holerites) deixam os dois NULL.
--   - CANAL: PORTAL (navegador) ou SISTEMA (tarefa automatica, sem IP).
--   AUDITORIA_MUDANCAS, as linhas "Campo / Antes / Depois" de um evento:
--   - VALOR_OCULTO = VERDADEIRO marca uma coluna SENSIVEL: o valor NAO e
--     gravado e o banco recusa (CHECK) ANTES e DEPOIS preenchidos nesse
--     caso. A tela mostra "valor nao registrado". Quais colunas sao
--     sensiveis e decisao da aplicacao (roadmap, decisao 13: origem do
--     trajeto, dados de candidato, autor e hora de resposta anonima,
--     arquivo de atestado). Senha, hash e token nem entram como campo.
--
-- "SO INCLUSAO"
--   O contrato nao tem rota para editar ou apagar um evento, e a tela diz que
--   nenhuma tela do sistema o faz. As duas tabelas nao tem coluna de
--   alteracao nem de exclusao logica: um evento e escrito uma vez. O BANCO
--   NAO IMPEDE o UPDATE e o DELETE, porque a regra do projeto e nao ter
--   trigger em tabela nova da aplicacao: a regra fica com a aplicacao (nenhum
--   caminho de codigo atualiza ou apaga auditoria). Se o dono quiser a
--   garantia no proprio banco, sao dois triggers BEFORE UPDATE e BEFORE
--   DELETE com SIGNAL, como o grupo ja faz em HOLERITES e ADVERTENCIAS: e uma
--   decisao dele, porque tambem impediria uma politica de retencao.
--
-- PRIVACIDADE (decisao 13 e formulario anonimo)
--   - O banco nao ve o que e sensivel alem do VALOR_OCULTO acima: a regra de
--     nunca gravar senha, hash, token, o autor ou a hora de uma resposta
--     anonima, e a de nao deixar descobrir quem participou de um formulario
--     anonimo (responder e participar NAO geram evento, e o lembrete entra so
--     como "lembrete enviado", sem destinatario e sem numero) sao da
--     aplicacao.
--   - O IP e dado pessoal: guarde so no que o contrato pede (origem).
--
-- COMO A TELA CONSULTA
--   - Ordem fixa: do mais recente para o mais antigo (QUANDO, ID).
--   - Periodo: "de" e "ate" sao DIAS no fuso -03:00, inclusive: a aplicacao
--     converte para um intervalo de instantes UTC.
--   - "apenasAlteracoes" (painel): tudo, menos ACAO ENTROU, ACESSO_NEGADO e
--     BAIXOU e menos AREA PONTO com ACAO REGISTROU.
--   - A busca procura em ATOR_NOME e em DESCRICAO (LIKE).
--   Os indices cobrem a ordem, o filtro por area, o filtro por acao e a
--   busca de eventos de um registro.
--
-- FUSO: QUANDO e UTC, em tabela so da aplicacao (nenhum CHECK ou trigger a
-- compara com colunas do primeiro grupo). O padrao usa UTC_TIMESTAMP(6), que
-- NAO depende do fuso do servidor. Para mostrar ou exportar em -03:00 a
-- aplicacao converte (a exportacao CSV diz -03:00). No EF Core, mapeie
-- QUANDO como gerada na inclusao (senao o EF manda 0001-01-01).
--
-- TABELAS NOVAS: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE AUDITORIA_EVENTOS (
    ID_AUDITORIA_EVENTO BIGINT AUTO_INCREMENT PRIMARY KEY,
    QUANDO DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    ATOR_TIPO VARCHAR(7) NOT NULL,
    ID_FUNCIONARIO_ATOR INT NULL,
    ATOR_NOME VARCHAR(150) NOT NULL,
    ATOR_PERFIL VARCHAR(12) NULL,
    AREA VARCHAR(20) NOT NULL,
    ACAO VARCHAR(20) NOT NULL,
    DESCRICAO VARCHAR(500) NOT NULL,
    REGISTRO_AFETADO VARCHAR(255) NOT NULL,
    ENTIDADE VARCHAR(50) NULL,
    ID_REGISTRO BIGINT NULL,
    CANAL VARCHAR(7) NOT NULL,
    IP VARCHAR(45) NULL,

    CONSTRAINT FK_AUDITORIA_EVENTOS_FUNCIONARIOS_ATORES
        FOREIGN KEY (ID_FUNCIONARIO_ATOR)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT CHK_AUDITORIA_EVENTOS_ATOR_TIPO CHECK (ATOR_TIPO IN ('PESSOA', 'SISTEMA')),
    -- pessoa tem funcionario e perfil opcional (quem nao tem nivel fica sem)
    -- e o sistema nao tem funcionario nem perfil
    CONSTRAINT CHK_AUDITORIA_EVENTOS_ATOR_PESSOA_OU_SISTEMA
        CHECK ((ATOR_TIPO = 'PESSOA' AND ID_FUNCIONARIO_ATOR IS NOT NULL)
            OR (ATOR_TIPO = 'SISTEMA' AND ID_FUNCIONARIO_ATOR IS NULL AND ATOR_PERFIL IS NULL)),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_ATOR_PERFIL
        CHECK (ATOR_PERFIL IS NULL OR ATOR_PERFIL IN ('Funcionario', 'Gestor', 'RH')),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_AREA
        CHECK (AREA IN ('ACESSO', 'COLABORADORES', 'DOCUMENTOS', 'CADASTROS', 'IMPORTACAO', 'PONTO', 'FERIADOS',
                        'FERIAS', 'HOLERITES', 'ADVERTENCIAS', 'AVISOS', 'FORMULARIOS', 'TREINAMENTOS', 'BENEFICIOS',
                        'ATESTADOS')),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_ACAO
        CHECK (ACAO IN ('ENTROU', 'ACESSO_NEGADO', 'CRIOU', 'ALTEROU', 'EXCLUIU', 'INATIVOU', 'REGISTROU',
                        'PEDIU_AJUSTE', 'APROVOU', 'RECUSOU', 'PUBLICOU', 'ENVIOU_EMAIL', 'IMPORTOU', 'BAIXOU')),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_DESCRICAO CHECK (CHAR_LENGTH(TRIM(DESCRICAO)) > 0),
    -- a entidade e o id do registro andam juntos
    CONSTRAINT CHK_AUDITORIA_EVENTOS_REGISTRO CHECK ((ENTIDADE IS NULL) = (ID_REGISTRO IS NULL)),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_ENTIDADE
        CHECK (ENTIDADE IS NULL OR REGEXP_LIKE(ENTIDADE, '^[A-Z][A-Z0-9_]*$', 'c')),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_CANAL CHECK (CANAL IN ('PORTAL', 'SISTEMA')),
    -- o sistema age pelo canal SISTEMA e vice-versa, e tarefa automatica nao tem IP
    CONSTRAINT CHK_AUDITORIA_EVENTOS_CANAL_DO_ATOR CHECK ((ATOR_TIPO = 'SISTEMA') = (CANAL = 'SISTEMA')),
    CONSTRAINT CHK_AUDITORIA_EVENTOS_IP_SO_NO_PORTAL CHECK (CANAL = 'PORTAL' OR IP IS NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- a lista: ordem fixa do mais recente para o mais antigo, e o periodo
CREATE INDEX IDX_AUDITORIA_EVENTOS_QUANDO ON AUDITORIA_EVENTOS(QUANDO, ID_AUDITORIA_EVENTO);
-- os filtros "Todas as areas" e "Todas as acoes" dentro do periodo
CREATE INDEX IDX_AUDITORIA_EVENTOS_AREA_QUANDO ON AUDITORIA_EVENTOS(AREA, QUANDO);
CREATE INDEX IDX_AUDITORIA_EVENTOS_ACAO_QUANDO ON AUDITORIA_EVENTOS(ACAO, QUANDO);
-- "os eventos deste registro" (por exemplo, quando a batida foi corrigida)
CREATE INDEX IDX_AUDITORIA_EVENTOS_REGISTRO ON AUDITORIA_EVENTOS(ENTIDADE, ID_REGISTRO, QUANDO);

CREATE TABLE AUDITORIA_MUDANCAS (
    ID_AUDITORIA_MUDANCA BIGINT AUTO_INCREMENT PRIMARY KEY,
    ID_AUDITORIA_EVENTO BIGINT NOT NULL,
    ORDEM SMALLINT NOT NULL,
    CAMPO VARCHAR(100) NOT NULL,
    VALOR_ANTES TEXT NULL,
    VALOR_DEPOIS TEXT NULL,
    VALOR_OCULTO BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT FK_AUDITORIA_MUDANCAS_EVENTOS
        FOREIGN KEY (ID_AUDITORIA_EVENTO)
        REFERENCES AUDITORIA_EVENTOS(ID_AUDITORIA_EVENTO),

    -- a ordem das linhas da tabela "Campo / Antes / Depois"
    CONSTRAINT UQ_AUDITORIA_MUDANCAS_EVENTO_ORDEM UNIQUE (ID_AUDITORIA_EVENTO, ORDEM),
    CONSTRAINT CHK_AUDITORIA_MUDANCAS_ORDEM CHECK (ORDEM >= 1),
    CONSTRAINT CHK_AUDITORIA_MUDANCAS_CAMPO CHECK (CHAR_LENGTH(TRIM(CAMPO)) > 0),
    -- coluna sensivel: o valor NAO e gravado (nenhum dos lados do OR da NULL)
    CONSTRAINT CHK_AUDITORIA_MUDANCAS_OCULTO_SEM_VALOR
        CHECK (VALOR_OCULTO = FALSE OR (VALOR_ANTES IS NULL AND VALOR_DEPOIS IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
