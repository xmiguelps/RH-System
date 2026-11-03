# Ficha da fatia 6: TAD (treinamentos)

Notas de cada tela: `docs/plano-front-end/telas/fatia-6.md` (leia a nota da tela antes de montá-la). Caminhos de tela: `docs/plano-front-end/05-caminhos-de-tela.md` (tabela única). Contrato: `contrato/fontes/60-fatia-6-tad.yaml` (a versão do contrato é o `info.version` de `contrato/openapi.yaml`; anote-a em `PROGRESSO.md` a cada tela). Esta ficha **não** traz passo a passo fino: a sessão o deriva do padrão da fatia 1b (última seção).

## 1. Objetivo e entrega demonstrável

**Objetivo.** O RH cria treinamentos com módulos (vídeo, texto, link, PDF), guarda como rascunho ou publica (com acesso liberado na data de início), escolhe para quem vale (todos, setores ou pessoas), acompanha quem fez e pode **encerrar** ou **excluir**. A pessoa avança módulo a módulo, em qualquer ordem, conclui ao terminar o último e recebe um **certificado em PDF com código de validação**. O gestor vê o andamento da equipe. Qualquer pessoa confere um certificado pelo código, sem login. **Sem prazo, sem quiz, sem nota, sem turmas presenciais, sem e-mail.**

**Entrega demonstrável** (com dados simulados; **contra a API real só a pessoa ou o dono o faz**, quando `contrato/STATUS.md` disser que a fatia 6 está `pronta`: a sessão não roda a API real):
1. O RH cria o treinamento "Prevenção de assédio no trabalho" com três módulos, marca "Emitir certificado" e o publica para todos; a lista o mostra "Agendado" e depois "Em andamento".
2. Um funcionário abre "Meus treinamentos", começa, abre cada módulo (texto na tela, vídeo e PDF em nova aba), conclui o último e vê "Você concluiu" com o número do certificado e "Ver certificado".
3. O RH abre "Ver quem fez" no treinamento, vê os cartões e a lista, baixa a lista em CSV e o certificado de uma pessoa.
4. O RH **encerra** um treinamento com gente que ainda não terminou (confirmação com os números) e vê que o treinamento com certificado emitido **não se exclui**.
5. O gestor abre "Treinamentos do setor" e vê quem da equipe concluiu cada um.
6. Alguém cola o código do certificado na página pública e vê "Certificado válido".

Incrementos do roadmap: 6a treinamentos e módulos, 6b progresso e certificado, **6c lembretes (cortado: o treinamento não tem prazo nem e-mail)** (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, fatia 6).

## 2. Telas (8 com print, mais as sem print)

| Tela | Nome | Perfil | PNG (`Figma/telas/`) | Nota |
|---|---|---|---|---|
| 22 | Treinamentos: lista e "Quem fez" | RH | `22-rh-treinamentos.png` | Tela 22 |
| 23 | Novo treinamento, etapa Detalhes | RH | `23-rh-treinamentos-novo-treinamento-etapa-detalhes.png` | Tela 23 |
| 24 | Etapa Conteúdo | RH | `24-rh-treinamentos-novo-treinamento-etapa-conteudo.png` | Tela 24 |
| 25 | Etapa Avaliação e certificado (vira "Certificado") | RH | `25-rh-treinamentos-novo-treinamento-etapa-avaliacao.png` | Tela 25 |
| 26 | Etapa Publicação | RH | `26-rh-treinamentos-novo-treinamento-etapa-publicacao.png` | Tela 26 |
| 41 | Meus treinamentos (Todos, Disponíveis, Em andamento) | funcionário (também gestor e RH) | `41-func-treinamentos.png` | Tela 41 |
| 42 | Meus treinamentos, Concluídos | funcionário (também gestor e RH) | `42-func-treinamentos-aba-concluidos.png` | Tela 42 |
| 58 | Treinamentos do setor | gestor | `58-gestor-treinamentos-da-equipe.png` | Tela 58 |
| sem print | Fazer o treinamento | todos | descrição em texto | "Tela sem print: Fazer o treinamento" |
| sem print | Conferir um certificado | público | descrição em texto | "Tela sem print: Conferir um certificado" |
| 52 (aba) | Aba Treinamentos da ficha da equipe | gestor | `52-gestor-painel-da-equipe-ficha.png` mostra só a aba Dados | "Tela 52 (aba Treinamentos...)" |

Também é desta fatia **ligar a caixa "Incluir no treinamento de onboarding"** da tela 02 (cadastro, 1b), da importação (ficha 2) **e** do diálogo "Readmitir" da ficha (tela 03, 1b): as três a deixaram **visível, desmarcada e desabilitada** ("Indisponível por enquanto"), e **esta ficha liga as três telas de uma vez** (nota "Ligações com outras telas"; bloco 7).

## 3. Rotas do contrato por tela

31 rotas na fatia (`contrato/ROTAS.md`). `listarSetores` e `listarFuncionarios` (fatia 1) e `obterUsuarioLogado` (fatia 0) são consumidas, não desta fatia.

| Tela | `operationId` |
|---|---|
| 22 lista | `obterIndicadoresDeTreinamentos`, `listarTreinamentos`, `obterTreinamento` (Editar), `encerrarTreinamento`, `excluirTreinamento` |
| 22 "Quem fez" | `listarParticipantesDeTreinamento`, `exportarParticipantesDeTreinamento`, `baixarCertificadoDeParticipante` (+ `listarSetores`) |
| 23 Detalhes | `criarTreinamento`, `atualizarTreinamento`, `obterTreinamento` |
| 24 Conteúdo | `adicionarModuloDeTreinamento`, `atualizarModuloDeTreinamento`, `reordenarModulosDeTreinamento`, `excluirModuloDeTreinamento`, `obterTreinamento` |
| 25 Certificado | `atualizarTreinamento`, `obterTreinamento` |
| 26 Publicação | `contarPublicoDeTreinamento`, `atualizarTreinamento`, `publicarTreinamento`, `obterTreinamento` (+ `listarSetores`, `listarFuncionarios`) |
| 41 e 42 | `listarMeusTreinamentos`, `inscreverMeEmTreinamento`, `iniciarMeuTreinamento`, `baixarMeuCertificado` |
| Fazer (sem print) | `obterMeuTreinamento`, `obterConteudoDeModulo`, `baixarArquivoDeModulo`, `concluirModuloDeTreinamento`, `inscreverMeEmTreinamento`, `iniciarMeuTreinamento`, `baixarMeuCertificado` |
| 58 gestor | `listarTreinamentosDaEquipe`, `listarPessoasDaEquipeNoTreinamento`, `listarMeusTreinamentos` (faixa "Sua parte") |
| 52 aba Treinamentos | `listarTreinamentosDePessoaDaEquipe` |
| Conferir (público) | `validarCertificado` |
| Tela 02, importação e Readmitir (ligação) | `listarTreinamentosDeOnboarding` (a caixa); `incluirFuncionarioNoOnboarding` é a regra que `criarFuncionario`, `confirmarImportacao` e `readmitirFuncionario` aplicam **por dentro**; **nenhuma tela a chama** (sem botão: dúvida `D6-4`, seção 10) |
| **Sem tela** | `listarTreinamentosDeFuncionario` (aba da ficha do RH: **não construir**; decidido pelo dono em 2026-11-02, seção 10) |

Conferência: 31 operações no contrato, 31 acima.

## 4. Regras e cortes que valem

**Cortes do dono (contrato, tag Treinamentos, e `contrato/CONVENCOES.md`, seções 12 e 13; design, seção 7: "sem quiz, nota, prova final nem turmas presenciais").**
- **Treinamento não tem prazo** (2026-10-30): sem "Termina em", "Período a b", "Prazos até", "Até dd/mm", "De dd/mm a dd/mm", "O prazo é ...", "faltam N dias", "atrasado", nem lembrete antes do fim. Existem **publicar**, a **data de início** (só libera o acesso) e **encerrar** ou **excluir** a qualquer momento.
- **Sem quiz, nota, prova final** (sem "Terminar com um quiz", "Nota mínima para concluir", coluna Nota, "Concluído com nota 9,0", "Quiz final e certificado"). **Sem turma presencial**: sem módulo "Aula presencial", sem "Híbrido" nem "Presencial", sem o campo "Tipo" do passo Detalhes e sem o filtro "Todos os tipos" (todo treinamento é online).
- **Sem e-mail algum** (nem "Avisar por e-mail ao publicar", nem "Lembrar quem não começou 7 dias antes do fim", nem "Lembrar quem não terminou" na tela 22, nem "Lembrar quem falta por e-mail" na tela 58). Quem quiser avisar usa um Aviso (fatia 4), à mão.
- **Encerrar e excluir** pedem confirmação `CONFIRMACAO_NECESSARIA` (409) com `pendentes`, `emAndamento` e `naoIniciaram` quando há gente que não concluiu; a tela repete com `confirmar=true`. **Treinamento com certificado emitido não se exclui** (422 `TREINAMENTO_COM_CERTIFICADOS`, mesmo com `confirmar=true`); a tela deixa "Excluir" desabilitado quando `certificadosEmitidos` é maior que zero, com a explicação "Treinamento com certificado emitido só pode ser encerrado". Encerrar: quem já começou continua e termina; quem não começou deixa de poder começar; ninguém novo entra; os certificados já emitidos continuam valendo. Não há como reabrir nem despublicar.
- **Certificado** (decisão do dono, 2026-10-29: a caixa "Emitir certificado" fica): marcada por padrão, **não muda depois de publicado**; sem ela, não há certificado, número nem "Ver certificado". O código de validação é um UUID v4 aleatório; a conferência pública mostra só o mínimo (nome, treinamento, data).
- **Rascunho e agendado:** o treinamento só aparece para a pessoa quando a data de início chega; não existe o cartão "Em breve". Público: todos, setores ou pessoas escolhidas (**sem "por cargo"**).
- **Vídeo é só link externo** e abre em nova aba (a política `default-src 'self'` impede incorporar). Módulo de PDF: até 10 MB.
- Texto de usuário (descrição, módulo de texto): texto ou markdown, **nunca HTML**. Datas: a API fala UTC e a tela mostra `America/Sao_Paulo`. Erros em `application/problem+json`: mostre o `detail` em português.

## 5. Decisões do dono ainda abertas (`x-provisorio`) e o que construir mesmo assim

Construa **o que o contrato diz**; a coluna da direita é o que muda se o dono decidir diferente (fonte: `contrato/PROVISORIOS.md`).

| Onde no contrato | O que a tela faz hoje | Se o dono mudar |
|---|---|---|
| `listarTreinamentosDeOnboarding`, `TreinamentoComando.onboarding`, `incluirFuncionarioNoOnboarding` | Há a caixa "É o treinamento de onboarding" no passo Detalhes; a caixa do cadastro lista os de onboarding em vigor | Muda a regra de qual é o de onboarding (por exemplo, um só por ano), não a forma de chamar |
| `atualizarTreinamento` | Depois de publicado só mudam título, descrição, "incluir admitidos" e a data de início enquanto agendado | Pode liberar mais campos e exigir rota nova |
| `adicionarModuloDeTreinamento`, `atualizarModuloDeTreinamento`, `reordenarModulosDeTreinamento`, `excluirModuloDeTreinamento` | Vídeo é só link externo; a lista de módulos só muda em rascunho | Vídeo em arquivo (o módulo `VIDEO` passa a aceitar `arquivo`); poder acrescentar módulo depois de publicado |
| `obterConteudoDeModulo`, `baixarArquivoDeModulo` | Só o PDF tem arquivo | O vídeo passa a vir por `baixarArquivoDeModulo` |
| `baixarCertificadoDeParticipante`, `baixarMeuCertificado`, `concluirModuloDeTreinamento`, `validarCertificado` | O PDF segue o modelo simples do back-end (o **número** já está decidido: `CERT-{ano}-{id}`, seção 10) | Muda o layout do PDF; a tela mostra `numero` como vier |

## 6. Ordem de construção e dependências

**Da fundação (fatia 1a) esta fatia usa:** shell com menu por perfil e rotas por perfil; cliente de API tipado e a variante **sem sessão** (página pública); dados simulados (MSW) gerados do contrato; formatadores de data; e os componentes-base: **janela em etapas** (a mesma da fatia 5: quem chegar primeiro a cria no lugar dos componentes compartilhados definido pela fundação e o outro a reutiliza), abas, tabela com paginação, selo, barra de progresso, cartão, caixa de marcar, escolha em cartões, campo de data em pt-BR, janela de confirmação, aviso temporário, **envio de arquivo (multipart)**, os auxiliares de download (`abrirLinkTemporario` para o certificado e o PDF de módulo; `baixarExportacao` para o CSV de "Quem fez"; `src\shared\arquivos\baixar.ts`), e o componente seguro de texto/markdown. **Da fatia 1b:** padrão de lista com filtros (tela 01), da gaveta de cadastro (tela 02), da ficha com abas (tela 03 e a ficha da equipe, tela 52). **Das bolinhas do menu:** a fundação já as lê (`useContadoresDoMenu`); esta fatia não mexe nelas. **Esta fatia não depende da 5** (roadmap: desde 2026-10-29 a 6 não depende mais da 5, porque não há quiz).

**Blocos** (cada bloco termina com os testes verdes, `PROGRESSO.md` atualizado, commit e push):
1. **Lista de treinamentos** (tela 22, parte de cima): indicadores, abas (com Rascunhos), busca, tabela, menu "..." (ainda sem as ações). *6a.*
2. **Assistente** (telas 23, 24, 25 e 26): Detalhes, Conteúdo (módulos com upload de PDF), Certificado, Publicação com contagem do público e publicar. *6a.*
3. **Encerrar, excluir e "Quem fez"** (tela 22): confirmações 409, `TREINAMENTO_COM_CERTIFICADOS`, seção "Quem fez", "Baixar lista", "Baixar" do certificado. *6a e 6b.*
4. **Meus treinamentos e fazer** (telas 41 e 42 e "Fazer o treinamento"): inscrever, começar, abrir módulos, concluir, certificado. *6b.*
5. **Gestor** (tela 58 e a aba Treinamentos da tela 52). *6a.*
6. **Página pública de conferência** do certificado (sem sessão). *6b.*
7. **Ligação:** liga a caixa "Incluir no treinamento de onboarding" **na tela 02 (cadastro) e na importação de uma vez** (e no diálogo "Readmitir" da ficha, tela 03, que a 1b deixou com as mesmas caixas desligadas: `telas/fatia-1.md`, "Ligação com outras telas"; `telas/fatia-6.md`, "Ligações com outras telas"; `listarTreinamentosDeOnboarding`; lista vazia desliga a caixa com a dica da nota); relatório final com as telas a conferir.

## 7. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações da fatia), rodado da raiz do repositório (`01-regras.md`, A14). Aqui o teste de cobertura confere as **31 operações** da seção 3, **menos** `listarTreinamentosDeFuncionario` (sem tela: **decisão do dono de 2026-11-02**, sem aba Treinamentos na ficha do RH) e `incluirFuncionarioNoOnboarding` (chamada por dentro de `criarFuncionario`, `confirmarImportacao` e `readmitirFuncionario`; nenhuma tela a chama, sem botão: D6-4). As duas exclusões ficam escritas, com o motivo, no `coberturaDaFatia6.test.ts`. O que esta ficha acrescenta é a lista abaixo, que é da fatia.

**Passos novos do `csp`:** além da matriz de perfis das telas desta fatia (a sonda de download da 1a já cobre Blob e link temporário), **um passo novo**, porque a página pública de conferência do certificado é uma página pública nova, sem sessão: o passo abre `/certificado` e `/certificado/<código de teste>` sem login (como a 1b abre `/esqueci-senha` e `/definir-senha`) e conta violações. O vídeo e o link do módulo abrem em **nova aba** (nunca `iframe`), então não há recurso externo a medir; o envio de PDF usa o `SeletorDeArquivo` da 1a. Aqui só se descreve o passo, sem código de `.mjs`: o script e o ponto de acréscimo (`ACRESCENTE AQUI`) são os da 1b (`fatia-1b-telas.md`, Task 10, passo 4).

**Exemplos nomeados** (`exemploDoContrato(operationId, status?, nome?)`, em `src\shared\teste\`; ver `03-contrato-e-mocks.md`): `atualizarTreinamento` 422 `travado`, `encerrado`; `encerrarTreinamento` 422 `naoEstaEmAndamento`, `jaEncerrado`; `inscreverMeEmTreinamento` 422 `obrigatorio`, `encerrado`; `iniciarMeuTreinamento` e `concluirModuloDeTreinamento` 422 `inscricao`, `encerrado`; `criarFuncionario`, `confirmarImportacao` e `readmitirFuncionario` 422 `semOnboarding`.

**Testes que precisam existir** (nomes descritivos; a sessão escolhe os nomes finais):
- **Contrato de parâmetros por tela:** cada tela confere os `query` e `path` enviados (o `tsc` não pega chave extra). Em especial: `contarPublicoDeTreinamento` envia `setorIds` e `funcionarioIds` **repetindo o parâmetro**; `confirmar=true` só na repetição; `aba` e `obrigatorio` em "Meus treinamentos"; `tamanhoPagina` e `setorId` em "Quem fez".
- **Texto proibido:** um teste renderiza cada tela da fatia e **falha** se encontrar, **nos rótulos da tela**, algum destes termos: "Quiz", "Nota", "Termina em", "Prazos até", "atrasad", "Aula presencial", "Híbrido", "Presencial", "Em breve", "Lembrar quem não terminou", "Lembrar quem falta", "Avisar por e-mail". Duas regras de método (as mesmas da ficha 5, seção 7, porque "nota" aparece dentro de outras palavras e nos dados):
  - **Já pronto na 1b:** `rotulosDaTela(contêiner)` e `acharTextoProibido(rotulos, texto)` de `src\test\textosProibidos.ts` (**não os reescreva**) e as linhas desta fatia acrescentadas à tabela de `src\app\textosProibidos.test.tsx`. O `rotulosDaTela` da 1b não lista `placeholder` nem `aria-label`: se uma tela os usar com texto proibido, acrescente-os ao `rotulosDaTela` e ao teste dele.
  - **Limite de palavra:** procure com expressão regular `\b...\b` e sem diferença de maiúsculas (`/\bnota\b/i`, `/\bquiz\b/i`; no prefixo `atrasad`, só o limite do começo: `/\batrasad/i`; nas frases, a frase inteira). "Presencial" não pode pegar "presencial" de um título de exemplo: por isso a regra seguinte.
  - **Só nos rótulos, não nos dados da API:** a asserção é sobre o texto visível **dentro do contêiner da tela** (`within(screen.getByRole('main'))`) e olha só os elementos de interface: títulos (`heading`), botões, abas, cabeçalhos de coluna (`columnheader`), `label`, `legend`, `placeholder` e `aria-label`. **Não** olha células (`cell`) nem o texto de itens que vêm dos mocks (título e descrição de um treinamento de exemplo).
- **Ligação da caixa de onboarding** (cadastro, importação **e readmissão**, de uma vez): a caixa "Incluir no treinamento de onboarding" **deixa de estar desabilitada** (nas três telas: a tela 02, a importação e o diálogo "Readmitir" da ficha, tela 03) e usa `listarTreinamentosDeOnboarding` (um item: "Incluir no treinamento {título}"; mais de um: "Incluir nos treinamentos de onboarding"; lista vazia: desligada com a dica "Nenhum treinamento de onboarding publicado"); o cadastro (`criarFuncionario`) e a readmissão (`readmitirFuncionario`) enviam `incluirNoOnboarding: true`; a importação confirma com `incluirNoOnboarding: true` e mostra "2 incluídos no onboarding" (`incluidosNoOnboarding`); 422 `SEM_TREINAMENTO_DE_ONBOARDING` mostra o `detail` nas três telas (exemplo nomeado `semOnboarding` nas três rotas). (Os testes da ficha 2 e da 1b que conferem esta caixa **desabilitada** passam a conferir a caixa **habilitada**, **inclusive** o de `DialogoReadmitir.test.tsx` da 1b ("as caixas de onboarding e de formulario ficam desligadas...": o teste que a 1b deixou sem dono, `fatia-1b-telas.md`, Task 3, parte D); com a caixa desmarcada, `incluirNoOnboarding: false` continua valendo.)
- **Tela 22:** a aba Rascunhos existe; "Conclusão" mostra "N de M concluíram" em obrigatório e "N inscritos" em opcional; "Excluir" fica desabilitado com `certificadosEmitidos` maior que zero e explica; "Encerrar" só em `EM_ANDAMENTO`; 409 mostra os números e repete com `confirmar=true`; 422 `TREINAMENTO_COM_CERTIFICADOS` mostra o `detail`; a tabela "Quem fez" não tem coluna Nota e "Baixar" só aparece com `certificadoDisponivel`.
- **Assistente:** não existem "Tipo", "Termina em", quiz, nota, "Aula presencial", nem as duas caixas de e-mail; a etapa 3 se chama "Certificado"; o `PUT` envia o comando inteiro; depois de publicado os campos travados abrem desabilitados e a lista de módulos fica só para correção de conteúdo; publicar sem módulo mostra `errors.modulos`; o upload de PDF (com `// @vitest-environment node`) e o limite de 10 MB.
- **Telas 41 e 42:** não existe "Em breve" nem prazo; o botão muda por situação (`DISPONIVEL`, `NAO_INICIADO`, `EM_ANDAMENTO`); selo "Onboarding"; **sem certificado** (`certificado` vazio) não há código nem "Ver certificado"; `encerrado` mostra o aviso.
- **Fazer o treinamento:** vídeo e link abrem em nova aba com `rel="noopener noreferrer"` e **não há `iframe`**; PDF abre pelo link temporário; concluir o último módulo mostra "Você concluiu" e o número do certificado; módulos fora de ordem funcionam; 422 `INSCRICAO_PENDENTE` e `TREINAMENTO_ENCERRADO` mostram o texto da nota.
- **Tela 58:** sem "Lembrar quem falta por e-mail", sem prazo; agendado mostra "Começa em"; a faixa "Sua parte" aparece só com item obrigatório não encerrado e não concluído.
- **Página pública:** nenhuma chamada leva `Authorization`; 404 e 429 mostram o texto da nota; o formato do código é conferido antes de chamar.
- **Número do certificado** (decisão do dono, 2026-11-02): o cartão "Você concluiu", a lista "Concluídos" (tela 42) e a tabela "Quem fez" (tela 22) mostram o `numero` **exatamente como a API o devolve** (no exemplo, `CERT-2026-00482`); o teste troca o `numero` do exemplo por outro texto (por exemplo `CERT-2030-00001`) e confere que a tela mostra o novo, **sem** montar nem reformatar o número no front e **sem** sigla de treinamento (`LGPD-...` não aparece).
- **Sem aba Treinamentos na ficha do RH** (decisão do dono, 2026-11-02): a ficha do RH (`/rh/colaboradores/:funcionarioId`) **não** tem a aba Treinamentos entre as `ABAS_DA_FICHA_RH` (as abas são Dados, Ponto, Férias, Holerites, Documentos e Advertências), e `capturar('listarTreinamentosDeFuncionario').quantas()` é 0; a aba Treinamentos da ficha do **gestor** (`/gestor/equipe/:funcionarioId?aba=treinamentos`) existe.
- **Menu "Meu espaço"** (decisão do dono, 2026-11-02): com o perfil RH e com o perfil Gestor, o grupo "Meu espaço" mostra "Meus treinamentos" e o clique leva a `/eu/treinamentos` (a tela abre com o menu do perfil ainda à vista); o perfil Funcionário **não** ganha o grupo e já tem "Treinamentos" em "Você na empresa". O teste usa `menuVisivel`/`todosOsCaminhosDoMenu` da 1a e `renderizarTela`.
- Cada tela cobre **carregando, vazio, erro e sem permissão**.

**Pendente de integração** (a sessão **não roda** contra a API real: registra "pendente de integração" em `PROGRESSO.md` **com o roteiro pronto** e consulta `contrato/STATUS.md` só para saber se a rota existe; **a pessoa ou o dono** roda o roteiro depois, com a pilha inteira):
- Todas as 31 rotas, até a fatia 6 estar `pronta` em `contrato/STATUS.md`.
- Em especial o que só a API real prova: upload real de PDF para o armazenamento; **geração do PDF do certificado** e a troca do código para UUID v4; links temporários reais; CSV real; a transição de situação pela data (agendado vira em andamento sem agendador); **conclusão simultânea** de módulos (um só certificado); a **inclusão de quem for admitido** pelo agendador; e o limite 429 da conferência pública.
- Conferência visual (a pessoa, uma vez, ao lado do PNG): telas **22, 23, 24, 25, 26, 41, 42 e 58**.

## 8. Riscos

1. **Textos do Figma que vazam** (prazo, quiz, nota, "Em breve", "Lembrar"): são muitos e estão em todas as telas. Defesa: seção 4, o teste de texto proibido e a leitura "O que sai ou muda" de cada nota.
2. **Roadmap e spec ainda dizem o contrário** (treinamento com prazo, lembrete, aviso de publicação): a sessão que os ler por engano reintroduz o prazo. A seção 9 lista as linhas; **vale o contrato**.
3. **`PUT` substitui tudo:** `atualizarTreinamento` volta ao padrão o campo omitido. Envie sempre o comando inteiro, lido do estado atual.
4. **Treinamento publicado trava campos e módulos:** a tela precisa desabilitar o certo (e não só mostrar o 422). Em ENCERRADO, tudo só leitura.
5. **Encerrado visto pela pessoa:** some para quem não começou; quem começou continua e termina. Cartão com `encerrado` verdadeiro precisa do aviso e não conta como pendência.
6. **Segurança de conteúdo:** nunca `iframe`, nunca HTML do usuário, links externos com `rel="noopener noreferrer"`, página pública sem sessão e sem recurso externo.
7. **Certificado:** o **número** é o que a API devolve (`CERT-{ano}-{id}`, decidido pelo dono em 2026-11-02; sem sigla por treinamento); o **PDF** segue o modelo simples do back-end (provisório). Mostre o que a API devolve; não monte número no front.
8. **Upload multipart nos testes:** o jsdom não o monta; use `// @vitest-environment node`.
9. **Gestor e RH fazem os treinamentos pelo menu "Meu espaço":** decidido pelo dono em 2026-11-02 (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o item **"Meus treinamentos"** existe no grupo "Meu espaço" do RH e do gestor e leva a `/eu/treinamentos` (a página abre para os três perfis); o gestor chega também pela faixa "Sua parte" da tela 58.
10. **Largura:** o Figma é 1440 px; a tela 22 é uma página longa com duas tabelas. A demo pode ser 1366 px.

## 9. Onde os documentos se contradizem (vale o contrato; não corrija os documentos)

- **Prazo e lembrete de treinamento.** `docs/superpowers/plans/2026-10-28-roadmap-completo.md` (seção "Fatia 6: TAD (treinamentos)": em "Incrementos", o item "6c. lembretes"; em "Regras", o item "Lembrete 7 dias antes do fim e inclusão de quem for admitido até o prazo"), `docs/superpowers/specs/2026-10-21-rh-system-design.md` (linha 284, "com prazo"; linha 288; linha 313, "treinamento publicado e lembrete de prazo"; linha 98) tratam treinamento com prazo, aviso de publicação e lembrete. `contrato/CONVENCOES.md`, seções 12 e 13 (decisão do dono, 2026-10-30), dizem que **não existem**, e o contrato os tira de todas as rotas.
- **Figma e contrato** (nas notas, "O que sai ou muda"): "Prazos até 31/10", "Período", filtro "Todos os tipos", "Híbrido" e "Presencial", "Em breve", "aulas", quiz e nota, "Lembrar ...", "Aula presencial", "Termina em", as duas caixas de e-mail da tela 26. Também os exemplos do PNG que o contrato torna impossíveis (opcional agendado com "18 inscritos" e "6 de 8 inscritos").
- Número do certificado: o Figma mostra siglas por treinamento (`LGPD-2026-00482`); o contrato usa `CERT-{ano}-{id}`, e o **dono decidiu manter** o do contrato em 2026-11-02 (`DUVIDAS.md`, "Decisões do dono").

## 10. Decisões do dono de 2026-11-02 e dúvidas já conhecidas (as dúvidas se registram em `DUVIDAS.md` com o modelo de lá; o texto abaixo é o conteúdo; **não bloqueiam**)

**Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; **não se registram como dúvida**):
- **Onde gestor e RH fazem os treinamentos deles** (antes `D-menu`): o RH e o gestor ganham o grupo **"Meu espaço"** no fim do menu, e o item **"Meus treinamentos"** é um deles, **ativo**, levando a `/eu/treinamentos`; o gestor chega também pela faixa "Sua parte" da tela 58.
- **Aba Treinamentos na ficha do RH** (antes `D6-2`): **não** se cria. `listarTreinamentosDeFuncionario` fica na exclusão da cobertura, com o motivo "decisão do dono, 2026-11-02"; a ficha do RH continua com as abas Dados, Ponto, Férias, Holerites, Documentos e Advertências. (A aba Treinamentos do **gestor**, tela 52, continua.)
- **Número do certificado** (antes `D6-3`): **fica `CERT-{ano}-{id}`**, o que a API devolve, sem sigla por treinamento; a tela mostra `numero` como vier.

**Dúvida ainda aberta:**
- **D6-4. Incluir no onboarding uma pessoa já cadastrada, a partir da ficha (sem tela).** Onde: ficha do colaborador (tela 03, fatia 1); rota `incluirFuncionarioNoOnboarding` (`POST /api/rh/funcionarios/{funcionarioId}/treinamentos/onboarding`); `contrato/fontes/60-fatia-6-tad.yaml`. O que achei: o contrato diz que a rota "existe para incluir depois uma pessoa já cadastrada (por exemplo, a partir da ficha)", mas o Figma não desenha nenhum botão para isso e nenhuma nota o pede; o cadastro, a importação e a readmissão já a aplicam por dentro pela caixa "Incluir no treinamento de onboarding". Sugiro **não construir o botão** (a rota fica na exclusão da cobertura, seção 7). **Pergunta:** o RH deve poder incluir no onboarding uma pessoa já cadastrada, a partir da ficha? Padrão enquanto o dono não responder: **sem botão**. **Não bloqueia.**

  ```
  Mensagem para o dono: dúvida D6-4 do front-end do RH-System (incluir no onboarding uma pessoa já cadastrada)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 6 (treinamentos), ficha do colaborador (tela 03, fatia 1); rota incluirFuncionarioNoOnboarding; contrato <versão lida de contrato\openapi.yaml>.
  O que achei: o contrato tem uma rota para incluir no treinamento de onboarding uma pessoa que já está cadastrada (por exemplo, a partir da ficha), mas o Figma não desenha nenhum botão para isso. Hoje a inclusão só acontece pela caixa "Incluir no treinamento de onboarding" ao cadastrar, importar ou readmitir.
  O que sugiro: não criar o botão; quem já está cadastrado e ficou de fora do onboarding não teria como entrar depois. Se você quiser, o botão "Incluir no onboarding" entra na ficha, só para o RH.
  Pergunta: o RH deve poder incluir no onboarding uma pessoa já cadastrada, a partir da ficha? (A) não, só pela caixa do cadastro, da importação e da readmissão; (B) sim, com um botão na ficha.
  Enquanto isso: sigo sem o botão.
  ```
- **x-provisorio** que o dono ainda decide (seção 5): onde ficam os vídeos, o que muda depois de publicar, a regra do onboarding e o modelo do certificado.

## 11. Repita o padrão da fatia 1b

`docs/plano-front-end/fatia-1b-telas.md` é o modelo. Para **cada tela** desta ficha, nesta ordem:
1. Leia a nota da tela em `telas/fatia-6.md`, abra o PNG correspondente (quando houver) e anote em `PROGRESSO.md` a versão do contrato.
2. Escreva o **teste de contrato** (parâmetros enviados) e o **teste da tela** com dados simulados (estados: carregando, vazio, erro, sem permissão, mais as regras da nota), **rode e veja falhar**.
3. Implemente o mínimo para passar: página, componentes da tela, ligação com a rota tipada.
4. Rode teste, `lint` e `format:check`; no fim do bloco, o `build` sob a política de segurança.
5. Atualize `PROGRESSO.md` (tela feita, versão do contrato, "pendente de integração" onde couber) e faça **commit e push** na branch da sessão, **sem coautor Claude**.
6. Dúvida nova: `DUVIDAS.md`, com o texto pronto; **não edite o contrato, o banco nem o back-end**.
