# RH-System: design

Data: 2026-10-21 · Status: aprovado pelo Miguel em 2026-10-21 (seções 1 a 13, incluindo as decisões da seção 13.2) · Tipo: TCC, com demo publicada no Azure

## 1. Objetivo e contexto

Sistema web de RH para cadastro e acompanhamento de colaboradores, ponto, comunicação, documentos, formulários e treinamentos. É um projeto acadêmico: o critério de sucesso é uma demo funcional no Azure, uma arquitetura defendível diante da banca (Clean Architecture) e um DER/MER e script SQL finais que batam com o banco real.

**Fontes deste design:** o esboço do dono, o script [RHSystem_Criacao_Tabelas_MySQL.sql](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql) e as respostas do dono durante o brainstorming. O protótipo do Figma (alta complexidade) foi lido em texto (seção 13). A pasta `Documentação/` não foi usada, a pedido do dono.

### Funcionalidades por perfil

**RH (acesso total):** gerenciar colaboradores (adicionar, editar, inativar, visualizar), importar colaboradores por Excel, criar formulários e quiz (entrevista de emprego automática, formulário de admissão, avaliação de funcionários para superiores e vice-versa), ponto (calendário do Brasil com feriados municipais cadastráveis, visualização e edição por funcionário), advertências, avisos simples e detalhados, holerites por funcionário, TAD (treinamentos de onboarding e similares), calendário do RH, notificações por e-mail e log de auditoria.

**Funcionário (acesso básico):** bater ponto, pedir manutenção de ponto (justificativa obrigatória), apresentar atestado, ver banco de horas, saldo de férias e próximo pagamento, pedir férias, ver holerites recebidos, ver aniversariantes, ver avisos, responder formulários pendentes, ver benefícios e emitir certificado após concluir um TAD.

**Gestor:** tudo do funcionário, mais o painel da equipe do **setor** de que é responsável. Da equipe vê contato, contrato, jornada, ponto, treinamentos, formulários respondidos e advertências. Faz a primeira etapa de aprovação de ajustes de ponto e de férias e registra advertências. **Não vê** CPF, endereço, holerites nem documentos, e não edita o cadastro (decisão do dono em 2026-10-21, seguindo o Figma).

## 2. Stack e restrições

- **Back-end:** C# no .NET 10 com Clean Architecture e EF Core **9.x**.
- **Banco:** MySQL com `Pomelo.EntityFrameworkCore.MySql` **9.0.0**.
- **Front-end:** React com TypeScript. Bibliotecas de UI, rotas e formulários serão escolhidas depois de ver o Figma.
- **Hospedagem:** API no Azure App Service, React no Azure Static Web Apps, Azure Database for MySQL Flexible Server e Azure Blob Storage.
- **Controle de versão:** GitHub (`xmiguelps/RH-System`), com GitHub Actions.

**Sobre o provider.** Pela API do NuGet (lida via resumo de ferramenta), o Pomelo termina na versão estável 9.0.0 e não existe versão 10.x. Um post de terceiros afirma que o Pomelo 9.0.0 exige EF Core 9.x e que o EF Core 9 roda no runtime do .NET 10. Isso é **inferido, não testado**; o teste de fumaça da fatia 0 confirma. Plano B: o fork Microting (`Microting.EntityFrameworkCore.MySql` 10.0.x, não oficial) ou trocar para SQL Server.

## 3. Arquitetura

```
RH-System/
  back-end/
    src/
      RHSystem.Domain          entidades, enums e regras de negócio puras
      RHSystem.Application     casos de uso, DTOs, interfaces, validações
      RHSystem.Infrastructure  EF Core + MySQL, migrations, Blob Storage, e-mail, Excel, jobs
      RHSystem.Api             endpoints, autenticação JWT, tratamento de erros, Swagger
    tests/
      Domain.Tests, Application.Tests, Api.IntegrationTests
  front-end/                   React + TypeScript
  Banco de Dados/, Figma/, Documentação/   (já existem)
  docs/                        specs e planos
```

- **Dependências:** Api e Infrastructure dependem de Application; Application depende de Domain; Domain não depende de nada.
- **Monólito modular:** dentro de cada projeto há uma pasta por módulo (Colaboradores, Ponto, Avisos e assim por diante). Um único deploy.
- **Casos de uso** são classes simples, sem biblioteca de mediação.
- **Entidades do Domain** são mapeadas direto pelo EF (Fluent API na Infrastructure), sem um segundo modelo de persistência. Se o professor exigir a separação, isso muda.
- **Testes de integração** usam MySQL real em contêiner (exige Docker na máquina de desenvolvimento).

## 4. Modelo de dados e perfis

O banco é **code-first**: as entidades e as migrations do EF são a fonte da verdade, e o script e o DER atuais viram ponto de partida. A última fatia regenera o DER/MER e o script SQL a partir do banco real.

**Mudanças estruturais em relação ao script atual:**

1. `GESTORES` deixa de ser tabela. O gestor é um funcionário com perfil Gestor, e o **setor** tem um funcionário responsável. O gestor de cada pessoa é o responsável do setor dela, e **não existe `GestorId` em `FUNCIONARIOS`**. A equipe de um gestor é o conjunto de funcionários dos setores de que ele é responsável. Isso elimina as chaves estrangeiras circulares e a duplicidade. O `ID_GESTOR` de `PONTOS_MANUTENCOES` e `ADVERTENCIAS` vira "funcionário que analisou ou aplicou".
2. `FUNCIONARIOS_DETALHES` sai. Os campos do cadastro (seção 13.3: matrícula, CPF, tipo de contrato, unidade, jornada e os demais) ficam em `FUNCIONARIOS`. O endereço e outros dados que o próprio colaborador completa no formulário de admissão também ficam na ficha; como as respostas do formulário chegam a esses campos é definido na fatia 5. O histórico de alterações é coberto pela auditoria.
3. `NIVEIS_DE_ACESSO` vira o enum `Perfil` (`RH`, `Gestor`, `Funcionario`) na tabela de usuários.
4. Inativar não apaga: o funcionário continua no banco com holerites e histórico, e perde o login.

**Autorização.** Cada caso de uso recebe o usuário logado e passa por **um filtro de escopo único**: RH vê tudo; Gestor vê a equipe do setor, **só os campos permitidos** (contato, contrato, jornada, ponto, treinamentos, formulários e advertências); Funcionário vê só a si mesmo. CPF, endereço, holerites e documentos são só do RH e do próprio colaborador. A restrição vale também por **campo**: cada perfil recebe um DTO diferente, e esconder o dado só na tela não conta. Os testes de integração cobrem uma matriz de perfil por endpoint, verificando linhas e campos.

**Arquivos** (holerite, atestado, certificado) ficam no Blob Storage, e o banco guarda só a referência. O download passa por um endpoint que valida o escopo e devolve um link temporário. Nunca há link público.

**Tabelas novas, por fatia** (o desenho detalhado vem no plano de cada fatia; o mapeamento do Figma está na seção 13.3):
- Fundação: auditoria (com IP e origem), importações em lote, fila de e-mails e tarefas agendadas.
- Colaboradores: unidades, documentos do colaborador e tabelas de apoio (cargos com jornada, setores, benefícios).
- Ponto: feriados (por unidade), várias marcações por dia (hoje `PONTOS` guarda um par entrada/saída por dia), ajustes em duas etapas, atestados e lançamentos do banco de horas.
- Comunicação e documentos: leitura de avisos, holerites com tipo e agendamento, advertências com ciência e contestação.
- Formulários: ciclos de avaliação, atribuição a quem deve responder (gera os "pendentes"), respondentes externos, respostas agrupadas por envio e pontuação do quiz.
- TAD: módulos, progresso, inscrições em turmas e certificados.
- Gestão: saldo e pedidos de férias em duas etapas, pedidos de benefício e dependentes, calendário do RH, regra e exceções de pagamento.

## 5. Autenticação, e-mails e auditoria

**Login.** E-mail e senha, com a senha guardada só como hash. O token de acesso é um JWT de vida curta, com refresh token no banco, revogável e com rotação. Como o React e a API ficam em domínios Azure diferentes, e navegadores costumam bloquear cookies entre domínios, os tokens vão por cabeçalho. O custo é um risco maior de roubo de token em caso de XSS, mitigado com expiração curta, rotação do refresh e cabeçalhos de segurança no front.

**Primeiro acesso e "esqueci minha senha"** usam a mesma mecânica: link por e-mail com token de uso único e prazo curto, guardado só como hash. Há limite de tentativas de login. O funcionário inativo não entra e perde o refresh token.

**E-mails.** O back-end decide quando enviar, e uma interface com implementação trocável envia. O provedor (por exemplo o serviço de e-mail do Azure ou SMTP) é decidido na fatia 1 e ainda **não foi verificado**. No desenvolvimento, os e-mails são capturados localmente. Os e-mails são gravados numa **fila no banco**, na mesma transação do fato que os gerou, e um serviço em segundo plano os envia com novas tentativas.

**Tarefas agendadas.** O mesmo serviço em segundo plano da API executa as publicações agendadas (holerite, aviso, formulário e treinamento), a consolidação diária do banco de horas e os lembretes automáticos. O estado fica no banco, as tarefas são idempotentes, e há uma instância só.

**Eventos que disparam e-mail** (aprovados): (1) convite e redefinição de senha; (2) novo aviso publicado; (3) holerite disponível; (4) formulário atribuído; (5) resultado de pedido de ajuste de ponto e de férias; (6) advertência registrada.

**Eventos adicionais vindos do Figma, a confirmar:** aviso com "enviar por e-mail" opcional; lembretes manuais ("lembrar quem falta") e o lembrete automático do formulário de admissão; treinamento publicado e lembrete 7 dias antes do prazo; resposta a pedido de benefício; férias registradas pelo RH.

**Auditoria.** Um interceptor do EF registra quem, quando, qual entidade e quais campos mudaram (antes e depois). Ações que não alteram dados (login, tentativa de acesso negado, marcação de ponto, download de holerite, importação, e-mails automáticos) são registradas explicitamente, com **IP e origem**. Senhas, hashes e tokens ficam fora do log. O log é só de inclusão, sem endpoint de edição ou exclusão, e só o RH consulta, com filtros por período, pessoa, área e ação e exportação em CSV.

## 6. Importação de colaboradores por Excel

O botão na tela de cadastro alterna para o modo "Importar planilha". Fluxo:

1. O RH baixa o **modelo da planilha**, com as colunas certas e uma aba com os valores válidos de cargo e setor.
2. Envia o arquivo **em uma única requisição**. O servidor valida **todas** as linhas sem gravar nada: campos obrigatórios, formato de e-mail, e-mail duplicado no arquivo ou já cadastrado, e existência de cargo e setor (o gestor vem do setor).
3. A tela mostra uma **prévia** com os erros por linha e coluna.
4. Regra **tudo ou nada**: nada é gravado até o arquivo estar sem erros. O RH corrige a planilha e envia de novo.
5. Ao confirmar, o servidor grava em lotes, limpando o rastreamento do EF entre os lotes.
6. O resultado fica registrado (quem importou, quando, totais, erros) e entra na auditoria.

**Decisões:**
- A importação **só cadastra**. Atualizar por planilha está fora do escopo.
- O processamento é **síncrono, com limite por arquivo** (proposta: 500 linhas).
- Ninguém recebe senha: o usuário nasce sem senha e recebe um **convite por e-mail**.
- O arquivo enviado é apagado depois de confirmado ou expirado.
- Só o perfil RH acessa a importação.
- A biblioteca de leitura de Excel (licença livre, por exemplo o ClosedXML) é confirmada na implementação.
- A planilha espelha o cadastro mínimo do Figma (seção 13.3) e repete as ações "depois de salvar" (convite, formulário de admissão, onboarding).
- O Figma não tem essa tela; os estados (escolher arquivo, prévia, resultado) são descritos quando formos às telas.

## 7. Ambientes, deploy e testes

- **Local:** `docker compose` com MySQL, captador de e-mails e Azurite (emulador do Blob).
- **Azure:** um único ambiente na nuvem, num único grupo de recursos. A região depende da conta (algumas contas de estudante limitam regiões; **inferido, não verificado**).
- **Pipeline (GitHub Actions):** a cada push ou PR roda build, testes e verificação de formatação, no back-end e no front-end. O deploy no Azure é **manual ou por tag**.
- **Migrations** rodam na **inicialização da API**, controladas por configuração. Serve para uma instância só; não serve para várias ao mesmo tempo.
- **Agendador e plano do App Service:** o serviço em segundo plano só roda enquanto a API está de pé. Isso exige o recurso Always On, que eu **inferi** não existir no plano gratuito (**não verificado**). O plano escolhido define o custo e é conferido antes do deploy da fatia 0.
- **Segredos** (connection string, chave do JWT, chaves do Blob) ficam nas configurações do App Service e nos secrets do GitHub. O Key Vault fica como melhoria futura.
- **Rede:** o firewall do MySQL libera só o App Service e, quando preciso, o IP do desenvolvedor.
- **Observabilidade:** endpoint de saúde e logs do App Service. O Application Insights é opcional.
- **Dados de demonstração:** um comando de seed cria dados **fictícios**, atrás de configuração. Nunca entra dado real.
- **Testes:** unidade (Domain e Application); integração com MySQL real, incluindo a matriz de perfis; testes de componente no front; ponta a ponta só para 2 ou 3 fluxos da demo (login, bater ponto, importar planilha).
- **Quando criar o ambiente Azure:** ao fim da fatia 0, depois de o dono confirmar qual conta tem os créditos, com novo deploy ao fim de cada fatia. Recursos que gastam crédito são parados ou apagados entre as sessões, e o custo de cada recurso é dito antes de criá-lo.

## 8. Fatias e ordem

A ordem segue as dependências. Se o escopo precisar encolher, o corte é por fatia inteira, de trás para a frente.

| # | Fatia | Depende de |
|---|---|---|
| 0 | **Esqueleto:** solution em camadas, `docker compose`, teste de fumaça do provider, login, shell do React, CI, deploy no Azure | |
| 1 | **Colaboradores e base transversal:** escopo por perfil (linhas e campos), auditoria, e-mail com fila e agendador, convite, redefinição de senha, cadastros de apoio (cargos, setores, unidades), cadastro com os campos do Figma, ficha, documentos do colaborador, edição, inativação | 0 |
| 2 | **Importação por Excel** | 1 |
| 3 | **Ponto:** quatro marcações por dia, espelho, ajustes em duas etapas (gestor e RH), atestado, feriados (nacionais e estaduais automáticos, municipais cadastrados), banco de horas com consolidação diária e as telas de ponto da equipe do gestor | 1 |
| 4 | **Comunicação e documentos:** avisos (simples, detalhados, agendados, com leitura), aniversariantes, holerites (tipos e agendamento), advertências (ciência e contestação) | 1 |
| 5 | **Formulários e quiz:** entrevista com link público, admissão, avaliações por ciclo, pesquisa, sugestões, quiz, assistente de 4 etapas e pendentes | 1 |
| 6 | **TAD:** treinamentos com módulos e progresso, inscrições, quiz final e certificado em PDF com código de validação | 5 (usa o quiz) |
| 7 | **Gestão:** férias em duas etapas, benefícios (pedidos e dependentes), painéis do RH e do gestor, calendário do RH ("próximos dias"), próximo pagamento | 3, 4 |
| 8 | **Fechamento:** DER/MER e script regenerados do banco real, seed completo da demo, testes de ponta a ponta, ensaio da banca | todas |

**Próximo pagamento (fatia 7).** O RH cadastra uma **regra padrão** (dia do mês, por exemplo "todo dia 30") e pode lançar **exceções de data para um mês específico**, que valem mais que a regra. A regra é única para a empresa toda. Se o mês não tem o dia, vale o último dia do mês. Se a data cair em fim de semana ou feriado, o pagamento **antecipa para o dia útil anterior**. As duas últimas regras são padrões propostos, a confirmar na revisão deste spec.

**Fonte dos feriados nacionais (fatia 3).** A escolha entre calcular por regra no código (incluindo os móveis, derivados da Páscoa) e usar uma biblioteca ou API é feita na fatia 3, com a licença conferida.

## 9. Fora do escopo (trabalhos futuros)

Cálculo de folha de pagamento (o RH sobe o PDF do holerite), integração com relógio de ponto físico (REP ou biometria; o dono confirmou que não haverá equipamento físico, o ponto é só pelo sistema), app mobile e layout para celular (o Figma só tem desktop), integração direta com sistema de folha (o "exportar para a folha" gera só um arquivo), login corporativo (SSO), várias empresas no mesmo sistema, atualização de colaboradores por planilha.

**Limitação documentada:** o horário da batida de ponto vem sempre do servidor, nunca do navegador. A conformidade legal completa do ponto eletrônico não é objetivo deste TCC e fica registrada como limitação.

## 10. Decisões do dono durante o brainstorming

| Decisão | Escolha |
|---|---|
| Propósito | TCC, demo no Azure |
| Prazo | Sem prazo por enquanto |
| Hospedagem | App Service, Static Web Apps, MySQL Flexible Server, Blob Storage |
| Banco | Code-first; DER atualizado no final |
| Provider | MySQL com EF Core 9 e Pomelo 9.0.0 |
| Repositório | Duas pastas, `back-end/` e `front-end/` |
| Equipe do gestor | O setor de que ele é responsável (o gestor de cada pessoa vem do setor; não há `GestorId`) |
| O que o gestor vê | Contato, contrato, jornada, ponto, treinamentos, formulários e advertências da equipe; sem CPF, endereço, holerites e documentos; não edita o cadastro |
| Importação com erro | Tudo ou nada |
| E-mails | Os 6 eventos da seção 5 |
| Ambiente Azure | Ao fim da fatia 0, após confirmar a conta |
| Holerite | O RH sobe o PDF |
| Próximo pagamento | Regra padrão mais exceções |
| Relógio de ponto físico | Fora do escopo; o ponto é só pelo sistema |

## 11. Riscos

1. **Provider MySQL no .NET 10.** Mitigado pelo teste de fumaça; plano B na seção 2. O runtime escolhido também precisa estar disponível no App Service (**não verificado**; se não estiver, usar .NET 9 com EF Core 9).
2. **Primeiro uso do Azure e créditos não confirmados.** Mitigado pelo deploy cedo e pelo controle de custo.
3. **Figma lido só em texto.** Capturas de tela e fluxos de navegação ainda não foram vistos e podem revelar detalhes que o texto não mostra.
4. **Regras de ponto, banco de horas e férias** (jornada, tolerância, hora extra, períodos aquisitivos) pesam em regra de negócio. O dono é consultado no início das fatias 3 e 7.
5. **Escopo grande.** Mitigado por fatias independentes e corte por fatia inteira.
6. **Volume do front-end:** 61 telas, só de desktop. Cada fatia inclui as suas próprias telas.
7. **Agendador e plano do App Service.** Se o Always On não estiver disponível no plano barato, as publicações agendadas e a consolidação do banco de horas podem não rodar. Conferir antes do deploy da fatia 0 (seção 7).

## 12. Pendências

- Figma conectado e mapeado em texto (seção 13). Falta ver as capturas de tela das telas principais e os fluxos do protótipo.
- Confirmar qual conta do SENAI tem os créditos (antes do deploy da fatia 0).
- O `pesquisador` foi testado e funcionou. O `revisor-de-plano` ainda não rodou: o plano passa por ele antes de executar.
- Confirmar com o professor se exige separação entre modelo de domínio e de persistência.
- Confirmar as regras de pagamento em fim de semana e feriado (seção 8).
- Definir quem aprova os pedidos de ajuste de ponto e de férias de um gestor (o Figma não mostra um gestor acima dele) e se o responsável do setor conta como membro da própria equipe. Decidir na fatia 1.
- Confirmar os eventos de e-mail adicionais da seção 5.

## 13. Mapeamento do Figma (alta complexidade)

**Fonte e limites.** Arquivo `bsZnCjHNeNgdGaLzVcaxjL`, página "Alta Complexidade" (`1:2`): 61 telas de 1440 px, só no tema claro. Li os **textos** das 61 telas, extraídos da estrutura do arquivo. **Não vi capturas de tela nem os fluxos de navegação** do protótipo. Nomes, datas e números nas telas são exemplos, não requisitos.

### 13.1 Telas por perfil

- **RH (32):** painel inicial; colaboradores (lista, adicionar, ficha com abas Dados, Ponto, Férias, Holerites, Documentos e Advertências, e modais registrar férias, enviar holerite e adicionar documento); ponto e feriados (com modal de pedidos de ajuste); advertências (e nova); formulários (lista, assistente de 4 etapas, quem respondeu); treinamentos (lista e assistente de 4 etapas); avisos (e novo); cadastros (cargos e modal); log de auditoria.
- **Funcionário (14):** tela inicial; meu ponto; férias (e pedir); holerites; avisos (e aviso aberto); formulários (e respondidos); treinamentos (e concluídos); advertências; benefícios (e aderir).
- **Gestor (15):** tela inicial, meu ponto, férias, holerites e avisos (as mesmas do funcionário); painel da equipe (com ficha e registrar advertência); ponto da equipe (com modal de pedido e espelho da pessoa); formulários, treinamentos e advertências da equipe (e registrar).
- **Não existem no Figma:** importação por Excel (já sabido), login, convite e redefinição de senha, tela de atestado (há só o botão "Enviar atestado"), calendário do RH como tela própria (aparece como "Próximos dias" no painel) e notificações.

### 13.2 Divergências com as seções aprovadas (decididas em 2026-10-21 e incorporadas às seções 1, 4, 5, 6, 7, 8, 9, 10 e 11)

1. **O que o gestor vê.** A seção 1 e o esboço dizem "tudo" da equipe. A ficha do gestor diz: "Só o RH altera dados cadastrais. CPF, endereço, holerites e documentos ficam visíveis apenas para o RH." O gestor vê contato, contrato, jornada, ponto, treinamentos e advertências.
2. **Quem é a equipe do gestor.** A seção 4 usa `GestorId` (subordinados diretos). O Figma define a equipe como o **setor** do gestor ("Equipe Comercial, 8 pessoas... quem está no seu setor"), e no cadastro o gestor é "o gestor do setor escolhido".

**Decisões do dono:** (1) o gestor vê o que o Figma desenha, sem CPF, endereço, holerites e documentos; (2) a equipe do gestor é o setor dele, e `GestorId` deixa de existir.

### 13.3 O que o Figma acrescenta, por fatia

- **Fatia 1 (colaboradores):**
  - Cadastro mínimo: nome, CPF, nascimento, telefone, e-mail corporativo, admissão, tipo de contrato, cargo, setor, gestor, nível de acesso e benefícios. O resto (endereço e outros) o próprio colaborador completa no formulário de admissão.
  - "Depois de salvar": enviar convite, enviar o formulário de admissão e incluir no treinamento de onboarding.
  - Matrícula, unidade (filial), jornada com horário (por exemplo 08:00 às 17:48), situação hoje (Ativo, Em admissão, Experiência, Atestado, Férias) e fim do período de experiência.
  - Documentos do colaborador: tipos, validade, situação Enviado ou Pendente, "pedir para a pessoa enviar" e lembrete. Só o RH e o próprio colaborador veem.
  - Cadastros de apoio mantidos pelo RH, como cargos com carga semanal.
  - Lista com busca por nome, CPF ou matrícula e filtros por setor e contrato.
- **Fatia 2 (importação):** a planilha deve espelhar o cadastro mínimo e repetir as ações "depois de salvar".
- **Fatia 3 (ponto):**
  - Quatro marcações por dia (entrada, saída para o almoço, volta, saída), espelho por colaborador com total, banco de horas, atrasos e faltas.
  - Banco de horas "atualizado até ontem", o que indica consolidação diária, e lançamento manual pelo RH.
  - Pedido de ajuste com justificativa em **duas etapas: gestor e depois RH**, com e-mail em cada etapa. O RH também pode aprovar direto.
  - Atestado enviado pelo funcionário e validado pelo RH.
  - Feriados nacionais e estaduais **importados automaticamente**. Municipais são cadastrados pelo RH, com "vale para" (unidade) e "repetir todo ano".
  - Exportar para a folha, espelho em PDF.
- **Fatia 4 (comunicação e documentos):**
  - Avisos simples (até 280 caracteres) e detalhados (seções e PDF), com público (todos ou setor), publicação agendada, expiração, e-mail opcional, fixar no topo e contagem de quem leu. Abrir o aviso o marca como lido.
  - Holerites com tipo (mensal, 13º, férias), PDF de até 5 MB, publicar agora ou agendar para o dia do pagamento, e e-mail ao publicar. A auditoria de exemplo cita publicação em lote para 101 colaboradores.
  - Advertências verbal ou escrita, aplicadas pelo RH ou pelo gestor. O colaborador confirma a ciência ou contesta com texto, e o RH responde por e-mail.
  - Aniversariantes dos próximos 14 dias na tela inicial.
- **Fatia 5 (formulários):**
  - Tipos: entrevista, admissão, avaliação (gestor avalia a equipe e a equipe avalia o gestor, por ciclo), quiz, pesquisa de clima e sugestões.
  - Assistente de 4 etapas com prévia ao vivo e verificação antes de publicar.
  - Perguntas: múltipla escolha, eliminatória, resposta curta e longa, nota dada pelo RH, quiz com pontuação, escala de 1 a 5 e envio de arquivo.
  - Publicação por link público (candidatos que **não são funcionários**), setores ou pessoas, agendada, com prazo e nota mínima.
  - Resultado por e-mail ao RH e convite automático dos aprovados.
- **Fatia 6 (TAD):**
  - Modalidade online, híbrido ou presencial, obrigatório ou opcional, com prazo.
  - Módulos de conteúdo (vídeo, texto, link, PDF, aula presencial) e progresso por módulo.
  - Inscrição em opcionais e em turmas presenciais.
  - Quiz final criado em Formulários, nota mínima e certificado em PDF com código (por exemplo `LGPD-2026-00482`).
  - Incluir quem for admitido até o fim do prazo e lembrete 7 dias antes.
- **Fatia 7 (gestão):**
  - Férias com as regras CLT do protótipo: até 3 partes (uma de pelo menos 14 dias e as outras de pelo menos 5), abono de até 10 dias, adiantamento do 13º, pedido com 30 dias de antecedência, não começar nos 2 dias antes de feriado ou fim de semana, e pagamento até 2 dias antes. Fluxo gestor e depois RH, com opção "sugerir outra data".
  - Benefícios com catálogo, pedidos de adesão ("quero aderir"), dependentes e beneficiários, trajeto do vale-transporte, resposta do RH em até 5 dias úteis.
  - Painel do RH: pendências, presença agora, próximos dias (que é o calendário do RH), treinamentos em andamento e últimas alterações.
  - Painel do gestor: equipe, férias por semana e aprovações.

### 13.4 Diferenças em relação ao script SQL

- `ADVERTENCIAS` aceita `PENDENTE`, `APLICADA` e `CANCELADA` ([linha 342](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L342)). O Figma usa Aguardando ciência, Ciente e Contestada, mais o tipo Verbal ou Escrita e o texto da contestação.
- `FORMULARIOS.TIPO_FORMULARIO` não tem quiz nem sugestão ([linha 447](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L447)).
- `HOLERITES` não tem tipo nem agendamento ([linhas 304-310](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L304-L310)).
- `BENEFICIOS_FUNCIONARIOS` guarda só o vínculo ativo ([linhas 510-514](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L510-L514)), sem pedido de adesão, dependente ou trajeto.
- Não existem tabelas para unidades, documentos do colaborador, dependentes, ciclos de avaliação, módulos de treinamento com progresso e inscrições em turmas.

### 13.5 Implicações técnicas (a incorporar nas seções 5, 7 e 11)

- **Tarefas agendadas** passam de "fila de e-mails" para um agendador geral: publicação agendada (holerite, aviso, formulário, treinamento), consolidação diária do banco de horas e lembretes automáticos. Em um App Service, um agendador dentro da API precisa do recurso Always On, que **inferi** não existir no plano gratuito (não verificado). Isso pode afetar o plano e o custo.
- **PDFs gerados:** certificado e espelho de ponto. **Exportações:** CSV do log, lista de treinamentos, respostas de formulários e "exportar para a folha". A biblioteca de PDF e a licença são conferidas na implementação.
- **Formulários públicos** têm respondentes externos, sem login. Exigem proteção contra abuso, limite de tamanho de arquivo e uma política de retenção dos dados dos candidatos (LGPD).
- **Eventos de e-mail adicionais** (ampliam a lista da seção 5): aviso com "enviar por e-mail", lembretes manuais ("lembrar quem falta"), lembrete automático do formulário de admissão, treinamento publicado e lembrete de prazo, resposta a pedido de benefício, férias registradas pelo RH.
- **Auditoria:** o Figma pede IP e origem, filtros por período, pessoa, área e ação, exportação em CSV, e registro de login, ponto, acesso negado e e-mails automáticos.
- **Front-end:** 61 telas. Só há layout de desktop em 1440 px, e nenhum layout móvel foi desenhado.

### 13.6 Decisões do dono a perguntar no início de cada fatia

- **Fatia 3:** regras de hora extra e banco de horas, formato do "exportar para a folha".
- **Fatia 4:** envio de holerites em lote (um PDF por colaborador, 101 de cada vez, seria inviável).
- **Fatia 5:** anonimato das avaliações e da pesquisa de clima, e como a "verificação de dado sensível" funciona.
- **Fatia 6:** onde ficam os vídeos (arquivo no Blob ou link externo).
- **Fatia 7:** confirmar as regras de férias do protótipo.
