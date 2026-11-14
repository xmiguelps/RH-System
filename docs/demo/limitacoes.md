# Limitações do sistema (o que dizer à banca e o que não prometer)

Este é um projeto de **TCC**. O que está pronto funciona e é testado; o que está abaixo **não está**, ou está de um jeito que serve à demonstração e não a uma empresa de verdade. Dizer isto com clareza na apresentação é melhor do que deixar a banca descobrir.

## Ponto: conformidade legal e relógio físico

- **Não é um registrador de ponto homologado.** O ponto é batido pelo navegador (`POST /api/eu/ponto/marcacoes`), com login e senha, **sem relógio físico**, sem leitor biométrico, sem foto, sem geolocalização e **sem o comprovante de registro** no formato que a norma pede. Para uso real numa empresa, o controle de jornada precisa seguir a CLT (art. 74) e a Portaria MTP nº 671/2021 (tipos de registrador, comprovante, arquivo fiscal, tempo de guarda). **Este sistema não foi avaliado juridicamente** e não deve ser apresentado como solução que cumpre essa norma.
- **Toda alteração do ponto deixa rastro.** Cada batida tem origem (`SISTEMA` ou `CORRIGIDA_RH`), o pedido de ajuste passa por duas etapas (gestor e RH) e tudo vai para o log de auditoria. Isso ajuda a rastrear, mas **não substitui** os requisitos legais de inviolabilidade do registro.
- **Regras de cálculo provisórias**, decididas por padrão e listadas como perguntas ao dono no `README.md` ("Ponto (fatia 3)"): hora extra e tolerância, formato do arquivo para a folha e a janela de 12 horas que separa um dia de trabalho do outro. Os **feriados estaduais** vêm de uma tabela no código com 23 das 27 UFs (ES, GO, MG e MT ainda sem linha, e a empresa escolhe uma UF só); os nacionais também vêm do código, e a tabela `FERIADOS` do banco traz os de 2026 mais os da empresa e os municipais cadastrados.
- **Banco de horas** guarda créditos e débitos em horas; não há regra de vencimento, compensação ou limite legal.

## Telas e plataforma

- **Só desktop.** O front foi desenhado para telas de 1280 px ou mais (o layout das telas logadas tem largura mínima); **não há aplicativo móvel** nem desenho para celular.
- **Uma empresa por banco.** A configuração da empresa (`CONFIGURACOES_EMPRESA`) tem uma linha só; não há várias empresas nem filiais no mesmo banco.
- **Português do Brasil, fuso de Brasília** (UTC-3, sem horário de verão), em toda a API.

## O que a demo não cobre

- **Folha de pagamento.** O sistema **exporta** o ponto para a folha e **guarda** os holerites (PDF enviado pelo RH); não calcula salário, impostos nem encargos. Não há integração com sistema de folha.
- **Assinatura digital.** Holerite, advertência e certificado não têm assinatura digital nem carimbo de tempo; a advertência tem ciência e contestação da pessoa, registradas no log.
- **Treinamento:** vídeo só por **link** externo (não há envio de vídeo); sem quiz nem prova final (cortado em 2026-10-29). O certificado tem código de validação e conferência pública por código.
- **E-mail:** a lista de e-mails que o sistema envia é **fechada** (`contrato/CONVENCOES.md`, seção 12: convite, redefinição de senha, resultado de ajuste de ponto e de férias, holerite publicado, aviso, advertência e lembretes de aviso e de formulário); o envio fica **desligado** por padrão e a fila acumula. Em produção precisa de um provedor configurado e de domínio verificado.
- **Arquivos** (holerite, atestado, documentos, certificado, PDF de módulo) exigem um armazenamento de blobs (Azure Blob ou Azurite no desenvolvimento). Sem ele, o envio e o download falham com mensagem, e o resto do sistema continua.

## Regras de negócio decididas por hipótese

Várias regras não estavam no contrato e foram **construídas pela hipótese mais segura**, cada uma com a pergunta ao dono e o custo se estiver errada no `README.md` (seções "Gestão (fatia 7)", "Treinamentos (fatia 6)" e as das fatias anteriores). As de maior efeito:

- **Férias:** as oito regras (duração de 5 a 30 dias, no máximo 3 partes, uma de 14 dias, abono, 30 dias de antecedência, saldo, sem sobreposição, não começar perto de feriado ou descanso) e a fórmula da parte de 14 dias são interpretações do texto do contrato; o saldo não vence.
- **Quem decide:** o gestor ausente, desligado ou trocado e a pessoa que muda de setor movem os pedidos parados para o RH; é uma decisão de produto, não uma regra da lei.
- **Benefícios:** quem pede de novo um benefício recusado ou retirado recomeça do zero (dependentes e trajetos antigos são cancelados).

## Operação

- **O agendador exige o App Service ligado o tempo todo (Always On).** Tarefas como concluir férias, consolidar o banco de horas, efetivar desligamentos e enviar lembretes rodam em segundo plano; com a aplicação dormindo, nada roda, e ao acordar o sistema recupera o que ficou para trás (cada tarefa é idempotente). Veja `custos-e-como-desligar.md`.
- **Uma instância da API.** As filas e as tarefas foram feitas para suportar mais de um processador (reserva com `SKIP LOCKED`), mas a demo foi pensada e testada com **uma** instância.
- **Sem recuperação de desastre além do backup do banco** do provedor.

## Segurança: o que está feito e o que não

Está feito e testado: senhas com hash, JWT de vida curta com renovação, matriz de perfis para cada rota (RH, gestor e funcionário) conferida pelos testes, limite de tentativas nas rotas públicas e de senha, dados pessoais fora do log, URLs de download temporárias. **Não foi feito:** auditoria externa, teste de invasão, autenticação em dois fatores, política de rotação de segredos e **nenhuma avaliação de conformidade com a LGPD** (base legal, política de retenção, atendimento a titulares, relatório de impacto). **Não use dados reais** nesta demonstração: todos os dados do seed são inventados.
