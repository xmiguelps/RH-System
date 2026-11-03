-- ============================================================
-- V031 - Beneficios: as tres marcas do catalogo
-- Fatia 1 (contrato: BeneficioCatalogoItem e BeneficioCatalogoComando)
-- ============================================================
-- POR QUE
--   O dono decidiu em 2026-10-30 que o catalogo de beneficios ganha tres
--   marcas, que o RH liga em Cadastros, Beneficios, e que a fatia 7 usa na
--   tela "Beneficios" do funcionario:
--   - OPCIONAL: a propria pessoa pode pedir o beneficio (e o que aparece em
--     "Disponiveis para voce"). Falso: so o RH o concede.
--   - ACEITA_DEPENDENTES: o pedido deixa incluir dependentes (plano de saude).
--   - ACEITA_TRAJETO: o pedido pede o trajeto (vale-transporte).
--   Hoje BENEFICIOS so tem nome e descricao (Criacao_Tabelas, secao 22).
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta as tres colunas, todas BOOLEAN NOT NULL com padrao FALSO.
--
-- POR QUE O PADRAO E FALSO (e nenhum UPDATE no fim)
--   Beneficio que ja existe fica "so o RH concede, sem dependentes e sem
--   trajeto", que e o comportamento de antes e o mais restritivo: ninguem
--   passa a poder pedir um beneficio sem o RH ter marcado. Quem quiser os
--   beneficios opcionais da demonstracao (3 e 6) marca pela tela ou pela
--   carga da fatia 8. Como o padrao e FALSO e a aplicacao grava FALSO,
--   nao existe o risco do EF Core omitir o valor (LEIA-ME, secao do EF).
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - So beneficio OPCIONAL pode ser pedido pela pessoa (fatia 7).
--   - Dependentes so em beneficio com ACEITA_DEPENDENTES, trajeto so em
--     beneficio com ACEITA_TRAJETO: sao regras entre tabelas e ficam na
--     aplicacao, como nas tabelas V024 e V025.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE BENEFICIOS
    ADD COLUMN OPCIONAL BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ACEITA_DEPENDENTES BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ACEITA_TRAJETO BOOLEAN NOT NULL DEFAULT FALSE;
