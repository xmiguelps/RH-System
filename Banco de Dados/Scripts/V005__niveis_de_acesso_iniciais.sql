-- ============================================================
-- V005 - Niveis de acesso iniciais (NIVEIS_DE_ACESSO)
-- Fatia 0 (login: o perfil do token vem do nivel de acesso do funcionario)
-- ============================================================
-- POR QUE
--   O login mapeia o NOME do nivel de acesso do funcionario para o perfil
--   da API: 'RH' vira Perfil.RH, 'Funcionario' vira Perfil.Funcionario e
--   'Funcionario Gestor' vira Perfil.Gestor (nomes confirmados pelo dono em
--   2026-10-28). Funcionario sem nivel ou com nivel desconhecido nao entra.
--
-- O QUE ESTE SCRIPT FAZ
--   Semeia os tres niveis com INSERT IGNORE: a chave unica de
--   NOME_NIVEL_ACESSO faz o IGNORE pular um nivel que o grupo ja tenha
--   semeado antes, em vez de derrubar a versao.
--
-- TABELA DO GRUPO: so dados; nenhuma coluna e nenhum trigger muda.
-- ============================================================

INSERT IGNORE INTO NIVEIS_DE_ACESSO (NOME_NIVEL_ACESSO, DESCRICAO) VALUES
    ('RH', 'Acesso total as rotinas de RH: cadastros, ponto, ferias, holerites, advertencias e auditoria'),
    ('Funcionario', 'Acesso pessoal: o proprio ponto, ferias, holerites, avisos, formularios e treinamentos'),
    ('Funcionario Gestor', 'Acesso pessoal e da equipe do proprio setor: aprovacoes, formularios e treinamentos da equipe');
