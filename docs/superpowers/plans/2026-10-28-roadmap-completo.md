# Roadmap completo do RH-System (fatias 0 a 8)

Data: 2026-10-28 · Status: rascunho para revisão do dono, já revisado uma vez pelo `revisor-de-plano` (achados de engenharia incorporados); atualizado em 2026-10-29 com as decisões das Partes 3 e 4 (formulários, treinamentos, férias, benefícios, documentos e atestado), ainda **sem nova revisão** · Spec: `docs/superpowers/specs/2026-10-21-rh-system-design.md` · Plano detalhado da fatia 0: `docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md`

## Como usar este documento

- Isto **não é um plano de tarefas.** Cada fatia ganha o seu plano passo a passo (skill `writing-plans`) **no começo dela**, revisado pelo `revisor-de-plano`, porque o plano detalhado depende de código e de um banco que ainda não existem.
- A ordem segue as dependências. **Ordem de corte** (só se um prazo passar a existir) é decisão do dono: a fatia 8 e a tela inicial mínima por perfil não se cortam. Desde 2026-10-29 a 6 não depende mais da 5 (sem quiz).
- **Toda fatia termina com:** testes passando (unidade, integração e a matriz de perfis), CI verde, deploy no Azure, roteiro da demo atualizado e as mudanças de banco registradas em scripts novos.
- **Scripts SQL:** depois do primeiro deploy (fim da fatia 0) todo script aplicado é congelado. Cada necessidade abaixo vira um **script novo**, numerado pelo dono: `ALTER` para tabelas, `DROP` e `CREATE` para triggers e funções. Os nomes abaixo são descritivos.
- **Os triggers do grupo guardam regras de negócio** (estados finais, nota mínima, quem pode aprovar, quem recebe cada aviso). Várias coisas que o Figma pede **são proibidas por esses triggers** e exigem reescrevê-los; a lista está na seção "Antes do congelamento".
- **Telas:** os ids são das seções da página "Alta Complexidade" do Figma (arquivo `bsZnCjHNeNgdGaLzVcaxjL`). A contagem fecha: 9 + 8 + 16 + 9 + 8 + 11 = 61 (32 do RH, 14 do funcionário, 15 do gestor). Só li os **textos**; as capturas de tela entram na fundação do front-end.

## Panorama

| # | Fatia | Entrega demonstrável | Telas | Depende de |
|---|---|---|---|---|
| 0 | Esqueleto | Login, shell, banco por scripts, CI, deploy no Azure | 0 | |
| 1 | Colaboradores e base transversal | RH cadastra, edita e inativa; convite; auditoria; escopo por perfil | 9 | 0 |
| 2 | Importação por Excel | RH cadastra vários de uma vez, com prévia de erros | 0 | 1 |
| 3 | Ponto | Bater ponto, espelho, ajustes em duas etapas, feriados, banco de horas | 8 | 1 |
| 4 | Comunicação e documentos | Avisos, holerites, advertências, aniversariantes | 16 | 1 |
| 5 | Formulários | Criar e responder formulários (inclusive anônimos), entrevista por link público, avaliações | 9 | 1 |
| 6 | TAD | Treinamentos com módulos, progresso, certificado em PDF | 8 | 1 |
| 7 | Gestão | Férias, benefícios, pagamento e calendário (7a a 7c); painéis (7d) | 11 | 3 e 4 (7d: 3 a 6) |
| 8 | Fechamento | DER, seed da demo, testes de ponta a ponta, ensaio da banca | 0 | todas |

## Antes do congelamento: lista para o grupo

Até o primeiro deploy (fim da fatia 0) o grupo ainda pode editar a base direto, o que é mais barato do que um script novo depois. **Depois do congelamento nada fica proibido**: cada item vira um script novo. **Já decidido pelo dono (2026-10-28): sem unidades; aprovação de férias e de ajuste de ponto em duas etapas (gestor e depois RH).** O que não existir no esquema de 2026-10-28 e o Figma pedir:

**A. Colunas e restrições**
- `FUNCIONARIOS`: matrícula e tipo de contrato (serão criados pelo Kauan, que também aparece como Kafly21 nos commits). A jornada vem da **carga semanal do cargo** e do **horário padrão da empresa** (um só para todos, ver "Configuração da empresa"), com atraso medido contra ele (decisão de 2026-10-28). A situação "Em admissão" **não precisa de coluna**: é o funcionário com data de admissão no futuro. **Sem período de experiência** (decisão de 2026-10-28): a etiqueta "Experiência" e o aviso de fim de experiência ficam escondidos.
- `PONTOS_MARCACOES`: origem (sistema ou corrigida pelo RH). `PONTOS_MANUTENCOES`: a segunda etapa; a correção pedida (data e hora e tipo propostos) vai numa **tabela nova de itens do pedido**, com **várias batidas por pedido** (dono, 2026-10-29) e um dia por pedido (Figma, telas 13 e 55: a correção é uma batida ou um período, entrada e saída).
- `BANCOS_HORAS`: **dia de referência**, com chave única por funcionário e dia, para a consolidação não duplicar e poder **recalcular um dia** depois de um ajuste aprovado. **Só lançamentos automáticos** (decisão de 2026-10-28), então não há coluna de origem.
- `ADVERTENCIAS`: **com contestação** (decisão de 2026-10-28): data da ciência, texto e data da contestação, e o status novo `CONTESTADA` no `CHECK`. O tipo verbal ou escrita já cabe em `TIPO_ADVERTENCIA`; o trigger de UPDATE já permite essas transições; a resposta do RH vai por e-mail e o Kauan decide se a guarda.
- `HOLERITES`: **com tipo** (mensal, 13º, férias; script `V012`) e **sem "visto"** (decisão de 2026-10-28). O `V012` ajusta a chave única `(funcionário, ano, mês)`, que hoje impede dois holerites no mesmo mês.
- `AVISOS`: **fixar no topo** e **rascunho** (o Kauan faz). **Sem público por setor e sem anexo PDF** (decisão de 2026-10-28). O e-mail é **sempre** enviado na publicação, então não precisa de coluna.
- `FORMULARIOS`: **sem tipo quiz** (decisão de 2026-10-29; o `CHECK` aceita `ENTREVISTA`, `ADMISSAO`, `AVALIACAO`, `PESQUISA` e `OUTRO`; o tipo sugestão usa `OUTRO`, decidido em 2026-10-29), publicação, prazo, **anônimo (o RH marca)**, link público. **Sem nota mínima.** `FORMULARIOS_RESPOSTAS`: respondente externo e resposta anônima, porque `ID_FUNCIONARIO` é `NOT NULL`.
- `FERIAS`: segunda etapa, adiantamento do 13º e "sugerir outra data" (os dois últimos mantidos pelo dono em 2026-10-29).
- **Configuração da empresa** (tabela nova): nome, UF, município e o **horário padrão** (entrada, saída e tolerância de atraso; tabela `CONFIGURACOES_EMPRESA`, script `V007`). A UF escolhe os feriados estaduais e o nome aparece no menu. Como **não há unidades**, os feriados municipais valem para todos e a tabela `FERIADOS` não precisa mudar por isso.

**B. Triggers que precisam ser reescritos (o Figma pede o que eles proíbem)** — **adiado: por enquanto o dono não quer tratar os triggers (2026-10-28)**, salvo a correção do Kauan nos de ponto.
- `TRG_AVISOS_AFTER_INSERT` cria uma linha de leitura para **todo funcionário ativo** no INSERT. Isso **serve**, porque o aviso agora é sempre para todos. O rascunho (coluna `ENVIADO` = não, decidido em 2026-10-29) também cria as linhas, o que é inofensivo; quem for admitido depois fica sem linha; a aplicação lista só `ENVIADO` = sim, não usa `FN_CONTAR_AVISOS_NAO_LIDOS` (ela contaria o rascunho) e mostra os avisos vigentes sem depender da linha de leitura.
- `PONTOS_MANUTENCOES` e `FERIAS`: em `PONTOS_MANUTENCOES` qualquer status diferente de `PENDENTE` é final (`Criacao_Triggers:498-502`) e em `FERIAS` só valem as transições de hoje (`:913-921`); aprovar ou recusar exige um `ID_GESTOR` (que não seja a própria pessoa). **Decidido em 2026-10-29:** esses triggers **não** são reescritos agora. A segunda etapa (gestor e depois RH) fica em colunas novas, os status continuam os de hoje e só mudam na decisão final, o que também mantém funcionando o trigger de inativação (`TRG_FUNCIONARIOS_AFTER_UPDATE`, `:117-140`, que lista os status de férias). Se o gestor estiver ausente, o RH liga uma chave que faz o gestor aprovar automaticamente; não há "RH aprova direto", exceto nos pedidos de quem é gestor: eles não têm etapa do gestor, só o RH decide, e quem do RH decide precisa estar também em `GESTORES`, porque o trigger exige um `ID_GESTOR` diferente da própria pessoa. Se o gestor recusa, o pedido acaba na hora. Uma reescrita para status próprios pode entrar depois do primeiro deploy, como script novo.
- `TRG_HOLERITES_BEFORE_INSERT` recusa competência futura; um holerite de férias pago antes do mês seria barrado. **Decidido em 2026-10-29:** o Kauan ajusta o trigger para aceitar competência futura no tipo férias (item 9 da lista), e o holerite errado guarda versões.
- `TRG_TREINAMENTOS_FUNCIONARIOS_*`: **sem nota** (decisão de 2026-10-29), então a regra de nota 70 deixa de atrapalhar: `CONCLUIDO` com nota vazia passa (só recusa nota preenchida abaixo de 70, `Criacao_Triggers:694` e `:715`, lidos em 2026-10-29). Os mesmos triggers criam o certificado na hora do `CONCLUIDO`, com `CODIGO_VALIDACAO = UUID()` e sem arquivo (`:729-747`); um INSERT da aplicação colide com a chave única.
- `FORMULARIOS_RESPOSTAS`: o candidato externo e a resposta anônima (**ambos decididos em 2026-10-29**) não cabem hoje porque `ID_FUNCIONARIO` é `NOT NULL`. O `TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT` só recusa formulário inativo, funcionário inativo e pergunta obrigatória vazia (`Criacao_Triggers:799-837`, relido em 2026-10-29). Texto anterior corrigido: o trigger não "exige funcionário ativo" para aceitar a resposta.
- `TRG_FUNCIONARIOS_*` e a regra do gestor do setor (ver fatia 1).

**C. Semântica a alinhar**
- **Decidido pelo dono (2026-10-28):** `CARGOS.CARGA_HORARIA` passa a ser **semanal** ("44 h", como no Figma); hoje está por dia. O trigger que a copia para `PONTOS.CARGA_HORARIA` (que é por dia) **está sendo corrigido pelo Kauan**. Conferir o resultado quando ele enviar.
- Nota de treinamento: **não haverá nota** (decisão de 2026-10-29); o banco fica como está e a escala (0 a 100 no banco, 0 a 10 no Figma) deixa de importar.
- Inativar funcionário também desativa todos os benefícios e cancela férias futuras (`SOLICITADA` ou `APROVADA`), sem avisar a pessoa. **Mantido assim** (dono, 2026-10-28).

**D. Decidido em 2026-10-29 (Partes 3 e 4).** O detalhe e a finalidade de cada item estão em `docs/pedidos-para-o-kauan.md` (itens 12 a 25).
- **Colunas novas:** `FORMULARIOS` (publicação, prazo, anônimo, link público); `FERIAS` (13º adiantado e "sugerir outra data"); etapa do gestor e chave de aprovação automática (`FERIAS`, `PONTOS_MANUTENCOES` e `GESTORES`); `BENEFICIOS_FUNCIONARIOS` (pedido de adesão); `TREINAMENTOS` (`PUBLICADO` e o público; rascunho e acesso liberado na data de início, dono, 2026-10-29); dia do pagamento na configuração da empresa.
- **Tabelas novas (finalidade aprovada):** documentos do colaborador, atestados, dependentes, trajeto do vale-transporte, módulos do treinamento, progresso por módulo, alternativas das perguntas, quem deve responder, o registro de participação em formulário anônimo e as exceções de pagamento (mês e data). **Formulário recorrente:** sem histórico de rodadas (dono, 2026-10-29). **Quem cria:** os scripts foram escritos pelo lado da aplicação a pedido do dono (`Banco de Dados/Scripts/V006` a `V029`, testados num MySQL 8.4) e o Kauan confere depois; sem triggers nas tabelas novas. Os itens 1 e 2 da lista (triggers de ponto) seguem com o Kauan, e o `V002` do grupo não aplica sem eles.
- **Cortado:** tipo quiz, prova final, nota de treinamento, turmas presenciais, pergunta eliminatória, "nota dada pelo RH" e "resposta certa e pontos".

## Decisões que atravessam fatias

1. **Fundação do front-end (começo da fatia 1).** Ver as capturas das telas principais, extrair os tokens de design do Figma, escolher rotas, formulários e UI (ou nenhuma) e montar os componentes-base. O spec adiou essas escolhas para depois de ver o Figma.
2. **E-mail.** Provedor (serviço de e-mail do Azure ou SMTP; só o custo e a conta são do dono). Regras: fila com limite de vazão (500 convites de uma vez não podem sair juntos), tratamento de devolução (os e-mails fictícios do seed e da importação), **limite de tentativas e resposta uniforme no "esqueci minha senha"** (o limitador da fatia 0 só cobre o login).
3. **Agendador (fatia 1).** Serviço em segundo plano na API, usado nas fatias 3 a 7. Como o ambiente pode ficar parado entre sessões, **cada tarefa recupera o que ficou para trás** ao subir (consolida vários dias, publica o que venceu) e é idempotente. Exige Always On.
4. **Fuso horário.** Os triggers usam `NOW()` e `CURDATE()` no fuso do servidor; a aplicação usa UTC. Política proposta: **instantes em UTC; datas de negócio e o `time_zone` do servidor MySQL em -03:00** (Brasil sem horário de verão). Sem isso, o admin do seed criado às 22h recebe "admissão amanhã" e a batida das 22h30 é recusada. Conferir o valor padrão do Flexible Server.
5. **Auditoria.** O interceptor do EF não enxerga o que os triggers mudam nem `ExecuteUpdate`/`ExecuteDelete`. Regra: **efeitos de trigger e operações em lote geram eventos de auditoria explícitos**; e nenhum caminho de negócio usa `ExecuteUpdate` sem registrar. **Exceção decidida pelo dono (2026-10-28):** os cancelamentos automáticos da inativação de funcionário não têm registro próprio.
6. **Conteúdo escrito por usuário** (avisos detalhados, perguntas de formulário, mensagens): mostrado como texto ou markdown sanitizado, **nunca HTML cru**. Importa porque o refresh token está no `localStorage` e um XSS viraria tomada de conta.
7. **Arquivos e Azure.** Storage Account (Blob) e o recurso de e-mail são criados **na fatia em que aparecem** (documentos na 1c), com custo dito antes; o `docker compose` ganha Azurite e o captador de e-mail nessa fatia. Upload só depois de a regra do banco aprovar; sobras são limpas por um job. Download sempre por link temporário e como anexo.
8. **PDF** (espelho na fatia 3, certificado na 6): uma biblioteca de licença conferida, com fontes que tenham acento, testada em Linux e com pouca memória.
9. **Exportações CSV** (auditoria, folha, respostas, listas): células que começam com `=`, `+`, `-` ou `@` são neutralizadas.
10. **Mensagens de trigger** são técnicas e sem acento; cada fatia decide como traduzi-las (a API já devolve um texto genérico e a regra original numa extensão).
11. **Funções do grupo:** cada fatia decide se a aplicação as chama ou as reimplementa. O spec manda confirmar com o grupo; para a banca, usar o trabalho do grupo pode pesar.
12. **Mapeamento do EF Core para os scripts `V006` a `V029` (sonda obrigatória na fatia 0 ou 1).** Três armadilhas, todas **inferidas e não testadas** (achado da revisão de 2026-10-29): (a) colunas com padrão no banco que a aplicação precisa gravar com o valor "falso" (`FORMULARIOS.ATIVO`, `EMITE_CERTIFICADO` e as caixas de `TREINAMENTOS`): se o mapeamento declarar o padrão do banco, o EF pode **omitir** o `false` e o banco gravar `TRUE` sem nenhum erro (o formulário em rascunho nasceria aceitando resposta); regra: **não declarar padrão do banco nessas colunas**, para o EF sempre enviar o valor; (b) colunas com `DEFAULT CURRENT_TIMESTAMP` (as tabelas novas não têm trigger): mapear como geradas na inclusão, senão o EF manda `0001-01-01`; (c) colunas **calculadas** (`CHAVE_DESTINO` em `FORMULARIOS_DESTINATARIOS` e `TREINAMENTOS_PUBLICO`, `CHAVE_AVALIADO` em `FORMULARIOS_PARTICIPACOES`): mapear como calculadas (fora do `INSERT`). **Sonda:** um teste de integração que grave pelo EF um formulário com `Ativo = false`, um treinamento com `EmiteCertificado = false`, um documento sem data e uma participação, e releia os quatro. O teste de "toda coluna obrigatória sem padrão está mapeada" (spec) vai apontar `CHAVE_AVALIADO` e `CHAVE_DESTINO` se não forem marcadas como calculadas.
13. **Privacidade nas tabelas novas.** Guardam dado pessoal de quem não é funcionário (candidatos e currículo), de menores (dependentes), endereço de casa (trajeto do vale-transporte) e dado de saúde (atestado, arquivo). Regras: **a auditoria "antes e depois" não grava o valor** de colunas sensíveis (origem do trajeto, dados de candidato, autor e hora de resposta anônima, arquivo de atestado); candidatos têm **prazo de retenção** e um job que apaga os vencidos, com aviso de privacidade; o log guarda a autorização de desconto em folha de adesão anterior (V021).
14. **Telas que não existem no Figma e eu desenho em estilo simples:** login (fatia 0), convite e redefinição de senha e "Meus documentos" do funcionário (fatia 1), importação por Excel (fatia 2), atestado (fatia 3), formulário público do candidato (fatia 5). **O gestor reutiliza as telas do funcionário** (formulários, treinamentos, benefícios e advertências, que o Figma não desenha para ele), com o menu do gestor.

---

## Fatia 0: esqueleto

Plano detalhado pronto (14 tarefas). Para começar: Docker e Node 22+ instalados, conferências de documentação do Azure, conta com créditos confirmada, o teste da Task 4 contra os scripts reais do grupo e a lista da seção "Antes do congelamento" fechada com o grupo (a Task 14 congela a base).

---

## Fatia 1: colaboradores e base transversal

**Entrega:** o RH cadastra, edita, inativa e consulta colaboradores; a pessoa recebe um convite e define a senha; tudo o que muda fica no log; cada perfil vê só o que pode.

**Incrementos:**
- **1a. Fundação técnica** (sem tela nova para o usuário): fundação do front-end, escopo por perfil (linhas e campos), auditoria, fila de e-mail, agendador, tela inicial mínima por perfil (saudação e menu).
- **1b. Cadastro, ficha e acesso:** lista, adicionar, ficha (aba Dados), editar, inativar, convite, redefinição e troca de senha. É o primeiro fluxo visível e traz a matriz de perfis por endpoint.
- **1c. Apoio e documentos:** cadastros de apoio (cargos, setores, benefícios) e a configuração da empresa, documentos do colaborador (primeiro uso do Blob) e "Meus documentos".

**Telas (9):** RH 01 `6:32364` colaboradores; 02 `6:32838` adicionar; 03 `6:33366` ficha, aba Dados; 09 `6:36297` aba Documentos; 10 `6:36845` modal adicionar documento; 29 `6:45650` cadastros; 30 `6:45934` modal novo cargo; 31 `6:46324` log de auditoria. Gestor 52 `6:51994` ficha da equipe (aba Dados; as outras abas chegam nas fatias 3, 4 e 6). Mais as telas que desenho: convite, redefinição e troca de senha, "Meus documentos".

**Banco:** *já existe:* `FUNCIONARIOS`, `FUNCIONARIOS_DETALHES` (CPF, RG, endereço, e-mail pessoal, telefone), `CARGOS`, `SETORES`, `GESTORES`, `NIVEIS_DE_ACESSO`, `USUARIOS`, `BENEFICIOS`, `BENEFICIOS_FUNCIONARIOS`. *Scripts novos:* campos do cadastro (lista A), tokens de convite e de redefinição (só o hash), `AUDITORIA`, fila de e-mails, tarefas agendadas, documentos do colaborador, configuração da empresa (nome, UF e município).

**API (esboço):** colaboradores (listar com busca por nome, CPF ou matrícula e filtros por setor e contrato; criar; editar; inativar; ficha), cadastros de apoio, documentos, auditoria (filtros e CSV), convite, redefinição e troca de senha.

**Regras:**
- A equipe do gestor é o setor dele. O cadastro preenche `FUNCIONARIOS.ID_GESTOR` com o gestor do setor, **mas os triggers recusam** o gestor ser o próprio funcionário (então o responsável do setor tem outro `ID_GESTOR` ou nenhum) e recusam gestor inativo; e quando o gestor do setor muda é preciso refazer o `ID_GESTOR` dos membros.
- Cada perfil recebe um DTO diferente: o gestor não recebe CPF, endereço, holerites nem documentos, e vê o **telefone mascarado**, como no Figma ("(11) 9****-7783").
- **O convite e o trigger de `USUARIOS`:** o trigger exige funcionário ativo com e-mail e `SENHA` com no mínimo 40 caracteres, e o usuário "nasce sem senha". Solução: criar o `USUARIOS` no convite com um **hash provisório aleatório e inutilizável**; a pessoa troca pela senha escolhida ao aceitar.
- O benefício no cadastro depende do catálogo (1c); até lá o campo fica desligado.
- Inativar cancela benefícios e férias futuras (trigger do grupo), **sem aviso e sem registro próprio dessas consequências** (decisão do dono, 2026-10-28): o log mostra a inativação, mas não os cancelamentos automáticos. **Desligamento com data futura** (dono, 2026-10-29): o banco inativa a pessoa assim que há data de desligamento (`CHK_FUNCIONARIOS_ATIVO_DESLIGAMENTO`, `Criacao_Tabelas:104`), então a aplicação guarda a data agendada em tabela própria e só a grava em `FUNCIONARIOS` **no dia seguinte ao último dia de trabalho, depois de a consolidação do banco de horas fechar esse dia**: gravar antes inativa a pessoa, e o banco recusa a batida e o lançamento dela (`Criacao_Triggers:389-392` e `:652-655`). A data gravada pode ser do passado.
- Senhas, hashes e tokens ficam fora do log.
- O admin do seed (fatia 0) não tem `FUNCIONARIOS_DETALHES`; a ficha dele é completada aqui.

**Dependências cruzadas:** "enviar formulário de admissão" (fatia 5) e "incluir no onboarding" (fatia 6) aparecem **desligadas** no cadastro e na importação até a fatia correspondente chegar.

**Testes:** matriz de perfil por endpoint (linhas e campos, inclusive telefone mascarado); comparação mapeamento × banco; convite e redefinição (token de uso único, expiração, reuso, limite de tentativas, resposta uniforme para e-mail inexistente); gestor do setor inativo ou igual ao funcionário; auditoria de login, acesso negado, alterações e efeitos de trigger; e-mail na fila com nova tentativa e com limite de vazão; ponta a ponta: o RH cadastra e a pessoa define a senha.

**Decisões do dono:** (quem aprova os pedidos de um gestor: decidido em 2026-10-29, só o RH, que fica também em `GESTORES`); se `FUNCIONARIOS.ID_GESTOR` pode diferir do gestor do setor; regra da matrícula; política de senha; provedor de e-mail; tipos de documento.

**Riscos:** as capturas do Figma podem mostrar detalhes que o texto não mostra; o grupo pode mudar tabelas usadas aqui (o teste de mapeamento avisa).

---

## Fatia 2: importação por Excel

**Entrega:** o RH alterna o cadastro para "Importar planilha", envia um arquivo, vê a prévia com os erros por linha e coluna e só confirma quando o arquivo está sem erros (tudo ou nada).

**Telas:** nenhuma no Figma; desenho os estados (escolher arquivo, prévia com erros, resultado).

**Banco:** *script novo:* registro das importações (quem, quando, totais, erros).

**API (esboço):** baixar o modelo (com uma aba de cargos e setores válidos), validar (sem gravar), confirmar. Um upload, validação de todas as linhas, limite de 500 linhas.

**Regras:**
- **A gravação é uma transação só** (500 linhas cabem), para que "tudo ou nada" valha também se o banco recusar uma linha no meio; os lotes servem só para não crescer a memória.
- Só cadastra; só o RH; ninguém recebe senha (convite); a planilha espelha o cadastro mínimo e repete as ações "depois de salvar" que já existirem (as desligadas continuam desligadas).
- O resultado entra na auditoria.
- O `.xlsx` é um zip: limitar também o **tamanho descompactado** e o número de planilhas e células.

**Testes:** erros em várias linhas (a prévia lista todos); e-mail duplicado no arquivo e no banco; **CPF inválido ou duplicado** (CPF é chave única em `FUNCIONARIOS_DETALHES`); cargo ou setor inexistente; 501 linhas; arquivo que não é Excel ou que descompacta para muito mais que o limite; confirmar duas vezes; recusa de uma linha por trigger no meio da gravação (nada fica gravado); perfil sem permissão.

**Decisões do dono:** biblioteca de Excel (licença livre, conferida na hora), colunas do modelo, se a importação marca como "Em admissão".

---

## Fatia 3: ponto

**Entrega:** o funcionário bate o ponto; vê o espelho e o banco de horas; pede ajuste com justificativa; o gestor e depois o RH aprovam; feriados entram no calendário; o RH exporta para a folha.

**Incrementos:** **3a.** bater ponto e espelho (horário do servidor, faltas e **atraso** medido contra o horário padrão da empresa). **3b.** ajustes em duas etapas e atestado. **3c.** feriados, banco de horas (consolidação diária), extrato e exportação.

**Telas (8):** RH 04 `6:33882` ficha, aba Ponto; 12 `6:37625` ponto e feriados; 13 `6:38098` modal pedidos de ajuste. Funcionário 33 `6:46961` meu ponto (e o cartão "Seu ponto hoje" da tela inicial 32 `6:46598`, que a 7d completa). Gestor 47 `6:50376` meu ponto; 54 `6:52939` ponto da equipe; 55 `6:53336` modal pedido de ajuste; 56 `6:53857` espelho da pessoa. Mais a tela do atestado, que desenho.

**Banco:** *já existe:* `PONTOS` (um por funcionário por dia), `PONTOS_MARCACOES`, `PONTOS_MANUTENCOES`, `BANCOS_HORAS`, `FERIADOS`, funções de horas e de consistência. *Scripts novos:* itens da lista A (correção pedida em tabela de itens, com várias batidas por pedido; etapa do gestor em colunas, origem, banco de horas) e da lista D, atestados (com arquivo).

**API (esboço):** registrar marcação, espelho do mês, banco de horas (extrato; **sem lançamento manual**, decisão de 2026-10-28), pedidos de ajuste (criar, listar, analisar como gestor, analisar como RH, chave de aprovação automática do gestor ausente, ligada pelo RH), atestado (enviar, validar), feriados (listar; cadastrar municipal), exportar para a folha, espelho em PDF.

**Regras:**
- A validação da sequência ENTRADA e SAIDA e o cálculo das horas ficam **com a aplicação**, apoiados nas funções do grupo (decisão de 2026-10-28).
- **Ajuste de ponto, divergência do Figma (tela 13):** o modal do RH mostra "Aprovar e corrigir o ponto" e "Recusar" mesmo no pedido "Com o gestor", e o rodapé diz "O gestor do setor também pode analisar". Pela decisão do dono (sem "RH decide direto", 2026-10-29), a aplicação só habilita esses botões depois do resultado do gestor. A tela do gestor (55) já diz "Depois de você, o RH confirma o ajuste".
- O horário vem do servidor. **Batida duplicada** (duplo clique): a chave única é por segundo; a aplicação trata a violação com uma resposta amigável e o front desabilita o botão.
- A consolidação do banco de horas é idempotente (usa a referência do dia), recupera vários dias depois de uma parada e **considera férias aprovadas e atestados validados**, para não lançar falta nesses dias (as férias só ganham tela na 7a, mas a tabela já existe).
- O atestado é dado de saúde. **Decisão de 2026-10-29:** o gestor vê o arquivo, só o da própria equipe, e **só o RH valida**; o acesso fica registrado na auditoria. Atestado validado vira **"Falta Compensada"**: o dia não conta como falta e **não desconta** do banco de horas, e a consolidação reduz o esperado do dia pelas horas cobertas (dia inteiro: esperado zero; gravar ou não uma linha zerada é escolha dela, porque o banco aceita quantidade zero). O RH pode **recusar** com motivo (a pessoa vê "recusado" e o motivo, e o dia segue como falta). O atestado vale para dias inteiros e para algumas horas (proposta: início e fim no dia, e só esse período é compensado). Atenção: o trigger de `BANCOS_HORAS` recusa INSERT para funcionário inativo (`Criacao_Triggers:652-655`), e a data de desligamento preenchida, mesmo futura, já inativa o funcionário (`:94-96`); a consolidação dos últimos dias de quem está saindo precisa tratar isso.
- E-mail em cada etapa do ajuste.
- O gestor só vê o espelho; só o RH altera marcações, e a correção fica marcada.
- Feriados nacionais entram sozinhos e os estaduais seguem a **UF da empresa** (configuração da fatia 1c); os municipais, o RH cadastra e valem para todos (**sem unidades**, decisão de 2026-10-28).
- A carga do cargo é **semanal** (decisão de 2026-10-28, com o trigger do ponto ajustado pelo Kauan); a esperada por dia é a semanal dividida por 5.

**Testes:** sequência inválida; jornada que atravessa a meia-noite; ponto em feriado; ajuste aprovado pelo gestor e recusado pelo RH e o contrário; pedido com várias batidas (uma corrigida e uma nova) e aprovação que aplica todas ou nenhuma; gestor tentando ver outro setor; consolidação duas vezes sem duplicar e depois de ficar parada; duplo clique na batida; relógio do navegador adiantado; atestado visto por quem não pode; exportação com acentos e com células que começam com `=`; fuso (batida às 22h30).

**Decisões do dono:** hora extra, **tolerância de atraso**, formato do "exportar para a folha", fonte dos feriados (cálculo ou biblioteca, com licença), quem registra ponto retroativo, uso das funções do grupo. (O RH aprovar direto: decidido em 2026-10-29, não haverá; há a chave de aprovação automática do gestor ausente.)

**Riscos:** regra de negócio pesada; o agendador depende de Always On; PDF do espelho (fontes e memória).

---

## Fatia 4: comunicação e documentos

**Entrega:** o RH publica avisos (simples e detalhados) e acompanha quem leu; publica holerites que ficam guardados por pessoa; registra advertências que a pessoa reconhece ou contesta; todos veem os aniversariantes.

**Incrementos:** **4a.** avisos. **4b.** holerites. **4c.** advertências e aniversariantes (a API; o cartão fica na tela inicial da 7d).

**Telas (16):** RH 07 `6:35315` e 08 `6:35821` (holerites); 11 `6:37261` ficha, aba Advertências; 14 `6:38403` e 15 `6:38759` (advertências); 27 `6:45002` e 28 `6:45400` (avisos). Funcionário 36 `6:47672` holerites; 37 `6:47862` e 38 `6:48086` (avisos); 43 `6:49216` advertências. Gestor 49 `6:50809` holerites; 50 `6:50994` avisos; 53 `6:52591` e 60 `6:54913` (registrar advertência); 59 `6:54615` advertências da equipe.

**Banco:** *já existe:* `AVISOS`, `AVISOS_VISUALIZACOES`, `HOLERITES`, `ADVERTENCIAS`. *Scripts novos:* lista A (avisos com fixar e a coluna `ENVIADO` para o rascunho; ajuste da chave única dos holerites; advertências com contestação, decididas em 2026-10-28). Os triggers ficam para depois (decisão de 2026-10-28).

**API (esboço):** avisos (criar, rascunho, agendar, encerrar, ler, quem leu e lembrar), holerites (enviar PDF, listar, baixar por link temporário, **enviar em lote**), advertências (registrar como RH ou gestor, confirmar ciência, contestar, responder), aniversariantes dos próximos 14 dias.

**Regras:**
- Aviso simples tem até 280 caracteres; o detalhado tem seções, **sem anexo PDF**; **sempre para todos** (sem público por setor); **sem HTML cru**. Holerite **com tipo** (o Kauan cria) e **sem "novo" ou "visto"**.
- As linhas de leitura nascem na publicação, **uma por funcionário ativo** (o trigger do grupo já faz isso e serve, porque o aviso é sempre para todos); quem for admitido depois vê os avisos vigentes; abrir marca como lido.
- Holerite: PDF de até 5 MB no Blob; o banco **proíbe apagar** holerite (trigger), então um holerite errado se **substitui** (nova versão) e não se exclui; o arquivo só é enviado depois de a regra do banco aprovar; sobras são limpas por job; o download valida o escopo e dá um link temporário como anexo, nunca público; competência futura (férias antecipadas) passa a valer no tipo férias, com o trigger ajustado pelo Kauan (2026-10-29).
- O gestor registra advertência só para a própria equipe; o RH recebe uma cópia.

**Testes:** o funcionário A abre o holerite do B (negado, inclusive por link); link expirado; PDF acima de 5 MB ou que não é PDF; arquivo órfão depois de recusa do banco; publicação agendada dispara uma vez; admitido depois vê o aviso vigente; contestar duas vezes; lote de holerites com uma linha inválida.

**Decisões do dono:** envio em lote de holerites (o e-mail de todo aviso publicado é sempre enviado: decidido em 2026-10-28); tipos de advertência; o que acontece depois da contestação; retenção de holerite de desligado (competência futura e substituição: decididas em 2026-10-29).

---

## Fatia 5: formulários

**Entrega:** o RH monta formulários (entrevista, admissão, avaliação, pesquisa, sugestão; sem quiz), publica para pessoas, setores ou por link público e acompanha as respostas; o funcionário responde os pendentes.

**Incrementos:** **5a.** construtor (assistente de 4 etapas com prévia). **5b.** responder e acompanhar, interno. **5c.** link público e entrevista, **uma superfície de segurança à parte** (candidatos que não são funcionários). **5d.** avaliações por ciclo.

**Telas (9):** RH 16 `6:39069` lista; 17 `6:39524`, 18 `6:39994`, 19 `6:40410`, 20 `6:40875` (etapas Detalhes, Perguntas, Verificação, Publicação); 21 `6:41305` quem respondeu. Funcionário 39 `6:48327` e 40 `6:48590`. Gestor 57 `6:54111` formulários da equipe. Mais a tela pública do candidato, que desenho.

**Banco:** *já existe:* `FORMULARIOS`, `FORMULARIOS_PERGUNTAS`, `FORMULARIOS_RESPOSTAS`. *Scripts novos:* publicação, prazo, anônimo (o RH marca), link público, atribuição (gera os "pendentes"), respondentes externos e anônimos, envio que agrupa as respostas, alternativas das perguntas e quem é avaliado nas avaliações. **Sem tipo quiz, sem nota mínima e sem pontuação.** **Formulário recorrente** (dono, 2026-10-29): intervalo de reabertura; **sem histórico** das rodadas anteriores (sem tabela de rodadas): ao reabrir, as respostas da rodada anterior são apagadas (o banco permite, porque `FORMULARIOS_RESPOSTAS` não tem trigger de exclusão; vale para todos os formulários recorrentes, dono, 2026-10-29). O RH pode exportar as respostas (CSV, `spec` 13.5) antes da reabertura.

**API (esboço):** formulários (criar, editar, verificar, publicar, encerrar), perguntas (múltipla escolha, resposta curta e longa, escala 1 a 5, envio de arquivo; **cortadas** em 2026-10-29: eliminatória, nota dada pelo RH, quiz com pontuação), responder, quem respondeu e lembrar, exportar, endpoint **público** do candidato.

**Regras:**
- O gestor vê só quem respondeu, não o conteúdo. **Formulário recorrente** (2026-10-29): o agendador reabre o formulário no intervalo que o RH escolher, para as mesmas pessoas, e cada reabertura é uma rodada nova.
- Sem resposta certa nem pergunta eliminatória (cortadas em 2026-10-29), não há nota a calcular nem gabarito a esconder do navegador.
- Arquivo enviado pelo candidato é servido como **anexo**, nunca exibido no mesmo domínio; há aviso de privacidade e um job apaga os dados de candidato vencidos.
- **Anonimato (decidido em 2026-10-29: o RH marca cada formulário como anônimo ou não, ao criar):** o interceptor de auditoria grava antes e depois, e hoje `ID_FUNCIONARIO` é obrigatório, então sem desenho próprio a pesquisa "anônima" seria só visual. O desenho: tabela ou coluna que não identifica quem respondeu e resultados agregados com tamanho mínimo de grupo. A lista de "pendentes" precisa saber quem já respondeu sem ligar a pessoa às respostas (registro de participação separado); o desenho vem no plano da fatia.
- E-mail "formulário atribuído"; "verificação" confere título, obrigatórias e dado sensível.

**Testes:** candidato com currículo grande ou de tipo errado; o mesmo link respondido várias vezes (limite); fora do prazo; o gestor tentando abrir respostas; formulário agendado publica uma vez; formulário anônimo não deixa rastro de quem respondeu; exportação com `=`.

**Decisões do dono:** anonimato já decidido em 2026-10-29 (o RH marca por formulário); como a verificação de dado sensível funciona; proteção do link público (limite simples ou captcha); retenção dos dados de candidato; como as respostas da admissão chegam aos campos do cadastro.

---

## Fatia 6: TAD (treinamentos)

**Entrega:** o RH cria treinamentos com módulos, guarda como rascunho ou publica com acesso liberado na data de início (dono, 2026-10-29), escolhendo para quem vale (todos, setores ou pessoas); a pessoa avança por módulo, conclui ao terminar o último e recebe um certificado em PDF com código de validação (sem prova final e sem nota, decisão de 2026-10-29).

**Incrementos:** **6a.** treinamentos e módulos. **6b.** progresso e certificado. **6c.** lembretes (sem turmas presenciais, decisão de 2026-10-29).

**Telas (8):** RH 22 `6:41863` lista e quem fez; 23 `6:42569`, 24 `6:43274`, 25 `6:43968`, 26 `6:44679` (etapas). Funcionário 41 `6:48821` e 42 `6:49071`. Gestor 58 `6:54370` treinamentos da equipe.

**Banco:** *já existe:* `TREINAMENTOS`, `TREINAMENTOS_FUNCIONARIOS`, `TREINAMENTOS_CERTIFICADOS`. *Scripts novos:* módulos e progresso por módulo (tabelas novas); `TREINAMENTOS` ganha `PUBLICADO` e o público (todos, setores ou pessoas escolhidas, sem "por cargo", Figma tela 26), com uma tabela nova para os setores e as pessoas escolhidas (a tabela não tem trigger; item 27 da lista do Kauan); a etapa "Avaliação e certificado" do assistente (tela 25) perde o quiz e a nota, e sobra a caixa "Emitir certificado", **que fica** (dono, 2026-10-29): `TREINAMENTOS` ganha a coluna "emite certificado" (sim ou não); **só online, sem coluna de modalidade** (dono, 2026-10-29). **Sem turmas, sem quiz e sem nota.** O trigger de nota deixa de atrapalhar (ver lista B); o do certificado continua como está (a aplicação atualiza a linha que ele cria). A inscrição em opcionais usa o status `INSCRITO`, que já existe.

**API (esboço):** treinamentos (criar, publicar, listar), módulos, progresso, conclusão, certificado (**o trigger já cria a linha com um UUID; a aplicação gera o PDF e atualiza essa linha**, sem tentar inserir outra), validação por código, inscrição em opcionais, exportar lista.

**Regras:**
- **Sem nota e sem nota mínima** (decisão de 2026-10-29): concluir todos os módulos marca o treinamento como `CONCLUIDO`, e o trigger cria o certificado.
- **Emite certificado (sim ou não, por treinamento; dono, 2026-10-29):** o trigger cria a linha do certificado sempre, então a aplicação trata o "não": não gera o PDF, não lista o certificado à pessoa e a validação por código responde "não encontrado" para esse treinamento.
- O código público de validação do certificado é o **UUID**, não o número sequencial do Figma (que seria enumerável); o número amigável aparece à parte. A validação pública mostra o mínimo de dados.
- Lembrete 7 dias antes do fim e inclusão de quem for admitido até o prazo, pelo agendador.
- PDF: ver "Decisões que atravessam fatias".

**Testes:** concluir sem terminar os módulos; certificado emitido uma vez e o PDF anexado à linha criada pelo trigger; código inexistente ou de outra pessoa; treinamento com "emite certificado" em não (concluir não gera PDF e o código da linha criada pelo trigger não valida); gestor vendo só o setor; fonte com acento no PDF.

**Decisões do dono:** onde ficam os vídeos (Blob ou link externo); modelo visual do certificado e o formato do número. (Escala e nota mínima: decidido em 2026-10-29, não haverá nota.)

---

## Fatia 7: gestão

**Entrega:** férias em duas etapas, benefícios com pedidos, pagamento e calendário, e os painéis do RH e do gestor.

**Incrementos:** **7a.** férias. **7b.** benefícios e dependentes. **7c.** pagamento e calendário. **7d.** painéis e telas iniciais completas (**dependem das fatias 3 a 6**).

**Telas (11):** RH 00 `6:32009` painel inicial; 05 `6:34321` ficha, aba Férias; 06 `6:34873` modal registrar férias. Funcionário 32 `6:46598` tela inicial; 34 `6:47148` férias; 35 `6:47418` pedir férias; 44 `6:49478` benefícios; 45 `6:49770` aderir. Gestor 46 `6:50018` tela inicial; 48 `6:50559` férias; 51 `6:51445` painel da equipe.

**Banco:** *já existe:* `FERIAS` (5 a 30 dias por período, abono de 1 a 10, um gestor), `BENEFICIOS`, `BENEFICIOS_FUNCIONARIOS`. *Scripts novos:* segunda etapa das férias (colunas novas, sem trigger reescrito), adiantamento do 13º e "sugerir outra data" (ambos mantidos em 2026-10-29), pedidos de benefício (colunas em `BENEFICIOS_FUNCIONARIOS`), dependentes e trajeto do vale-transporte (tabelas novas), regra de pagamento (na configuração da empresa) e exceções de pagamento (tabela nova: mês e data; decidido em 2026-10-29).

**API (esboço):** férias (saldo e períodos, pedir, analisar como gestor, confirmar como RH, registrar pelo RH, sugerir outra data), benefícios (catálogo, adesão, análise), pagamento (regra padrão e exceções por mês), calendário (próximos dias), painéis. E-mail com o resultado das férias.

**Regras de férias do protótipo (a confirmar):** até 3 partes (uma de pelo menos 14 dias, as outras de pelo menos 5); abono de até 10 dias; adiantamento do 13º; pedido com 30 dias de antecedência; não começar nos 2 dias antes de feriado ou fim de semana; pagamento até 2 dias antes. O banco já exige 5 a 30 dias e abono de 1 a 10; o resto fica com a aplicação. Com a etapa do gestor em colunas (decisão de 2026-10-29), o trigger de hoje permite o fluxo, porque o status só muda na decisão final. O agendador faz `APROVADA` para `EM_GOZO` e `CONCLUIDA` (o trigger só aceita essas transições, `Criacao_Triggers:916-917`, e nada as dispara) e cancela `SOLICITADA` cuja data de início já passou. Também expira a sugestão de data sem resposta (proposta: 5 dias corridos depois da sugestão ou 2 dias antes da data sugerida, o que vier primeiro). O RH registra férias pela ficha já `APROVADA` (o `INSERT` de `FERIAS` não valida status nem gestor, `Criacao_Triggers:865-905`). A chave de aprovação automática vale para os pedidos de quem tem aquele gestor em `FUNCIONARIOS.ID_GESTOR`; se o gestor for inativado, o RH refaz o `ID_GESTOR` (regra da fatia 1).

**Próximo pagamento:** regra padrão (dia do mês, por exemplo "todo dia 30") e exceções por mês; se o mês não tem o dia, vale o último; se cai em fim de semana ou feriado, antecipa para o dia útil anterior (regras aceitas pelo dono em 2026-10-29). Liga também a publicação agendada de holerite "para o dia do pagamento" (fatia 4).

**Painéis:** pendências, presença agora, próximos dias, treinamentos em andamento, últimas alterações; no gestor, férias por semana e aprovações. A tela inicial do funcionário inclui o cartão de ponto (fatia 3) e os aniversariantes (API da fatia 4).

**Testes:** período sobreposto; férias perto de feriado; saldo insuficiente; duas etapas (e o gestor aprovando o próprio pedido: negado); pagamento em fevereiro; painel de gestor sem equipe; inativar o funcionário cancela férias futuras (efeito do trigger do grupo, sem aviso e sem registro próprio).

**Decisões do dono:** regras de férias (a segunda etapa já foi decidida em 2026-10-29); catálogo de benefícios e regras de adesão; definição de "presença agora".

---

## Fatia 8: fechamento

**Entrega:** o sistema pronto para a banca.

- DER/MER atualizado a partir dos scripts, e scripts finais revisados com o grupo.
- Seed completo e **fictício** da demo (um RH, gestores, funcionários com ponto, holerites, avisos, formulários e treinamentos), com e-mails que não geram devolução.
- Testes de ponta a ponta nos fluxos da demo (login, bater ponto, importar planilha, pedir e aprovar férias).
- Roteiro e ensaio; limitações documentadas (conformidade legal do ponto, sem relógio físico, só desktop); custos e como desligar o ambiente.

**Decisões do dono:** quais fluxos entram no roteiro e o que mostrar como trabalhos futuros.

---

## Dependências cruzadas e como adiar

| Ponto | Nasce | Fecha | Enquanto isso |
|---|---|---|---|
| "Enviar formulário de admissão" (cadastro e importação) | 1 e 2 | 5 | Ação desligada |
| "Incluir no onboarding" (cadastro e importação) | 1 e 2 | 6 | Ação desligada |
| Benefícios no cadastro | 1b | 1c (catálogo) | Campo desligado |
| Log de auditoria com eventos de todos os módulos | 1 | cada fatia | Mostra o que já existe |
| Cartão "Seu ponto hoje" na tela inicial | 3 | 7d | Cartão solto até a tela inicial completa |
| Consolidação do banco de horas lê férias e atestado | 3 | 7a (tela de férias) | A tabela de férias já existe; a consolidação a lê |
| UF da empresa para os feriados estaduais | 3 | 1c (configuração da empresa) | Configuração criada na 1c |
| Aniversariantes | 4 (API) | 7d (tela) | Sem cartão até a 7d |
| Holerite agendado "para o dia do pagamento" | 4 | 7c | Agenda por data; a regra de pagamento liga depois |
| ~~Quiz de treinamento~~ | | | **Cortado em 2026-10-29** (sem quiz e sem prova final); a fatia 6 não depende mais da 5 |
| Espelho em PDF e certificado em PDF | 3 e 6 | 3 | A biblioteca de PDF entra na fatia 3 |
| Blob, Storage Account e e-mail no Azure | 1c e 1a | cada uso | Criados quando aparecem, com custo dito antes |
| Painéis do RH e do gestor | 7d | 3 a 6 | Só na 7d |
| Abas Ponto, Férias, Treinamentos e Advertências da ficha | 1 | 3, 4, 6, 7 | A ficha cresce por fatia |

## O que precisa de resposta, em ordem de urgência

1. **Antes de congelar a base (fim da fatia 0), com o grupo:** a lista da seção "Antes do congelamento" (A, B e C): o que entra agora e o que vira script novo. Não é bloqueio: depois do congelamento tudo continua possível por scripts novos.
2. ~~Professor: exige domínio separado da persistência?~~ **Respondido em 2026-10-28: não exige.**
3. **Dono:** a conta do Azure com créditos (o dono vai verificar em 2026-10-28).
4. **Começo de cada fatia:** as decisões listadas nela.
