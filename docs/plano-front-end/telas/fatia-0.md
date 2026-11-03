# Notas de tela da fatia 0: entrar e shell do portal

Fontes lidas: `contrato\fontes\10-fatia-0-autenticacao.yaml` (a versão do contrato é o `info.version` de `contrato\openapi.yaml`; confira-a antes de começar e anote-a no `PROGRESSO.md`), `docs\plano-front-end\fatia-0.md` (Global Constraints e Tasks 3 e 4), o texto das antigas Tasks 10 e 11 do plano do back-end (hoje só há ali um aviso de que elas "saíram" para `fatia-0.md`; conferi os rótulos e as mensagens contra o texto original, que o `fatia-0.md` repete sem mudança), `contrato\CONVENCOES.md` (seções 5 e 11), `contrato\ELENCO.md` e os PNG `Figma\telas\01-rh-colaboradores.png`, `29-rh-cadastros.png`, `31-rh-log-de-auditoria.png`, `32-func-tela-inicial.png` e `52-gestor-painel-da-equipe-ficha.png` (só o menu lateral e o rodapé; a fatia 0 não tem tela própria no Figma).

## Como ler estas notas

- **(li)** é o que está escrito no PNG ou no arquivo citado. **(contrato)** é o que o `operationId` citado diz. **(desenho meu)** é a tela que o Figma não tem: a descrição é a proposta deste plano, em estilo simples. **(suponho)** é inferência minha onde nada decide: confira na conferência visual ou registre em `DUVIDAS.md`.
- **Onde o Figma, o contrato e o plano divergem, valem o contrato e os documentos.** Os textos da tela de entrar que estão em `fatia-0.md` **valem exatamente como escritos** (os testes dependem deles).
- As cores, as medidas e a fonte estão em `04-visual.md`. Esta nota não repete hexadecimais; diz só "como o shell dos PNG".
- Nenhuma tela da fatia 0 tem PNG. A pessoa confere a **tela de entrar** pela descrição abaixo e o **shell** olhando o menu lateral dos PNG 01, 32 e 52.

**Caminhos de tela:** a tabela única é `05-caminhos-de-tela.md` (Entrar `/entrar`; as páginas públicas ficam fora da área logada e sem menu).

## Mapa tela → rotas

| Tela | Perfil | PNG | Rotas que a tela chama (`operationId`) |
|---|---|---|---|
| Entrar | qualquer pessoa, sem login | nenhum | `entrar` |
| Sessão guardada (a porta de acesso renova a sessão antes de decidir entre Entrar e o shell) | qualquer perfil | nenhum | `renovarSessao` |
| Shell do portal, botão "Sair" | Funcionário, Gestor, RH | nenhum (menu lateral e rodapé nos PNG 01, 32 e 52) | `sair` |
| Cliente de API (`ClienteApi.eu`, sem tela própria na fatia 0) | os três | nenhum | `obterUsuarioLogado` |
| Nenhuma (a API e a CI conferem; o front não chama) | publico | nenhum | `verificarSaude` |

**Cobertura:** as 5 rotas do contrato da fatia 0 (`contrato\ROTAS.md`, "fatia 0 autenticacao (5 rotas)") aparecem acima. **Quatro têm tela; `verificarSaude` não tem e o front não a chama** (é a sonda de saúde da API, usada pela CI e pelo ambiente: Tasks 12 a 14 do plano do back-end). `obterUsuarioLogado` existe no cliente (`criarClienteApi(...).eu`, Task 2 de `fatia-0.md`) mas **nenhuma tela da fatia 0 a chama**: o `usuario` (com `cargo` e `setor`) já vem dentro de `entrar` e de `renovarSessao`. As saudações das telas iniciais (fatias 1a e 7) é que passam a usá-la.

---

## Tela sem print: Entrar (login)

**Descrição em texto** (o Figma não tem esta tela; roadmap, decisão 14, "login (fatia 0)"; textos de `fatia-0.md`, Task 4). É uma página pública, sem menu. Na fatia 0 não há biblioteca de rotas: a `PortaDeAcesso` mostra esta tela sempre que não há usuário. A fatia 1a, ao trazer as rotas, a mantém como a página das pessoas sem sessão em `/entrar` (`05-caminhos-de-tela.md`; as páginas públicas "Esqueci minha senha", `/esqueci-senha`, e "Definir senha", `/definir-senha?token=...`, da fatia 1 ficam ao lado dela, fora da área logada).

**Layout (desenho meu; as cores e medidas saem de `04-visual.md`)**
- Uma coluna estreita centralizada, sobre o fundo creme dos PNG, com um cartão branco de uns 400 px no máximo e **que funciona de 360 px em diante** (suponho: o endereço da tela de entrar também pode ser aberto no celular).
- No alto do cartão, o quadrado claro arredondado com a letra "R" do menu lateral (li nos PNG 01 e 32; na tela de entrar ele fica sobre fundo claro, então use a versão em verde-petróleo de `04-visual.md`) e o título **`RH-System`** (texto de `fatia-0.md`, Task 4).
- Dois campos e um botão, nesta ordem, um embaixo do outro:
  1. rótulo **`E-mail`**, campo `type="email"` (`autoComplete="username"`, suponho, para o gerenciador de senhas do navegador);
  2. rótulo **`Senha`**, campo `type="password"` (`autoComplete="current-password"`, suponho);
  3. botão **`Entrar`**, cheio, na largura do cartão.
- A mensagem de erro aparece **entre o campo Senha e o botão**, em cor de erro da paleta, com `role="alert"` (suponho a posição; o plano só exige o `role`).
- Sem mais nada na fatia 0: **sem** "Esqueci minha senha" (a rota é da fatia 1; o link entra na 1b, abaixo do botão), **sem** "Lembrar de mim", **sem** "Criar conta" (o contrato não tem: o acesso nasce pelo convite do RH).

**Regras dos campos (`fatia-0.md`, Task 4; contrato `EntrarComando`)**
- E-mail: `trim()` e pelo menos 1 caractere. A API compara o e-mail sem diferença de maiúsculas e sem espaços nas pontas (contrato).
- Senha: pelo menos 1 caractere e **nenhum `trim`**: uma senha com espaço no começo ou no fim é válida e vai exatamente como foi digitada (contrato: até 128 caracteres).
- Campo vazio **não chama a API**: a tela mostra `Preencha o e-mail e a senha.` (a mesma frase do 400 do contrato).
- O botão fica `disabled` enquanto o envio está em andamento; **clique duplo = uma única requisição**.

**Mensagens de erro (cada uma em `role="alert"`)**

| Situação | Texto na tela | Origem |
|---|---|---|
| Campos vazios (sem chamar a API) ou 400 | `Preencha o e-mail e a senha.` | `fatia-0.md`; é o `detail` do exemplo do 400 da rota `entrar` (`Erro400Login`, contrato 0.2.1; li) |
| 401 | `E-mail ou senha inválidos.` | contrato: o 401 é o mesmo para e-mail inexistente, senha errada e inativo com senha errada (li) |
| 403 com `detail` | o próprio `detail` | contrato (li): dois casos, `Seu acesso está desativado. Fale com o RH.` (inativo com a senha certa) e `Seu perfil de acesso não está configurado. Fale com o RH.` (sem nível de acesso) |
| 403 sem `detail` | `Seu acesso está desativado. Fale com o RH.` | `fatia-0.md` |
| 429 | `Muitas tentativas. Aguarde um minuto e tente de novo.` | contrato: limite de 5 tentativas por minuto por IP (li) |
| 500, ou 200 que não é uma sessão (defeito do back-end) | `Erro no servidor. Tente novamente em instantes.` | `fatia-0.md` (o cliente lança `ErroDeApi` 500 quando `ehSessao` falha) |
| Sem conexão (status 0) ou qualquer outro erro | `Não foi possível conectar. Tente novamente.` | `fatia-0.md` |

Li que os textos de 400, 401, 403 (acesso desativado), 429 e 500 de `fatia-0.md` são **idênticos** ao `detail` dos exemplos do contrato. O segundo 403 do contrato (perfil não configurado) não tem texto fixo no plano: por isso a tela mostra o `detail`. O texto de falha de rede só existe no plano (o contrato não tem resposta para "sem conexão").

**Estados**
- **Verificando a sessão guardada:** com um refresh guardado, a `PortaDeAcesso` mostra `<p role="status">Carregando…</p>` enquanto renova, **sem** a tela de entrar piscar. Sem refresh guardado, não há espera e nada é chamado.
- **Vazio (inicial):** campos em branco, botão habilitado.
- **Enviando:** botão `Entrar` desabilitado (o texto do botão **não muda**: os testes procuram o botão pelo nome `Entrar`).
- **Erro:** a mensagem da tabela acima; os campos mantêm o que foi digitado, e a senha também (a pessoa corrige e tenta de novo).
- **Sessão que não renova** (refresh vencido ou usado: 401 em `renovarSessao`): a tela de entrar aparece **sem mensagem** (suponho que é o desejado; `fatia-0.md` não define mensagem de "sessão expirada" e a regra A5 de `01-regras.md` proíbe inventá-la).
- **Sucesso:** a `PortaDeAcesso` troca a tela pelo shell; o campo Senha some.
- **Sem permissão:** não se aplica aqui (o 403 do login é a mensagem de acesso desativado, acima).

**Rotas:** `entrar` (`POST /api/auth/entrar`, pública, `security: []`); `renovarSessao` (`POST /api/auth/renovar`, pública) só na porta de acesso.

**Observações para quem monta**
- O token de acesso fica só em memória e o refresh token no `localStorage`, chave `rh.refresh` (`01-regras.md`, A9). A tela de entrar **não** lê nem escreve nada além disso; não registre a senha no console.
- O 401 com e-mail inexistente e com senha errada é igual de propósito (a API não revela se o e-mail existe): **não** escreva "e-mail não encontrado" nem "senha incorreta".

---

## Tela sem print: Shell do portal

**Descrição em texto.** É a moldura que envolve todas as telas depois do login: menu lateral à esquerda e a área de conteúdo à direita. A fatia 0 entrega uma **versão mínima**, sem biblioteca de rotas, para provar o login e a política de segurança; a fatia 1a a completa com o menu por perfil. As duas versões estão descritas aqui para que o que a 0 constrói não precise ser refeito.

### O que a fatia 0 entrega (`fatia-0.md`, Task 4: `ShellDoPortal`)

- O texto **`Portal de RH`** (o nome do menu no Figma, li nos PNG 01, 32 e 52).
- O **nome do usuário** (`usuario.nome`).
- O **rótulo do perfil**: `Funcionario` → `Funcionário`, `Gestor` → `Gestor`, `RH` → `RH` (`rotulosDePerfil`).
- O botão **`Sair`**: limpa a sessão (memória, `localStorage`, estado) **primeiro** e depois chama `sair` com o refresh em uso, ignorando falha (`AuthProvider.sair`). A pessoa volta à tela de entrar e `rh.refresh` some.
- **Não** mostra `cargo` nem `setor` e **não** chama `obterEmpresa` (a rota é da fatia 1; o nome da empresa entra na 1a).

### O shell completo (fatia 1a), como o Figma o desenha

**Menu lateral (li nos PNG 01, 29, 31, 32 e 52): largura de uns 233 px, fundo verde-petróleo escuro, texto claro.** De cima para baixo:

1. **Marca:** quadrado claro arredondado com a letra "R", o texto `Portal de RH` e, embaixo dele, em corpo menor, o **nome da empresa**. O Figma escreve literalmente `[Nome da empresa]` (marcador). O nome real é `nomeEmpresa` de `obterEmpresa` (contrato: "aparece no menu de todas as telas"). Com `404 EMPRESA_NAO_CONFIGURADA` (a tabela nasce vazia; a demonstração já vem configurada), mostre **só** `Portal de RH`, sem a segunda linha. Enquanto carrega, a segunda linha fica vazia.
2. **Grupos de itens**, cada um com um título pequeno e itens abaixo. O item da página atual fica destacado. Alguns itens têm uma **bolinha com número** (ver a tabela). Os grupos e itens de cada perfil estão abaixo.
3. **Rodapé** (separado por uma linha): avatar redondo com as **iniciais** (primeira letra do primeiro nome e do último: "AS", "BL", "CR"), o **nome** e, embaixo, uma linha de apoio:
   - **RH:** `Perfil RH, acesso total` (li no PNG 01; o texto é fixo);
   - **Funcionário:** `{cargo}, {setor}`, por exemplo `Analista pleno, Financeiro` (li no PNG 32; o contrato diz que `cargo` e `setor` de `UsuarioResposta` servem a este rodapé);
   - **Gestor:** `Perfil gestor, setor {setor}`, por exemplo `Perfil gestor, setor Comercial` (li no PNG 52).
   Com `cargo` ou `setor` `null` (o contrato permite), mostre só o que existe (suponho); com os dois `null`, só o nome.
4. **`Trocar senha` e `Sair` (desenho meu):** o Figma **não desenha** nenhum dos dois. Proponho dois botões de texto pequenos **no rodapé, abaixo da linha de apoio**, sempre visíveis: `Trocar senha` (abre o modal descrito na nota da fatia 1) e `Sair`. Mantê-los visíveis (em vez de um menu que abre ao clicar no avatar) preserva os testes da fatia 0, que procuram o botão `Sair` pelo nome. **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D-rodape`): é este desenho, dois botões de texto pequenos no rodapé do menu, abaixo do nome e do cargo ("Trocar senha", que chega na 1b, e "Sair").

**Menus por perfil (li nos PNG)**

| Perfil | Grupo → itens (as bolinhas estão entre parênteses; o número é de exemplo, o real vem da API) |
|---|---|
| RH (PNG 01, 29, 31) | **Visão geral:** Painel. **Pessoas:** Colaboradores, Advertências, Formulários, Treinamentos. **Rotina:** Ponto e feriados (4), Avisos. **Sistema:** Cadastros, Log de auditoria. **Meu espaço** (decisão do dono, 2026-11-02; o Figma não o desenha): Meu ponto, Férias, Holerites, Minhas advertências, Meus formulários, Meus treinamentos, Benefícios. |
| Funcionário (PNG 32) | **Seu dia a dia:** Início, Meu ponto, Férias, Holerites. **Comunicação:** Avisos (2), Formulários (1). **Você na empresa:** Treinamentos, Advertências, Benefícios. |
| Gestor (PNG 52; o mesmo em 46 e 51) | **Seu dia a dia:** Início, Meu ponto, Férias, Holerites. **Seu setor:** Painel da equipe (2), Ponto da equipe, Formulários, Treinamentos, Advertências. **Comunicação:** Avisos (2). **Meu espaço** (decisão do dono, 2026-11-02; o Figma não o desenha): Meus formulários, Meus treinamentos, Minhas advertências (Meu ponto, Férias, Holerites e Benefícios ele já tem em "Seu dia a dia"). |

**O que muda em relação ao Figma (e por quê)**
- **Acrescenta "Meus documentos"** (tela da fatia 1, sem print; roadmap, decisão 14). Onde (suponho; os três menus do Figma não o têm): Funcionário, no grupo "Você na empresa", depois de Benefícios; Gestor, no grupo "Seu dia a dia", depois de Holerites (a nota da tela 46 da fatia 7 pede o mesmo para "Benefícios" do gestor); RH, no grupo "Rotina", depois de Avisos. A bolinha dele é `documentosPendentes` de `obterContadoresDoMenuDoFuncionario` (a rota serve aos três perfis; a fundação 1a já a lê). **Decidido pelo dono em 2026-11-02**: "Meus documentos" continua nos três menus.
- **RH e gestor ganham o grupo "Meu espaço"** no fim do menu (**decidido pelo dono em 2026-11-02**, `DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D-menu`), com os itens pessoais **ativos** e levando às telas pessoais (`05-caminhos-de-tela.md`, regra 6): **RH:** Meu ponto (`/eu/meu-ponto`), Férias (`/eu/ferias`), Holerites (`/eu/holerites`), Minhas advertências (`/eu/advertencias`), Meus formulários (`/eu/formularios`), Meus treinamentos (`/eu/treinamentos`) e Benefícios (`/eu/beneficios`); **gestor** (que já tem Meu ponto, Férias, Holerites e Benefícios): Meus formulários, Meus treinamentos e Minhas advertências. O funcionário não ganha o grupo. Cada item aparece quando a rota da tela existe (regra da 1a), então o grupo se completa fatia a fatia.
- **Gestor ganha "Benefícios"** (decisão 14; nota da tela 46, fatia 7). É da fatia 7; a 1a só reserva o lugar, se a fundação mostrar itens de telas que ainda não existem.
- **Em Cadastros** o menu lateral do próprio Cadastros perde "Níveis de acesso" (decisão do dono, 2026-10-30) e ganha "Empresa" (fatia 1) e "Pagamento" (fatia 7). Isso é da tela 29, na nota da fatia 1.
- **Os itens de telas que só chegam em fatias posteriores** (Painel, Advertências, Formulários, Treinamentos, Ponto e feriados, Avisos no RH; quase tudo do funcionário e do gestor) são decisão da fundação (1a): ou ficam fora do menu até a fatia da tela, ou ficam como espaço reservado. **Não** deixe um item que leva a uma página que não existe.
- **As bolinhas** vêm de `obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor` e `obterContadoresDoMenuDoRh` (`contadores-do-menu`; nota da tela 32, fatia 7). **A fundação (1a) já as lê** com `useContadoresDoMenu` e as mostra no menu; os valores reais chegam quando as fatias que os produzem existirem no back-end (até lá, o dado simulado do contrato). Nenhuma ficha manda "trocar a ausência de bolinhas".
- **Nome do perfil:** na fatia 0 o shell escreve `Funcionário` (rótulo do plano). O campo "Nível de acesso" do cadastro e a coluna "Quem" da auditoria escrevem **`Colaborador`** para o mesmo valor `Funcionario` (contrato; li "Colaborador" nos PNG 02 e 31). Não é contradição: na 1a o rodapé troca o rótulo do perfil por cargo e setor (funcionário) ou por "Perfil gestor, setor X" (gestor). Use "Colaborador" no cadastro e na auditoria e "Funcionário" só onde `rotulosDePerfil` já o usa.

**Rotas:** `sair` (botão `Sair`); `obterEmpresa` (nome da empresa; **fatia 1a**, contrato em `20-fatia-1-colaboradores.yaml`).

**Estados**
- Carregando a sessão: `Carregando…` (acima). Sem usuário, o shell **não** aparece.
- Falha ao carregar o nome da empresa (404 ou erro): só `Portal de RH`. Nenhuma mensagem de erro por causa disso.
- Perfil que o contrato não conhece: `ehSessao` rejeita a sessão e a pessoa fica na tela de entrar (`fatia-0.md`, Review Focus 5). O shell nunca mostra rótulo vazio.

**Observações para quem monta**
- Largura de 1440 px nos PNG; a demo pode ser em 1366 px. A coluna do menu mede 233 px nos dois (li); o que encolhe é o conteúdo (`telas\fatia-8.md`, seção 3).
- `Sair` não pede confirmação (suponho; nada no plano nem no contrato pede).

---

## Perguntas abertas e decisões do dono

**Decididas pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; **não se registram como dúvida**):

1. **Onde ficam "Trocar senha" e "Sair"** (antes `D-rodape`): dois botões de texto pequenos no **rodapé do menu**, abaixo do nome e do cargo (mantém o `Sair` da fatia 0 como está). Sem `x-provisorio`: é desenho.
2. **Itens pessoais no menu do RH e do gestor** (antes `D-menu`): o grupo **"Meu espaço"** existe, com os itens ativos (ver "O que muda em relação ao Figma"); **"Meus documentos"** continua nos três menus (no grupo "Rotina" do RH) e o gestor tem também "Benefícios". A rota `listarMeusDocumentos` serve aos três perfis (`x-perfis: [Funcionario, Gestor, RH]`). Sem `x-provisorio`.

Sem pergunta aberta nova nesta fatia:

3. Nenhum `x-provisorio` do `PROVISORIOS.md` toca a fatia 0.
