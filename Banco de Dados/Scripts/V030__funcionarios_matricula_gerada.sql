-- ============================================================
-- V030 - Matricula gerada pelo sistema: contador (SEQUENCIAS) e
--        formato da matricula em FUNCIONARIOS
-- Fatia 1 (contrato: FuncionarioFichaRh.matricula)
-- ============================================================
-- POR QUE
--   O contrato diz que a matricula e GERADA PELO SISTEMA e so se le:
--   texto de 5 digitos, sequencial, com zero a esquerda (00482). Ela nao
--   vem no corpo de criar nem de editar, e a importacao por planilha
--   (fatia 2) tambem a gera. FUNCIONARIOS.MATRICULA existe desde o V006
--   (texto, unica, aceita NULL), mas nada gera o proximo numero. Sem um
--   contador, duas pessoas cadastradas ao mesmo tempo disputariam o mesmo
--   numero (a segunda bateria na chave unica) ou a aplicacao teria de
--   adivinhar o maior.
--
-- O QUE ESTE SCRIPT FAZ
--   1) Acrescenta o CHECK CHK_FUNCIONARIOS_MATRICULA: a matricula e nula
--      ou tem exatamente 5 digitos.
--   2) Cria SEQUENCIAS, um contador por nome (uma linha por sequencia) e
--      ja grava a linha MATRICULA. O valor inicial e a maior matricula
--      numerica que ja existir em FUNCIONARIOS (0 se nao houver).
--   A ORDEM E DE PROPOSITO: o ALTER do passo 1 e o unico que pode falhar
--   por causa de dados antigos (uma matricula fora do formato). O DDL do
--   MySQL nao desfaz, entao, se ele falhar, nada foi criado ainda e o
--   script pode ser repetido depois de corrigir a matricula.
--
-- COMO A APLICACAO PEGA O PROXIMO NUMERO
--   Dentro da MESMA transacao que grava o funcionario, na MESMA conexao:
--     UPDATE SEQUENCIAS SET ULTIMO_VALOR = LAST_INSERT_ID(ULTIMO_VALOR + 1)
--      WHERE NOME = "MATRICULA"
--     SELECT LAST_INSERT_ID()
--   e formata com LPAD(numero, 5, "0"). A linha do contador fica travada
--   ate o COMMIT, entao dois cadastros simultaneos esperam um pelo outro
--   e nunca recebem o mesmo numero. Se a transacao desfizer, o contador
--   desfaz junto: NAO ha buraco na numeracao (um AUTO_INCREMENT, ao
--   contrario, gastaria o numero de um cadastro recusado). Para reservar
--   N numeros de uma vez (importacao de 500 linhas), some N em vez de 1:
--   os numeros sao do (valor devolvido - N + 1) ate o valor devolvido.
--
-- DECISOES QUE VOCE PODE REVER
--   1) O CHECK de 5 digitos vem do contrato (decidido pelo dono em
--      2026-10-30). Passando de 99999 funcionarios o cadastro seria
--      recusado em voz alta, que e melhor que gravar uma matricula de
--      outro tamanho em silencio. Se o dono mudar o formato, troque o CHECK.
--   2) As linhas que ja existem e estao sem matricula (o administrador
--      criado pelo seed da fatia 0, por exemplo) FICAM SEM matricula: este
--      script nao numera ninguem, porque o contrato aceita matricula nula
--      e no primeiro deploy nao ha funcionario nenhum. Quem cria o
--      administrador pode pegar um numero do contador como qualquer cadastro.
--   3) Matricula informada a mao (a carga da demonstracao, fatia 8) nao
--      move o contador. Depois da carga, ajuste ULTIMO_VALOR para o maior
--      numero usado, senao o proximo cadastro bate na chave unica.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT E A TABELA NOVA NAO TEM TRIGGER.
-- ============================================================

ALTER TABLE FUNCIONARIOS
    ADD CONSTRAINT CHK_FUNCIONARIOS_MATRICULA
        CHECK (MATRICULA IS NULL OR MATRICULA REGEXP '^[0-9]{5}$');

CREATE TABLE SEQUENCIAS (
    NOME VARCHAR(50) NOT NULL PRIMARY KEY,
    ULTIMO_VALOR BIGINT NOT NULL DEFAULT 0,

    -- o nome e um codigo em maiusculas (o REGEXP_LIKE com o 3o argumento
    -- "c" diferencia maiuscula de minuscula, ao contrario da collation)
    CONSTRAINT CHK_SEQUENCIAS_NOME CHECK (REGEXP_LIKE(NOME, '^[A-Z][A-Z0-9_]*$', 'c')),
    CONSTRAINT CHK_SEQUENCIAS_ULTIMO_VALOR CHECK (ULTIMO_VALOR >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO SEQUENCIAS (NOME, ULTIMO_VALOR)
SELECT 'MATRICULA', COALESCE(MAX(CAST(MATRICULA AS UNSIGNED)), 0)
  FROM FUNCIONARIOS
 WHERE MATRICULA REGEXP '^[0-9]+$';
