-- ============================================================
-- V026 - Modulos do treinamento e progresso por modulo
-- Item 21 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O treinamento e feito de MODULOS (Figma: etapa "Conteudo" do
--   assistente, ex.: "1 O que e assedio moral e sexual - Video, 15
--   minutos", "2 Situacoes do dia a dia - Texto", "3 Canais de denuncia -
--   Arquivo PDF"). A pessoa avanca modulo a modulo. Hoje o banco so
--   sabe o status do treinamento inteiro (TREINAMENTOS_FUNCIONARIOS).
--   Decisoes do dono (2026-10-29):
--   - sem prova final e sem nota, o treinamento fica CONCLUIDO quando a
--     pessoa conclui TODOS os modulos, e ai o certificado sai,
--   - so online (por isso nao ha coluna de modalidade), sem o tipo de
--     modulo "aula presencial" (o Figma tem esse botao, fica escondido),
--   - "inscrever-se" nos opcionais usa o status INSCRITO, que ja existe.
--
-- O QUE ESTE SCRIPT FAZ
--   1) TREINAMENTOS_MODULOS: cada modulo, com ordem, titulo e tipo
--      (VIDEO, TEXTO, LINK ou PDF). DURACAO_MINUTOS e o "15 minutos" do
--      video no Figma.
--   2) TREINAMENTOS_MODULOS_PROGRESSO: quem concluiu qual modulo. Uma
--      linha existe SO quando o modulo foi concluido.
--
-- ONDE FICAM OS VIDEOS (ainda em decisao do dono)
--   Arquivo no Blob Storage ou link externo (YouTube etc.). Isso muda as
--   colunas do modulo. Para nao travar, o modulo tem AS DUAS:
--   URL_EXTERNA e CAMINHO_ARQUIVO. O CHECK exige o que cada tipo precisa:
--     VIDEO : URL_EXTERNA ou CAMINHO_ARQUIVO
--     TEXTO : CONTEUDO_TEXTO
--     LINK  : URL_EXTERNA
--     PDF   : CAMINHO_ARQUIVO
--   Quando o dono decidir, pode-se remover a coluna que sobrar.
--
-- QUEM MARCA CONCLUIDO
--   A APLICACAO, quando a pessoa conclui o ultimo modulo: atualiza
--   TREINAMENTOS_FUNCIONARIOS para CONCLUIDO. Os triggers do grupo
--   (TRG_TREINAMENTOS_FUNCIONARIOS_*) cuidam do resto (data de conclusao
--   e linha do certificado). Nao ha trigger novo.
--
-- CUIDADO
--   CONCLUIDO e FINAL (TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_UPDATE). Um
--   modulo acrescentado DEPOIS nao reabre quem ja concluiu, a aplicacao
--   trata (por exemplo, avisando o RH).
--
-- TABELAS NOVAS: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE TREINAMENTOS_MODULOS (
    ID_MODULO INT AUTO_INCREMENT PRIMARY KEY,
    ID_TREINAMENTO INT NOT NULL,
    ORDEM INT NOT NULL,
    TITULO VARCHAR(200) NOT NULL,
    TIPO_MODULO VARCHAR(10) NOT NULL,
    DURACAO_MINUTOS SMALLINT NULL,
    URL_EXTERNA VARCHAR(500) NULL,
    CAMINHO_ARQUIVO VARCHAR(255) NULL,
    CONTEUDO_TEXTO MEDIUMTEXT NULL,

    CONSTRAINT FK_TREINAMENTOS_MODULOS_TREINAMENTOS
        FOREIGN KEY (ID_TREINAMENTO)
        REFERENCES TREINAMENTOS(ID_TREINAMENTO),

    CONSTRAINT UQ_TREINAMENTOS_MODULOS_TREINAMENTO_ORDEM UNIQUE (ID_TREINAMENTO, ORDEM),
    CONSTRAINT CHK_TREINAMENTOS_MODULOS_ORDEM CHECK (ORDEM > 0),
    CONSTRAINT CHK_TREINAMENTOS_MODULOS_TIPO CHECK (TIPO_MODULO IN ('VIDEO', 'TEXTO', 'LINK', 'PDF')),
    CONSTRAINT CHK_TREINAMENTOS_MODULOS_DURACAO CHECK (DURACAO_MINUTOS IS NULL OR DURACAO_MINUTOS > 0),
    CONSTRAINT CHK_TREINAMENTOS_MODULOS_CONTEUDO
        CHECK ((TIPO_MODULO = 'VIDEO' AND (URL_EXTERNA IS NOT NULL OR CAMINHO_ARQUIVO IS NOT NULL))
               OR (TIPO_MODULO = 'TEXTO' AND CONTEUDO_TEXTO IS NOT NULL)
               OR (TIPO_MODULO = 'LINK' AND URL_EXTERNA IS NOT NULL)
               OR (TIPO_MODULO = 'PDF' AND CAMINHO_ARQUIVO IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE TREINAMENTOS_MODULOS_PROGRESSO (
    ID_MODULO_PROGRESSO INT AUTO_INCREMENT PRIMARY KEY,
    ID_MODULO INT NOT NULL,
    ID_FUNCIONARIO INT NOT NULL,
    DATA_CONCLUSAO DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_TREINAMENTOS_MODULOS_PROGRESSO_MODULOS
        FOREIGN KEY (ID_MODULO)
        REFERENCES TREINAMENTOS_MODULOS(ID_MODULO),

    CONSTRAINT FK_TREINAMENTOS_MODULOS_PROGRESSO_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT UQ_TREINAMENTOS_MODULOS_PROGRESSO_MODULO_FUNC UNIQUE (ID_MODULO, ID_FUNCIONARIO)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
