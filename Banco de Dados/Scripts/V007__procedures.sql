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

-- ------------------------------------------------------------
-- SP_PUBLICAR_HOLERITE: o RH publica (ou agenda) o PRIMEIRO holerite de uma pessoa para uma competencia e um tipo (versao 1).
-- Espelha: enviarHolerite, POST /api/rh/funcionarios/{funcionarioId}/holerites (EnviarHoleriteCasoDeUso).
--
-- P_PUBLICAR_EM: NULL ou a data de hoje publica agora (DATA_PUBLICACAO = NOW()); uma data futura AGENDA: DATA_PUBLICACAO = 00:00 desse dia
-- e a pessoa so ve o holerite a partir dai (a API so mostra holerite com DATA_PUBLICACAO <= agora).
-- P_VERSAO devolve sempre 1 (foi mantido para quem ja chamava a procedure antiga).
--
-- Recusas, na ordem:
--   1. entradas: funcionario, mes (1 a 12), ano (2000 a 2100), tipo (MENSAL, DECIMO_TERCEIRO, FERIAS, em maiusculas), caminho do arquivo,
--      data de publicacao anterior a hoje;
--   2. funcionario nao encontrado (pessoa INATIVA pode receber holerite: a rescisao sai depois do desligamento);
--   3. competencia anterior a admissao, posterior ao desligamento, ou futura (so FERIAS pode ser futura): as mesmas regras e os mesmos
--      textos do trigger TRG_HOLERITES_BEFORE_INSERT, conferidas aqui para sairem ANTES da recusa 4, como na API (o trigger segue de rede);
--   4. ja existe holerite (qualquer versao) dessa pessoa, competencia e tipo: para corrigir, SP_SUBSTITUIR_HOLERITE.
-- A linha da pessoa fica travada (FOR UPDATE): dois envios iguais ao mesmo tempo viram um holerite e uma recusa 4.
--
-- O que a API faz e a procedure NAO: guardar o PDF no armazenamento e conferir que e PDF de ate 5 MB (aqui so entra o caminho, que precisa
-- apontar para um arquivo que ja existe la); calcular a data "no dia do pagamento" (dia da empresa, feriados e excecoes do mes: quem chama
-- passa a data pronta em P_PUBLICAR_EM); criar a tarefa PUBLICAR_HOLERITE que manda o e-mail (a API recupera sozinha, ao iniciar, os
-- holerites agendados e os publicados nos ultimos 7 dias); o evento de auditoria; o envio em lote (e a mesma operacao, uma chamada por pessoa).
--
-- Mudou em relacao ao arquivo antigo: a antiga criava em silencio uma VERSAO NOVA quando a competencia ja existia (a API recusa com
-- HOLERITE_JA_EXISTE e tem outra operacao para corrigir: SP_SUBSTITUIR_HOLERITE); nao agendava (sempre NOW()); nao conferia o ano;
-- aceitava o tipo em minusculas; P_ANO e P_MES viraram INT (um valor fora da faixa agora tem recusa de negocio); entrou P_PUBLICAR_EM.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_PUBLICAR_HOLERITE(
    IN P_ID_FUNCIONARIO INT,
    IN P_ANO INT,
    IN P_MES INT,
    IN P_TIPO_HOLERITE VARCHAR(20),
    IN P_CAMINHO_ARQUIVO VARCHAR(255),
    IN P_PUBLICAR_EM DATE,
    OUT P_ID_HOLERITE INT,
    OUT P_VERSAO SMALLINT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_DESLIGAMENTO DATE DEFAULT NULL;
    DECLARE V_COMPETENCIA DATE DEFAULT NULL;
    DECLARE V_EXISTE INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_HOLERITE = NULL;
    SET P_VERSAO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario do holerite.';
    END IF;

    IF P_MES IS NULL OR P_MES NOT BETWEEN 1 AND 12 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o mes do holerite, de 1 a 12.';
    END IF;

    IF P_ANO IS NULL OR P_ANO NOT BETWEEN 2000 AND 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o ano do holerite, de 2000 a 2100.';
    END IF;

    IF P_TIPO_HOLERITE IS NULL OR CAST(P_TIPO_HOLERITE AS BINARY) NOT IN ('MENSAL', 'DECIMO_TERCEIRO', 'FERIAS') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o tipo do holerite: MENSAL, DECIMO_TERCEIRO ou FERIAS.';
    END IF;

    IF P_CAMINHO_ARQUIVO IS NULL OR TRIM(P_CAMINHO_ARQUIVO) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o caminho do arquivo do holerite.';
    END IF;

    IF P_PUBLICAR_EM IS NOT NULL AND P_PUBLICAR_EM < CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de publicacao do holerite nao pode ser anterior a hoje.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, DATA_ADMISSAO, DATA_DESLIGAMENTO
      INTO V_ID_FUNCIONARIO, V_ADMISSAO, V_DESLIGAMENTO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SET V_COMPETENCIA = STR_TO_DATE(CONCAT(P_ANO, '-', LPAD(P_MES, 2, '0'), '-01'), '%Y-%m-%d');

    IF LAST_DAY(V_COMPETENCIA) < V_ADMISSAO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A competencia do holerite e anterior a admissao.';
    END IF;

    IF V_DESLIGAMENTO IS NOT NULL AND V_COMPETENCIA > V_DESLIGAMENTO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A competencia do holerite e posterior ao desligamento.';
    END IF;

    IF V_COMPETENCIA > CURDATE() AND P_TIPO_HOLERITE <> 'FERIAS' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel cadastrar holerite de competencia futura.';
    END IF;

    -- Leitura com trava: ve o que outra sessao acabou de gravar (a leitura comum olharia a foto do comeco da transacao).
    SELECT ID_HOLERITE INTO V_EXISTE
      FROM HOLERITES
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND ANO = P_ANO
       AND MES = P_MES
       AND TIPO_HOLERITE = P_TIPO_HOLERITE
     ORDER BY VERSAO DESC
     LIMIT 1
     FOR UPDATE;

    IF V_EXISTE IS NOT NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Ja existe holerite desta competencia e tipo para o funcionario. Para corrigir, use SP_SUBSTITUIR_HOLERITE.';
    END IF;

    INSERT INTO HOLERITES
        (ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, DATA_PUBLICACAO, CAMINHO_ARQUIVO)
    VALUES
        (P_ID_FUNCIONARIO, P_ANO, P_MES, P_TIPO_HOLERITE, 1,
         CASE WHEN P_PUBLICAR_EM IS NULL OR P_PUBLICAR_EM <= CURDATE() THEN NOW() ELSE CAST(P_PUBLICAR_EM AS DATETIME) END,
         TRIM(P_CAMINHO_ARQUIVO));

    SET P_ID_HOLERITE = LAST_INSERT_ID();
    SET P_VERSAO = 1;
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_SUBSTITUIR_HOLERITE: o RH corrige um holerite enviado errado guardando a VERSAO SEGUINTE da mesma pessoa, competencia e tipo
-- (holerite nunca e apagado nem alterado: TRG_HOLERITES_BEFORE_DELETE). A pessoa passa a ver a maior versao ja publicada.
-- Espelha: substituirHolerite, POST /api/rh/holerites/{holeriteId}/versoes (SubstituirHoleriteCasoDeUso).
--
-- P_ID_HOLERITE tem de ser a versao ATUAL (a maior) da competencia. P_PUBLICAR_EM como em SP_PUBLICAR_HOLERITE: se a versao nova e
-- agendada, a antiga segue valendo ate o dia. Devolve o id da linha nova e a versao dela.
--
-- Recusas, na ordem:
--   1. entradas: holerite, caminho do arquivo, data de publicacao anterior a hoje;
--   2. holerite nao encontrado;
--   3. o holerite informado ja foi substituido por uma versao mais nova (HOLERITE_VERSAO_DESATUALIZADA);
--   4. a competencia contra a pessoa de HOJE (anterior a admissao, posterior ao desligamento, futura): recusa do trigger
--      TRG_HOLERITES_BEFORE_INSERT, com o texto dele.
-- A linha da pessoa fica travada (FOR UPDATE, a mesma trava de SP_PUBLICAR_HOLERITE): duas substituicoes do mesmo holerite ao mesmo
-- tempo viram uma versao nova e uma recusa 3.
--
-- O que a API faz e a procedure NAO: o mesmo de SP_PUBLICAR_HOLERITE (arquivo, dia do pagamento, tarefa do e-mail, auditoria).
--
-- Nao existia no arquivo antigo: la a SP_PUBLICAR_HOLERITE fazia as duas coisas, sem conferir qual versao estava sendo corrigida.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_SUBSTITUIR_HOLERITE(
    IN P_ID_HOLERITE INT,
    IN P_CAMINHO_ARQUIVO VARCHAR(255),
    IN P_PUBLICAR_EM DATE,
    OUT P_ID_HOLERITE_NOVO INT,
    OUT P_VERSAO SMALLINT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ANO INT DEFAULT NULL;
    DECLARE V_MES INT DEFAULT NULL;
    DECLARE V_TIPO VARCHAR(20) DEFAULT NULL;
    DECLARE V_VERSAO INT DEFAULT NULL;
    DECLARE V_MAIOR_VERSAO INT DEFAULT NULL;
    DECLARE V_TRAVA INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_HOLERITE_NOVO = NULL;
    SET P_VERSAO = NULL;

    IF P_ID_HOLERITE IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o holerite a substituir.';
    END IF;

    IF P_CAMINHO_ARQUIVO IS NULL OR TRIM(P_CAMINHO_ARQUIVO) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o caminho do arquivo do holerite.';
    END IF;

    IF P_PUBLICAR_EM IS NOT NULL AND P_PUBLICAR_EM < CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de publicacao do holerite nao pode ser anterior a hoje.';
    END IF;

    START TRANSACTION;

    -- A linha do holerite nao muda nunca; o que serializa e a trava da pessoa, tomada antes de olhar a maior versao.
    SELECT ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO
      INTO V_ID_FUNCIONARIO, V_ANO, V_MES, V_TIPO, V_VERSAO
      FROM HOLERITES
     WHERE ID_HOLERITE = P_ID_HOLERITE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Holerite nao encontrado.';
    END IF;

    SELECT ID_FUNCIONARIO INTO V_TRAVA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    -- Leitura com trava: ve a versao que outra sessao acabou de gravar (a leitura comum olharia a foto do comeco da transacao).
    SELECT VERSAO INTO V_MAIOR_VERSAO
      FROM HOLERITES
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
       AND ANO = V_ANO
       AND MES = V_MES
       AND TIPO_HOLERITE = V_TIPO
     ORDER BY VERSAO DESC
     LIMIT 1
     FOR UPDATE;

    IF V_VERSAO <> V_MAIOR_VERSAO THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Este holerite ja foi substituido por uma versao mais nova. Substitua a versao atual.';
    END IF;

    INSERT INTO HOLERITES
        (ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, DATA_PUBLICACAO, CAMINHO_ARQUIVO)
    VALUES
        (V_ID_FUNCIONARIO, V_ANO, V_MES, V_TIPO, V_VERSAO + 1,
         CASE WHEN P_PUBLICAR_EM IS NULL OR P_PUBLICAR_EM <= CURDATE() THEN NOW() ELSE CAST(P_PUBLICAR_EM AS DATETIME) END,
         TRIM(P_CAMINHO_ARQUIVO));

    SET P_ID_HOLERITE_NOVO = LAST_INSERT_ID();
    SET P_VERSAO = V_VERSAO + 1;
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_ENVIAR_ATESTADO: a pessoa envia o atestado dela, de DIAS INTEIROS (sem horas) ou de ALGUMAS HORAS de um dia so. Nasce ENVIADO.
-- Espelha: enviarAtestado, POST /api/eu/ponto/atestados (EnviarAtestadoCasoDeUso).
--
-- Recusas, na ordem:
--   1. entradas: funcionario; data de inicio e de fim; fim antes do inicio; as duas horas juntas ou nenhuma; horas so em minutos cheios,
--      de 00:00 a 23:59 (a API le HH:mm); atestado de horas em um dia so; hora de fim depois da de inicio; caminho do arquivo;
--   2. funcionario nao encontrado;
--   3. funcionario inativo (na API quem esta inativo nem entra no sistema; aqui a recusa e explicita).
-- A API nao limita o periodo: atestado de dia futuro, de dia antigo ou de antes da admissao e aceito (quem julga e o RH, ao decidir).
--
-- O que a API faz e a procedure NAO: guardar o arquivo (PDF ou imagem, ate 5 MB, conferido pelo conteudo) e limpar o nome dele (aqui
-- entram o caminho ja gravado no armazenamento e o nome como vier; nome vazio vira NULL); o evento de auditoria. Nenhum e-mail sai.
--
-- Mudou em relacao ao arquivo antigo: uma mensagem por regra (a antiga respondia tudo com "Periodo, horario ou arquivo do atestado
-- invalido."); funcionario inexistente e inativo separados; horas com segundos ou fora do dia recusadas; nome do arquivo vazio vira NULL.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_ENVIAR_ATESTADO(
    IN P_ID_FUNCIONARIO INT,
    IN P_DATA_INICIO DATE,
    IN P_DATA_FIM DATE,
    IN P_HORA_INICIO TIME,
    IN P_HORA_FIM TIME,
    IN P_CAMINHO_ARQUIVO VARCHAR(255),
    IN P_NOME_ARQUIVO VARCHAR(255),
    OUT P_ID_ATESTADO INT
)
BEGIN
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_ATESTADO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario do atestado.';
    END IF;

    IF P_DATA_INICIO IS NULL OR P_DATA_FIM IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio e a data de fim do atestado.';
    END IF;

    IF P_DATA_FIM < P_DATA_INICIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de fim do atestado nao pode ser antes da data de inicio.';
    END IF;

    IF (P_HORA_INICIO IS NULL) <> (P_HORA_FIM IS NULL) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a hora de inicio e a hora de fim juntas.';
    END IF;

    IF P_HORA_INICIO IS NOT NULL THEN
        IF P_HORA_INICIO NOT BETWEEN '00:00:00' AND '23:59:00' OR P_HORA_FIM NOT BETWEEN '00:00:00' AND '23:59:00'
           OR SECOND(P_HORA_INICIO) <> 0 OR SECOND(P_HORA_FIM) <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe as horas do atestado em horas e minutos, de 00:00 a 23:59.';
        END IF;

        IF P_DATA_INICIO <> P_DATA_FIM THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'O atestado de algumas horas vale para um dia so: use a mesma data de inicio e de fim.';
        END IF;

        IF P_HORA_FIM <= P_HORA_INICIO THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A hora de fim do atestado precisa ser depois da hora de inicio.';
        END IF;
    END IF;

    IF P_CAMINHO_ARQUIVO IS NULL OR TRIM(P_CAMINHO_ARQUIVO) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o caminho do arquivo do atestado.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR SHARE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario inativo nao pode enviar atestado.';
    END IF;

    INSERT INTO ATESTADOS
        (ID_FUNCIONARIO, DATA_INICIO, DATA_FIM, HORA_INICIO, HORA_FIM,
         CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL, STATUS_ATESTADO, DATA_ENVIO)
    VALUES
        (P_ID_FUNCIONARIO, P_DATA_INICIO, P_DATA_FIM, P_HORA_INICIO, P_HORA_FIM,
         TRIM(P_CAMINHO_ARQUIVO), NULLIF(TRIM(P_NOME_ARQUIVO), ''), 'ENVIADO', NOW());

    SET P_ID_ATESTADO = LAST_INSERT_ID();
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_DECIDIR_ATESTADO: a decisao FINAL do RH sobre um atestado ENVIADO. P_DECISAO: VALIDADO (o dia ou as horas deixam de contar como
-- falta) ou RECUSADO (com o motivo; o dia segue como falta).
-- Espelha: validarAtestado e recusarAtestado, POST /api/rh/ponto/atestados/{atestadoId}/validar e /recusar (DecidirAtestadoCasoDeUso).
--
-- Grava STATUS_ATESTADO, ID_FUNCIONARIO_RH, DATA_DECISAO = NOW() e MOTIVO_RECUSA (o motivo sem espacos nas pontas; NULL ao validar, mesmo
-- que venha um). O RH pode decidir o proprio atestado, como na API.
--
-- Recusas, na ordem:
--   1. entradas: atestado; responsavel do RH; decisao (VALIDADO ou RECUSADO, em maiusculas); na recusa, o motivo de 1 a 500 caracteres;
--   2. atestado nao encontrado;
--   3. atestado ja decidido (ATESTADO_JA_DECIDIDO): a decisao e final;
--   4. responsavel do RH inexistente ou inativo.
-- Travas: a pessoa do atestado (FOR UPDATE, a mesma trava do ponto, como a API faz ao validar) e depois o atestado: de duas decisoes ao
-- mesmo tempo uma vale e a outra cai na recusa 3.
--
-- O que a API faz e a procedure NAO: RECALCULAR O BANCO DE HORAS. Ao validar, a API recalcula cada dia do atestado que ja passou (da
-- admissao ate ontem): o lancamento de falta desses dias em BANCOS_HORAS e refeito pelo calculo do espelho, que e codigo da API. Depois de
-- validar por aqui um atestado com dia passado, o saldo desses dias fica como estava ate a API recalcular o dia. Tambem ficam de fora:
-- conferir que o responsavel tem perfil de RH (so se confere que existe e esta ativo) e o evento de auditoria. Nenhum e-mail sai.
--
-- Mudou em relacao ao arquivo antigo: "inexistente" e "ja analisado" viraram duas recusas; o motivo e aparado e limitado a 500; a decisao
-- em minusculas e recusada; a pessoa do atestado e travada antes do atestado (mesma ordem de travas da API).
-- ------------------------------------------------------------
CREATE PROCEDURE SP_DECIDIR_ATESTADO(
    IN P_ID_ATESTADO INT,
    IN P_ID_FUNCIONARIO_RH INT,
    IN P_DECISAO VARCHAR(10),
    IN P_MOTIVO_RECUSA TEXT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_TRAVA INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(10) DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_ATESTADO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o atestado.';
    END IF;

    IF P_ID_FUNCIONARIO_RH IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o responsavel do RH pela decisao.';
    END IF;

    IF P_DECISAO IS NULL OR CAST(P_DECISAO AS BINARY) NOT IN ('VALIDADO', 'RECUSADO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Decisao de atestado invalida: informe VALIDADO ou RECUSADO.';
    END IF;

    IF P_DECISAO = 'RECUSADO'
       AND (P_MOTIVO_RECUSA IS NULL OR CHAR_LENGTH(TRIM(P_MOTIVO_RECUSA)) NOT BETWEEN 1 AND 500) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o motivo da recusa, de 1 a 500 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM ATESTADOS
     WHERE ID_ATESTADO = P_ID_ATESTADO;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Atestado nao encontrado.';
    END IF;

    SELECT ID_FUNCIONARIO INTO V_TRAVA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_ATESTADO INTO V_STATUS
      FROM ATESTADOS
     WHERE ID_ATESTADO = P_ID_ATESTADO
     FOR UPDATE;

    IF V_STATUS <> 'ENVIADO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este atestado ja foi decidido.';
    END IF;

    SELECT ATIVO INTO V_RH_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

    IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
    END IF;

    UPDATE ATESTADOS
       SET STATUS_ATESTADO = CASE WHEN P_DECISAO = 'RECUSADO' THEN 'RECUSADO' ELSE 'VALIDADO' END,
           ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
           DATA_DECISAO = NOW(),
           MOTIVO_RECUSA = CASE WHEN P_DECISAO = 'RECUSADO' THEN TRIM(P_MOTIVO_RECUSA) ELSE NULL END
     WHERE ID_ATESTADO = P_ID_ATESTADO;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_SOLICITAR_BENEFICIO: a pessoa pede a adesao a um beneficio OPCIONAL, com a data de inicio, a autorizacao de desconto em folha e,
-- se o beneficio aceitar, os dependentes e o trajeto do vale-transporte. O pedido nasce PENDENTE e INATIVO; os dependentes e o trajeto
-- nascem PENDENTE junto, com a mesma data.
-- Espelha: pedirAdesaoABeneficio, POST /api/eu/beneficios-adesoes (PedirAdesaoABeneficioCasoDeUso).
--
-- P_DEPENDENTES: NULL (nenhum) ou o texto de uma lista JSON com ate 10 itens, com os nomes de campo do contrato:
--   '[{"nome": "Ana Lima", "parentesco": "Filha", "dataNascimento": "2015-03-09"}]'
-- P_TRAJETO_ORIGEM, P_TRAJETO_DESTINO, P_TRAJETO_LINHAS_MEIOS: os tres NULL (sem trajeto) ou os tres preenchidos.
--
-- QUEM PEDE DE NOVO: existe UMA linha por pessoa e beneficio (chave unica). Se a pessoa ja tem a linha RECUSADA, CANCELADA ou aprovada e
-- DESATIVADA pelo RH, a MESMA linha e reaberta: volta a PENDENTE com a data, o inicio e a autorizacao novos; quem respondeu, quando e o
-- motivo da recusa sao limpos; os dependentes e trajetos dela que estavam PENDENTE ou APROVADO viram CANCELADO (os RECUSADO e CANCELADO
-- ficam de historico); e entram os do pedido novo. P_ID_BENEFICIO_FUNCIONARIOS devolve o id da linha (a nova ou a reaberta).
--
-- Recusas, na ordem:
--   1. entradas: funcionario; beneficio; data de inicio (obrigatoria, de hoje ate 2100); autorizacao de desconto (TRUE ou FALSE, obrigatoria);
--      dependentes (JSON valido, lista, ate 10; nome de 1 a 150; parentesco de 1 a 30; nascimento AAAA-MM-DD, de 1900 ate hoje); trajeto
--      (os tres campos; origem e destino ate 200, linhas e meios ate 255);
--   2. funcionario nao encontrado;
--   3. beneficio nao encontrado;
--   4. a pessoa ja tem o beneficio ATIVO ou um pedido PENDENTE dele (BENEFICIO_JA_ATIVO_OU_PENDENTE);
--   5. funcionario inativo;
--   6. beneficio nao opcional (o RH concede direto na ficha);
--   7. vieram dependentes e o beneficio nao aceita dependentes;
--   8. veio trajeto e o beneficio nao aceita trajeto.
-- A linha da pessoa fica travada (FOR UPDATE): dois pedidos iguais ao mesmo tempo viram um pedido e uma recusa 4.
--
-- O que a API faz e a procedure NAO: o evento de auditoria (que guarda a autorizacao anterior na reabertura). Nenhum e-mail sai.
--
-- Mudou em relacao ao arquivo antigo: a antiga recusava QUALQUER linha existente (quem foi recusado nunca mais podia pedir); nao conferia
-- se o beneficio e opcional; nao gravava dependentes nem trajeto; aceitava data de inicio nula ou passada e autorizacao nula (virava FALSE).
-- Entraram P_DEPENDENTES e os tres P_TRAJETO_*, antes do OUT.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_SOLICITAR_BENEFICIO(
    IN P_ID_FUNCIONARIO INT,
    IN P_ID_BENEFICIO INT,
    IN P_DATA_INICIO_DESEJADA DATE,
    IN P_AUTORIZA_DESCONTO_FOLHA BOOLEAN,
    IN P_DEPENDENTES TEXT,
    IN P_TRAJETO_ORIGEM TEXT,
    IN P_TRAJETO_DESTINO TEXT,
    IN P_TRAJETO_LINHAS_MEIOS TEXT,
    OUT P_ID_BENEFICIO_FUNCIONARIOS INT
)
BEGIN
    DECLARE V_DEPENDENTES JSON DEFAULT NULL;
    DECLARE V_QTD_DEPENDENTES INT DEFAULT 0;
    DECLARE V_ERRO_DEPENDENTE INT DEFAULT NULL;
    DECLARE V_TEM_TRAJETO BOOLEAN DEFAULT FALSE;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_BENEFICIO INT DEFAULT NULL;
    DECLARE V_OPCIONAL BOOLEAN DEFAULT NULL;
    DECLARE V_ACEITA_DEPENDENTES BOOLEAN DEFAULT NULL;
    DECLARE V_ACEITA_TRAJETO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_ADESAO INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(20) DEFAULT NULL;
    DECLARE V_BENEFICIO_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_BENEFICIO_FUNCIONARIOS = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario que pede o beneficio.';
    END IF;

    IF P_ID_BENEFICIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o beneficio.';
    END IF;

    IF P_DATA_INICIO_DESEJADA IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio desejada.';
    END IF;

    IF P_DATA_INICIO_DESEJADA < CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de inicio nao pode ser anterior a hoje.';
    END IF;

    IF YEAR(P_DATA_INICIO_DESEJADA) > 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe uma data de inicio ate 2100.';
    END IF;

    IF P_AUTORIZA_DESCONTO_FOLHA IS NULL OR P_AUTORIZA_DESCONTO_FOLHA NOT IN (0, 1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe se autoriza o desconto em folha: TRUE ou FALSE.';
    END IF;

    IF P_DEPENDENTES IS NOT NULL AND TRIM(P_DEPENDENTES) <> '' THEN
        IF NOT JSON_VALID(P_DEPENDENTES) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Os dependentes precisam vir como uma lista JSON.';
        END IF;

        SET V_DEPENDENTES = CAST(P_DEPENDENTES AS JSON);

        IF JSON_TYPE(V_DEPENDENTES) = 'NULL' THEN
            SET V_DEPENDENTES = NULL;
        ELSEIF JSON_TYPE(V_DEPENDENTES) <> 'ARRAY' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Os dependentes precisam vir como uma lista JSON.';
        ELSE
            SET V_QTD_DEPENDENTES = JSON_LENGTH(V_DEPENDENTES);
        END IF;
    END IF;

    IF V_QTD_DEPENDENTES > 10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe no maximo 10 dependentes.';
    END IF;

    IF V_QTD_DEPENDENTES > 0 THEN
        -- O menor codigo de erro entre os itens (NULL: todos valem). A data e conferida como texto antes de virar DATE.
        SELECT MIN(CASE
                   WHEN D.NOME IS NULL OR CHAR_LENGTH(TRIM(D.NOME)) = 0 THEN 1
                   WHEN CHAR_LENGTH(TRIM(D.NOME)) > 150 THEN 2
                   WHEN D.PARENTESCO IS NULL OR CHAR_LENGTH(TRIM(D.PARENTESCO)) = 0 THEN 3
                   WHEN CHAR_LENGTH(TRIM(D.PARENTESCO)) > 30 THEN 4
                   WHEN D.NASCIMENTO IS NULL OR NOT REGEXP_LIKE(D.NASCIMENTO, '^[0-9]{4}-[0-9]{2}-[0-9]{2}$') THEN 5
                   WHEN SUBSTRING(D.NASCIMENTO, 1, 4) < '1900' THEN 7
                   WHEN SUBSTRING(D.NASCIMENTO, 6, 2) NOT BETWEEN '01' AND '12' OR SUBSTRING(D.NASCIMENTO, 9, 2) < '01' THEN 5
                   WHEN CAST(SUBSTRING(D.NASCIMENTO, 9, 2) AS UNSIGNED)
                        > DAY(LAST_DAY(CONCAT(SUBSTRING(D.NASCIMENTO, 1, 7), '-01'))) THEN 5
                   WHEN D.NASCIMENTO > DATE_FORMAT(CURDATE(), '%Y-%m-%d') THEN 6
               END)
          INTO V_ERRO_DEPENDENTE
          FROM JSON_TABLE(V_DEPENDENTES, '$[*]' COLUMNS (
                   NOME TEXT PATH '$.nome' NULL ON EMPTY NULL ON ERROR,
                   PARENTESCO TEXT PATH '$.parentesco' NULL ON EMPTY NULL ON ERROR,
                   NASCIMENTO VARCHAR(40) PATH '$.dataNascimento' NULL ON EMPTY NULL ON ERROR)) AS D;

        IF V_ERRO_DEPENDENTE = 1 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o nome de cada dependente.';
        ELSEIF V_ERRO_DEPENDENTE = 2 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nome do dependente pode ter ate 150 caracteres.';
        ELSEIF V_ERRO_DEPENDENTE = 3 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o parentesco de cada dependente.';
        ELSEIF V_ERRO_DEPENDENTE = 4 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O parentesco do dependente pode ter ate 30 caracteres.';
        ELSEIF V_ERRO_DEPENDENTE = 5 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de nascimento de cada dependente no formato AAAA-MM-DD.';
        ELSEIF V_ERRO_DEPENDENTE = 6 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de nascimento do dependente nao pode ser futura.';
        ELSEIF V_ERRO_DEPENDENTE = 7 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe uma data de nascimento do dependente a partir de 1900.';
        END IF;
    END IF;

    SET V_TEM_TRAJETO = COALESCE(TRIM(P_TRAJETO_ORIGEM), '') <> ''
                        OR COALESCE(TRIM(P_TRAJETO_DESTINO), '') <> ''
                        OR COALESCE(TRIM(P_TRAJETO_LINHAS_MEIOS), '') <> '';

    IF V_TEM_TRAJETO THEN
        IF COALESCE(TRIM(P_TRAJETO_ORIGEM), '') = '' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a origem do trajeto.';
        ELSEIF CHAR_LENGTH(TRIM(P_TRAJETO_ORIGEM)) > 200 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A origem do trajeto pode ter ate 200 caracteres.';
        ELSEIF COALESCE(TRIM(P_TRAJETO_DESTINO), '') = '' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o destino do trajeto.';
        ELSEIF CHAR_LENGTH(TRIM(P_TRAJETO_DESTINO)) > 200 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O destino do trajeto pode ter ate 200 caracteres.';
        ELSEIF COALESCE(TRIM(P_TRAJETO_LINHAS_MEIOS), '') = '' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe as linhas ou os meios de transporte do trajeto.';
        ELSEIF CHAR_LENGTH(TRIM(P_TRAJETO_LINHAS_MEIOS)) > 255 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'As linhas ou os meios do trajeto podem ter ate 255 caracteres.';
        END IF;
    END IF;

    START TRANSACTION;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SELECT ID_BENEFICIO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO
      INTO V_ID_BENEFICIO, V_OPCIONAL, V_ACEITA_DEPENDENTES, V_ACEITA_TRAJETO
      FROM BENEFICIOS
     WHERE ID_BENEFICIO = P_ID_BENEFICIO
     FOR SHARE;

    IF V_ID_BENEFICIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Beneficio nao encontrado.';
    END IF;

    SELECT ID_BENEFICIO_FUNCIONARIOS, STATUS_PEDIDO, BENEFICIO_ATIVO
      INTO V_ID_ADESAO, V_STATUS, V_BENEFICIO_ATIVO
      FROM BENEFICIOS_FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND ID_BENEFICIO = P_ID_BENEFICIO
     FOR UPDATE;

    IF V_STATUS = 'PENDENTE' OR (V_STATUS = 'APROVADO' AND V_BENEFICIO_ATIVO = TRUE) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'O funcionario ja tem este beneficio ou ja pediu a adesao e o RH ainda vai responder.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel pedir beneficio para funcionario inativo.';
    END IF;

    IF V_OPCIONAL = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este beneficio nao e opcional: o RH o concede direto na ficha.';
    END IF;

    IF V_QTD_DEPENDENTES > 0 AND V_ACEITA_DEPENDENTES = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este beneficio nao permite incluir dependentes.';
    END IF;

    IF V_TEM_TRAJETO AND V_ACEITA_TRAJETO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este beneficio nao tem trajeto: so o vale-transporte tem.';
    END IF;

    IF V_ID_ADESAO IS NULL THEN
        INSERT INTO BENEFICIOS_FUNCIONARIOS
            (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO,
             DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA)
        VALUES
            (P_ID_FUNCIONARIO, P_ID_BENEFICIO, FALSE, 'PENDENTE',
             V_AGORA, P_DATA_INICIO_DESEJADA, P_AUTORIZA_DESCONTO_FOLHA);

        SET V_ID_ADESAO = LAST_INSERT_ID();
    ELSE
        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'PENDENTE',
               BENEFICIO_ATIVO = FALSE,
               DATA_PEDIDO = V_AGORA,
               DATA_INICIO_DESEJADA = P_DATA_INICIO_DESEJADA,
               AUTORIZA_DESCONTO_FOLHA = P_AUTORIZA_DESCONTO_FOLHA,
               ID_FUNCIONARIO_RH = NULL,
               DATA_RESPOSTA = NULL,
               MOTIVO_RECUSA = NULL
         WHERE ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO;

        -- O que nao faz parte do pedido novo e cancelado: senao o dependente ou o trajeto de uma adesao antiga reapareceria aprovado
        -- numa adesao que o RH ainda nem decidiu.
        UPDATE DEPENDENTES
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO
           AND STATUS_PEDIDO IN ('PENDENTE', 'APROVADO');

        UPDATE TRAJETOS_VALE_TRANSPORTE
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO
           AND STATUS_PEDIDO IN ('PENDENTE', 'APROVADO');
    END IF;

    IF V_QTD_DEPENDENTES > 0 THEN
        INSERT INTO DEPENDENTES
            (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, DATA_PEDIDO)
        SELECT V_ID_ADESAO, TRIM(D.NOME), TRIM(D.PARENTESCO), CAST(D.NASCIMENTO AS DATE), 'PENDENTE', V_AGORA
          FROM JSON_TABLE(V_DEPENDENTES, '$[*]' COLUMNS (
                   ORDEM FOR ORDINALITY,
                   NOME TEXT PATH '$.nome' NULL ON EMPTY NULL ON ERROR,
                   PARENTESCO TEXT PATH '$.parentesco' NULL ON EMPTY NULL ON ERROR,
                   NASCIMENTO VARCHAR(40) PATH '$.dataNascimento' NULL ON EMPTY NULL ON ERROR)) AS D
         ORDER BY D.ORDEM;
    END IF;

    IF V_TEM_TRAJETO THEN
        INSERT INTO TRAJETOS_VALE_TRANSPORTE
            (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, DATA_PEDIDO)
        VALUES
            (V_ID_ADESAO, TRIM(P_TRAJETO_ORIGEM), TRIM(P_TRAJETO_DESTINO), TRIM(P_TRAJETO_LINHAS_MEIOS), 'PENDENTE', V_AGORA);
    END IF;

    SET P_ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO;
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_DECIDIR_BENEFICIO: o RH aprova ou recusa um pedido de ADESAO pendente. P_DECISAO: APROVADO ou RECUSADO (com o motivo).
-- Espelha: aprovarAdesaoABeneficio e recusarAdesaoABeneficio, POST /api/rh/beneficios-adesoes/{adesaoId}/aprovar e /recusar
-- (DecidirPedidoDeBeneficioCasoDeUso).
--
-- APROVADO: a adesao fica APROVADO e ATIVA, com quem respondeu e quando, sem motivo; os dependentes e o trajeto PENDENTE dela sao
--   aprovados JUNTO, com o mesmo RH e a mesma data.
-- RECUSADO: a adesao fica RECUSADO (inativa), com quem respondeu, quando e o motivo (sem espacos nas pontas); os dependentes e o trajeto
--   PENDENTE dela viram CANCELADO, sem resposta. A pessoa pode pedir de novo (SP_SOLICITAR_BENEFICIO reabre a mesma linha).
-- O RH pode decidir o proprio pedido, como na API.
--
-- Recusas, na ordem:
--   1. entradas: pedido; responsavel do RH; decisao (APROVADO ou RECUSADO, em maiusculas); na recusa, o motivo de 1 a 500 caracteres;
--   2. pedido nao encontrado;
--   3. pedido ja respondido (BENEFICIO_PEDIDO_JA_DECIDIDO): so o PENDENTE se decide;
--   4. ao aprovar, a pessoa do pedido esta inativa (o trigger so olha o INSERT; a regra e da API);
--   5. responsavel do RH inexistente ou inativo.
-- Travas: a pessoa do pedido (FOR UPDATE) e depois a adesao, na ordem da API: de dois RHs ao mesmo tempo um decide e o outro cai na recusa 3.
--
-- O que a API faz e a procedure NAO: conferir que o responsavel tem perfil de RH (so se confere que existe e esta ativo) e o evento de
-- auditoria. Nenhum e-mail sai. A decisao de um dependente ou de um trajeto pedido DEPOIS, numa adesao ja ativa, e outra operacao da API
-- (aprovar/recusar dependente e trajeto) e nao tem procedure.
--
-- Mudou em relacao ao arquivo antigo: a antiga nao tocava nos dependentes nem no trajeto do pedido (ficavam PENDENTE para sempre, dentro
-- de uma adesao recusada ou ja ativa); aprovava beneficio de pessoa inativa; "inexistente" e "ja respondido" eram uma recusa so; o motivo
-- nao era aparado nem limitado.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_DECIDIR_BENEFICIO(
    IN P_ID_BENEFICIO_FUNCIONARIOS INT,
    IN P_ID_FUNCIONARIO_RH INT,
    IN P_DECISAO VARCHAR(10),
    IN P_MOTIVO_RECUSA TEXT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(20) DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_BENEFICIO_FUNCIONARIOS IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o pedido de beneficio.';
    END IF;

    IF P_ID_FUNCIONARIO_RH IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o responsavel do RH pela decisao.';
    END IF;

    IF P_DECISAO IS NULL OR CAST(P_DECISAO AS BINARY) NOT IN ('APROVADO', 'RECUSADO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Decisao de beneficio invalida: informe APROVADO ou RECUSADO.';
    END IF;

    IF P_DECISAO = 'RECUSADO'
       AND (P_MOTIVO_RECUSA IS NULL OR CHAR_LENGTH(TRIM(P_MOTIVO_RECUSA)) NOT BETWEEN 1 AND 500) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o motivo da recusa, de 1 a 500 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM BENEFICIOS_FUNCIONARIOS
     WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de beneficio nao encontrado.';
    END IF;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_PEDIDO INTO V_STATUS
      FROM BENEFICIOS_FUNCIONARIOS
     WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
     FOR UPDATE;

    IF V_STATUS <> 'PENDENTE' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido de beneficio ja foi respondido.';
    END IF;

    IF P_DECISAO = 'APROVADO' AND V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel ativar beneficio para funcionario inativo.';
    END IF;

    SELECT ATIVO INTO V_RH_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

    IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
    END IF;

    IF P_DECISAO = 'APROVADO' THEN
        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'APROVADO',
               BENEFICIO_ATIVO = TRUE,
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA,
               MOTIVO_RECUSA = NULL
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS;

        UPDATE DEPENDENTES
           SET STATUS_PEDIDO = 'APROVADO',
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';

        UPDATE TRAJETOS_VALE_TRANSPORTE
           SET STATUS_PEDIDO = 'APROVADO',
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';
    ELSE
        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'RECUSADO',
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA,
               MOTIVO_RECUSA = TRIM(P_MOTIVO_RECUSA)
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS;

        UPDATE DEPENDENTES
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';

        UPDATE TRAJETOS_VALE_TRANSPORTE
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';
    END IF;

    COMMIT;
END$$






-- [[DOMINIO-D: ADVERTENCIA, TREINAMENTO, FORMULARIO E DESLIGAMENTO]]
-- (preenchido pela secao D)






-- [[DOMINIO-E: CADASTROS]]
-- Quatro procedures: SP_CRIAR_USUARIO, SP_CADASTRAR_FUNCIONARIO, SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO e SP_CADASTRAR_CARGO.
-- A quinta do arquivo antigo, SP_CADASTRAR_NIVEL_ACESSO, foi REMOVIDA de proposito (ver o bloco no fim desta secao).
--
-- Textos: "aparar" aqui e tirar os espacos em branco das pontas (inclusive tabulacao e quebra de linha, como o Trim da API), com
-- REGEXP_REPLACE(x, '^[[:space:]]+|[[:space:]]+$', ''). As comparacoes de variavel com coluna levam COLLATE utf8mb4_unicode_ci (a
-- collation das tabelas), para a procedure nao depender da collation padrao do banco em que foi criada.

-- ------------------------------------------------------------
-- SP_CRIAR_USUARIO: cria o login (USUARIOS) de um funcionario, SEM senha utilizavel.
--
-- Espelha: a criacao do login dentro de enviarConviteDeAcesso (POST /api/rh/usuarios/{funcionarioId}/convite) e de criarFuncionario com
-- enviarConvite = true; codigo em ServicoDeConvite.EmitirAsync. Na API ninguem recebe senha e ninguem grava o hash de outra pessoa: o
-- login nasce com SENHA_PROVISORIA = TRUE e um valor em SENHA que nao confere com senha nenhuma (HashProvisorio: Base64 de 48 bytes
-- aleatorios, o primeiro 0xFF, 64 caracteres); a pessoa escolhe a senha pelo link do convite.
--
-- Regras, na ordem das recusas (a mesma do contrato):
--   1. P_ID_FUNCIONARIO obrigatorio;
--   2. o funcionario existe (a linha fica travada, FOR UPDATE: e o que poe em fila duas criacoes do login da mesma pessoa);
--   3. esta ativo;
--   4. tem e-mail corporativo (e o login dele);
--   5. se ja tem login com a senha definida: recusa (SENHA_JA_DEFINIDA na API: a pessoa usa "Esqueci minha senha");
--   6. se ja tem login desativado: recusa (USUARIO_DESATIVADO na API: o RH ativa antes);
--   7. se ja tem login provisorio e ativo: nao grava nada e devolve o ID_USUARIO que existe (a API tambem so cria "o login que falta").
--
-- O que a API faz e esta procedure NAO: gerar o link do convite (USUARIOS_TOKENS_SENHA guarda so o hash do token; o token em claro so
-- existe no e-mail), enfileirar o e-mail, registrar o evento de auditoria e conferir o perfil de quem chama. Depois desta procedure o RH
-- envia o convite pela API (Usuarios do sistema, "Reenviar convite"): ela acha o login provisorio e emite o link.
--
-- Mudou em relacao ao arquivo antigo: saiu o parametro P_SENHA_HASH (a antiga aceitava qualquer texto de 40 caracteres como senha
-- definida, deixava SENHA_PROVISORIA = FALSE e a pessoa nunca poderia receber o convite); "inexistente" e "inativo" viraram recusas
-- separadas; login que ja existe deixou de estourar a chave unica (erro 1062) e passou a ter as tres respostas acima.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CRIAR_USUARIO(
    IN P_ID_FUNCIONARIO INT,
    OUT P_ID_USUARIO INT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_EMAIL VARCHAR(150) DEFAULT NULL;
    DECLARE V_ID_USUARIO INT DEFAULT NULL;
    DECLARE V_USUARIO_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_SENHA_PROVISORIA BOOLEAN DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_USUARIO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, ATIVO, EMAIL_CORPORATIVO
      INTO V_ID_FUNCIONARIO, V_ATIVO, V_EMAIL
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel criar usuario para funcionario inativo.';
    END IF;

    IF V_EMAIL IS NULL OR REGEXP_REPLACE(V_EMAIL, '^[[:space:]]+|[[:space:]]+$', '') = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario precisa ter e-mail corporativo para criar o usuario.';
    END IF;

    SELECT ID_USUARIO, ATIVO, SENHA_PROVISORIA
      INTO V_ID_USUARIO, V_USUARIO_ATIVO, V_SENHA_PROVISORIA
      FROM USUARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ID_USUARIO IS NOT NULL THEN
        IF V_SENHA_PROVISORIA = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario ja tem usuario com a senha definida.';
        END IF;

        IF V_USUARIO_ATIVO = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O usuario do funcionario esta desativado. Ative o usuario em vez de criar outro.';
        END IF;
    ELSE
        -- O mesmo formato do HashProvisorio da API: 48 bytes aleatorios com o primeiro 0xFF, em Base64 (64 caracteres, sem quebra de linha).
        INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA, ATIVO, SENHA_PROVISORIA)
        VALUES (P_ID_FUNCIONARIO, TO_BASE64(CONCAT(0xFF, RANDOM_BYTES(47))), TRUE, TRUE);

        SET V_ID_USUARIO = LAST_INSERT_ID();
    END IF;

    COMMIT;

    SET P_ID_USUARIO = V_ID_USUARIO;
END$$

-- ------------------------------------------------------------
-- SP_CADASTRAR_FUNCIONARIO: cadastra um colaborador (FUNCIONARIOS) e os dados pessoais dele (FUNCIONARIOS_DETALHES) numa transacao.
--
-- Espelha: criarFuncionario (POST /api/rh/funcionarios); codigo em CriarFuncionarioCasoDeUso, CadastroDeFuncionarios.CadastrarAsync,
-- ValidacaoDeCamposDoCadastro, GeradorDeMatriculas e GestorDoFuncionario. Os dados pessoais opcionais (RG, e-mail pessoal e endereco) nao
-- existem no cadastro da API (la entram depois, por editarFuncionario): aqui sao opcionais (NULL = como a API cadastra) e, quando vem,
-- seguem as regras de editarFuncionario (ValidacaoDeCamposDaEdicao).
--
-- Regras, na ordem das recusas (entradas primeiro, depois o estado do banco; a API devolve os erros de campo todos juntos, aqui sai o primeiro):
--   1. nome obrigatorio, ate 150 caracteres (aparado);
--   2. CPF: so os digitos contam (a pontuacao e descartada, como na API); 11 digitos, nao todos iguais, digitos verificadores certos;
--   3. data de nascimento obrigatoria e nao futura;
--   4. telefone obrigatorio: 10 ou 11 digitos, com DDD (so os digitos sao guardados);
--   5. e-mail corporativo obrigatorio, no formato algo@dominio.ext, ate 150 caracteres (guardado aparado e em minusculas);
--   6. data de admissao obrigatoria (pode ser futura: a pessoa fica "em admissao" ate o dia);
--   7. tipo de contrato: CLT ou ESTAGIO;
--   8. opcionais: e-mail pessoal (mesmo formato), RG e orgao emissor (ate 20), CEP (8 digitos, sem pontuacao), UF (uma das 27 siglas),
--      logradouro (150), numero (10), complemento, bairro e cidade (100); texto em branco vale como ausente;
--   9. cargo e setor existem; o nivel de acesso existe e e um dos tres do V005 (RH, Funcionario, Funcionario Gestor);
--  10. CPF ainda nao cadastrado (inclusive em cadastro INATIVO: quem volta a empresa e readmitido, nao cadastrado de novo);
--  11. e-mail corporativo ainda nao cadastrado (idem; sem diferenciar maiusculas nem acentos, como a chave unica);
--  12. o gestor do setor nao pode estar inativo (GESTOR_DO_SETOR_INATIVO na API; o trigger TRG_FUNCIONARIOS_BEFORE_INSERT recusaria o mesmo);
--  13. ainda ha matricula de 5 digitos (o contador nao passa de 99999).
--
-- Efeitos: a MATRICULA e gerada aqui (contador SEQUENCIAS, linha MATRICULA: 00001, 00002...) e devolvida em P_MATRICULA; o ID_GESTOR e o
-- gestor do setor escolhido (NULL se o setor nao tem gestor); ATIVO = TRUE. A linha do contador e travada logo no comeco da transacao:
-- dois cadastros ao mesmo tempo ficam em fila (sem matricula repetida e sem buraco: se o cadastro e recusado, o numero volta no ROLLBACK).
-- Se outro programa gravar o mesmo CPF ou e-mail no meio do caminho, a chave unica do banco decide (erro 1062) e a recusa sai com a
-- mesma mensagem das regras 10 e 11.
--
-- O que a API faz e esta procedure NAO: dar os beneficios escolhidos (beneficioIds), criar o login e enviar o convite (use
-- SP_CRIAR_USUARIO e depois o convite pela API), incluir no onboarding, enviar o formulario de admissao, registrar auditoria e conferir
-- o perfil de quem chama.
--
-- Mudou em relacao ao arquivo antigo: sairam P_MATRICULA de entrada (a matricula e gerada; agora e parametro de SAIDA, no fim) e
-- P_ID_GESTOR (o gestor e derivado do setor); os textos ficaram mais largos so para a recusa por tamanho sair com mensagem propria. A
-- antiga nao validava CPF (so o tamanho), nascimento, telefone, e-mail, contrato, cargo, setor nem nivel, aceitava gestor de qualquer
-- setor, gravava telefone com pontuacao e e-mail com maiusculas e deixava a duplicidade estourar como erro 1062.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CADASTRAR_FUNCIONARIO(
    IN P_NOME VARCHAR(255),
    IN P_DATA_NASCIMENTO DATE,
    IN P_DATA_ADMISSAO DATE,
    IN P_EMAIL_CORPORATIVO VARCHAR(255),
    IN P_ID_CARGO INT,
    IN P_ID_SETOR INT,
    IN P_ID_NIVEL_ACESSO INT,
    IN P_TIPO_CONTRATO VARCHAR(30),
    IN P_CPF VARCHAR(30),
    IN P_RG VARCHAR(60),
    IN P_RG_ORGAO_EMISSOR VARCHAR(60),
    IN P_EMAIL_PESSOAL VARCHAR(255),
    IN P_TELEFONE VARCHAR(30),
    IN P_CEP VARCHAR(20),
    IN P_LOGRADOURO VARCHAR(255),
    IN P_NUMERO VARCHAR(30),
    IN P_COMPLEMENTO VARCHAR(255),
    IN P_BAIRRO VARCHAR(255),
    IN P_CIDADE VARCHAR(255),
    IN P_UF VARCHAR(10),
    OUT P_ID_FUNCIONARIO INT,
    OUT P_MATRICULA VARCHAR(20)
)
BEGIN
    DECLARE V_NOME VARCHAR(255);
    DECLARE V_CPF VARCHAR(30);
    DECLARE V_TELEFONE VARCHAR(30);
    DECLARE V_EMAIL VARCHAR(255);
    DECLARE V_TIPO_CONTRATO VARCHAR(30);
    DECLARE V_EMAIL_PESSOAL VARCHAR(255);
    DECLARE V_RG VARCHAR(60);
    DECLARE V_RG_ORGAO_EMISSOR VARCHAR(60);
    DECLARE V_CEP VARCHAR(20);
    DECLARE V_UF VARCHAR(10);
    DECLARE V_LOGRADOURO VARCHAR(255);
    DECLARE V_NUMERO VARCHAR(30);
    DECLARE V_COMPLEMENTO VARCHAR(255);
    DECLARE V_BAIRRO VARCHAR(255);
    DECLARE V_CIDADE VARCHAR(255);
    DECLARE V_I INT DEFAULT 1;
    DECLARE V_SOMA INT DEFAULT 0;
    DECLARE V_D1 INT DEFAULT 0;
    DECLARE V_D2 INT DEFAULT 0;
    DECLARE V_NOME_NIVEL VARCHAR(100) DEFAULT NULL;
    DECLARE V_REPETIDO_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_GESTOR INT DEFAULT NULL;
    DECLARE V_GESTOR_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_NUMERO_MATRICULA BIGINT DEFAULT NULL;
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;

    -- A chave unica recusou (CPF ou e-mail corporativo gravados por outro programa durante este cadastro): desfaz e diz qual dado se repete.
    DECLARE EXIT HANDLER FOR 1062
    BEGIN
        ROLLBACK;
        IF EXISTS (SELECT 1 FROM FUNCIONARIOS_DETALHES WHERE CPF = V_CPF COLLATE utf8mb4_unicode_ci) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este CPF.';
        ELSEIF EXISTS (SELECT 1 FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = V_EMAIL COLLATE utf8mb4_unicode_ci) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este e-mail corporativo.';
        ELSE
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um registro com estes dados.';
        END IF;
    END;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_FUNCIONARIO = NULL;
    SET P_MATRICULA = NULL;

    -- ---------------- entradas
    SET V_NOME = REGEXP_REPLACE(COALESCE(P_NOME, ''), '^[[:space:]]+|[[:space:]]+$', '');
    IF V_NOME = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o nome do funcionario.';
    END IF;
    IF CHAR_LENGTH(V_NOME) > 150 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nome pode ter ate 150 caracteres.';
    END IF;

    SET V_CPF = REGEXP_REPLACE(COALESCE(P_CPF, ''), '[^0-9]', '');
    -- A lista dos "todos iguais" e escrita por extenso de proposito: no MySQL 8.4, REPEAT(LEFT(V_CPF, 1), 11) sobre uma variavel local
    -- ALTERA a propria variavel (ela passa a valer o digito repetido), e todo CPF sairia como invalido.
    IF V_CPF NOT REGEXP '^[0-9]{11}$'
       OR V_CPF IN ('00000000000', '11111111111', '22222222222', '33333333333', '44444444444',
                    '55555555555', '66666666666', '77777777777', '88888888888', '99999999999') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;
    WHILE V_I <= 9 DO
        SET V_SOMA = V_SOMA + CAST(SUBSTRING(V_CPF, V_I, 1) AS UNSIGNED) * (11 - V_I);
        SET V_I = V_I + 1;
    END WHILE;
    SET V_D1 = (V_SOMA * 10) MOD 11;
    IF V_D1 = 10 THEN SET V_D1 = 0; END IF;
    SET V_SOMA = 0;
    SET V_I = 1;
    WHILE V_I <= 10 DO
        SET V_SOMA = V_SOMA + CAST(SUBSTRING(V_CPF, V_I, 1) AS UNSIGNED) * (12 - V_I);
        SET V_I = V_I + 1;
    END WHILE;
    SET V_D2 = (V_SOMA * 10) MOD 11;
    IF V_D2 = 10 THEN SET V_D2 = 0; END IF;
    IF V_D1 <> CAST(SUBSTRING(V_CPF, 10, 1) AS UNSIGNED) OR V_D2 <> CAST(SUBSTRING(V_CPF, 11, 1) AS UNSIGNED) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;

    IF P_DATA_NASCIMENTO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de nascimento.';
    END IF;
    IF P_DATA_NASCIMENTO > CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de nascimento nao pode ser no futuro.';
    END IF;

    SET V_TELEFONE = REGEXP_REPLACE(COALESCE(P_TELEFONE, ''), '[^0-9]', '');
    IF CHAR_LENGTH(V_TELEFONE) NOT IN (10, 11) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o telefone com DDD (10 ou 11 digitos).';
    END IF;

    SET V_EMAIL = LOWER(REGEXP_REPLACE(COALESCE(P_EMAIL_CORPORATIVO, ''), '^[[:space:]]+|[[:space:]]+$', ''));
    IF V_EMAIL = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o e-mail corporativo.';
    END IF;
    IF CHAR_LENGTH(V_EMAIL) > 150 OR V_EMAIL NOT REGEXP '^[^@[:space:]]+@[^@[:space:]]+[.][^@[:space:]]+$' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe um e-mail corporativo valido, com ate 150 caracteres.';
    END IF;

    IF P_DATA_ADMISSAO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de admissao.';
    END IF;

    -- O valor gravado e sempre o texto exato que a API le ('CLT' ou 'ESTAGIO'), mesmo que chegue em minusculas.
    SET V_TIPO_CONTRATO = CASE UPPER(REGEXP_REPLACE(COALESCE(P_TIPO_CONTRATO, ''), '^[[:space:]]+|[[:space:]]+$', '')) COLLATE utf8mb4_bin
        WHEN 'CLT' THEN 'CLT'
        WHEN 'ESTAGIO' THEN 'ESTAGIO'
        ELSE NULL
    END;
    IF V_TIPO_CONTRATO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o tipo de contrato: CLT ou ESTAGIO.';
    END IF;

    -- ---------------- entradas opcionais (texto em branco vale como ausente)
    SET V_EMAIL_PESSOAL = NULLIF(REGEXP_REPLACE(COALESCE(P_EMAIL_PESSOAL, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF V_EMAIL_PESSOAL IS NOT NULL AND (CHAR_LENGTH(V_EMAIL_PESSOAL) > 150 OR V_EMAIL_PESSOAL NOT REGEXP '^[^@[:space:]]+@[^@[:space:]]+[.][^@[:space:]]+$') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe um e-mail pessoal valido, com ate 150 caracteres.';
    END IF;

    SET V_RG = NULLIF(REGEXP_REPLACE(COALESCE(P_RG, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_RG) > 20 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O RG pode ter ate 20 caracteres.';
    END IF;

    SET V_RG_ORGAO_EMISSOR = NULLIF(REGEXP_REPLACE(COALESCE(P_RG_ORGAO_EMISSOR, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_RG_ORGAO_EMISSOR) > 20 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O orgao emissor do RG pode ter ate 20 caracteres.';
    END IF;

    SET V_CEP = NULLIF(REGEXP_REPLACE(COALESCE(P_CEP, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF V_CEP IS NOT NULL AND V_CEP NOT REGEXP '^[0-9]{8}$' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O CEP tem 8 digitos, sem pontuacao.';
    END IF;

    SET V_UF = UPPER(NULLIF(REGEXP_REPLACE(COALESCE(P_UF, ''), '^[[:space:]]+|[[:space:]]+$', ''), ''));
    IF V_UF IS NOT NULL AND V_UF COLLATE utf8mb4_bin NOT IN ('AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA', 'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC', 'SP', 'SE', 'TO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a sigla do estado (por exemplo SP).';
    END IF;

    SET V_LOGRADOURO = NULLIF(REGEXP_REPLACE(COALESCE(P_LOGRADOURO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_LOGRADOURO) > 150 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O logradouro pode ter ate 150 caracteres.';
    END IF;

    SET V_NUMERO = NULLIF(REGEXP_REPLACE(COALESCE(P_NUMERO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_NUMERO) > 10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O numero do endereco pode ter ate 10 caracteres.';
    END IF;

    SET V_COMPLEMENTO = NULLIF(REGEXP_REPLACE(COALESCE(P_COMPLEMENTO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_COMPLEMENTO) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O complemento pode ter ate 100 caracteres.';
    END IF;

    SET V_BAIRRO = NULLIF(REGEXP_REPLACE(COALESCE(P_BAIRRO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_BAIRRO) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O bairro pode ter ate 100 caracteres.';
    END IF;

    SET V_CIDADE = NULLIF(REGEXP_REPLACE(COALESCE(P_CIDADE, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_CIDADE) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A cidade pode ter ate 100 caracteres.';
    END IF;

    -- ---------------- estado
    START TRANSACTION;

    -- Primeiro comando da transacao: trava o contador da matricula. Dois cadastros ao mesmo tempo ficam em fila aqui, e as conferencias
    -- abaixo ja enxergam o que o cadastro da frente gravou.
    UPDATE SEQUENCIAS
       SET ULTIMO_VALOR = ULTIMO_VALOR + 1
     WHERE NOME = 'MATRICULA';

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A linha MATRICULA nao existe na tabela SEQUENCIAS (o script V001 a cria).';
    END IF;

    SELECT ULTIMO_VALOR INTO V_NUMERO_MATRICULA
      FROM SEQUENCIAS
     WHERE NOME = 'MATRICULA';

    IF NOT EXISTS (SELECT 1 FROM CARGOS WHERE ID_CARGO = P_ID_CARGO) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O cargo nao existe.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM SETORES WHERE ID_SETOR = P_ID_SETOR) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O setor nao existe.';
    END IF;

    SELECT NOME_NIVEL_ACESSO INTO V_NOME_NIVEL
      FROM NIVEIS_DE_ACESSO
     WHERE ID_NIVEL_ACESSO = P_ID_NIVEL_ACESSO;

    IF V_NOME_NIVEL IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nivel de acesso nao existe.';
    END IF;

    IF V_NOME_NIVEL COLLATE utf8mb4_bin NOT IN ('RH', 'Funcionario', 'Funcionario Gestor') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nivel de acesso desconhecido: use RH, Funcionario ou Funcionario Gestor.';
    END IF;

    SELECT F.ATIVO INTO V_REPETIDO_ATIVO
      FROM FUNCIONARIOS_DETALHES D
      JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = D.ID_FUNCIONARIO
     WHERE D.CPF = V_CPF COLLATE utf8mb4_unicode_ci
     LIMIT 1;

    IF V_REPETIDO_ATIVO = TRUE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este CPF.';
    END IF;
    IF V_REPETIDO_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este CPF pertence a um cadastro inativo. Se a pessoa esta voltando, readmita em vez de cadastrar de novo.';
    END IF;

    SELECT ATIVO INTO V_REPETIDO_ATIVO
      FROM FUNCIONARIOS
     WHERE EMAIL_CORPORATIVO = V_EMAIL COLLATE utf8mb4_unicode_ci
     LIMIT 1;

    IF V_REPETIDO_ATIVO = TRUE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este e-mail corporativo.';
    END IF;
    IF V_REPETIDO_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este e-mail corporativo pertence a um cadastro inativo. Se a pessoa esta voltando, readmita.';
    END IF;

    -- O gestor da pessoa e o gestor do setor (FOR SHARE: ninguem inativa o gestor nem troca o gestor do setor ate o COMMIT).
    SELECT G.ID_GESTOR, F.ATIVO
      INTO V_ID_GESTOR, V_GESTOR_ATIVO
      FROM SETORES S
      JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
      JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
     WHERE S.ID_SETOR = P_ID_SETOR
       FOR SHARE;

    IF V_GESTOR_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O gestor do setor escolhido esta inativo. Troque o gestor do setor antes de continuar.';
    END IF;

    IF V_NUMERO_MATRICULA > 99999 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'As matriculas de 5 digitos acabaram: o contador passaria de 99999.';
    END IF;

    INSERT INTO FUNCIONARIOS (
        NOME, DATA_NASCIMENTO, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO,
        ID_CARGO, ID_SETOR, ID_GESTOR, ID_NIVEL_ACESSO, MATRICULA, TIPO_CONTRATO
    )
    VALUES (
        V_NOME, P_DATA_NASCIMENTO, P_DATA_ADMISSAO, TRUE, V_EMAIL,
        P_ID_CARGO, P_ID_SETOR, V_ID_GESTOR, P_ID_NIVEL_ACESSO, LPAD(V_NUMERO_MATRICULA, 5, '0'), V_TIPO_CONTRATO
    );

    SET V_ID_FUNCIONARIO = LAST_INSERT_ID();

    INSERT INTO FUNCIONARIOS_DETALHES (
        ID_FUNCIONARIO, CPF, RG, RG_ORGAO_EMISSOR, EMAIL_PESSOAL, TELEFONE,
        CEP, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO, CIDADE, UF
    )
    VALUES (
        V_ID_FUNCIONARIO, V_CPF, V_RG, V_RG_ORGAO_EMISSOR, V_EMAIL_PESSOAL, V_TELEFONE,
        V_CEP, V_LOGRADOURO, V_NUMERO, V_COMPLEMENTO, V_BAIRRO, V_CIDADE, V_UF
    );

    COMMIT;

    SET P_ID_FUNCIONARIO = V_ID_FUNCIONARIO;
    SET P_MATRICULA = LPAD(V_NUMERO_MATRICULA, 5, '0');
END$$

-- ------------------------------------------------------------
-- SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO: troca o nivel de acesso (o perfil) de um funcionario.
--
-- Espelha: a troca do campo "perfil" em editarFuncionario (PUT /api/rh/funcionarios/{funcionarioId}); codigo em
-- EdicaoDeFuncionarios.ConferirRegrasDaEdicaoAsync. So muda FUNCIONARIOS.ID_NIVEL_ACESSO: promover alguem a Funcionario Gestor NAO cria
-- linha em GESTORES nem lhe da setor (na API isso acontece quando o RH escolhe a pessoa como gestor de um setor, que tambem a promove).
--
-- Regras, na ordem das recusas:
--   1. funcionario e nivel obrigatorios;
--   2. o funcionario existe (linha travada, FOR UPDATE); pode estar inativo (a API tambem edita o cadastro de quem esta inativo);
--   3. o nivel existe e e um dos tres do V005 (RH, Funcionario, Funcionario Gestor): quem tem outro nivel nao consegue entrar no sistema;
--   4. o nivel ja e o pedido: nao grava nada e NAO recusa (na API, salvar a ficha sem mudar o perfil nao e erro);
--   5. quem gerencia um setor (SETORES.ID_GESTOR aponta para a linha dele em GESTORES) precisa continuar Funcionario Gestor
--      (GESTOR_PRECISA_TER_PERFIL_GESTOR na API): o RH tira a pessoa do setor antes.
--
-- O que a API faz e esta procedure NAO: impedir que o RH logado mude o PROPRIO nivel (NAO_PODE_ALTERAR_O_PROPRIO_PERFIL: depende de quem
-- esta logado, e a procedure nao sabe quem a chama), registrar auditoria e conferir o perfil de quem chama. A API nao tem regra de
-- "ultimo RH"; esta procedure tambem nao.
--
-- Mudou em relacao ao arquivo antigo: aceitava qualquer linha de NIVEIS_DE_ACESSO, recusava a troca pelo mesmo nivel (tratava como
-- erro o que nao e), nao distinguia "funcionario nao existe" de "nivel igual" e deixava rebaixar quem gerencia um setor.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO(
    IN P_ID_FUNCIONARIO INT,
    IN P_ID_NIVEL_ACESSO INT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ID_NIVEL_ATUAL INT DEFAULT NULL;
    DECLARE V_NOME_NIVEL VARCHAR(100) DEFAULT NULL;
    DECLARE V_GERENCIA_SETOR INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_FUNCIONARIO IS NULL OR P_ID_NIVEL_ACESSO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario e o nivel de acesso.';
    END IF;

    START TRANSACTION;

    -- Primeiro comando da transacao (uma leitura com trava enxerga o que ja foi confirmado): a troca de gestor de setor da API tambem
    -- grava nesta linha, entao as duas operacoes ficam em fila.
    SELECT ID_FUNCIONARIO, ID_NIVEL_ACESSO
      INTO V_ID_FUNCIONARIO, V_ID_NIVEL_ATUAL
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SELECT NOME_NIVEL_ACESSO INTO V_NOME_NIVEL
      FROM NIVEIS_DE_ACESSO
     WHERE ID_NIVEL_ACESSO = P_ID_NIVEL_ACESSO;

    IF V_NOME_NIVEL IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nivel de acesso nao encontrado.';
    END IF;

    IF V_NOME_NIVEL COLLATE utf8mb4_bin NOT IN ('RH', 'Funcionario', 'Funcionario Gestor') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nivel de acesso desconhecido: use RH, Funcionario ou Funcionario Gestor.';
    END IF;

    IF NOT (V_ID_NIVEL_ATUAL <=> P_ID_NIVEL_ACESSO) THEN
        IF V_NOME_NIVEL COLLATE utf8mb4_bin <> 'Funcionario Gestor' THEN
            SELECT COUNT(*) INTO V_GERENCIA_SETOR
              FROM SETORES S
              JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
             WHERE G.ID_FUNCIONARIO = P_ID_FUNCIONARIO;

            IF V_GERENCIA_SETOR > 0 THEN
                SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario gerencia um setor e precisa ter o nivel Funcionario Gestor. Tire-o do setor antes de mudar o nivel.';
            END IF;
        END IF;

        UPDATE FUNCIONARIOS
           SET ID_NIVEL_ACESSO = P_ID_NIVEL_ACESSO
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO;
    END IF;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_CADASTRAR_CARGO: cria um cargo.
--
-- Espelha: criarCargo (POST /api/rh/cargos); codigo em CargosCasoDeUso.CriarAsync, ValidacaoDosCadastros e Cargo.Criar.
--
-- Regras, na ordem das recusas:
--   1. nome obrigatorio, de 1 a 100 caracteres (aparado);
--   2. descricao opcional, ate 255 caracteres (em branco vale como ausente);
--   3. carga horaria OBRIGATORIA: e a carga SEMANAL em horas (ex.: 44), maior que zero, de no maximo 168 (as horas de uma semana), com
--      ate duas casas decimais. O ponto de um dia usa a carga semanal dividida por 5;
--   4. nome unico, sem diferenciar maiusculas nem acentos (a chave unica UQ_CARGOS_NOME, utf8mb4_unicode_ci, e quem decide: assim dois
--      cadastros iguais ao mesmo tempo nao passam os dois).
--
-- O que a API faz e esta procedure NAO: registrar auditoria e conferir o perfil de quem chama.
--
-- Mudou em relacao ao arquivo antigo: aceitava cargo SEM carga horaria (o contrato nao aceita, e a API mostraria 0 horas), carga acima
-- de 168 e com mais de duas casas (arredondava em silencio), e o nome repetido estourava como erro 1062. P_CARGA_HORARIA ficou mais
-- largo (DECIMAL(12,6)) so para a procedure enxergar a terceira casa e recusar em vez de arredondar; os textos, idem, para a recusa por
-- tamanho sair com mensagem propria.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CADASTRAR_CARGO(
    IN P_NOME_CARGO VARCHAR(255),
    IN P_DESCRICAO VARCHAR(1000),
    IN P_CARGA_HORARIA DECIMAL(12,6),
    OUT P_ID_CARGO INT
)
BEGIN
    DECLARE V_NOME VARCHAR(255);
    DECLARE V_DESCRICAO VARCHAR(1000);

    DECLARE EXIT HANDLER FOR 1062
    BEGIN
        ROLLBACK;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um cargo com este nome.';
    END;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_CARGO = NULL;

    SET V_NOME = REGEXP_REPLACE(COALESCE(P_NOME_CARGO, ''), '^[[:space:]]+|[[:space:]]+$', '');
    IF V_NOME = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o nome do cargo.';
    END IF;
    IF CHAR_LENGTH(V_NOME) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nome do cargo pode ter ate 100 caracteres.';
    END IF;

    SET V_DESCRICAO = NULLIF(REGEXP_REPLACE(COALESCE(P_DESCRICAO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_DESCRICAO) > 255 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A descricao do cargo pode ter ate 255 caracteres.';
    END IF;

    IF P_CARGA_HORARIA IS NULL OR P_CARGA_HORARIA <= 0 OR P_CARGA_HORARIA > 168 OR P_CARGA_HORARIA <> ROUND(P_CARGA_HORARIA, 2) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a carga semanal em horas: maior que zero, ate 168, com ate duas casas decimais.';
    END IF;

    START TRANSACTION;

    INSERT INTO CARGOS (NOME_CARGO, DESCRICAO, CARGA_HORARIA)
    VALUES (V_NOME, V_DESCRICAO, P_CARGA_HORARIA);

    SET P_ID_CARGO = LAST_INSERT_ID();

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_CADASTRAR_NIVEL_ACESSO: REMOVIDA (nao existe no V007).
--
-- A API nao cria niveis de acesso: os tres niveis sao fixos, semeados pelo V005 (RH, Funcionario, Funcionario Gestor), e o codigo
-- converte o NOME do nivel no perfil do login (Perfis.PerfilPorNome). Um nivel com outro nome nao da perfil nenhum: quem o recebesse
-- nao conseguiria entrar ("Funcionario sem nivel ou com nivel desconhecido nao entra", V005). Uma procedure que cria niveis so criaria
-- linhas que o sistema nao sabe usar; por isso ela saiu, e SP_CADASTRAR_FUNCIONARIO e SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO recusam
-- qualquer nivel fora dos tres.
-- ------------------------------------------------------------


DELIMITER ;
