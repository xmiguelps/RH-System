-- ============================================================
-- SEED FICTICIO DA DEMO (banca do TCC)
--
-- NAO E UM SCRIPT DE VERSAO: fica fora de "Banco de Dados/Scripts" de proposito (a API aplica sozinha so o que esta la). Rode DEPOIS que a API
-- aplicou os scripts V001 a V005, num banco SEM nenhum colaborador (o primeiro INSERT de verdade e a configuracao da empresa, que so tem uma linha: uma segunda rodada falha logo no
-- comeco, sem deixar nada pela metade que importe). Tudo e inventado: nomes, CPFs (digitos validos, de ninguem), telefones e e-mails.
--
-- Como rodar (uma sessao so, porque o script usa variaveis de usuario; veja o LEIA-ME.md desta pasta; no PowerShell o operador < nao existe: use o Git Bash ou cmd /c):
--   mysql --default-character-set=utf8mb4 -h <servidor> -u <usuario> -p <banco> < seed_demo.sql
--
-- Variaveis que quem roda pode definir ANTES (SET @nome = ...; na mesma sessao), todas com padrao:
--   @hoje     o dia "de hoje" da demo (padrao: CURDATE()). Todas as datas do seed saem dele, entao a demo nunca fica "velha". Nao pode ser depois
--             do dia de hoje do servidor: o trigger do banco de horas recusa lancamento com data futura.
--   @dominio  o dominio dos e-mails fictícios (padrao: rhsystem-demo.example). Veja o LEIA-ME.md sobre devolucao de e-mail.
--
-- Login: todos os usuarios usam a mesma senha de demonstracao, "Demo#RH2026" (o hash abaixo e o do SenhaHasher da API). Troque antes de
-- publicar a demo num endereco aberto. Entrar como: helena.martins@<dominio> (RH), rafael.costa@<dominio> (gestor de Tecnologia),
-- beatriz.nogueira@<dominio> (gestora do Comercial) ou lucas.pereira@<dominio> (funcionario).
--
-- O que o seed NAO cria, de proposito: holerites, atestados e documentos ENVIADOS (todos apontam para ARQUIVO no Blob, e um caminho que nao
-- existe la quebra o download); o roteiro da demo envia esses ao vivo, pela propria API. Tambem nao cria eventos de auditoria: o log
-- nasce das acoes feitas na demo.
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET @hoje = COALESCE(@hoje, CURDATE());
-- O CONVERT fixa a collation da variavel (a mesma das tabelas); sem ele, quem rodou com outro SET NAMES teria "Illegal mix of collations" nas comparacoes.
SET @dominio = CONVERT(COALESCE(@dominio, 'rhsystem-demo.example') USING utf8mb4) COLLATE utf8mb4_unicode_ci;
SET @senha = 'AQAAAAIAAYagAAAAEOAXtDJhauBe0zfvR32StdDwBKwnFoBQSqIaziI+VOLdgnA4+xaQKwMPWBk8z2JjxA==';

-- Guarda: @hoje nao pode ser depois do dia de hoje do servidor (o trigger do banco de horas e outros recusam data futura, e a falha viria no meio do script, com
-- coisas ja gravadas). Se o proximo comando falhar com CHK_SEQUENCIAS_ULTIMO_VALOR, e isto: corrija o @hoje. Nada foi gravado ainda.
INSERT INTO SEQUENCIAS (NOME, ULTIMO_VALOR) VALUES ('DEMO_GUARDA', IF(@hoje > CURDATE(), -1, 0));
DELETE FROM SEQUENCIAS WHERE NOME = 'DEMO_GUARDA';

-- ------------------------------------------------------------
-- 1. Empresa, cargos e setores
-- ------------------------------------------------------------
INSERT INTO CONFIGURACOES_EMPRESA (ID_CONFIGURACAO_EMPRESA, NOME_EMPRESA, UF, MUNICIPIO, HORARIO_ENTRADA, HORARIO_SAIDA, TOLERANCIA_ATRASO_MINUTOS, DIA_PAGAMENTO)
VALUES (1, 'Empresa Demonstração Ltda', 'SP', 'São Paulo', '08:00:00', '17:00:00', 10, 5);

INSERT INTO CARGOS (NOME_CARGO, DESCRICAO, CARGA_HORARIA) VALUES
    ('Analista de RH', 'Cuida do cadastro, do ponto, das férias e dos benefícios.', 40.00),
    ('Gerente de Tecnologia', 'Lidera a equipe de desenvolvimento e suporte.', 40.00),
    ('Gerente Comercial', 'Lidera a equipe de vendas.', 40.00),
    ('Desenvolvedor', 'Desenvolve e mantém os sistemas da empresa.', 40.00),
    ('Analista de Suporte', 'Atende os chamados internos de tecnologia.', 40.00),
    ('Vendedor', 'Atende clientes e fecha vendas.', 40.00),
    ('Assistente Comercial', 'Apoia a equipe de vendas.', 40.00),
    ('Assistente Administrativo', 'Rotinas administrativas e atendimento interno.', 40.00);

INSERT INTO SETORES (NOME_SETOR, DESCRICAO) VALUES
    ('Tecnologia', 'Desenvolvimento e suporte de sistemas.'),
    ('Comercial', 'Vendas e relacionamento com clientes.'),
    ('Administrativo', 'Recursos humanos e rotinas administrativas.');

-- ------------------------------------------------------------
-- 2. Pessoas (uma tabela de apoio resume o elenco; as linhas reais saem dela)
-- ------------------------------------------------------------
-- Tabelas de apoio (comuns, nao temporarias: o usuario do banco no Azure pode nao ter CREATE TEMPORARY TABLES). Saem no fim do script.
DROP TABLE IF EXISTS TMP_DEMO_PESSOAS;
DROP TABLE IF EXISTS TMP_DEMO_PONTOS;
DROP TABLE IF EXISTS TMP_DEMO_DIAS;
CREATE TABLE TMP_DEMO_PESSOAS (
    ORDEM INT PRIMARY KEY,
    CHAVE VARCHAR(20) NOT NULL,
    NOME VARCHAR(150) NOT NULL,
    EMAIL_LOCAL VARCHAR(60) NOT NULL,
    CARGO VARCHAR(60) NOT NULL,
    SETOR VARCHAR(60) NOT NULL,
    NIVEL VARCHAR(40) NOT NULL,
    ADMISSAO DATE NOT NULL,
    NASCIMENTO DATE NOT NULL,
    CPF CHAR(11) NOT NULL,
    TELEFONE VARCHAR(30) NOT NULL
);

INSERT INTO TMP_DEMO_PESSOAS (ORDEM, CHAVE, NOME, EMAIL_LOCAL, CARGO, SETOR, NIVEL, ADMISSAO, NASCIMENTO, CPF, TELEFONE) VALUES
    (1, 'helena',   'Helena Martins',    'helena.martins',    'Analista de RH',           'Administrativo', 'RH',                 DATE_SUB(@hoje, INTERVAL 52 MONTH), '1988-03-14', '30000000035', '11900000001'),
    (2, 'rafael',   'Rafael Costa',      'rafael.costa',      'Gerente de Tecnologia',    'Tecnologia',     'Funcionario Gestor', DATE_SUB(@hoje, INTERVAL 60 MONTH), '1985-07-02', '30000791903', '11900000002'),
    (3, 'beatriz',  'Beatriz Nogueira',  'beatriz.nogueira',  'Gerente Comercial',        'Comercial',      'Funcionario Gestor', DATE_SUB(@hoje, INTERVAL 48 MONTH), '1987-11-23', '30001583832', '11900000003'),
    (4, 'lucas',    'Lucas Pereira',     'lucas.pereira',     'Desenvolvedor',            'Tecnologia',     'Funcionario',        DATE_SUB(@hoje, INTERVAL 30 MONTH), '1994-05-09', '30002375770', '11900000004'),
    (5, 'camila',   'Camila Duarte',     'camila.duarte',     'Desenvolvedor',            'Tecnologia',     'Funcionario',        DATE_SUB(@hoje, INTERVAL 20 MONTH), '1996-01-30', '30003167607', '11900000005'),
    (6, 'diego',    'Diego Farias',      'diego.farias',      'Analista de Suporte',      'Tecnologia',     'Funcionario',        DATE_SUB(@hoje, INTERVAL 14 MONTH), '1992-09-17', '30003959546', '11900000006'),
    (7, 'thiago',   'Thiago Mello',      'thiago.mello',      'Vendedor',                 'Comercial',      'Funcionario',        DATE_SUB(@hoje, INTERVAL 26 MONTH), '1990-12-05', '30004751418', '11900000007'),
    (8, 'vanessa',  'Vanessa Lima',      'vanessa.lima',      'Vendedor',                 'Comercial',      'Funcionario',        DATE_SUB(@hoje, INTERVAL 18 MONTH), '1993-04-21', '30005543355', '11900000008'),
    (9, 'patricia', 'Patrícia Gomes',    'patricia.gomes',    'Assistente Comercial',     'Comercial',      'Funcionario',        DATE_SUB(@hoje, INTERVAL 9 MONTH),  '1998-08-12', '30006335292', '11900000009'),
    (10, 'carla',   'Carla Mendes',      'carla.mendes',      'Assistente Administrativo', 'Administrativo', 'Funcionario',        DATE_SUB(@hoje, INTERVAL 40 MONTH), '1991-02-27', '30007127120', '11900000010'),
    (11, 'bruno',   'Bruno Azevedo',     'bruno.azevedo',     'Assistente Administrativo', 'Administrativo', 'Funcionario',        DATE_SUB(@hoje, INTERVAL 6 MONTH),  '1999-06-03', '30007919069', '11900000011'),
    (12, 'julia',   'Júlia Andrade',     'julia.andrade',     'Desenvolvedor',            'Tecnologia',     'Funcionario',        DATE_ADD(@hoje, INTERVAL 5 DAY),    '1997-10-19', '30008710988', '11900000012');

INSERT INTO FUNCIONARIOS (NOME, DATA_NASCIMENTO, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO, ID_CARGO, ID_SETOR, ID_NIVEL_ACESSO, MATRICULA, TIPO_CONTRATO)
SELECT p.NOME, p.NASCIMENTO, p.ADMISSAO, TRUE, CONCAT(p.EMAIL_LOCAL, '@', @dominio), c.ID_CARGO, s.ID_SETOR, n.ID_NIVEL_ACESSO, LPAD(p.ORDEM, 5, '0'), 'CLT'
  FROM TMP_DEMO_PESSOAS p
  JOIN CARGOS c ON c.NOME_CARGO = p.CARGO
  JOIN SETORES s ON s.NOME_SETOR = p.SETOR
  JOIN NIVEIS_DE_ACESSO n ON n.NOME_NIVEL_ACESSO = p.NIVEL
 ORDER BY p.ORDEM;

UPDATE SEQUENCIAS SET ULTIMO_VALOR = 12 WHERE NOME = 'MATRICULA';

INSERT INTO FUNCIONARIOS_DETALHES (ID_FUNCIONARIO, CPF, TELEFONE, EMAIL_PESSOAL, CEP, LOGRADOURO, NUMERO, BAIRRO, CIDADE, UF)
SELECT f.ID_FUNCIONARIO, p.CPF, p.TELEFONE, CONCAT(p.EMAIL_LOCAL, '.pessoal@', @dominio), '01310100', 'Avenida Paulista', CAST(100 + p.ORDEM * 10 AS CHAR), 'Bela Vista', 'São Paulo', 'SP'
  FROM TMP_DEMO_PESSOAS p
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT(p.EMAIL_LOCAL, '@', @dominio);

INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA, ATIVO, SENHA_PROVISORIA)
SELECT f.ID_FUNCIONARIO, @senha, TRUE, FALSE
  FROM TMP_DEMO_PESSOAS p
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT(p.EMAIL_LOCAL, '@', @dominio);

-- Os dois gestores, o setor de cada um e o gestor de quem e da equipe (o Administrativo fica sem gestor: o RH decide os pedidos de la).
INSERT INTO GESTORES (ID_FUNCIONARIO)
SELECT f.ID_FUNCIONARIO FROM FUNCIONARIOS f WHERE f.EMAIL_CORPORATIVO IN (CONCAT('rafael.costa@', @dominio), CONCAT('beatriz.nogueira@', @dominio));

UPDATE SETORES s
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('rafael.costa@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = f.ID_FUNCIONARIO
   SET s.ID_GESTOR = g.ID_GESTOR
 WHERE s.NOME_SETOR = 'Tecnologia';

UPDATE SETORES s
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('beatriz.nogueira@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = f.ID_FUNCIONARIO
   SET s.ID_GESTOR = g.ID_GESTOR
 WHERE s.NOME_SETOR = 'Comercial';

UPDATE FUNCIONARIOS m
  JOIN SETORES s ON s.ID_SETOR = m.ID_SETOR
  JOIN GESTORES g ON g.ID_GESTOR = s.ID_GESTOR
   SET m.ID_GESTOR = g.ID_GESTOR
 WHERE g.ID_FUNCIONARIO <> m.ID_FUNCIONARIO;

-- ------------------------------------------------------------
-- 3. Ponto dos ultimos 62 dias (dias uteis, sem feriado, de quem ja era admitido): cobre o mes atual e o anterior inteiros (o espelho e mensal e todo dia util sem batida vira falta)
--    Entrada entre 08:00 e 08:09 (dentro da tolerancia); saida do almoco 12:00; volta 13:00; saida entre 17:00 e 17:11. O Diego chega atrasado em
--    dois dias, o Thiago falta em um (sem ponto) e a Vanessa esta de ferias hoje (sem ponto nos dias do gozo).
-- ------------------------------------------------------------
-- N conta os dias uteis para tras (1 = o ultimo dia util antes de @hoje): os "dias do Diego e do Thiago" abaixo sao por esse numero, nao pelo calendario.
CREATE TABLE TMP_DEMO_DIAS (DIA DATE PRIMARY KEY, N INT NOT NULL);
INSERT INTO TMP_DEMO_DIAS (DIA, N)
WITH RECURSIVE dias (n, d) AS (
    SELECT 1, DATE_SUB(@hoje, INTERVAL 1 DAY)
    UNION ALL
    SELECT n + 1, DATE_SUB(d, INTERVAL 1 DAY) FROM dias WHERE n < 62
)
SELECT d, ROW_NUMBER() OVER (ORDER BY d DESC) FROM dias WHERE WEEKDAY(d) < 5 AND NOT EXISTS (SELECT 1 FROM FERIADOS fe WHERE fe.DATA_FERIADO = dias.d AND fe.ATIVO = TRUE AND fe.TIPO_FERIADO = 'NACIONAL');

CREATE TABLE TMP_DEMO_PONTOS (
    ID_FUNCIONARIO INT NOT NULL,
    DIA DATE NOT NULL,
    ENTRADA DATETIME NOT NULL,
    SAIDA DATETIME NOT NULL,
    PRIMARY KEY (ID_FUNCIONARIO, DIA)
);

INSERT INTO TMP_DEMO_PONTOS (ID_FUNCIONARIO, DIA, ENTRADA, SAIDA)
SELECT f.ID_FUNCIONARIO, d.DIA,
       TIMESTAMP(d.DIA, SEC_TO_TIME(8 * 3600 + (CASE WHEN f.EMAIL_CORPORATIVO = CONCAT('diego.farias@', @dominio) AND d.N IN (3, 8) THEN 35 ELSE MOD(f.ID_FUNCIONARIO * 7 + d.N, 10) END) * 60)),
       TIMESTAMP(d.DIA, SEC_TO_TIME(17 * 3600 + MOD(f.ID_FUNCIONARIO * 3 + d.N, 12) * 60))
  FROM FUNCIONARIOS f
  JOIN TMP_DEMO_DIAS d ON d.DIA >= f.DATA_ADMISSAO
 WHERE f.ATIVO = TRUE
   AND NOT (f.EMAIL_CORPORATIVO = CONCAT('thiago.mello@', @dominio) AND d.N = 5)
   AND NOT (f.EMAIL_CORPORATIVO = CONCAT('vanessa.lima@', @dominio) AND d.DIA >= DATE_SUB(@hoje, INTERVAL 2 DAY));

INSERT INTO PONTOS (ID_FUNCIONARIO, DATA, HORAS_TRABALHADAS, CARGA_HORARIA)
SELECT t.ID_FUNCIONARIO, t.DIA,
       ROUND((TIMESTAMPDIFF(MINUTE, t.ENTRADA, TIMESTAMP(t.DIA, '12:00:00')) + TIMESTAMPDIFF(MINUTE, TIMESTAMP(t.DIA, '13:00:00'), t.SAIDA)) / 60, 2),
       8.00
  FROM TMP_DEMO_PONTOS t;

INSERT INTO PONTOS_MARCACOES (ID_PONTO, DATA_HORA_MARCACAO, TIPO_MARCACAO, ORIGEM)
SELECT p.ID_PONTO, m.INSTANTE, m.TIPO, 'SISTEMA'
  FROM TMP_DEMO_PONTOS t
  JOIN PONTOS p ON p.ID_FUNCIONARIO = t.ID_FUNCIONARIO AND p.DATA = t.DIA
  JOIN (
        SELECT ID_FUNCIONARIO, DIA, ENTRADA AS INSTANTE, 'ENTRADA' AS TIPO FROM TMP_DEMO_PONTOS
        UNION ALL SELECT ID_FUNCIONARIO, DIA, TIMESTAMP(DIA, '12:00:00'), 'SAIDA' FROM TMP_DEMO_PONTOS
        UNION ALL SELECT ID_FUNCIONARIO, DIA, TIMESTAMP(DIA, '13:00:00'), 'ENTRADA' FROM TMP_DEMO_PONTOS
        UNION ALL SELECT ID_FUNCIONARIO, DIA, SAIDA, 'SAIDA' FROM TMP_DEMO_PONTOS
       ) m ON m.ID_FUNCIONARIO = t.ID_FUNCIONARIO AND m.DIA = t.DIA;

-- Banco de horas: o seed NAO lanca nada. O dominio so cria lancamento pelo calculo automatico (o agendador consolida cada dia a partir das batidas, a falta do Thiago
-- vira debito e as saidas depois do horario viram credito); lancamento gravado aqui seria reescrito ou apagado no primeiro recalculo. O saldo aparece alguns minutos depois
-- de a API estar no ar com o seed (o agendador recupera os ultimos 60 dias a cada ciclo).

-- Hoje: se @hoje e o dia de hoje do servidor e ja passou das 09:30, parte da equipe ja bateu a entrada (a demo comeca com "quem esta trabalhando agora"). Com @hoje no
-- passado nao cria nada: a hora do servidor nao decide o ponto de um dia que ja acabou.
INSERT INTO PONTOS (ID_FUNCIONARIO, DATA, HORAS_TRABALHADAS, CARGA_HORARIA)
SELECT f.ID_FUNCIONARIO, @hoje, NULL, 8.00
  FROM FUNCIONARIOS f
 WHERE @hoje = CURDATE() AND WEEKDAY(@hoje) < 5 AND TIME(NOW()) >= '09:30:00' AND f.ATIVO = TRUE AND f.DATA_ADMISSAO <= @hoje
   AND f.EMAIL_CORPORATIVO IN (CONCAT('lucas.pereira@', @dominio), CONCAT('camila.duarte@', @dominio), CONCAT('rafael.costa@', @dominio), CONCAT('beatriz.nogueira@', @dominio), CONCAT('carla.mendes@', @dominio), CONCAT('helena.martins@', @dominio))
   AND NOT EXISTS (SELECT 1 FROM FERIADOS fe WHERE fe.DATA_FERIADO = @hoje AND fe.ATIVO = TRUE AND fe.TIPO_FERIADO = 'NACIONAL');

INSERT INTO PONTOS_MARCACOES (ID_PONTO, DATA_HORA_MARCACAO, TIPO_MARCACAO, ORIGEM)
SELECT p.ID_PONTO, TIMESTAMP(@hoje, SEC_TO_TIME(8 * 3600 + MOD(p.ID_FUNCIONARIO * 7, 10) * 60)), 'ENTRADA', 'SISTEMA'
  FROM PONTOS p
 WHERE p.DATA = @hoje;

-- ------------------------------------------------------------
-- 4. Avisos (publicados ha 8 a 10 dias: a recuperacao do agendador so reenfileira o e-mail de aviso dos ultimos 7 dias, e o seed nao deve mandar e-mail)
-- ------------------------------------------------------------
INSERT INTO AVISOS (ID_FUNCIONARIO, TITULO, CONTEUDO, DATA_PUBLICACAO, DATA_EXPIRACAO, TIPO_AVISO, FIXADO, ENVIADO)
SELECT f.ID_FUNCIONARIO, a.titulo, a.conteudo, TIMESTAMP(DATE_SUB(@hoje, INTERVAL a.dias DAY), '09:00:00'), a.expira, a.tipo, a.fixado, a.enviado
  FROM (
        SELECT 'Bem-vindos à demonstração do RH System' AS titulo, 'Esta é uma demonstração com dados fictícios. Explore o ponto, as férias, os benefícios, os formulários e os treinamentos. Nada aqui é de pessoas reais.' AS conteudo, 10 AS dias, NULL AS expira, 'DETALHADO' AS tipo, TRUE AS fixado, TRUE AS enviado
        UNION ALL SELECT 'Confraternização de fim de ano', 'Reserve a data: a confraternização da empresa será na última sexta-feira do ano, a partir das 18h.', 9, DATE_ADD(@hoje, INTERVAL 60 DAY), 'SIMPLES', FALSE, TRUE
        UNION ALL SELECT 'Atualização do cadastro', 'Confira seus dados na ficha e avise o RH se algo estiver diferente. O prazo é o fim do mês.', 8, DATE_ADD(@hoje, INTERVAL 20 DAY), 'DETALHADO', FALSE, TRUE
       ) a
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio);

INSERT INTO AVISOS (ID_FUNCIONARIO, TITULO, CONTEUDO, DATA_PUBLICACAO, DATA_EXPIRACAO, TIPO_AVISO, FIXADO, ENVIADO)
SELECT f.ID_FUNCIONARIO, 'Rascunho: regras do banco de horas', 'Texto ainda em revisão pelo RH.', NULL, NULL, 'DETALHADO', FALSE, FALSE
  FROM FUNCIONARIOS f WHERE f.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio);

-- Quem ja leu o aviso fixado (o trigger do banco ja criou uma linha "nao lida" por pessoa quando o aviso foi enviado).
UPDATE AVISOS_VISUALIZACOES v
  JOIN AVISOS a ON a.ID_AVISO = v.ID_AVISO AND a.TITULO = 'Bem-vindos à demonstração do RH System'
  JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = v.ID_FUNCIONARIO AND f.EMAIL_CORPORATIVO IN (CONCAT('lucas.pereira@', @dominio), CONCAT('rafael.costa@', @dominio))
   SET v.VISUALIZADO = TRUE, v.DATA_VISUALIZACAO = TIMESTAMP(DATE_SUB(@hoje, INTERVAL 2 DAY), '10:15:00');

-- ------------------------------------------------------------
-- 5. Ferias (uma em gozo, uma esperando o gestor, uma esperando o RH, uma ja concluida)
-- ------------------------------------------------------------
-- O periodo aquisitivo e o primeiro ano de cada pessoa (o mais antigo em que o pedido cabe, como a aplicacao escolhe).
INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO, STATUS_FERIAS,
                    DATA_SOLICITACAO, DATA_ANALISE, ID_GESTOR, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ID_FUNCIONARIO_RH, DATA_CONFIRMACAO_RH)
SELECT f.ID_FUNCIONARIO, f.DATA_ADMISSAO, DATE_SUB(DATE_ADD(f.DATA_ADMISSAO, INTERVAL 1 YEAR), INTERVAL 1 DAY),
       DATE_SUB(@hoje, INTERVAL 2 DAY), DATE_ADD(@hoje, INTERVAL 11 DAY), FALSE, 0, 'EM_GOZO',
       TIMESTAMP(DATE_SUB(@hoje, INTERVAL 60 DAY), '10:00:00'), TIMESTAMP(DATE_SUB(@hoje, INTERVAL 57 DAY), '10:00:00'), g.ID_GESTOR, 'APROVOU', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 58 DAY), '10:00:00'),
       rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 57 DAY), '10:00:00')
  FROM FUNCIONARIOS f
  JOIN FUNCIONARIOS gf ON gf.EMAIL_CORPORATIVO = CONCAT('beatriz.nogueira@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = gf.ID_FUNCIONARIO
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio)
 WHERE f.EMAIL_CORPORATIVO = CONCAT('vanessa.lima@', @dominio);

-- Lucas: pediu 15 dias e espera o gestor (aparece em "Esperando sua aprovação" do Rafael).
INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO, STATUS_FERIAS, DATA_SOLICITACAO, ID_GESTOR)
SELECT f.ID_FUNCIONARIO, f.DATA_ADMISSAO, DATE_SUB(DATE_ADD(f.DATA_ADMISSAO, INTERVAL 1 YEAR), INTERVAL 1 DAY),
       DATE_ADD(@hoje, INTERVAL 45 DAY), DATE_ADD(@hoje, INTERVAL 59 DAY), FALSE, 0, 'SOLICITADA', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 1 DAY), '15:30:00'), g.ID_GESTOR
  FROM FUNCIONARIOS f
  JOIN FUNCIONARIOS gf ON gf.EMAIL_CORPORATIVO = CONCAT('rafael.costa@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = gf.ID_FUNCIONARIO
 WHERE f.EMAIL_CORPORATIVO = CONCAT('lucas.pereira@', @dominio);

-- Camila: o gestor ja aprovou; falta a confirmacao do RH (aparece nas pendencias do RH).
INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO, STATUS_FERIAS, DATA_SOLICITACAO, ID_GESTOR, RESULTADO_GESTOR, DATA_ETAPA_GESTOR)
SELECT f.ID_FUNCIONARIO, f.DATA_ADMISSAO, DATE_SUB(DATE_ADD(f.DATA_ADMISSAO, INTERVAL 1 YEAR), INTERVAL 1 DAY),
       DATE_ADD(@hoje, INTERVAL 70 DAY), DATE_ADD(@hoje, INTERVAL 79 DAY), FALSE, 0, 'SOLICITADA', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 4 DAY), '09:20:00'), g.ID_GESTOR, 'APROVOU', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 2 DAY), '11:00:00')
  FROM FUNCIONARIOS f
  JOIN FUNCIONARIOS gf ON gf.EMAIL_CORPORATIVO = CONCAT('rafael.costa@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = gf.ID_FUNCIONARIO
 WHERE f.EMAIL_CORPORATIVO = CONCAT('camila.duarte@', @dominio);

-- Thiago: ja tirou 10 dias (concluidas), para o saldo da ficha ter historico.
INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO, STATUS_FERIAS,
                    DATA_SOLICITACAO, DATA_ANALISE, ID_GESTOR, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ID_FUNCIONARIO_RH, DATA_CONFIRMACAO_RH)
SELECT f.ID_FUNCIONARIO, f.DATA_ADMISSAO, DATE_SUB(DATE_ADD(f.DATA_ADMISSAO, INTERVAL 1 YEAR), INTERVAL 1 DAY),
       DATE_SUB(@hoje, INTERVAL 120 DAY), DATE_SUB(@hoje, INTERVAL 111 DAY), FALSE, 0, 'CONCLUIDA',
       TIMESTAMP(DATE_SUB(@hoje, INTERVAL 200 DAY), '10:00:00'), TIMESTAMP(DATE_SUB(@hoje, INTERVAL 197 DAY), '10:00:00'), g.ID_GESTOR, 'APROVOU', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 198 DAY), '10:00:00'),
       rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 197 DAY), '10:00:00')
  FROM FUNCIONARIOS f
  JOIN FUNCIONARIOS gf ON gf.EMAIL_CORPORATIVO = CONCAT('beatriz.nogueira@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = gf.ID_FUNCIONARIO
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio)
 WHERE f.EMAIL_CORPORATIVO = CONCAT('thiago.mello@', @dominio);

-- ------------------------------------------------------------
-- 6. Beneficios: catalogo, adesoes, um dependente e um trajeto
-- ------------------------------------------------------------
INSERT INTO BENEFICIOS (NOME_BENEFICIO, DESCRICAO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO) VALUES
    ('Vale-refeição', 'Crédito mensal no cartão para refeições.', FALSE, FALSE, FALSE),
    ('Seguro de vida', 'Seguro de vida em grupo, pago pela empresa.', FALSE, FALSE, FALSE),
    ('Plano de saúde', 'Plano de saúde nacional, com opção de incluir dependentes.', TRUE, TRUE, FALSE),
    ('Plano odontológico', 'Cobertura odontológica, com opção de incluir dependentes.', TRUE, TRUE, FALSE),
    ('Vale-transporte', 'Auxílio para o deslocamento entre casa e trabalho.', TRUE, FALSE, TRUE),
    ('Auxílio-creche', 'Reembolso de creche ou pré-escola para quem tem filhos de até 6 anos.', TRUE, TRUE, FALSE);

-- Quem ja esta ativo: vale-refeicao e seguro de vida para todos que ja comecaram (o RH deu direto, sem pedido).
INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA)
SELECT f.ID_FUNCIONARIO, b.ID_BENEFICIO, TRUE, 'APROVADO', NULL, rh.ID_FUNCIONARIO, TIMESTAMP(f.DATA_ADMISSAO, '10:00:00')
  FROM FUNCIONARIOS f
  JOIN BENEFICIOS b ON b.NOME_BENEFICIO IN ('Vale-refeição', 'Seguro de vida')
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio)
 WHERE f.DATA_ADMISSAO <= @hoje;

-- Lucas: plano de saude aprovado, com uma filha dependente aprovada.
INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA, ID_FUNCIONARIO_RH, DATA_RESPOSTA)
SELECT f.ID_FUNCIONARIO, b.ID_BENEFICIO, TRUE, 'APROVADO', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 90 DAY), '09:00:00'), DATE_SUB(@hoje, INTERVAL 80 DAY), TRUE, rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 88 DAY), '14:00:00')
  FROM FUNCIONARIOS f
  JOIN BENEFICIOS b ON b.NOME_BENEFICIO = 'Plano de saúde'
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio)
 WHERE f.EMAIL_CORPORATIVO = CONCAT('lucas.pereira@', @dominio);

INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, DATA_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA)
SELECT bf.ID_BENEFICIO_FUNCIONARIOS, 'Helena Pereira', 'FILHA', '2019-05-21', 'APROVADO', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 90 DAY), '09:00:00'), rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 88 DAY), '14:00:00')
  FROM BENEFICIOS_FUNCIONARIOS bf
  JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = bf.ID_FUNCIONARIO AND f.EMAIL_CORPORATIVO = CONCAT('lucas.pereira@', @dominio)
  JOIN BENEFICIOS b ON b.ID_BENEFICIO = bf.ID_BENEFICIO AND b.NOME_BENEFICIO = 'Plano de saúde'
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio);

-- Diego: pediu o plano odontologico e espera o RH (aparece nas pendencias do RH).
INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA)
SELECT f.ID_FUNCIONARIO, b.ID_BENEFICIO, FALSE, 'PENDENTE', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 2 DAY), '16:10:00'), DATE_ADD(@hoje, INTERVAL 20 DAY), TRUE
  FROM FUNCIONARIOS f
  JOIN BENEFICIOS b ON b.NOME_BENEFICIO = 'Plano odontológico'
 WHERE f.EMAIL_CORPORATIVO = CONCAT('diego.farias@', @dominio);

-- Vanessa: vale-transporte aprovado, com o trajeto aprovado e um pedido de troca de trajeto pendente.
INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA, ID_FUNCIONARIO_RH, DATA_RESPOSTA)
SELECT f.ID_FUNCIONARIO, b.ID_BENEFICIO, TRUE, 'APROVADO', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 150 DAY), '09:00:00'), DATE_SUB(@hoje, INTERVAL 140 DAY), TRUE, rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 148 DAY), '14:00:00')
  FROM FUNCIONARIOS f
  JOIN BENEFICIOS b ON b.NOME_BENEFICIO = 'Vale-transporte'
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio)
 WHERE f.EMAIL_CORPORATIVO = CONCAT('vanessa.lima@', @dominio);

INSERT INTO TRAJETOS_VALE_TRANSPORTE (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, DATA_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA)
SELECT bf.ID_BENEFICIO_FUNCIONARIOS, 'Rua das Acácias, 120, Vila Mariana, São Paulo', 'Avenida Paulista, 1000, São Paulo', 'Ônibus 175T-10 e Metrô Linha 2', 'APROVADO', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 150 DAY), '09:00:00'), rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 148 DAY), '14:00:00')
  FROM BENEFICIOS_FUNCIONARIOS bf
  JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = bf.ID_FUNCIONARIO AND f.EMAIL_CORPORATIVO = CONCAT('vanessa.lima@', @dominio)
  JOIN BENEFICIOS b ON b.ID_BENEFICIO = bf.ID_BENEFICIO AND b.NOME_BENEFICIO = 'Vale-transporte'
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio);

INSERT INTO TRAJETOS_VALE_TRANSPORTE (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, DATA_PEDIDO)
SELECT bf.ID_BENEFICIO_FUNCIONARIOS, 'Rua Nova, 10, Mooca, São Paulo', 'Avenida Paulista, 1000, São Paulo', 'Metrô Linha 3 e Linha 2', 'PENDENTE', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 3 DAY), '08:40:00')
  FROM BENEFICIOS_FUNCIONARIOS bf
  JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = bf.ID_FUNCIONARIO AND f.EMAIL_CORPORATIVO = CONCAT('vanessa.lima@', @dominio)
  JOIN BENEFICIOS b ON b.ID_BENEFICIO = bf.ID_BENEFICIO AND b.NOME_BENEFICIO = 'Vale-transporte';

-- ------------------------------------------------------------
-- 7. Documentos pedidos pelo RH (ainda sem arquivo), uma advertencia e a excecao de pagamento do proximo mes
-- ------------------------------------------------------------
INSERT INTO FUNCIONARIOS_DOCUMENTOS (ID_FUNCIONARIO, TIPO_DOCUMENTO, DESCRICAO, STATUS_DOCUMENTO, DATA_PEDIDO)
SELECT f.ID_FUNCIONARIO, d.tipo, d.descricao, 'PENDENTE', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 2 DAY), '11:00:00')
  FROM (
        SELECT 'bruno.azevedo' AS local_, 'COMPROVANTE_DE_RESIDENCIA' AS tipo, 'Comprovante de residência dos últimos 3 meses.' AS descricao
        UNION ALL SELECT 'patricia.gomes', 'CARTEIRA_DE_TRABALHO', 'Cópia da carteira de trabalho (página da foto e do contrato).'
       ) d
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT(d.local_, '@', @dominio);

INSERT INTO ADVERTENCIAS (ID_FUNCIONARIO, ID_GESTOR, DATA_ADVERTENCIA, TIPO_ADVERTENCIA, MOTIVO, DESCRICAO, STATUS_ADVERTENCIA)
SELECT f.ID_FUNCIONARIO, g.ID_GESTOR, DATE_SUB(@hoje, INTERVAL 3 DAY), 'VERBAL', 'Atrasos', 'Dois atrasos no mesmo mês, fora da tolerância da empresa.', 'PENDENTE'
  FROM FUNCIONARIOS f
  JOIN FUNCIONARIOS gf ON gf.EMAIL_CORPORATIVO = CONCAT('rafael.costa@', @dominio)
  JOIN GESTORES g ON g.ID_FUNCIONARIO = gf.ID_FUNCIONARIO
 WHERE f.EMAIL_CORPORATIVO = CONCAT('diego.farias@', @dominio);

INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO)
SELECT YEAR(DATE_ADD(@hoje, INTERVAL 1 MONTH)), MONTH(DATE_ADD(@hoje, INTERVAL 1 MONTH)), DATE_ADD(DATE_SUB(DATE_ADD(@hoje, INTERVAL 1 MONTH), INTERVAL DAYOFMONTH(DATE_ADD(@hoje, INTERVAL 1 MONTH)) - 1 DAY), INTERVAL 7 DAY);

-- ------------------------------------------------------------
-- 8. Formularios: uma pesquisa para todos, uma avaliacao (os gestores avaliam a equipe) e o formulario de admissao da Julia
-- ------------------------------------------------------------
INSERT INTO FORMULARIOS (TITULO, DESCRICAO, TIPO_FORMULARIO, ID_FUNCIONARIO_AUTOR, DATA_CRIACAO, ATIVO, PUBLICO, DATA_PUBLICACAO, DATA_PRAZO, ANONIMO, LEMBRETE_AUTOMATICO)
SELECT x.titulo, x.descricao, x.tipo, rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 10 DAY), '09:00:00'), TRUE, 'PESSOAS', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 2 DAY), '08:00:00'), DATE_ADD(@hoje, INTERVAL x.prazo DAY), x.anonimo, TRUE
  FROM (
        SELECT 'Pesquisa de clima organizacional' AS titulo, 'Queremos saber como você se sente no trabalho. As respostas são anônimas.' AS descricao, 'PESQUISA' AS tipo, 12 AS prazo, TRUE AS anonimo
        UNION ALL SELECT 'Formulário de admissão', 'Informe seus dados para o cadastro: contato de emergência e dados bancários.', 'ADMISSAO', 15, FALSE
       ) x
  JOIN FUNCIONARIOS rh ON rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio);

INSERT INTO FORMULARIOS (TITULO, DESCRICAO, TIPO_FORMULARIO, ID_FUNCIONARIO_AUTOR, DATA_CRIACAO, ATIVO, SENTIDO_AVALIACAO, PUBLICO, DATA_PUBLICACAO, DATA_PRAZO, ANONIMO, LEMBRETE_AUTOMATICO)
SELECT 'Avaliação de desempenho, ciclo 2026.2', 'Cada gestor avalia as pessoas da própria equipe.', 'AVALIACAO', rh.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 10 DAY), '09:00:00'), TRUE, 'GESTOR_AVALIA_EQUIPE', 'PESSOAS', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 2 DAY), '08:00:00'), DATE_ADD(@hoje, INTERVAL 20 DAY), FALSE, TRUE
  FROM FUNCIONARIOS rh WHERE rh.EMAIL_CORPORATIVO = CONCAT('helena.martins@', @dominio);

INSERT INTO FORMULARIOS_PERGUNTAS (ID_FORMULARIO, TEXTO_PERGUNTA, TIPO_RESPOSTA, ORDEM, OBRIGATORIA)
SELECT fo.ID_FORMULARIO, q.texto, q.tipo, q.ordem, q.obrigatoria
  FROM FORMULARIOS fo
  JOIN (
        SELECT 'Pesquisa de clima organizacional' AS titulo, 'Como você avalia o clima da sua equipe?' AS texto, 'ESCALA' AS tipo, 1 AS ordem, TRUE AS obrigatoria
        UNION ALL SELECT 'Pesquisa de clima organizacional', 'O que mais ajudaria o seu dia a dia?', 'RESPOSTA_LONGA', 2, FALSE
        UNION ALL SELECT 'Formulário de admissão', 'Nome e telefone de um contato de emergência', 'RESPOSTA_CURTA', 1, TRUE
        UNION ALL SELECT 'Formulário de admissão', 'Banco, agência e conta para o pagamento do salário', 'RESPOSTA_CURTA', 2, TRUE
        UNION ALL SELECT 'Avaliação de desempenho, ciclo 2026.2', 'Qualidade das entregas no ciclo', 'ESCALA', 1, TRUE
        UNION ALL SELECT 'Avaliação de desempenho, ciclo 2026.2', 'Comentários e pontos de desenvolvimento', 'RESPOSTA_LONGA', 2, FALSE
       ) q ON q.titulo = fo.TITULO;

-- Quem deve responder: a pesquisa, todos que ja comecaram; a admissao, so a Julia; a avaliacao, os dois gestores.
INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO)
SELECT fo.ID_FORMULARIO, f.ID_FUNCIONARIO
  FROM FORMULARIOS fo
  JOIN FUNCIONARIOS f ON f.DATA_ADMISSAO <= @hoje
 WHERE fo.TITULO = 'Pesquisa de clima organizacional';

INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO)
SELECT fo.ID_FORMULARIO, f.ID_FUNCIONARIO
  FROM FORMULARIOS fo
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('julia.andrade@', @dominio)
 WHERE fo.TITULO = 'Formulário de admissão';

INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO)
SELECT fo.ID_FORMULARIO, f.ID_FUNCIONARIO
  FROM FORMULARIOS fo
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO IN (CONCAT('rafael.costa@', @dominio), CONCAT('beatriz.nogueira@', @dominio))
 WHERE fo.TITULO = 'Avaliação de desempenho, ciclo 2026.2';

-- ------------------------------------------------------------
-- 9. Treinamentos: um obrigatorio (com uma pessoa em andamento e uma que concluiu e ganhou o certificado) e o de integracao
-- ------------------------------------------------------------
INSERT INTO TREINAMENTOS (TITULO, DESCRICAO, TIPO_TREINAMENTO, DATA_INICIO, DATA_FIM, OBRIGATORIO, PUBLICADO, PUBLICO, EMITE_CERTIFICADO, INCLUI_ADMITIDOS)
VALUES
    ('Segurança da informação e LGPD', 'Boas práticas para proteger os dados da empresa e das pessoas.', NULL, DATE_SUB(@hoje, INTERVAL 20 DAY), DATE_ADD(@hoje, INTERVAL 40 DAY), TRUE, TRUE, 'TODOS', TRUE, TRUE),
    ('Integração de novos colaboradores', 'Conheça a empresa, as pessoas e as ferramentas do dia a dia.', 'ONBOARDING', DATE_SUB(@hoje, INTERVAL 60 DAY), NULL, FALSE, TRUE, 'TODOS', FALSE, TRUE);

INSERT INTO TREINAMENTOS_MODULOS (ID_TREINAMENTO, ORDEM, TITULO, TIPO_MODULO, DURACAO_MINUTOS, URL_EXTERNA, CONTEUDO_TEXTO)
SELECT t.ID_TREINAMENTO, m.ordem, m.titulo, m.tipo, m.duracao, m.url, m.texto
  FROM TREINAMENTOS t
  JOIN (
        SELECT 'Segurança da informação e LGPD' AS titulo_t, 1 AS ordem, 'Por que proteger os dados' AS titulo, 'TEXTO' AS tipo, 8 AS duracao, NULL AS url, 'Dados pessoais são de quem os fornece. A empresa só pode usá-los para o que informou e precisa guardá-los com cuidado.' AS texto
        UNION ALL SELECT 'Segurança da informação e LGPD', 2, 'Cartilha de boas práticas', 'LINK', 15, 'https://www.gov.br/anpd/pt-br', NULL
        UNION ALL SELECT 'Segurança da informação e LGPD', 3, 'Senhas e acessos', 'TEXTO', 6, NULL, 'Use senhas longas, diferentes para cada sistema, e nunca compartilhe a sua com ninguém, nem com o RH ou o suporte.'
        UNION ALL SELECT 'Integração de novos colaboradores', 1, 'Bem-vindo à empresa', 'TEXTO', 10, NULL, 'Aqui você encontra como a empresa funciona, quem são as pessoas de cada setor e onde pedir ajuda.'
       ) m ON m.titulo_t = t.TITULO;

-- Quem faz o treinamento obrigatorio: todos que ja comecaram. O Lucas fez o modulo 1; a Camila fez os tres e concluiu; os demais estao inscritos.
INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO)
SELECT t.ID_TREINAMENTO, f.ID_FUNCIONARIO,
       CASE f.EMAIL_CORPORATIVO WHEN CONCAT('lucas.pereira@', @dominio) THEN 'EM_ANDAMENTO' ELSE 'INSCRITO' END,
       CASE WHEN f.EMAIL_CORPORATIVO = CONCAT('lucas.pereira@', @dominio) THEN TIMESTAMP(DATE_SUB(@hoje, INTERVAL 3 DAY), '10:00:00') ELSE NULL END
  FROM TREINAMENTOS t
  JOIN FUNCIONARIOS f ON f.DATA_ADMISSAO <= @hoje AND f.EMAIL_CORPORATIVO <> CONCAT('camila.duarte@', @dominio)
 WHERE t.TITULO = 'Segurança da informação e LGPD';

INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO)
SELECT t.ID_TREINAMENTO, f.ID_FUNCIONARIO, 'EM_ANDAMENTO', TIMESTAMP(DATE_SUB(@hoje, INTERVAL 5 DAY), '10:00:00')
  FROM TREINAMENTOS t
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('camila.duarte@', @dominio)
 WHERE t.TITULO = 'Segurança da informação e LGPD';

INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO)
SELECT m.ID_MODULO, f.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 3 DAY), '10:30:00')
  FROM TREINAMENTOS_MODULOS m
  JOIN TREINAMENTOS t ON t.ID_TREINAMENTO = m.ID_TREINAMENTO AND t.TITULO = 'Segurança da informação e LGPD'
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('lucas.pereira@', @dominio)
 WHERE m.ORDEM = 1;

INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO)
SELECT m.ID_MODULO, f.ID_FUNCIONARIO, TIMESTAMP(DATE_SUB(@hoje, INTERVAL 5 DAY), ADDTIME('10:00:00', SEC_TO_TIME(m.ORDEM * 600)))
  FROM TREINAMENTOS_MODULOS m
  JOIN TREINAMENTOS t ON t.ID_TREINAMENTO = m.ID_TREINAMENTO AND t.TITULO = 'Segurança da informação e LGPD'
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('camila.duarte@', @dominio);

-- A conclusao da Camila: o trigger do banco cria a linha do certificado; o codigo dele e um UUID v1, e a API so confere codigo v4 (como no fluxo real,
-- em que a aplicacao troca o codigo na mesma transacao). O PDF nasce no primeiro download.
UPDATE TREINAMENTOS_FUNCIONARIOS tf
  JOIN TREINAMENTOS t ON t.ID_TREINAMENTO = tf.ID_TREINAMENTO AND t.TITULO = 'Segurança da informação e LGPD'
  JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = tf.ID_FUNCIONARIO AND f.EMAIL_CORPORATIVO = CONCAT('camila.duarte@', @dominio)
   SET tf.STATUS_TREINAMENTO = 'CONCLUIDO', tf.DATA_CONCLUSAO = TIMESTAMP(DATE_SUB(@hoje, INTERVAL 5 DAY), '10:40:00');

UPDATE TREINAMENTOS_CERTIFICADOS c
  JOIN TREINAMENTOS t ON t.ID_TREINAMENTO = c.ID_TREINAMENTO AND t.TITULO = 'Segurança da informação e LGPD'
   SET c.CODIGO_VALIDACAO = LOWER(CONCAT(HEX(RANDOM_BYTES(4)), '-', HEX(RANDOM_BYTES(2)), '-4', SUBSTR(HEX(RANDOM_BYTES(2)), 2), '-', ELT(1 + FLOOR(RAND() * 4), '8', '9', 'a', 'b'), SUBSTR(HEX(RANDOM_BYTES(2)), 2), '-', HEX(RANDOM_BYTES(6))));

-- A Julia ainda nao comecou: o treinamento de integracao ja a espera.
INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO)
SELECT t.ID_TREINAMENTO, f.ID_FUNCIONARIO, 'INSCRITO'
  FROM TREINAMENTOS t
  JOIN FUNCIONARIOS f ON f.EMAIL_CORPORATIVO = CONCAT('julia.andrade@', @dominio)
 WHERE t.TITULO = 'Integração de novos colaboradores';

DROP TABLE TMP_DEMO_PONTOS;
DROP TABLE TMP_DEMO_DIAS;
DROP TABLE TMP_DEMO_PESSOAS;
