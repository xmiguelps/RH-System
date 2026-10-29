# Roadmap completo do RH-System (fatias 0 a 8)

Data: 2026-10-28 · Status: rascunho para revisão do dono, já revisado uma vez pelo `revisor-de-plano` (achados de engenharia incorporados) · Spec: `docs/superpowers/specs/2026-10-21-rh-system-design.md` · Plano detalhado da fatia 0: `docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md`

## Como usar este documento

- Isto **não é um plano de tarefas.** Cada fatia ganha o seu plano passo a passo (skill `writing-plans`) **no começo dela**, revisado pelo `revisor-de-plano`, porque o plano detalhado depende de código e de um banco que ainda não existem.
- A ordem segue as dependências. **Ordem de corte** (só se um prazo passar a existir) é decisão do dono: a fatia 8 e a tela inicial mínima por perfil não se cortam, e a 6 cai junto se a 5 cair.
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
| 5 | Formulários e quiz | Criar e responder formulários, entrevista por link público, avaliações | 9 | 1 |
| 6 | TAD | Treinamentos, progresso, certificado em PDF | 8 | 5 |
| 7 | Gestão | Férias, benefícios, pagamento e calendário (7a a 7c); painéis (7d) | 11 | 3 e 4 (7d: 3 a 6) |
| 8 | Fechamento | DER, seed da demo, testes de ponta a ponta, ensaio da banca | 0 | todas |

## Antes do congelamento: lista para o grupo

Até o primeiro deploy (fim da fatia 0) o grupo ainda pode editar a base direto, o que é mais barato do que um script novo depois. **Depois do congelamento nada fica proibido**: cada item vira um script novo. **Já decidido pelo dono (2026-10-28): sem unidades; aprovação de férias e de ajuste de ponto em duas etapas (gestor e depois RH).** O que não existir no esquema de 2026-10-28 e o Figma pedir:

**A. Colunas e restrições**
- `FUNCIONARIOS`: matrícula e tipo de contrato (serão criados pelo Kauan, que também aparece como Kafly21 nos commits). A jornada vem da **carga semanal do cargo** e do **horário padrão da empresa** (um só para todos, ver "Configuração da empresa"), com atraso medido contra ele (decisão de 2026-10-28). A situação "Em admissão" **não precisa de coluna**: é o funcionário com data de admissão no futuro. **Sem período de experiência** (decisão de 2026-10-28): a etiqueta "Experiência" e o aviso de fim de experiência ficam escondidos.
- `PONTOS_MARCACOES`: origem (sistema ou corrigida pelo RH). `PONTOS_MANUTENCOES`: a correção pedida (data e hora e tipo propostos) e a segunda etapa.
- `BANCOS_HORAS`: **dia de referência**, com chave única por funcionário e dia, para a consolidação não duplicar e poder **recalcular um dia** depois de um ajuste aprovado. **Só lançamentos automáticos** (decisão de 2026-10-28), então não há coluna de origem.
- `ADVERTENCIAS`: **com contestação** (decisão de 2026-10-28): data da ciência, texto e data da contestação, e o status novo `CONTESTADA` no `CHECK`. O tipo verbal ou escrita já cabe em `TIPO_ADVERTENCIA`; o trigger de UPDATE já permite essas transições; a resposta do RH vai por e-mail e o Kauan decide se a guarda.
- `HOLERITES`: **com tipo** (mensal, 13º, férias; o Kauan cria) e **sem "visto"** (decisão de 2026-10-28). O Kauan ajusta a chave única `(funcionário, ano, mês)`, que hoje impede dois holerites no mesmo mês.
- `AVISOS`: **fixar no topo** e **rascunho** (o Kauan faz). **Sem público por setor e sem anexo PDF** (decisão de 2026-10-28). O e-mail é **sempre** enviado na publicação, então não precisa de coluna.
- `FORMULARIOS`: tipos quiz e sugestão (o `CHECK` aceita `ENTREVISTA`, `ADMISSAO`, `AVALIACAO`, `PESQUISA` e `OUTRO`), publicação, prazo, nota mínima, link público. `FORMULARIOS_RESPOSTAS`: respondente externo e pesquisa anônima, porque `ID_FUNCIONARIO` é `NOT NULL`.
- `FERIAS`: segunda etapa, adiantamento do 13º, "sugerir outra data".
- **Configuração da empresa** (tabela nova): nome, UF, município e o **horário padrão** (entrada, saída e tolerância de atraso; o Kauan cria o horário padrão e a tabela, a combinar). A UF escolhe os feriados estaduais e o nome aparece no menu. Como **não há unidades**, os feriados municipais valem para todos e a tabela `FERIADOS` não precisa mudar por isso.

**B. Triggers que precisam ser reescritos (o Figma pede o que eles proíbem)** — **adiado: por enquanto o dono não quer tratar os triggers (2026-10-28)**, salvo a correção do Kauan nos de ponto.
- `TRG_AVISOS_AFTER_INSERT` cria uma linha de leitura para **todo funcionário ativo** no INSERT. Isso **serve**, porque o aviso agora é sempre para todos. Ficam de fora o rascunho (que também criaria as linhas) e quem for admitido depois; a aplicação mostra os avisos vigentes sem depender da linha de leitura.
- `PONTOS_MANUTENCOES` e `FERIAS`: o status depois de `PENDENTE` ou `SOLICITADA` é final ou vai direto a aprovada ou rejeitada, e aprovar exige um `ID_GESTOR` (que não seja a própria pessoa). A segunda etapa (gestor e depois RH) e "o RH aprova direto" exigem status novos e reescrita.
- `TRG_HOLERITES_BEFORE_INSERT` recusa competência futura; um holerite de férias pago antes do mês seria barrado.
- `TRG_TREINAMENTOS_FUNCIONARIOS_*`: a **nota mínima 70 é fixa**; na escala 0 a 10 do Figma todo `CONCLUIDO` com nota é recusado, e a "nota mínima configurável" não cabe. Os mesmos triggers criam o certificado na hora, com `CODIGO_VALIDACAO = UUID()` e sem arquivo; um INSERT da aplicação colide com a chave única.
- `TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT` exige funcionário ativo: candidato externo e pesquisa anônima ficam impossíveis.
- `TRG_FUNCIONARIOS_*` e a regra do gestor do setor (ver fatia 1).

**C. Semântica a alinhar**
- **Decidido pelo dono (2026-10-28):** `CARGOS.CARGA_HORARIA` passa a ser **semanal** ("44 h", como no Figma); hoje está por dia. O trigger que a copia para `PONTOS.CARGA_HORARIA` (que é por dia) **está sendo corrigido pelo Kauan**. Conferir o resultado quando ele enviar.
- Nota de treinamento: 0 a 100 no banco, 0 a 10 no Figma.
- Inativar funcionário também desativa todos os benefícios e cancela férias futuras (`SOLICITADA` ou `APROVADA`), sem avisar a pessoa. **Mantido assim** (dono, 2026-10-28).

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
12. **Telas que não existem no Figma e eu desenho em estilo simples:** login (fatia 0), convite e redefinição de senha e "Meus documentos" do funcionário (fatia 1), importação por Excel (fatia 2), atestado (fatia 3), formulário público do candidato (fatia 5). **O gestor reutiliza as telas do funcionário** (formulários, treinamentos, benefícios e advertências, que o Figma não desenha para ele), com o menu do gestor.

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
- Inativar cancela benefícios e férias futuras (trigger do grupo), **sem aviso e sem registro próprio dessas consequências** (decisão do dono, 2026-10-28): o log mostra a inativação, mas não os cancelamentos automáticos.
- Senhas, hashes e tokens ficam fora do log.
- O admin do seed (fatia 0) não tem `FUNCIONARIOS_DETALHES`; a ficha dele é completada aqui.

**Dependências cruzadas:** "enviar formulário de admissão" (fatia 5) e "incluir no onboarding" (fatia 6) aparecem **desligadas** no cadastro e na importação até a fatia correspondente chegar.

**Testes:** matriz de perfil por endpoint (linhas e campos, inclusive telefone mascarado); comparação mapeamento × banco; convite e redefinição (token de uso único, expiração, reuso, limite de tentativas, resposta uniforme para e-mail inexistente); gestor do setor inativo ou igual ao funcionário; auditoria de login, acesso negado, alterações e efeitos de trigger; e-mail na fila com nova tentativa e com limite de vazão; ponta a ponta: o RH cadastra e a pessoa define a senha.

**Decisões do dono:** quem aprova os pedidos de um gestor (o trigger exige um `ID_GESTOR` que não seja a própria pessoa; se for "o RH", o trigger muda); se `FUNCIONARIOS.ID_GESTOR` pode diferir do gestor do setor; regra da matrícula; política de senha; provedor de e-mail; tipos de documento.

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

**Banco:** *já existe:* `PONTOS` (um por funcionário por dia), `PONTOS_MARCACOES`, `PONTOS_MANUTENCOES`, `BANCOS_HORAS`, `FERIADOS`, funções de horas e de consistência. *Scripts novos:* itens da lista A e B (correção pedida, segunda etapa, origem, banco de horas), atestados (com arquivo).

**API (esboço):** registrar marcação, espelho do mês, banco de horas (extrato; **sem lançamento manual**, decisão de 2026-10-28), pedidos de ajuste (criar, listar, analisar como gestor, analisar como RH, aprovação direta pelo RH), atestado (enviar, validar), feriados (listar; cadastrar municipal), exportar para a folha, espelho em PDF.

**Regras:**
- A validação da sequência ENTRADA e SAIDA e o cálculo das horas ficam **com a aplicação**, apoiados nas funções do grupo (decisão de 2026-10-28).
- O horário vem do servidor. **Batida duplicada** (duplo clique): a chave única é por segundo; a aplicação trata a violação com uma resposta amigável e o front desabilita o botão.
- A consolidação do banco de horas é idempotente (usa a referência do dia), recupera vários dias depois de uma parada e **considera férias aprovadas e atestados validados**, para não lançar falta nesses dias (as férias só ganham tela na 7a, mas a tabela já existe).
- O atestado é dado de saúde: **só o RH vê**, com acesso registrado na auditoria; o gestor não vê.
- E-mail em cada etapa do ajuste.
- O gestor só vê o espelho; só o RH altera marcações, e a correção fica marcada.
- Feriados nacionais entram sozinhos e os estaduais seguem a **UF da empresa** (configuração da fatia 1c); os municipais, o RH cadastra e valem para todos (**sem unidades**, decisão de 2026-10-28).
- A carga do cargo é **semanal** (decisão de 2026-10-28, com o trigger do ponto ajustado pelo Kauan); a esperada por dia é a semanal dividida por 5.

**Testes:** sequência inválida; jornada que atravessa a meia-noite; ponto em feriado; ajuste aprovado pelo gestor e recusado pelo RH e o contrário; gestor tentando ver outro setor; consolidação duas vezes sem duplicar e depois de ficar parada; duplo clique na batida; relógio do navegador adiantado; atestado visto por quem não pode; exportação com acentos e com células que começam com `=`; fuso (batida às 22h30).

**Decisões do dono:** hora extra, **tolerância de atraso**, formato do "exportar para a folha", fonte dos feriados (cálculo ou biblioteca, com licença), quem registra ponto retroativo, se o RH aprova direto, uso das funções do grupo.

**Riscos:** regra de negócio pesada; o agendador depende de Always On; PDF do espelho (fontes e memória).

---

## Fatia 4: comunicação e documentos

**Entrega:** o RH publica avisos (simples e detalhados) e acompanha quem leu; publica holerites que ficam guardados por pessoa; registra advertências que a pessoa reconhece ou contesta; todos veem os aniversariantes.

**Incrementos:** **4a.** avisos. **4b.** holerites. **4c.** advertências e aniversariantes (a API; o cartão fica na tela inicial da 7d).

**Telas (16):** RH 07 `6:35315` e 08 `6:35821` (holerites); 11 `6:37261` ficha, aba Advertências; 14 `6:38403` e 15 `6:38759` (advertências); 27 `6:45002` e 28 `6:45400` (avisos). Funcionário 36 `6:47672` holerites; 37 `6:47862` e 38 `6:48086` (avisos); 43 `6:49216` advertências. Gestor 49 `6:50809` holerites; 50 `6:50994` avisos; 53 `6:52591` e 60 `6:54913` (registrar advertência); 59 `6:54615` advertências da equipe.

**Banco:** *já existe:* `AVISOS`, `AVISOS_VISUALIZACOES`, `HOLERITES`, `ADVERTENCIAS`. *Scripts novos:* lista A (avisos com fixar e rascunho; ajuste da chave única dos holerites; advertências, em decisão). Os triggers ficam para depois (decisão de 2026-10-28).

**API (esboço):** avisos (criar, rascunho, agendar, encerrar, ler, quem leu e lembrar), holerites (enviar PDF, listar, baixar por link temporário, **enviar em lote**), advertências (registrar como RH ou gestor, confirmar ciência, contestar, responder), aniversariantes dos próximos 14 dias.

**Regras:**
- Aviso simples tem até 280 caracteres; o detalhado tem seções, **sem anexo PDF**; **sempre para todos** (sem público por setor); **sem HTML cru**. Holerite **com tipo** (o Kauan cria) e **sem "novo" ou "visto"**.
- As linhas de leitura nascem na publicação, **uma por funcionário ativo** (o trigger do grupo já faz isso e serve, porque o aviso é sempre para todos); quem for admitido depois vê os avisos vigentes; abrir marca como lido.
- Holerite: PDF de até 5 MB no Blob; o banco **proíbe apagar** holerite (trigger), então um holerite errado se **substitui** (nova versão) e não se exclui; o arquivo só é enviado depois de a regra do banco aprovar; sobras são limpas por job; o download valida o escopo e dá um link temporário como anexo, nunca público; competência futura (férias antecipadas) depende da decisão sobre o trigger.
- O gestor registra advertência só para a própria equipe; o RH recebe uma cópia.

**Testes:** o funcionário A abre o holerite do B (negado, inclusive por link); link expirado; PDF acima de 5 MB ou que não é PDF; arquivo órfão depois de recusa do banco; publicação agendada dispara uma vez; admitido depois vê o aviso vigente; contestar duas vezes; lote de holerites com uma linha inválida.

**Decisões do dono:** envio em lote de holerites (o e-mail de todo aviso publicado é sempre enviado: decidido em 2026-10-28); tipos de advertência; o que acontece depois da contestação; competência futura e substituição de holerite; retenção de holerite de desligado.

---

## Fatia 5: formulários e quiz

**Entrega:** o RH monta formulários (entrevista, admissão, avaliação, quiz, pesquisa, sugestão), publica para pessoas, setores ou por link público e acompanha as respostas; o funcionário responde os pendentes.

**Incrementos:** **5a.** construtor (assistente de 4 etapas com prévia). **5b.** responder e acompanhar, interno. **5c.** link público e entrevista, **uma superfície de segurança à parte** (candidatos que não são funcionários). **5d.** avaliações por ciclo.

**Telas (9):** RH 16 `6:39069` lista; 17 `6:39524`, 18 `6:39994`, 19 `6:40410`, 20 `6:40875` (etapas Detalhes, Perguntas, Verificação, Publicação); 21 `6:41305` quem respondeu. Funcionário 39 `6:48327` e 40 `6:48590`. Gestor 57 `6:54111` formulários da equipe. Mais a tela pública do candidato, que desenho.

**Banco:** *já existe:* `FORMULARIOS`, `FORMULARIOS_PERGUNTAS`, `FORMULARIOS_RESPOSTAS`. *Scripts novos:* tipos, publicação, prazo, nota mínima, link público, atribuição (gera os "pendentes"), respondentes externos e anônimos (e o trigger reescrito), envio que agrupa as respostas, opções e pontuação, ciclos.

**API (esboço):** formulários (criar, editar, verificar, publicar, encerrar), perguntas (múltipla escolha, eliminatória, resposta curta e longa, nota dada pelo RH, quiz com pontuação, escala 1 a 5, envio de arquivo), responder, quem respondeu e lembrar, exportar, endpoint **público** do candidato.

**Regras:**
- O gestor vê só quem respondeu, não o conteúdo.
- **A resposta certa e as perguntas eliminatórias nunca vão para o navegador** (vale também para o quiz interno); a nota é calculada no servidor.
- Arquivo enviado pelo candidato é servido como **anexo**, nunca exibido no mesmo domínio; há aviso de privacidade e um job apaga os dados de candidato vencidos.
- **Anonimato:** o interceptor de auditoria grava antes e depois, e hoje `ID_FUNCIONARIO` é obrigatório, então sem desenho próprio a pesquisa "anônima" seria só visual. O desenho: tabela ou coluna que não identifica quem respondeu e resultados agregados com tamanho mínimo de grupo.
- E-mail "formulário atribuído"; "verificação" confere título, obrigatórias, resposta certa do quiz e dado sensível.

**Testes:** candidato com currículo grande ou de tipo errado; o mesmo link respondido várias vezes (limite); fora do prazo; eliminatória; o gestor tentando abrir respostas; formulário agendado publica uma vez; a resposta certa não aparece em nenhuma resposta da API; pesquisa anônima não deixa rastro de quem respondeu; exportação com `=`.

**Decisões do dono:** anonimato (avaliação do gestor e pesquisa de clima); como a verificação de dado sensível funciona; proteção do link público (limite simples ou captcha); retenção dos dados de candidato; como as respostas da admissão chegam aos campos do cadastro.

---

## Fatia 6: TAD (treinamentos)

**Entrega:** o RH cria treinamentos com módulos e quiz final e publica; a pessoa avança por módulo, conclui e recebe um certificado em PDF com código de validação.

**Incrementos:** **6a.** treinamentos e módulos. **6b.** progresso, quiz e certificado. **6c.** presencial, turmas e lembretes.

**Telas (8):** RH 22 `6:41863` lista e quem fez; 23 `6:42569`, 24 `6:43274`, 25 `6:43968`, 26 `6:44679` (etapas). Funcionário 41 `6:48821` e 42 `6:49071`. Gestor 58 `6:54370` treinamentos da equipe.

**Banco:** *já existe:* `TREINAMENTOS`, `TREINAMENTOS_FUNCIONARIOS`, `TREINAMENTOS_CERTIFICADOS`. *Scripts novos:* modalidade, módulos, progresso, turmas e inscrições, ligação com o quiz, nota mínima por treinamento e a escala; os triggers de nota e de certificado reescritos (lista B).

**API (esboço):** treinamentos (criar, publicar, listar), módulos, progresso, conclusão, certificado (**o trigger já cria a linha com um UUID; a aplicação gera o PDF e atualiza essa linha**, sem tentar inserir outra), validação por código, inscrição em opcionais e turmas, exportar lista.

**Regras:**
- A nota mínima vira configurável por treinamento (hoje é 70 fixo no trigger).
- O código público de validação do certificado é o **UUID**, não o número sequencial do Figma (que seria enumerável); o número amigável aparece à parte. A validação pública mostra o mínimo de dados.
- Lembrete 7 dias antes do fim e inclusão de quem for admitido até o prazo, pelo agendador.
- PDF: ver "Decisões que atravessam fatias".

**Testes:** concluir sem terminar os módulos; nota abaixo da mínima configurada; certificado emitido uma vez e o PDF anexado à linha criada pelo trigger; código inexistente ou de outra pessoa; gestor vendo só o setor; turma cheia; fonte com acento no PDF.

**Decisões do dono:** onde ficam os vídeos (Blob ou link externo); modelo visual do certificado e o formato do número; escala e regra da nota mínima.

---

## Fatia 7: gestão

**Entrega:** férias em duas etapas, benefícios com pedidos, pagamento e calendário, e os painéis do RH e do gestor.

**Incrementos:** **7a.** férias. **7b.** benefícios e dependentes. **7c.** pagamento e calendário. **7d.** painéis e telas iniciais completas (**dependem das fatias 3 a 6**).

**Telas (11):** RH 00 `6:32009` painel inicial; 05 `6:34321` ficha, aba Férias; 06 `6:34873` modal registrar férias. Funcionário 32 `6:46598` tela inicial; 34 `6:47148` férias; 35 `6:47418` pedir férias; 44 `6:49478` benefícios; 45 `6:49770` aderir. Gestor 46 `6:50018` tela inicial; 48 `6:50559` férias; 51 `6:51445` painel da equipe.

**Banco:** *já existe:* `FERIAS` (5 a 30 dias por período, abono de 1 a 10, um gestor), `BENEFICIOS`, `BENEFICIOS_FUNCIONARIOS`. *Scripts novos:* segunda etapa das férias e o trigger reescrito, adiantamento do 13º, "sugerir outra data", pedidos de benefício, dependentes, trajeto do vale-transporte, regra e exceções de pagamento.

**API (esboço):** férias (saldo e períodos, pedir, analisar como gestor, confirmar como RH, registrar pelo RH, sugerir outra data), benefícios (catálogo, adesão, análise), pagamento (regra padrão e exceções por mês), calendário (próximos dias), painéis. E-mail com o resultado das férias.

**Regras de férias do protótipo (a confirmar):** até 3 partes (uma de pelo menos 14 dias, as outras de pelo menos 5); abono de até 10 dias; adiantamento do 13º; pedido com 30 dias de antecedência; não começar nos 2 dias antes de feriado ou fim de semana; pagamento até 2 dias antes. O banco já exige 5 a 30 dias e abono de 1 a 10; o resto fica com a aplicação. **O trigger hoje não permite "o RH confirma depois do gestor"** (ver lista B).

**Próximo pagamento:** regra padrão (dia do mês, por exemplo "todo dia 30") e exceções por mês; se o mês não tem o dia, vale o último; se cai em fim de semana ou feriado, antecipa para o dia útil anterior (padrões propostos). Liga também a publicação agendada de holerite "para o dia do pagamento" (fatia 4).

**Painéis:** pendências, presença agora, próximos dias, treinamentos em andamento, últimas alterações; no gestor, férias por semana e aprovações. A tela inicial do funcionário inclui o cartão de ponto (fatia 3) e os aniversariantes (API da fatia 4).

**Testes:** período sobreposto; férias perto de feriado; saldo insuficiente; duas etapas (e o gestor aprovando o próprio pedido: negado); pagamento em fevereiro; painel de gestor sem equipe; inativar o funcionário cancela férias futuras (efeito do trigger do grupo, sem aviso e sem registro próprio).

**Decisões do dono:** regras de férias e a segunda etapa; catálogo de benefícios e regras de adesão; regras de pagamento em fim de semana e feriado; definição de "presença agora".

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
| Quiz de treinamento | 6 | 5 | O quiz é um formulário do tipo quiz |
| Espelho em PDF e certificado em PDF | 3 e 6 | 3 | A biblioteca de PDF entra na fatia 3 |
| Blob, Storage Account e e-mail no Azure | 1c e 1a | cada uso | Criados quando aparecem, com custo dito antes |
| Painéis do RH e do gestor | 7d | 3 a 6 | Só na 7d |
| Abas Ponto, Férias, Treinamentos e Advertências da ficha | 1 | 3, 4, 6, 7 | A ficha cresce por fatia |

## O que precisa de resposta, em ordem de urgência

1. **Antes de congelar a base (fim da fatia 0), com o grupo:** a lista da seção "Antes do congelamento" (A, B e C): o que entra agora e o que vira script novo. Não é bloqueio: depois do congelamento tudo continua possível por scripts novos.
2. ~~Professor: exige domínio separado da persistência?~~ **Respondido em 2026-10-28: não exige.**
3. **Dono:** a conta do Azure com créditos (o dono vai verificar em 2026-10-28).
4. **Começo de cada fatia:** as decisões listadas nela.
