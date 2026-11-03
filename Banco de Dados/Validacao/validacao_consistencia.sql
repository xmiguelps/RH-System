-- ============================================================
-- SELECTS DE VALIDACAO
-- RHSystem (arquivo movido do fim do script de tabelas; nao faz parte dos scripts versionados)
-- Foco em inconsistencias que as constraints NAO impedem.
-- Cada select deve retornar 0 linhas.
-- ============================================================

-- ============================================================
-- 1. FUNCIONARIOS sem FUNCIONARIOS_DETALHES
-- ============================================================

SELECT
    F.ID_FUNCIONARIO,
    F.NOME
FROM FUNCIONARIOS F
LEFT JOIN FUNCIONARIOS_DETALHES D
    ON D.ID_FUNCIONARIO = F.ID_FUNCIONARIO
WHERE D.ID_FUNCIONARIO IS NULL;


-- ============================================================
-- 2. USUARIOS ativos vinculados a funcionarios inativos
-- ============================================================

SELECT
    U.ID_USUARIO,
    U.ID_FUNCIONARIO,
    F.NOME,
    F.ATIVO
FROM USUARIOS U
JOIN FUNCIONARIOS F
    ON F.ID_FUNCIONARIO = U.ID_FUNCIONARIO
WHERE U.ATIVO = TRUE
  AND F.ATIVO = FALSE;


-- ============================================================
-- 3. USUARIOS sem e-mail corporativo (impossibilita o login)
-- ============================================================

SELECT
    U.ID_USUARIO,
    U.ID_FUNCIONARIO,
    F.NOME
FROM USUARIOS U
JOIN FUNCIONARIOS F
    ON F.ID_FUNCIONARIO = U.ID_FUNCIONARIO
WHERE F.EMAIL_CORPORATIVO IS NULL
   OR TRIM(F.EMAIL_CORPORATIVO) = '';


-- ============================================================
-- 4. FUNCIONARIOS ativos com gestor que e funcionario inativo
-- ============================================================

SELECT
    F.ID_FUNCIONARIO,
    F.NOME,
    G.ID_GESTOR,
    FG.NOME AS NOME_GESTOR
FROM FUNCIONARIOS F
JOIN GESTORES G
    ON G.ID_GESTOR = F.ID_GESTOR
JOIN FUNCIONARIOS FG
    ON FG.ID_FUNCIONARIO = G.ID_FUNCIONARIO
WHERE F.ATIVO = TRUE
  AND FG.ATIVO = FALSE;


-- ============================================================
-- 5. PONTOS com quantidade impar de marcacoes
-- Quantidade impar pode indicar entrada/saida sem par.
-- A sequencia ENTRADA/SAIDA deve tambem ser validada pela aplicacao.
-- ============================================================

SELECT
    P.ID_PONTO,
    P.ID_FUNCIONARIO,
    P.DATA,
    COUNT(M.ID_MARCACAO) AS QUANTIDADE_MARCACOES
FROM PONTOS P
JOIN PONTOS_MARCACOES M
    ON M.ID_PONTO = P.ID_PONTO
GROUP BY P.ID_PONTO, P.ID_FUNCIONARIO, P.DATA
HAVING MOD(COUNT(M.ID_MARCACAO), 2) <> 0;


-- ============================================================
-- 6. FERIAS sobrepostas do mesmo funcionario
-- (considera apenas ferias solicitadas, aprovadas ou em gozo)
-- ============================================================

SELECT
    A.ID_FUNCIONARIO,
    A.ID_FERIAS AS ID_FERIAS_A,
    B.ID_FERIAS AS ID_FERIAS_B,
    A.DATA_INICIO AS INICIO_A,
    A.DATA_FIM AS FIM_A,
    B.DATA_INICIO AS INICIO_B,
    B.DATA_FIM AS FIM_B
FROM FERIAS A
JOIN FERIAS B
    ON B.ID_FUNCIONARIO = A.ID_FUNCIONARIO
   AND B.ID_FERIAS > A.ID_FERIAS
   AND A.DATA_INICIO <= B.DATA_FIM
   AND B.DATA_INICIO <= A.DATA_FIM
WHERE A.STATUS_FERIAS IN ('SOLICITADA', 'APROVADA', 'EM_GOZO')
  AND B.STATUS_FERIAS IN ('SOLICITADA', 'APROVADA', 'EM_GOZO');


-- ============================================================
-- 7. FERIAS ativas de funcionarios desligados
-- ============================================================

SELECT
    FE.ID_FERIAS,
    FE.ID_FUNCIONARIO,
    F.NOME,
    F.DATA_DESLIGAMENTO,
    FE.DATA_INICIO,
    FE.STATUS_FERIAS
FROM FERIAS FE
JOIN FUNCIONARIOS F
    ON F.ID_FUNCIONARIO = FE.ID_FUNCIONARIO
WHERE FE.STATUS_FERIAS IN ('SOLICITADA', 'APROVADA', 'EM_GOZO')
  AND F.DATA_DESLIGAMENTO IS NOT NULL
  AND FE.DATA_INICIO > F.DATA_DESLIGAMENTO;


-- ============================================================
-- 8. PONTOS registrados em feriado nacional ativo
-- (para conferencia de jornada e adicional de feriado)
-- ============================================================

SELECT
    P.ID_PONTO,
    P.ID_FUNCIONARIO,
    P.DATA,
    FD.NOME_FERIADO
FROM PONTOS P
JOIN FERIADOS FD
    ON FD.DATA_FERIADO = P.DATA
WHERE FD.ATIVO = TRUE
  AND FD.TIPO_FERIADO = 'NACIONAL';


-- ============================================================
-- 9. HOLERITES sem arquivo ou sem data de publicacao
-- ============================================================

SELECT
    ID_HOLERITE,
    ID_FUNCIONARIO,
    ANO,
    MES,
    DATA_PUBLICACAO,
    CAMINHO_ARQUIVO
FROM HOLERITES
WHERE CAMINHO_ARQUIVO IS NULL
   OR TRIM(CAMINHO_ARQUIVO) = ''
   OR DATA_PUBLICACAO IS NULL;


-- ============================================================
-- 10. TREINAMENTOS_CERTIFICADOS sem dados de emissao ou validacao
-- ============================================================

SELECT
    ID_CERTIFICADO,
    ID_TREINAMENTO,
    ID_FUNCIONARIO,
    DATA_EMISSAO,
    CODIGO_VALIDACAO
FROM TREINAMENTOS_CERTIFICADOS
WHERE DATA_EMISSAO IS NULL
   OR CODIGO_VALIDACAO IS NULL
   OR TRIM(CODIGO_VALIDACAO) = '';


-- ============================================================
-- 11. FORMULARIOS_PERGUNTAS sem ordem definida
-- ============================================================

SELECT
    ID_FORMULARIO_PERGUNTA,
    ID_FORMULARIO,
    TEXTO_PERGUNTA,
    ORDEM
FROM FORMULARIOS_PERGUNTAS
WHERE ORDEM IS NULL;


-- ============================================================
-- FIM DOS SELECTS DE VALIDACAO
-- ============================================================
-- FIM DO SCRIPT
-- ============================================================


-- ============================================================
-- 12. FUNCIONARIOS ativos cujo ID_GESTOR diverge do gestor do setor
-- (o proprio gestor do setor e ignorado: o ID_GESTOR dele fica NULL)
-- ============================================================

SELECT
    F.ID_FUNCIONARIO,
    F.NOME,
    F.ID_SETOR,
    F.ID_GESTOR AS ID_GESTOR_DO_FUNCIONARIO,
    S.ID_GESTOR AS ID_GESTOR_DO_SETOR
FROM FUNCIONARIOS F
JOIN SETORES S
    ON S.ID_SETOR = F.ID_SETOR
LEFT JOIN GESTORES G
    ON G.ID_GESTOR = S.ID_GESTOR
WHERE F.ATIVO = TRUE
  AND NOT (F.ID_GESTOR <=> S.ID_GESTOR)
  AND (G.ID_FUNCIONARIO IS NULL OR G.ID_FUNCIONARIO <> F.ID_FUNCIONARIO);
