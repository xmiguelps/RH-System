-- ============================================================
-- V029 - Treinamentos: rascunho/publicacao, publico e certificado
-- Item 27 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE (Figma, assistente "Novo treinamento", telas 23 a 26)
--   - RASCUNHO E AGENDAMENTO (dono, 2026-10-29: sim). Todas as etapas tem
--     "Salvar rascunho e sair" e a ultima oferece "Publicar agora, com
--     acesso liberado em 15/10" ou "Salvar como rascunho". O 15/10 e a data
--     "Comeca em" da primeira etapa.
--   - PARA QUEM VALE: "Todos", "Setores" (um ou mais) ou "Pessoas
--     escolhidas". Nao existe "por cargo".
--   - CERTIFICADO (dono, 2026-10-29: MANTEM A CAIXA): a etapa "Avaliacao e
--     certificado" tem "Emitir certificado com codigo de validacao ao
--     concluir", o RH escolhe por treinamento.
--
-- O QUE ESTE SCRIPT FAZ
--   1) TREINAMENTOS ganha:
--      - PUBLICADO: uma coluna basta, sem data de publicacao a parte.
--          rascunho = PUBLICADO falso
--          agendado = PUBLICADO verdadeiro e DATA_INICIO no futuro
--          publicado e liberado = PUBLICADO verdadeiro e a data de
--                                 inicio ja chegou
--        A aplicacao mostra o treinamento so quando PUBLICADO e a data de
--        inicio chegou, nao precisa de agendador para "ligar" nada.
--        DATA_INICIO e DATA_FIM (que ja existiam) passam a valer como
--        "acesso liberado em" e "prazo".
--      - PUBLICO: TODOS, SETORES ou PESSOAS (NULL ate o RH escolher).
--      - EMITE_CERTIFICADO: sim/nao (padrao sim),
--      - INCLUI_ADMITIDOS, AVISA_POR_EMAIL e LEMBRA_ANTES_DO_FIM: as tres
--        caixas da ultima etapa do assistente ("Incluir quem for admitido
--        ate o fim", "Avisar por e-mail ao publicar" e "Lembrar quem nao
--        comecou 7 dias antes do fim"). Todas com padrao sim. Precisam ser
--        GUARDADAS porque o agendador as consulta depois (um treinamento
--        agendado envia o e-mail so na data da publicacao).
--   2) Cria TREINAMENTOS_PUBLICO: os setores ou as pessoas escolhidas
--      (uma linha por setor ou por pessoa). Quando PUBLICO e TODOS, nao ha
--      linhas aqui.
--
-- TREINAMENTOS QUE JA EXISTIAM
--   O padrao de PUBLICADO e FALSO (nada vira publico sem querer), como em
--   AVISOS.ENVIADO (V013). Os treinamentos que ja existem estavam
--   disponiveis, entao o fim do script marca todos como publicados e,
--   para NAO mudar quem os ve, define o publico como PESSOAS e copia para
--   TREINAMENTOS_PUBLICO quem ja tinha linha em TREINAMENTOS_FUNCIONARIOS.
--   (Falha segura: se o fim do script se perder, eles viram rascunho.)
--
-- SOBRE EMITE_CERTIFICADO = FALSO (leia)
--   NAO mexi no trigger que cria o certificado. TRG_TREINAMENTOS_
--   FUNCIONARIOS_AFTER_INSERT/_UPDATE criam a linha em
--   TREINAMENTOS_CERTIFICADOS (com um UUID) SEMPRE que o treinamento vira
--   CONCLUIDO. Com a coluna em FALSO, quem trata e a APLICACAO: nao gera o
--   PDF, nao mostra o certificado a pessoa e a validacao por codigo
--   responde "nao encontrado" para esse treinamento. A linha criada pelo
--   trigger fica la, sem uso.
--   A aplicacao NAO deve deixar mudar EMITE_CERTIFICADO depois da primeira
--   conclusao: desligar depois de emitir faria certificados ja entregues
--   responderem "nao encontrado", e ligar depois exporia conclusoes
--   antigas. (Regra minha: confirme com o dono.)
--   Para quem mapeia no EF Core: EMITE_CERTIFICADO e as tres caixas tem
--   padrao VERDADEIRO no banco. Nao declare esses padroes no mapeamento,
--   senao o EF pode omitir o valor "falso" e o banco gravar "verdadeiro".
--
-- A COLUNA CHAVE_DESTINO (mesma ideia de V018)
--   O MySQL aceita varios NULL numa chave unica, por isso a chave contra
--   linhas repetidas e feita sobre uma coluna gerada (NULL vira 0). Nao
--   grave nela.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - PUBLICO = SETORES exige linhas com ID_SETOR, PUBLICO = PESSOAS exige
--     linhas com ID_FUNCIONARIO (outra tabela: nao da para um CHECK).
--   - Resolver "setor" em pessoas na hora e, se INCLUI_ADMITIDOS, incluir
--     quem for admitido ate o fim do treinamento.
--   - Treinamento OBRIGATORIO: a lista de pendentes do RH e do gestor usa
--     o prazo (DATA_FIM).
--
-- TREINAMENTOS NAO TEM TRIGGER (so TREINAMENTOS_FUNCIONARIOS tem), nada do
-- grupo e alterado. TABELA NOVA: SEM TRIGGER (regra do dono, 2026-10-29).
-- ============================================================

ALTER TABLE TREINAMENTOS
    ADD COLUMN PUBLICADO BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN PUBLICO VARCHAR(10) NULL,
    ADD COLUMN EMITE_CERTIFICADO BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN INCLUI_ADMITIDOS BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN AVISA_POR_EMAIL BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN LEMBRA_ANTES_DO_FIM BOOLEAN NOT NULL DEFAULT TRUE,
    ADD CONSTRAINT CHK_TREINAMENTOS_PUBLICO
        CHECK (PUBLICO IS NULL OR PUBLICO IN ('TODOS', 'SETORES', 'PESSOAS'));

CREATE TABLE TREINAMENTOS_PUBLICO (
    ID_TREINAMENTO_PUBLICO INT AUTO_INCREMENT PRIMARY KEY,
    ID_TREINAMENTO INT NOT NULL,
    ID_SETOR INT NULL,
    ID_FUNCIONARIO INT NULL,
    CHAVE_DESTINO VARCHAR(30) GENERATED ALWAYS AS
        (CONCAT_WS(':', IFNULL(ID_FUNCIONARIO, 0), IFNULL(ID_SETOR, 0))) STORED,

    CONSTRAINT FK_TREINAMENTOS_PUBLICO_TREINAMENTOS
        FOREIGN KEY (ID_TREINAMENTO)
        REFERENCES TREINAMENTOS(ID_TREINAMENTO),

    CONSTRAINT FK_TREINAMENTOS_PUBLICO_SETORES
        FOREIGN KEY (ID_SETOR)
        REFERENCES SETORES(ID_SETOR),

    CONSTRAINT FK_TREINAMENTOS_PUBLICO_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT UQ_TREINAMENTOS_PUBLICO_DESTINO UNIQUE (ID_TREINAMENTO, CHAVE_DESTINO),
    -- setor OU pessoa, nunca os dois e nunca nenhum
    CONSTRAINT CHK_TREINAMENTOS_PUBLICO_SETOR_OU_PESSOA
        CHECK ((ID_SETOR IS NULL) <> (ID_FUNCIONARIO IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- treinamentos que ja existiam: continuam publicados, para as pessoas que
-- ja participavam (ver "TREINAMENTOS QUE JA EXISTIAM" no topo)
UPDATE TREINAMENTOS SET PUBLICADO = TRUE, PUBLICO = 'PESSOAS';

INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO)
SELECT DISTINCT ID_TREINAMENTO, ID_FUNCIONARIO
  FROM TREINAMENTOS_FUNCIONARIOS;
