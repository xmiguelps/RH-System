-- ============================================================
-- V007: PROCEDURES (decisao do dono, 2026-11-17)
-- Reescrita do arquivo de procedures do grupo (RHSystem_Procedures_RHSystem_Atual.sql, 24 procedures) alinhada ao contrato da API
-- (contrato/) e aos scripts V001 a V006, testada em MySQL 8.4 (back-end/tests/.../Procedures).
--
-- Somente PROCEDURES: nenhuma tabela, FUNCTION ou TRIGGER e criada aqui. Cada procedure e UMA operacao transacional de negocio, com as mesmas
-- regras e os mesmos efeitos no BANCO que o caso de uso equivalente da API. O que fica de fora de proposito (a API faz e o banco nao pode):
-- autorizacao por perfil, envio de e-mail, arquivo no armazenamento, eventos de auditoria e tarefas agendadas.
-- Mensagens de recusa: SIGNAL SQLSTATE '45000', sem acento (como as dos triggers do V002).
-- ============================================================

DELIMITER $$

-- [[DOMINIO-A: PONTO]]
-- (preenchido pela secao A)






-- [[DOMINIO-B: FERIAS]]
-- (preenchido pela secao B)






-- [[DOMINIO-C: HOLERITE, ATESTADO E BENEFICIOS]]
-- (preenchido pela secao C)






-- [[DOMINIO-D: ADVERTENCIA, TREINAMENTO, FORMULARIO E DESLIGAMENTO]]
-- Nove procedures: D1 SP_GERAR_ADVERTENCIA, D2 SP_REGISTRAR_CIENCIA_ADVERTENCIA, D3 SP_CONTESTAR_ADVERTENCIA,
-- D4 SP_RESPONDER_CONTESTACAO_ADVERTENCIA (nova), D5 SP_CONCLUIR_MODULO_TREINAMENTO, D6 SP_ENCERRAR_FORMULARIO,
-- D7 SP_AGENDAR_DESLIGAMENTO, D8 SP_CANCELAR_DESLIGAMENTO e D9 SP_EFETIVAR_DESLIGAMENTO.
-- Em todas, a PRIMEIRA leitura da transacao e a que trava (FOR UPDATE): as leituras seguintes ja enxergam o que a chamada concorrente gravou.

-- ------------------------------------------------------------
-- D1. SP_GERAR_ADVERTENCIA: registra uma advertencia (nasce PENDENTE).
--
-- Espelha: registrarAdvertencia (POST /api/rh/funcionarios/{funcionarioId}/advertencias) e registrarAdvertenciaDaEquipe
--   (POST /api/gestor/equipe/{funcionarioId}/advertencias); casos de uso RegistrarAdvertenciaDoRhCasoDeUso,
--   RegistrarAdvertenciaDaEquipeCasoDeUso e RegistradorDeAdvertencia.
--
-- Quem registra (os tres jeitos da API):
--   P_ID_FUNCIONARIO_RH preenchido e P_ID_GESTOR nulo ...... o RH registra e aplica (aplicadaPor = RH).
--   P_ID_FUNCIONARIO_RH e P_ID_GESTOR preenchidos .......... o RH registra em nome do GESTOR DA PESSOA (aplicadaPor = GESTOR_DO_COLABORADOR):
--                                                            P_ID_GESTOR tem de ser o FUNCIONARIOS.ID_GESTOR dela, e ele ativo.
--   P_ID_FUNCIONARIO_RH nulo e P_ID_GESTOR preenchido ...... o gestor registra pela tela dele: a pessoa tem de ser da EQUIPE dele
--                                                            (o setor de que ele e o gestor em SETORES.ID_GESTOR; ele mesmo nao e da equipe).
--
-- Regras, na ordem de recusa:
--   1. entradas: funcionario; quem registra (gestor, RH ou os dois); data do ocorrido (obrigatoria, hoje ou antes); tipo (VERBAL ou
--      ESCRITA); motivo (1 a 255 caracteres, aparado); descricao (1 a 5000, aparada);
--   2. a pessoa existe;
--   3. a pessoa esta ativa (a mesma mensagem do trigger TRG_ADVERTENCIAS_BEFORE_INSERT);
--   4. a data do ocorrido nao e anterior a admissao (ADVERTENCIA_ANTES_DA_ADMISSAO);
--   5. quem registra: o RH existe e esta ativo; o gestor e o gestor ativo da pessoa (ADVERTENCIA_SEM_GESTOR) ou, sem o RH, o gestor
--      existe, esta ativo, nao e a propria pessoa e a pessoa e da equipe dele.
--
-- A API faz e esta procedure nao: conferir o PERFIL de quem registra (RH ou Gestor), o e-mail a pessoa (e a copia ao RH quando o
--   gestor registra) e o evento de auditoria.
--
-- Mudou em relacao ao arquivo antigo: dois parametros novos (P_ID_FUNCIONARIO_RH, gravado em ADVERTENCIAS.ID_FUNCIONARIO_RH do V006,
--   e P_DATA_OCORRIDO; a antiga gravava CURDATE() fixo); P_MOTIVO virou TEXT para a procedure poder recusar o motivo longo com a
--   mensagem certa; a antiga nao conferia pessoa inativa, admissao, tipo, descricao, quem registrava nem a equipe do gestor.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_GERAR_ADVERTENCIA(
    IN P_ID_FUNCIONARIO INT,
    IN P_ID_GESTOR INT,
    IN P_ID_FUNCIONARIO_RH INT,
    IN P_DATA_OCORRIDO DATE,
    IN P_TIPO_ADVERTENCIA VARCHAR(50),
    IN P_MOTIVO TEXT,
    IN P_DESCRICAO TEXT,
    OUT P_ID_ADVERTENCIA INT
)
BEGIN
    DECLARE V_MOTIVO TEXT;
    DECLARE V_DESCRICAO TEXT;
    DECLARE V_TIPO VARCHAR(50);
    DECLARE V_PESSOA INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_SETOR INT DEFAULT NULL;
    DECLARE V_GESTOR_DA_PESSOA INT DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_FUNC_DO_GESTOR INT DEFAULT NULL;
    DECLARE V_GESTOR_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_SETOR_DO_GESTOR INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_ADVERTENCIA = NULL;

    -- Aparar como a API (espacos, tabulacoes e quebras de linha nas pontas) e contar por caractere.
    SET V_MOTIVO = REGEXP_REPLACE(COALESCE(P_MOTIVO, ''), '^[[:space:]]+|[[:space:]]+$', '');
    SET V_DESCRICAO = REGEXP_REPLACE(COALESCE(P_DESCRICAO, ''), '^[[:space:]]+|[[:space:]]+$', '');
    SET V_TIPO = UPPER(TRIM(COALESCE(P_TIPO_ADVERTENCIA, '')));

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario advertido.';
    END IF;

    IF P_ID_GESTOR IS NULL AND P_ID_FUNCIONARIO_RH IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe quem registra a advertencia: o gestor, o responsavel do RH ou os dois.';
    END IF;

    IF P_DATA_OCORRIDO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data do ocorrido.';
    END IF;

    IF P_DATA_OCORRIDO > CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data do ocorrido nao pode ser futura.';
    END IF;

    IF V_TIPO NOT IN ('VERBAL', 'ESCRITA') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Tipo de advertencia invalido: use VERBAL ou ESCRITA.';
    END IF;

    IF CHAR_LENGTH(V_MOTIVO) = 0 OR CHAR_LENGTH(V_MOTIVO) > 255 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o motivo, com ate 255 caracteres.';
    END IF;

    IF CHAR_LENGTH(V_DESCRICAO) = 0 OR CHAR_LENGTH(V_DESCRICAO) > 5000 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a descricao, com ate 5000 caracteres.';
    END IF;

    START TRANSACTION;

    -- A linha da pessoa fica travada: uma inativacao ao mesmo tempo espera, e a advertencia nunca nasce para quem ja saiu.
    SELECT ID_FUNCIONARIO, ATIVO, DATA_ADMISSAO, ID_SETOR, ID_GESTOR
      INTO V_PESSOA, V_ATIVO, V_ADMISSAO, V_SETOR, V_GESTOR_DA_PESSOA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_PESSOA IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel registrar advertencia para funcionario inativo.';
    END IF;

    IF P_DATA_OCORRIDO < V_ADMISSAO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data do ocorrido nao pode ser anterior a admissao do funcionario.';
    END IF;

    IF P_ID_FUNCIONARIO_RH IS NOT NULL THEN
        SELECT ATIVO INTO V_RH_ATIVO
          FROM FUNCIONARIOS
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

        IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
        END IF;

        IF P_ID_GESTOR IS NOT NULL THEN
            -- O RH registra em nome do gestor DA PESSOA: so vale o gestor dela, e ativo.
            SELECT F.ATIVO INTO V_GESTOR_ATIVO
              FROM GESTORES G
              JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
             WHERE G.ID_GESTOR = P_ID_GESTOR;

            IF V_GESTOR_DA_PESSOA IS NULL OR V_GESTOR_DA_PESSOA <> P_ID_GESTOR
               OR V_GESTOR_ATIVO IS NULL OR V_GESTOR_ATIVO = FALSE THEN
                SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O gestor informado nao e o gestor ativo do funcionario.';
            END IF;
        END IF;
    ELSE
        -- O gestor registra pela tela dele: so para a equipe dele.
        SELECT G.ID_FUNCIONARIO, F.ATIVO
          INTO V_FUNC_DO_GESTOR, V_GESTOR_ATIVO
          FROM GESTORES G
          JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
         WHERE G.ID_GESTOR = P_ID_GESTOR;

        IF V_FUNC_DO_GESTOR IS NULL OR V_GESTOR_ATIVO = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Gestor inexistente ou inativo.';
        END IF;

        IF V_FUNC_DO_GESTOR = P_ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O gestor nao pode advertir a si mesmo.';
        END IF;

        -- A equipe e o setor de que ele e o gestor (se for de mais de um, vale o de menor id, como na API).
        SELECT MIN(ID_SETOR) INTO V_SETOR_DO_GESTOR
          FROM SETORES
         WHERE ID_GESTOR = P_ID_GESTOR;

        IF V_SETOR_DO_GESTOR IS NULL OR V_SETOR IS NULL OR V_SETOR <> V_SETOR_DO_GESTOR THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario nao e da equipe do gestor informado.';
        END IF;
    END IF;

    INSERT INTO ADVERTENCIAS (
        ID_FUNCIONARIO, ID_GESTOR, ID_FUNCIONARIO_RH, DATA_ADVERTENCIA,
        TIPO_ADVERTENCIA, MOTIVO, DESCRICAO, STATUS_ADVERTENCIA
    )
    VALUES (
        P_ID_FUNCIONARIO, P_ID_GESTOR, P_ID_FUNCIONARIO_RH, P_DATA_OCORRIDO,
        V_TIPO, V_MOTIVO, V_DESCRICAO, 'PENDENTE'
    );

    SET P_ID_ADVERTENCIA = LAST_INSERT_ID();
    COMMIT;
END$$

-- ------------------------------------------------------------
-- D2. SP_REGISTRAR_CIENCIA_ADVERTENCIA: a pessoa confirma que tomou ciencia (nao e concordar). PENDENTE -> APLICADA.
--
-- Espelha: confirmarCienciaDeAdvertencia (POST /api/eu/advertencias/{advertenciaId}/ciencia); ConfirmarCienciaDeAdvertenciaCasoDeUso
--   e AdvertenciasRepositorio.ConfirmarCienciaAsync (UPDATE condicional em PENDENTE e na pessoa dona).
--
-- Regras, na ordem de recusa:
--   1. entradas: advertencia e funcionario;
--   2. a advertencia existe e e DESTE funcionario ("nao existe" e "e de outra pessoa" dao a mesma mensagem, como o 404 da API);
--   3. a advertencia esta PENDENTE (ADVERTENCIA_NAO_ESTA_PENDENTE): repetir a ciencia, ou dar ciencia depois de contestar, e recusado.
--   Efeito: STATUS_ADVERTENCIA = APLICADA e DATA_CIENCIA = agora (horario de Brasilia, ao segundo).
--
-- A API faz e esta procedure nao: o evento de auditoria. Nao ha e-mail.
--
-- Mudou em relacao ao arquivo antigo: a antiga so gravava DATA_CIENCIA, sem mudar a situacao (a advertencia ficava PENDENTE para
--   sempre), e aceitava a ciencia em advertencia contestada, aplicada ou cancelada que ainda nao tivesse a data.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_REGISTRAR_CIENCIA_ADVERTENCIA(
    IN P_ID_ADVERTENCIA INT,
    IN P_ID_FUNCIONARIO INT
)
BEGIN
    DECLARE V_DONO INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_ADVERTENCIA IS NULL OR P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a advertencia e o funcionario.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, STATUS_ADVERTENCIA
      INTO V_DONO, V_STATUS
      FROM ADVERTENCIAS
     WHERE ID_ADVERTENCIA = P_ID_ADVERTENCIA
     FOR UPDATE;

    IF V_DONO IS NULL OR V_DONO <> P_ID_FUNCIONARIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Advertencia nao encontrada para o funcionario informado.';
    END IF;

    IF V_STATUS IS NULL OR V_STATUS <> 'PENDENTE' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A advertencia nao esta pendente: a ciencia so vale para advertencia PENDENTE.';
    END IF;

    UPDATE ADVERTENCIAS
       SET STATUS_ADVERTENCIA = 'APLICADA',
           DATA_CIENCIA = NOW()
     WHERE ID_ADVERTENCIA = P_ID_ADVERTENCIA;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D3. SP_CONTESTAR_ADVERTENCIA: a pessoa contesta a advertencia, explicando. PENDENTE -> CONTESTADA.
--
-- Espelha: contestarAdvertencia (POST /api/eu/advertencias/{advertenciaId}/contestacao); ContestarAdvertenciaCasoDeUso e
--   AdvertenciasRepositorio.ContestarAsync (UPDATE condicional em PENDENTE e na pessoa dona).
--
-- Regras, na ordem de recusa:
--   1. entradas: advertencia, funcionario e o texto (10 a 2000 caracteres, aparado);
--   2. a advertencia existe e e DESTE funcionario;
--   3. a advertencia esta PENDENTE (ADVERTENCIA_NAO_ESTA_PENDENTE): a primeira contestacao nunca e sobrescrita, e nao se contesta
--      depois da ciencia.
--   Efeito: STATUS_ADVERTENCIA = CONTESTADA, TEXTO_CONTESTACAO e DATA_CONTESTACAO = agora (horario de Brasilia, ao segundo).
--
-- A API faz e esta procedure nao: o evento de auditoria. Nao ha e-mail (o RH encontra a contestacao na aba "Contestada").
--
-- Mudou em relacao ao arquivo antigo: a antiga aceitava contestar a advertencia APLICADA (depois da ciencia), nao tinha tamanho minimo
--   nem maximo para o texto e nao o aparava.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CONTESTAR_ADVERTENCIA(
    IN P_ID_ADVERTENCIA INT,
    IN P_ID_FUNCIONARIO INT,
    IN P_TEXTO_CONTESTACAO TEXT
)
BEGIN
    DECLARE V_TEXTO TEXT;
    DECLARE V_DONO INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET V_TEXTO = REGEXP_REPLACE(COALESCE(P_TEXTO_CONTESTACAO, ''), '^[[:space:]]+|[[:space:]]+$', '');

    IF P_ID_ADVERTENCIA IS NULL OR P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a advertencia e o funcionario.';
    END IF;

    IF CHAR_LENGTH(V_TEXTO) < 10 OR CHAR_LENGTH(V_TEXTO) > 2000 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Explique o que aconteceu no texto da contestacao, com de 10 a 2000 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, STATUS_ADVERTENCIA
      INTO V_DONO, V_STATUS
      FROM ADVERTENCIAS
     WHERE ID_ADVERTENCIA = P_ID_ADVERTENCIA
     FOR UPDATE;

    IF V_DONO IS NULL OR V_DONO <> P_ID_FUNCIONARIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Advertencia nao encontrada para o funcionario informado.';
    END IF;

    IF V_STATUS IS NULL OR V_STATUS <> 'PENDENTE' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A advertencia nao esta pendente: so a advertencia PENDENTE pode ser contestada.';
    END IF;

    UPDATE ADVERTENCIAS
       SET STATUS_ADVERTENCIA = 'CONTESTADA',
           TEXTO_CONTESTACAO = V_TEXTO,
           DATA_CONTESTACAO = NOW()
     WHERE ID_ADVERTENCIA = P_ID_ADVERTENCIA;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D4. SP_RESPONDER_CONTESTACAO_ADVERTENCIA (NOVA: nao existia no arquivo antigo): o RH responde a contestacao.
--     MANTER: CONTESTADA -> APLICADA. ACATAR: CONTESTADA -> CANCELADA (e cancelada nao muda mais: trigger).
--
-- Espelha: responderContestacaoDeAdvertencia (POST /api/rh/advertencias/{advertenciaId}/resposta);
--   ResponderContestacaoDeAdvertenciaCasoDeUso e AdvertenciasRepositorio.ResponderAsync (UPDATE condicional em CONTESTADA).
--
-- Por que existe: sem ela a advertencia contestada pela procedure D3 ficaria parada em CONTESTADA, sem saida pelo banco.
--
-- Regras, na ordem de recusa:
--   1. entradas: advertencia e decisao (MANTER ou ACATAR);
--   2. a advertencia existe;
--   3. a advertencia esta CONTESTADA (ADVERTENCIA_NAO_ESTA_CONTESTADA): responder duas vezes e recusado.
--   Efeito: so a situacao muda. O texto e a data da contestacao ficam (e por eles que a API deduz o desfecho: APLICADA com texto de
--   contestacao = mantida; CANCELADA = acatada).
--
-- A API faz e esta procedure nao: o TEXTO da resposta do RH (1 a 2000 caracteres) NAO tem coluna no banco: a API o manda por e-mail a
--   pessoa e o guarda na auditoria. Por isso a procedure nao o recebe; quem usar a procedure sem a API tem de avisar a pessoa por
--   fora. Tambem ficam de fora o perfil RH de quem responde e o evento de auditoria.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_RESPONDER_CONTESTACAO_ADVERTENCIA(
    IN P_ID_ADVERTENCIA INT,
    IN P_DECISAO VARCHAR(10)
)
BEGIN
    DECLARE V_DECISAO VARCHAR(10);
    DECLARE V_ID INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET V_DECISAO = UPPER(TRIM(COALESCE(P_DECISAO, '')));

    IF P_ID_ADVERTENCIA IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a advertencia.';
    END IF;

    IF V_DECISAO NOT IN ('MANTER', 'ACATAR') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Decisao invalida: use MANTER ou ACATAR.';
    END IF;

    START TRANSACTION;

    SELECT ID_ADVERTENCIA, STATUS_ADVERTENCIA
      INTO V_ID, V_STATUS
      FROM ADVERTENCIAS
     WHERE ID_ADVERTENCIA = P_ID_ADVERTENCIA
     FOR UPDATE;

    IF V_ID IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Advertencia nao encontrada.';
    END IF;

    IF V_STATUS IS NULL OR V_STATUS <> 'CONTESTADA' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A advertencia nao esta contestada: so a contestacao em aberto pode ser respondida.';
    END IF;

    UPDATE ADVERTENCIAS
       SET STATUS_ADVERTENCIA = CASE WHEN V_DECISAO = 'ACATAR' THEN 'CANCELADA' ELSE 'APLICADA' END
     WHERE ID_ADVERTENCIA = P_ID_ADVERTENCIA;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D5. SP_CONCLUIR_MODULO_TREINAMENTO: a pessoa conclui um modulo; o ultimo modulo conclui o treinamento e registra o certificado.
--
-- Espelha: concluirModuloDeTreinamento (POST /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/concluir);
--   ConcluirModuloDeTreinamentoCasoDeUso, ConclusaoDoTreinamentoRepositorio e MinhaParticipacaoRepositorio.
--
-- NAO existe trigger que atualize o status geral a partir do progresso dos modulos (o comentario do arquivo antigo dizia que sim): os
--   triggers TRG_TREINAMENTOS_FUNCIONARIOS_* so reagem a mudanca de STATUS_TREINAMENTO. Quem muda o status e esta procedure.
--
-- Regras, na ordem de recusa (a transacao comeca travando a linha do TREINAMENTO, a mesma trava da API: duas conclusoes do mesmo
-- treinamento se enfileiram, e os dois ultimos modulos concluidos ao mesmo tempo dao um so CONCLUIDO e um so certificado):
--   1. entradas: modulo e funcionario;
--   2. o modulo existe;
--   3. o treinamento esta liberado: publicado e com a data de inicio ja chegada (ou sem data);
--   4. o funcionario existe e esta ativo;
--   5. sem linha em TREINAMENTOS_FUNCIONARIOS: no obrigatorio, ou fora do publico do treinamento (TODOS, o setor dele ou ele entre as
--      pessoas escolhidas), ele nao participa; no opcional do publico dele, falta a inscricao (INSCRICAO_PENDENTE) ou, se o RH ja
--      encerrou, o treinamento esta encerrado para ele (TREINAMENTO_ENCERRADO);
--   6. linha REPROVADO, CANCELADO ou sem situacao: nao participa;
--   7. treinamento encerrado e linha so INSCRITO (nunca comecou): TREINAMENTO_ENCERRADO. Quem ja estava EM_ANDAMENTO conclui
--      normalmente, mesmo depois do encerramento.
--   Efeitos:
--   - linha ja CONCLUIDO, ou modulo ja concluido e ainda faltam outros: nada e gravado (repetir e inofensivo);
--   - grava o progresso do modulo (DATA_CONCLUSAO = agora);
--   - a primeira conclusao de quem estava INSCRITO tambem inicia: EM_ANDAMENTO, DATA_INICIO = hoje;
--   - se era o ultimo modulo que faltava (ou todos ja estavam concluidos e a linha nao): CONCLUIDO, DATA_CONCLUSAO = hoje; o trigger
--     TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_UPDATE cria a linha do certificado com um UUID versao 1 (previsivel) e a procedure, como a
--     API, troca o codigo por um UUID versao 4 aleatorio na mesma transacao (a conferencia publica da API so aceita versao 4).
--     A nota nunca e gravada.
--   P_TREINAMENTO_CONCLUIDO volta TRUE so quando ESTA chamada concluiu o treinamento (o treinamentoConcluido da resposta da API).
--
-- A API faz e esta procedure nao: gerar o PDF do certificado (CAMINHO_ARQUIVO fica NULL; a API gera o arquivo no primeiro download) e o
--   evento de auditoria. A linha do certificado nasce mesmo em treinamento com EMITE_CERTIFICADO = FALSE (e do trigger); a API a ignora.
--
-- Mudou em relacao ao arquivo antigo: parametro novo OUT P_TREINAMENTO_CONCLUIDO; a antiga so gravava o progresso (ninguem saia de
--   INSCRITO nem chegava a CONCLUIDO, e o certificado nunca nascia), recusava o treinamento encerrado ate para quem estava em
--   andamento, nao conferia a data de inicio e reescrevia a data de um modulo ja concluido.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CONCLUIR_MODULO_TREINAMENTO(
    IN P_ID_MODULO INT,
    IN P_ID_FUNCIONARIO INT,
    OUT P_TREINAMENTO_CONCLUIDO BOOLEAN
)
PROC: BEGIN
    DECLARE V_ID_TREINAMENTO INT DEFAULT NULL;
    DECLARE V_PUBLICADO BOOLEAN DEFAULT NULL;
    DECLARE V_DATA_INICIO DATE DEFAULT NULL;
    DECLARE V_OBRIGATORIO BOOLEAN DEFAULT NULL;
    DECLARE V_ENCERRADO_EM DATETIME DEFAULT NULL;
    DECLARE V_PUBLICO VARCHAR(10) DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_SETOR INT DEFAULT NULL;
    DECLARE V_ID_LINHA INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;
    DECLARE V_DO_PUBLICO BOOLEAN DEFAULT FALSE;
    DECLARE V_TOTAL INT DEFAULT 0;
    DECLARE V_CONCLUIDOS INT DEFAULT 0;
    DECLARE V_JA_CONCLUIDO INT DEFAULT 0;
    DECLARE V_TUDO BOOLEAN DEFAULT FALSE;
    DECLARE V_HEX CHAR(32);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_TREINAMENTO_CONCLUIDO = FALSE;

    IF P_ID_MODULO IS NULL OR P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o modulo e o funcionario.';
    END IF;

    START TRANSACTION;

    -- Trava a linha do treinamento (e a do modulo). Tem de ser a PRIMEIRA leitura da transacao: as seguintes ja enxergam o que a
    -- conclusao anterior do mesmo treinamento gravou.
    SELECT T.ID_TREINAMENTO, T.PUBLICADO, T.DATA_INICIO, T.OBRIGATORIO, T.ENCERRADO_EM, T.PUBLICO
      INTO V_ID_TREINAMENTO, V_PUBLICADO, V_DATA_INICIO, V_OBRIGATORIO, V_ENCERRADO_EM, V_PUBLICO
      FROM TREINAMENTOS_MODULOS M
      JOIN TREINAMENTOS T ON T.ID_TREINAMENTO = M.ID_TREINAMENTO
     WHERE M.ID_MODULO = P_ID_MODULO
     FOR UPDATE;

    IF V_ID_TREINAMENTO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Modulo de treinamento nao encontrado.';
    END IF;

    IF V_PUBLICADO = FALSE OR (V_DATA_INICIO IS NOT NULL AND V_DATA_INICIO > CURDATE()) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Treinamento ainda nao liberado: precisa estar publicado e com a data de inicio ja chegada.';
    END IF;

    SELECT ATIVO, ID_SETOR INTO V_ATIVO, V_SETOR
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO;

    IF V_ATIVO IS NULL OR V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario inexistente ou inativo.';
    END IF;

    SELECT ID_TREINAMENTO_FUNCIONARIO, STATUS_TREINAMENTO
      INTO V_ID_LINHA, V_STATUS
      FROM TREINAMENTOS_FUNCIONARIOS
     WHERE ID_TREINAMENTO = V_ID_TREINAMENTO
       AND ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ID_LINHA IS NULL THEN
        SET V_DO_PUBLICO = CASE
            WHEN V_PUBLICO = 'TODOS' THEN TRUE
            WHEN V_PUBLICO = 'SETORES' THEN V_SETOR IS NOT NULL AND EXISTS (
                SELECT 1 FROM TREINAMENTOS_PUBLICO
                 WHERE ID_TREINAMENTO = V_ID_TREINAMENTO AND ID_SETOR = V_SETOR)
            WHEN V_PUBLICO = 'PESSOAS' THEN EXISTS (
                SELECT 1 FROM TREINAMENTOS_PUBLICO
                 WHERE ID_TREINAMENTO = V_ID_TREINAMENTO AND ID_FUNCIONARIO = P_ID_FUNCIONARIO)
            ELSE FALSE
        END;

        IF V_OBRIGATORIO = TRUE OR V_DO_PUBLICO = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario nao participa deste treinamento.';
        END IF;

        IF V_ENCERRADO_EM IS NOT NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Treinamento encerrado pelo RH: quem nao tinha comecado nao conclui mais modulos.';
        END IF;

        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Inscricao pendente: inscreva o funcionario no treinamento antes de concluir modulos.';
    END IF;

    IF V_STATUS IS NULL OR V_STATUS NOT IN ('INSCRITO', 'EM_ANDAMENTO', 'CONCLUIDO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario nao participa deste treinamento.';
    END IF;

    IF V_ENCERRADO_EM IS NOT NULL AND V_STATUS = 'INSCRITO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Treinamento encerrado pelo RH: quem nao tinha comecado nao conclui mais modulos.';
    END IF;

    IF V_STATUS = 'CONCLUIDO' THEN
        COMMIT; -- ja concluido (o banco recusa mudar um CONCLUIDO): nada a gravar
        LEAVE PROC;
    END IF;

    SELECT COUNT(*) INTO V_TOTAL
      FROM TREINAMENTOS_MODULOS
     WHERE ID_TREINAMENTO = V_ID_TREINAMENTO;

    SELECT COUNT(*) INTO V_CONCLUIDOS
      FROM TREINAMENTOS_MODULOS_PROGRESSO P
      JOIN TREINAMENTOS_MODULOS M ON M.ID_MODULO = P.ID_MODULO
     WHERE M.ID_TREINAMENTO = V_ID_TREINAMENTO
       AND P.ID_FUNCIONARIO = P_ID_FUNCIONARIO;

    SELECT COUNT(*) INTO V_JA_CONCLUIDO
      FROM TREINAMENTOS_MODULOS_PROGRESSO
     WHERE ID_MODULO = P_ID_MODULO
       AND ID_FUNCIONARIO = P_ID_FUNCIONARIO;

    IF V_JA_CONCLUIDO = 0 THEN
        SET V_CONCLUIDOS = V_CONCLUIDOS + 1;
    END IF;

    SET V_TUDO = (V_TOTAL > 0 AND V_CONCLUIDOS = V_TOTAL);

    IF V_JA_CONCLUIDO > 0 AND V_TUDO = FALSE THEN
        COMMIT; -- repetir um modulo ja concluido nao toca no banco
        LEAVE PROC;
    END IF;

    IF V_JA_CONCLUIDO = 0 THEN
        INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO)
        VALUES (P_ID_MODULO, P_ID_FUNCIONARIO, NOW());
    END IF;

    IF V_STATUS = 'INSCRITO' THEN
        UPDATE TREINAMENTOS_FUNCIONARIOS
           SET STATUS_TREINAMENTO = 'EM_ANDAMENTO',
               DATA_INICIO = CURDATE()
         WHERE ID_TREINAMENTO_FUNCIONARIO = V_ID_LINHA;
    END IF;

    IF V_TUDO = TRUE THEN
        -- O trigger AFTER UPDATE cria a linha do certificado neste UPDATE.
        UPDATE TREINAMENTOS_FUNCIONARIOS
           SET STATUS_TREINAMENTO = 'CONCLUIDO',
               DATA_CONCLUSAO = CURDATE()
         WHERE ID_TREINAMENTO_FUNCIONARIO = V_ID_LINHA;

        -- UUID versao 4 em minusculas (aleatorio, RANDOM_BYTES), no lugar do UUID() versao 1 do trigger.
        SET V_HEX = LOWER(HEX(RANDOM_BYTES(16)));

        UPDATE TREINAMENTOS_CERTIFICADOS
           SET CODIGO_VALIDACAO = CONCAT(
                   SUBSTRING(V_HEX, 1, 8), '-',
                   SUBSTRING(V_HEX, 9, 4), '-4',
                   SUBSTRING(V_HEX, 14, 3), '-',
                   ELT(1 + (CONV(SUBSTRING(V_HEX, 17, 1), 16, 10) & 3), '8', '9', 'a', 'b'),
                   SUBSTRING(V_HEX, 18, 3), '-',
                   SUBSTRING(V_HEX, 21, 12)),
               DATA_EMISSAO = CURDATE()
         WHERE ID_TREINAMENTO = V_ID_TREINAMENTO
           AND ID_FUNCIONARIO = P_ID_FUNCIONARIO;

        SET P_TREINAMENTO_CONCLUIDO = TRUE;
    END IF;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D6. SP_ENCERRAR_FORMULARIO: o RH encerra um formulario PUBLICADO; ninguem mais responde e as proximas reaberturas sao canceladas.
--
-- Espelha: encerrarFormulario (POST /api/rh/formularios/{formularioId}/encerrar?confirmar=true); EncerrarFormularioCasoDeUso e
--   FormulariosRepositorio.EncerrarAsync.
--
-- Regras, na ordem de recusa:
--   1. entrada: o formulario;
--   2. o formulario existe;
--   3. o formulario esta PUBLICADO (FORMULARIO_NAO_PUBLICADO), pela mesma conta da API: ATIVO ligado, sem data de encerramento e com o
--      prazo ainda nao vencido (DATA_PRAZO nulo, hoje ou depois). Rascunho, agendado, encerrado e o de prazo vencido (que a API ja
--      mostra como encerrado, mesmo antes de a tarefa dela gravar) sao recusados.
--   Efeito: ATIVO = FALSE e DATA_ENCERRAMENTO = agora no mesmo UPDATE (o CHECK os amarra) e PROXIMA_REABERTURA = NULL (a recorrencia
--   continua descrita, mas nao reabre mais; nao existe "reabrir").
--
-- A API faz e esta procedure nao:
--   - a CONFIRMACAO: sem confirmar=true a API responde 409 CONFIRMACAO_NECESSARIA quando ainda ha quem possa responder (a conta dos
--     pendentes e feita em codigo, sobre os destinatarios) e SEMPRE no formulario anonimo. A procedure vale como a chamada ja
--     confirmada: quem a chama decidiu encerrar;
--   - cancelar a tarefa agendada ENCERRAR_FORMULARIO do prazo (a tarefa que sobrar rele o formulario e nao faz nada);
--   - o perfil RH e o evento de auditoria.
--
-- Mudou em relacao ao arquivo antigo: a antiga encerrava qualquer formulario com ATIVO ligado sem olhar o prazo e deixava a proxima
--   reabertura marcada (o formulario recorrente reabriria sozinho depois de encerrado a mao).
-- ------------------------------------------------------------
CREATE PROCEDURE SP_ENCERRAR_FORMULARIO(
    IN P_ID_FORMULARIO INT
)
BEGIN
    DECLARE V_ID INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ENCERRAMENTO DATETIME DEFAULT NULL;
    DECLARE V_PRAZO DATE DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_FORMULARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o formulario.';
    END IF;

    START TRANSACTION;

    SELECT ID_FORMULARIO, ATIVO, DATA_ENCERRAMENTO, DATA_PRAZO
      INTO V_ID, V_ATIVO, V_ENCERRAMENTO, V_PRAZO
      FROM FORMULARIOS
     WHERE ID_FORMULARIO = P_ID_FORMULARIO
     FOR UPDATE;

    IF V_ID IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Formulario nao encontrado.';
    END IF;

    IF V_ATIVO = FALSE OR V_ENCERRAMENTO IS NOT NULL OR (V_PRAZO IS NOT NULL AND V_PRAZO < CURDATE()) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'So um formulario publicado pode ser encerrado.';
    END IF;

    UPDATE FORMULARIOS
       SET ATIVO = FALSE,
           DATA_ENCERRAMENTO = NOW(),
           PROXIMA_REABERTURA = NULL
     WHERE ID_FORMULARIO = P_ID_FORMULARIO;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D7. SP_AGENDAR_DESLIGAMENTO: o RH informa o ULTIMO DIA de trabalho; a pessoa continua ativa ate o dia passar.
--
-- Espelha: inativarFuncionario (POST /api/rh/funcionarios/{funcionarioId}/desligamento); InativarFuncionarioCasoDeUso e
--   DesligamentoDeFuncionarios.AgendarAsync.
--
-- Por que "agendar" e nao gravar a data direto: o banco inativa a pessoa assim que FUNCIONARIOS.DATA_DESLIGAMENTO tem valor (trigger
--   TRG_FUNCIONARIOS_BEFORE_UPDATE) e a partir dai recusa o ponto e o banco de horas dela. Por isso o ultimo dia fica guardado em
--   DESLIGAMENTOS_AGENDADOS e so vai para FUNCIONARIOS quando a efetivacao pode acontecer (D9).
--
-- Regras, na ordem de recusa (trava a linha da pessoa, a mesma primeira trava de D8 e D9):
--   1. entradas: funcionario e ultimo dia;
--   2. a pessoa existe;
--   3. o ultimo dia nao e anterior a admissao;
--   4. o ultimo dia nao passa de 10 anos a partir de hoje (erro de digitacao);
--   5. a pessoa ja esta inativa, ou ja tem um desligamento vigente, AGENDADO ou EFETIVADO (DESLIGAMENTO_JA_REGISTRADO).
--   O ultimo dia PODE ser hoje ou do passado (nao ha data minima alem da admissao).
--   Efeito: cria a linha AGENDADO. Como na API, se a efetivacao ja pode acontecer (o dia seguinte ao ultimo dia ja comecou E o banco
--   de horas do ultimo dia ja foi consolidado; ver D9), efetiva NA MESMA transacao: a linha ja sai EFETIVADO e a pessoa, inativa.
--   Senao fica AGENDADO, a espera de SP_EFETIVAR_DESLIGAMENTO.
--
-- A API faz e esta procedure nao: criar a tarefa agendada EFETIVAR_DESLIGAMENTO (com a API no ar nao faz falta: a recuperacao dela, na
--   subida e a cada hora, efetiva todo AGENDADO vencido e agenda a tarefa dos demais; SEM a API, alguem tem de chamar
--   SP_EFETIVAR_DESLIGAMENTO depois do ultimo dia), o perfil RH e o evento de auditoria.
--
-- Mudou em relacao ao arquivo antigo: o limite de 10 anos; a efetivacao na hora quando o dia ja passou; mensagens separadas para
--   "nao existe", "inativo" e "ja tem desligamento".
-- ------------------------------------------------------------
CREATE PROCEDURE SP_AGENDAR_DESLIGAMENTO(
    IN P_ID_FUNCIONARIO INT,
    IN P_ULTIMO_DIA_TRABALHO DATE,
    OUT P_ID_DESLIGAMENTO_AGENDADO INT
)
BEGIN
    DECLARE V_PESSOA INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_DATA_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_VIGENTES INT DEFAULT 0;
    DECLARE V_ULTIMO_CONSOLIDADO DATE DEFAULT NULL;
    DECLARE V_ID_GESTOR INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_DESLIGAMENTO_AGENDADO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario.';
    END IF;

    IF P_ULTIMO_DIA_TRABALHO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ultimo dia de trabalho e obrigatorio.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, ATIVO, DATA_ADMISSAO
      INTO V_PESSOA, V_ATIVO, V_DATA_ADMISSAO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_PESSOA IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF P_ULTIMO_DIA_TRABALHO < V_DATA_ADMISSAO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O ultimo dia de trabalho nao pode ser anterior a data de admissao.';
    END IF;

    IF P_ULTIMO_DIA_TRABALHO > DATE_ADD(CURDATE(), INTERVAL 10 YEAR) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O ultimo dia de trabalho nao pode passar de 10 anos a partir de hoje.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este funcionario ja esta inativo.';
    END IF;

    -- Leitura SEM trava de proposito: com a pessoa travada ninguem mais agenda para ela, e o FOR UPDATE de uma linha que ainda nao
    -- existe seguraria bloqueio de intervalo (duas pessoas diferentes sendo desligadas juntas dariam deadlock). A chave unica
    -- UQ_DESLIGAMENTOS_AGENDADOS_VIGENTE e a garantia final.
    SELECT COUNT(*) INTO V_VIGENTES
      FROM DESLIGAMENTOS_AGENDADOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND VIGENTE = 1;

    IF V_VIGENTES > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este funcionario ja tem um desligamento registrado.';
    END IF;

    INSERT INTO DESLIGAMENTOS_AGENDADOS (ID_FUNCIONARIO, ULTIMO_DIA_TRABALHO, SITUACAO)
    VALUES (P_ID_FUNCIONARIO, P_ULTIMO_DIA_TRABALHO, 'AGENDADO');

    SET P_ID_DESLIGAMENTO_AGENDADO = LAST_INSERT_ID();

    -- Ja pode efetivar? O dia seguinte ao ultimo dia ja comecou e o banco de horas do ultimo dia ja foi consolidado (ver D9).
    SELECT MAX(DIA_REFERENCIA) INTO V_ULTIMO_CONSOLIDADO
      FROM TAREFAS_AGENDADAS
     WHERE TIPO = 'CONSOLIDAR_BANCO_HORAS'
       AND SITUACAO = 'CONCLUIDA';

    IF CURDATE() > P_ULTIMO_DIA_TRABALHO
       AND V_ULTIMO_CONSOLIDADO IS NOT NULL AND V_ULTIMO_CONSOLIDADO >= P_ULTIMO_DIA_TRABALHO THEN
        -- A efetivacao: o MESMO bloco de SP_EFETIVAR_DESLIGAMENTO (D9). Quem mudar um, muda o outro.
        UPDATE FUNCIONARIOS
           SET DATA_DESLIGAMENTO = P_ULTIMO_DIA_TRABALHO,
               ATIVO = FALSE
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO;

        UPDATE DESLIGAMENTOS_AGENDADOS
           SET SITUACAO = 'EFETIVADO',
               EFETIVADO_EM = UTC_TIMESTAMP(6)
         WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO;

        SELECT ID_GESTOR INTO V_ID_GESTOR
          FROM GESTORES
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO;

        IF V_ID_GESTOR IS NOT NULL THEN
            UPDATE PONTOS_MANUTENCOES
               SET ID_GESTOR = NULL
             WHERE STATUS_SOLICITACAO = 'PENDENTE' AND ID_GESTOR = V_ID_GESTOR AND RESULTADO_GESTOR IS NULL;

            UPDATE FERIAS
               SET ID_GESTOR = NULL
             WHERE STATUS_FERIAS = 'SOLICITADA' AND ID_GESTOR = V_ID_GESTOR AND RESULTADO_GESTOR IS NULL;
        END IF;

        UPDATE DEPENDENTES D
          JOIN BENEFICIOS_FUNCIONARIOS B ON B.ID_BENEFICIO_FUNCIONARIOS = D.ID_BENEFICIO_FUNCIONARIOS
           SET D.STATUS_PEDIDO = 'CANCELADO'
         WHERE B.ID_FUNCIONARIO = P_ID_FUNCIONARIO AND D.STATUS_PEDIDO = 'PENDENTE';

        UPDATE TRAJETOS_VALE_TRANSPORTE T
          JOIN BENEFICIOS_FUNCIONARIOS B ON B.ID_BENEFICIO_FUNCIONARIOS = T.ID_BENEFICIO_FUNCIONARIOS
           SET T.STATUS_PEDIDO = 'CANCELADO'
         WHERE B.ID_FUNCIONARIO = P_ID_FUNCIONARIO AND T.STATUS_PEDIDO = 'PENDENTE';

        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO AND STATUS_PEDIDO = 'PENDENTE';
    END IF;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D8. SP_CANCELAR_DESLIGAMENTO: o RH desiste de um desligamento que ainda esta AGENDADO.
--
-- Espelha: cancelarDesligamentoAgendado (DELETE /api/rh/funcionarios/{funcionarioId}/desligamento); CancelarDesligamentoCasoDeUso e
--   DesligamentoDeFuncionarios.CancelarAsync. A API cancela pelo id da PESSOA (o desligamento vigente dela); a procedure mantem o
--   parametro do arquivo antigo, o id do DESLIGAMENTO (o que SP_AGENDAR_DESLIGAMENTO devolve).
--
-- Regras, na ordem de recusa (trava a pessoa e depois o desligamento, a mesma ordem de D7 e D9: a corrida entre cancelar e efetivar
-- termina coerente, nunca CANCELADO com a pessoa inativa):
--   1. entrada: o desligamento;
--   2. o desligamento existe;
--   3. esta AGENDADO (SEM_DESLIGAMENTO_AGENDADO): o EFETIVADO nao se cancela (quem volta a empresa e READMITIDO, pela API), e o
--      CANCELADO ou REVERTIDO ja acabou.
--   Efeito: SITUACAO = CANCELADO e CANCELADO_EM = agora em UTC (esta tabela guarda UTC, como CRIADO_EM). A pessoa continua ativa e
--   pode receber um novo agendamento.
--
-- A API faz e esta procedure nao: cancelar a tarefa agendada EFETIVAR_DESLIGAMENTO (a que sobrar encontra o desligamento cancelado e
--   termina sem fazer nada), o perfil RH e o evento de auditoria.
--
-- Mudou em relacao ao arquivo antigo: P_ID_DESLIGAMENTO_AGENDADO e INT (o tipo da coluna; a antiga declarava BIGINT); trava a pessoa
--   antes do desligamento; mensagens separadas para "nao existe" e "nao esta agendado".
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CANCELAR_DESLIGAMENTO(
    IN P_ID_DESLIGAMENTO_AGENDADO INT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_PESSOA INT DEFAULT NULL;
    DECLARE V_SITUACAO VARCHAR(10) DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_DESLIGAMENTO_AGENDADO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o desligamento.';
    END IF;

    -- A pessoa so se descobre lendo o desligamento, e a ordem de travas e a pessoa primeiro: esta leitura e sem trava e FORA da
    -- transacao (o estado e relido depois de travar).
    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM DESLIGAMENTOS_AGENDADOS
     WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Desligamento nao encontrado.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_PESSOA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT SITUACAO INTO V_SITUACAO
      FROM DESLIGAMENTOS_AGENDADOS
     WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO
     FOR UPDATE;

    IF V_SITUACAO IS NULL OR V_SITUACAO <> 'AGENDADO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao ha desligamento agendado para cancelar: so o desligamento AGENDADO pode ser cancelado.';
    END IF;

    UPDATE DESLIGAMENTOS_AGENDADOS
       SET SITUACAO = 'CANCELADO',
           CANCELADO_EM = UTC_TIMESTAMP(6)
     WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- D9. SP_EFETIVAR_DESLIGAMENTO: grava o desligamento agendado no cadastro da pessoa (ela fica inativa).
--
-- Espelha: DesligamentoDeFuncionarios.EfetivarAsync (nao e rota: quem chama e a tarefa agendada EFETIVAR_DESLIGAMENTO e a recuperacao
--   dela), com a condicao CondicaoDoBancoDeHorasParaDesligamento e os efeitos de PedidosParadosNoGestorDoPonto,
--   PedidosParadosNoGestorDasFerias e CancelaPedidosDeBeneficioAoDesligar.
--
-- Regras, na ordem de recusa (trava a pessoa e depois o desligamento, e rele o estado com os dois travados):
--   1. entrada: o desligamento;
--   2. o desligamento existe;
--   3. ja EFETIVADO: nao e erro, a procedure termina sem gravar nada (e idempotente, como na API: a tarefa pode rodar duas vezes);
--   4. CANCELADO ou REVERTIDO: recusa (nao ha o que efetivar);
--   5. o DIA SEGUINTE ao ultimo dia de trabalho ja comecou (hoje > ultimo dia): no proprio ultimo dia a pessoa ainda trabalha e bate
--      ponto;
--   6. o banco de horas do ultimo dia ja foi consolidado: existe tarefa CONSOLIDAR_BANCO_HORAS CONCLUIDA em TAREFAS_AGENDADAS com
--      DIA_REFERENCIA igual ou posterior ao ultimo dia (a consolidacao e a tarefa diaria da API). Antes disso a pessoa ainda tem dia
--      por lancar no banco de horas, e inativa-la faria o trigger TRG_BANCOS_HORAS_BEFORE_INSERT recusar o lancamento.
--   Efeitos, na mesma transacao:
--   - FUNCIONARIOS: DATA_DESLIGAMENTO = o ultimo dia de trabalho (nao a data de hoje) e ATIVO = FALSE; o trigger
--     TRG_FUNCIONARIOS_AFTER_UPDATE desativa o login (USUARIOS), desativa os beneficios ativos e cancela as ferias futuras;
--   - DESLIGAMENTOS_AGENDADOS: SITUACAO = EFETIVADO e EFETIVADO_EM = agora em UTC;
--   - se a pessoa e gestora: os pedidos de ajuste de ponto (PENDENTE) e de ferias (SOLICITADA) parados na etapa dela, ainda sem
--     resultado dela, passam para a etapa do RH (ID_GESTOR = NULL). O que espera a resposta da pessoa a uma sugestao de data
--     (SUGERIU_DATA) nao muda;
--   - os pedidos de beneficio da pessoa que ainda esperavam o RH (adesao, dependentes e trajetos PENDENTE) viram CANCELADO.
--
-- A API faz e esta procedure nao: o evento de auditoria da inativacao e os eventos resumo dos pedidos que passaram ao RH.
--
-- Mudou em relacao ao arquivo antigo: a antiga efetivava NO ultimo dia de trabalho (a pessoa perdia o ponto do proprio ultimo dia) e
--   sem esperar o banco de horas; nao passava ao RH os pedidos parados no gestor desligado nem cancelava os pedidos de beneficio
--   pendentes; recusava o ja efetivado em vez de terminar sem erro; P_ID_DESLIGAMENTO_AGENDADO e INT (a antiga declarava BIGINT).
-- ------------------------------------------------------------
CREATE PROCEDURE SP_EFETIVAR_DESLIGAMENTO(
    IN P_ID_DESLIGAMENTO_AGENDADO INT
)
PROC: BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_PESSOA INT DEFAULT NULL;
    DECLARE V_ULTIMO_DIA DATE DEFAULT NULL;
    DECLARE V_SITUACAO VARCHAR(10) DEFAULT NULL;
    DECLARE V_ULTIMO_CONSOLIDADO DATE DEFAULT NULL;
    DECLARE V_ID_GESTOR INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_DESLIGAMENTO_AGENDADO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o desligamento.';
    END IF;

    -- Leitura sem trava e FORA da transacao, so para saber a pessoa (a ordem de travas e a pessoa primeiro); o estado e relido depois.
    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM DESLIGAMENTOS_AGENDADOS
     WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Desligamento nao encontrado.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_PESSOA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT ULTIMO_DIA_TRABALHO, SITUACAO
      INTO V_ULTIMO_DIA, V_SITUACAO
      FROM DESLIGAMENTOS_AGENDADOS
     WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO
     FOR UPDATE;

    IF V_SITUACAO = 'EFETIVADO' THEN
        COMMIT; -- ja efetivado: nada a fazer
        LEAVE PROC;
    END IF;

    IF V_SITUACAO IS NULL OR V_SITUACAO <> 'AGENDADO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Desligamento cancelado ou revertido: nao ha o que efetivar.';
    END IF;

    IF CURDATE() <= V_ULTIMO_DIA THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O desligamento so pode ser efetivado a partir do dia seguinte ao ultimo dia de trabalho.';
    END IF;

    SELECT MAX(DIA_REFERENCIA) INTO V_ULTIMO_CONSOLIDADO
      FROM TAREFAS_AGENDADAS
     WHERE TIPO = 'CONSOLIDAR_BANCO_HORAS'
       AND SITUACAO = 'CONCLUIDA';

    IF V_ULTIMO_CONSOLIDADO IS NULL OR V_ULTIMO_CONSOLIDADO < V_ULTIMO_DIA THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O banco de horas do ultimo dia de trabalho ainda nao foi consolidado: aguarde a consolidacao.';
    END IF;

    -- A efetivacao: o MESMO bloco de SP_AGENDAR_DESLIGAMENTO (D7). Quem mudar um, muda o outro.
    UPDATE FUNCIONARIOS
       SET DATA_DESLIGAMENTO = V_ULTIMO_DIA,
           ATIVO = FALSE
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO;

    UPDATE DESLIGAMENTOS_AGENDADOS
       SET SITUACAO = 'EFETIVADO',
           EFETIVADO_EM = UTC_TIMESTAMP(6)
     WHERE ID_DESLIGAMENTO_AGENDADO = P_ID_DESLIGAMENTO_AGENDADO;

    SELECT ID_GESTOR INTO V_ID_GESTOR
      FROM GESTORES
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO;

    IF V_ID_GESTOR IS NOT NULL THEN
        UPDATE PONTOS_MANUTENCOES
           SET ID_GESTOR = NULL
         WHERE STATUS_SOLICITACAO = 'PENDENTE' AND ID_GESTOR = V_ID_GESTOR AND RESULTADO_GESTOR IS NULL;

        UPDATE FERIAS
           SET ID_GESTOR = NULL
         WHERE STATUS_FERIAS = 'SOLICITADA' AND ID_GESTOR = V_ID_GESTOR AND RESULTADO_GESTOR IS NULL;
    END IF;

    UPDATE DEPENDENTES D
      JOIN BENEFICIOS_FUNCIONARIOS B ON B.ID_BENEFICIO_FUNCIONARIOS = D.ID_BENEFICIO_FUNCIONARIOS
       SET D.STATUS_PEDIDO = 'CANCELADO'
     WHERE B.ID_FUNCIONARIO = V_ID_FUNCIONARIO AND D.STATUS_PEDIDO = 'PENDENTE';

    UPDATE TRAJETOS_VALE_TRANSPORTE T
      JOIN BENEFICIOS_FUNCIONARIOS B ON B.ID_BENEFICIO_FUNCIONARIOS = T.ID_BENEFICIO_FUNCIONARIOS
       SET T.STATUS_PEDIDO = 'CANCELADO'
     WHERE B.ID_FUNCIONARIO = V_ID_FUNCIONARIO AND T.STATUS_PEDIDO = 'PENDENTE';

    UPDATE BENEFICIOS_FUNCIONARIOS
       SET STATUS_PEDIDO = 'CANCELADO'
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO AND STATUS_PEDIDO = 'PENDENTE';

    COMMIT;
END$$






-- [[DOMINIO-E: CADASTROS]]
-- (preenchido pela secao E)






DELIMITER ;
