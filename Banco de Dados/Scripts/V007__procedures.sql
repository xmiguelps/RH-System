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
