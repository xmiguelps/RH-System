# DÚVIDAS do front-end

> Aqui ficam as dúvidas que **o plano, o contrato e as notas de tela não resolvem**. Cada uma tem um **texto pronto para a pessoa colar** e mandar ao dono; o texto **não menciona Claude nem IA** (começa com `Mensagem para o dono:`) e **nunca vai no corpo de um PR** (o PR só lista o código e uma linha de cada dúvida: `01-regras.md`, C3). As regras estão em `01-regras.md`, **seção D**: registre na hora; mostre **na hora**, **de novo uma única vez** na próxima mensagem da pessoa, **quando não puder mais avançar** e **no final**; nunca mais que isso. Dúvida que não bloqueia: continue; bloqueio parcial: faça o resto. Algumas dúvidas **já são conhecidas** e estão na tabela "Dúvidas já conhecidas", mais abaixo, cada uma com o **seu código fixo**.
>
> O bloco marcado **EXEMPLO** (`D-000`, mais abaixo) mostra o formato e **é inventado**: apague-o ao começar.

## Decisões do dono

(Leia esta seção **antes de cada tarefa**: ela é o que você consulta. Uma linha por dúvida respondida. As de 2026-11-02 já estão aqui: **mandam sobre o texto antigo** de qualquer ficha, nota ou tarefa.)

| Data | Dúvida | Decisão do dono | Onde afeta |
|---|---|---|---|
| 2026-11-02 | `D-menu` | **A: sim.** O RH e o gestor ganham um grupo **"Meu espaço"** no fim do menu com os itens pessoais. **RH:** Meu ponto, Férias, Holerites, Minhas advertências, Meus formulários, Meus treinamentos, Benefícios. **Gestor** (que já tem Meu ponto, Férias, Holerites e Benefícios): Meus formulários, Meus treinamentos, Minhas advertências. (Ajuste de 2026-11-02: o item do RH se chama "Minhas advertências", para não repetir o "Advertências" do grupo Pessoas; leva à mesma rota, `/eu/advertencias`.) "Meus documentos" continua nos três menus. | `fatia-1a-fundacao.md`, Task 12 (`menu.ts`: linhas **ativas**); fichas 3, 4, 5, 6 e 7; `05-caminhos-de-tela.md`; os itens abrem as telas pessoais `/eu/...` e só aparecem quando a rota existe |
| 2026-11-02 | `D-rodape` | **A:** dois botões de texto pequenos no **rodapé do menu**, abaixo do nome e do cargo: "Trocar senha" (chega na 1b) e "Sair". | `fatia-1a-fundacao.md`, Task 12 (`RodapeDoMenu.tsx`); `fatia-1b-telas.md`, Task 7 |
| 2026-11-02 | `D-fonte` | **Manter a Inter** (trocar para a Hanken Grotesk só se o dono pedir depois). | `02-pilha.md`, `04-visual.md` (seção 4), `fatia-1a-fundacao.md`, Task 1; a pessoa ainda confere a letra ao lado do PNG |
| 2026-11-02 | `D6-3` | **Manter `CERT-{ano}-{id}`** (o que a API devolve); sem sigla por treinamento. | `fichas\fatia-6.md`; a tela do certificado mostra o `numero` como vem |
| 2026-11-02 | `D6-2` | **Não** criar a aba Treinamentos na ficha do colaborador do RH. | `fichas\fatia-6.md`; `listarTreinamentosDeFuncionario` fica fora da cobertura, com o motivo "decisão do dono, 2026-11-02" |
| 2026-11-02 | `D5-3` | **Deixar como está:** o gestor **não** vê formulário encerrado na lista da tela 57. | `fichas\fatia-5.md`, tela 57 (`listarFormulariosDaEquipe`); sem caminho para o estado "anônimo encerrado" do gestor, só trate a resposta se ela chegar |
| 2026-11-02 | `D-F4-5` | **Manter** o botão "Lembrar quem não viu" do aviso (tela 27); o e-mail entrou na lista fechada do contrato (`CONVENCOES.md`, seção 12, versão 0.2.1); **sem limite** de lembretes por aviso. **Não é mais provisório.** | `fichas\fatia-4.md`, tela 27 (`lembrarQuemNaoViuAviso`): construa normalmente e **não** anote "usa provisório" |
| 2026-11-02 | `D5-4` | **Manter** o botão "Lembrar quem falta" do gestor (tela 57). **Não é mais provisório.** | `fichas\fatia-5.md`, tela 57 (`lembrarQuemFaltaNaEquipe`): construa normalmente e **não** anote "usa provisório" |

## Modelo de uma dúvida

Copie o bloco abaixo para a seção "Dúvidas" e preencha. O que está entre `<...>` é para você preencher. **O código:** a dúvida **conhecida** (tabela "Dúvidas já conhecidas") usa o **código fixo** dela, **nunca** `D-001`; só a dúvida que **você** achar por conta própria recebe o número seguinte (`D-001`, `D-002`, ...).

````markdown
### <código> · <título curto> · [aberta | respondida | cancelada]

- **Registrada em:** <data e hora> (<fatia · tarefa · passo>)
- **Bloqueia:** <não | parcialmente (o que continua: ...) | totalmente>
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [ ]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida <código> do front-end do RH-System (<título curto>)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: <arquivo do plano e tarefa ou seção; tela NN, nome; rota (operationId); versão do contrato (`info.version`, lida de `contrato\openapi.yaml`)>.
  O que achei: <o fato, com arquivo e linha do que li; o que falta ou se contradiz>.
  O que sugiro: <proposta concreta e por quê; o que muda se você escolher outra>.
  Pergunta: <uma pergunta que se responde com "sim", "não" ou escolhendo A ou B>.
  Enquanto isso: <o que estou fazendo: sigo com X / parei em Y>.
  ```

- **Resposta do dono:** (vazio até a pessoa trazer; com a data)
- **O que mudou por causa da resposta:** (vazio; o que você fez; e a linha acrescentada em "Decisões do dono")
````

## Dúvidas já conhecidas (registre quando chegar ao passo que o plano indica)

Só estão aqui as que **continuam abertas** (as respondidas estão em "Decisões do dono", acima). **Cada dúvida tem um código só**, o da primeira coluna: ao registrar, copie o bloco do modelo para a seção "Dúvidas" **com esse código** (nunca `D-001`) e preencha os campos. O **conteúdo** de cada uma (o que achei, o que sugiro, a pergunta) mora no arquivo da coluna "Nasce em"; o texto para colar sai dele e do modelo acima. **Nenhuma bloqueia.**

| Código | Nasce em | O que fazer enquanto o dono não responder (o padrão) |
|---|---|---|
| `D-benef` | `fatia-1b-telas.md`, Task 2, passo 6 (tela 02, benefícios já marcados no cadastro); o conteúdo está em "Dúvidas conhecidas desta fatia" do mesmo arquivo | Todos os benefícios **desmarcados** (o que o contrato diz): quem cadastra marca os que valem. |
| `D-editar-inativo` | `fatia-1b-telas.md`, Task 3, parte D, passo D5 (tela 03, "Editar dados" para quem está inativo); o conteúdo está no mesmo arquivo | "Editar dados" aparece **também para quem está inativo**. |
| `D-F4-1` | `fichas\fatia-4.md`, seção "4b. Dúvidas já conhecidas" (tela 08, modal "Enviar holerite") | Construa a terceira opção, "Agendar para uma data" (o contrato a permite; o Figma não a desenha). |
| `D-F4-3` | `fichas\fatia-4.md`, seção 4b (telas 36, 49 e 43: link "Fale com o RH") | Mostre como texto simples, **sem link** (o contrato não tem rota nem endereço para isso). |
| `D-F4-4` | `fichas\fatia-4.md`, seção 4b (envio de holerites em lote, `enviarHoleritesEmLote`, sem tela) | Um botão "Enviar holerites em lote" ao lado de "Adicionar colaborador" na lista de colaboradores (tela 01), que abre a tela descrita em `telas\fatia-4.md`. |
| `D5-2` | `fichas\fatia-5.md`, seção "10. Decisões do dono de 2026-11-02 e dúvidas já conhecidas" (tela 16, "Encerrou em ...") | Mostre o dia de `encerradoEmUtc` (pode dar um dia depois do prazo que o PNG mostra). |
| `D6-4` | `fichas\fatia-6.md`, seção 10 (incluir no onboarding uma pessoa já cadastrada, a partir da ficha da tela 03; `incluirFuncionarioNoOnboarding`) | **Sem botão**: a rota fica na exclusão da cobertura. |
| `D-F7-1` | `fichas\fatia-7.md`, seção "9. Decisão do dono sobre o menu e dúvidas que a sessão já pode registrar" (tela 51, sugestões de outra data) | Deixe assim: o gestor só vê as sugestões que aguardam resposta na aba Férias da ficha da pessoa. |
| `D-F7-2` | `fichas\fatia-7.md`, seção 9 (tela 7.4, "Pagamento") | Siga o contrato: monte "Pagamento" dentro de Cadastros, ao lado de "Empresa". |
| `D-F8-1` | `fichas\fatia-8.md`, seção "9. Dúvidas que a sessão pode registrar" (roteiro da demo) | Descreva os quatro fluxos no roteiro; o dono corta. |
| `D-F8-2` | `fichas\fatia-8.md`, seção 9 (plano B da demo) | Se a API cair, mostre as telas pelo servidor de desenvolvimento com os dados de `ELENCO.md`, avisando a banca. |
| `D-F8-3` | `fichas\fatia-8.md`, seção 9 (resolução do projetor) | Projete para 1366 px, com piso de 1280 px (`04-visual.md`, seção 9). |

**Não é dúvida:** CPF e telefone completos do RH aparecem **com pontuação** (`846.392.710-69`, `(11) 98765-4410`); é o padrão adotado.

## Dúvidas

### D-000 · EXEMPLO (inventado): botão "Exportar em PDF" sem rota no contrato · aberta

- **Registrada em:** 2026-11-04 10:12 (F3·T2·S4)
- **Bloqueia:** parcialmente (o que continua: o resto da tela e os filtros)
- **Mostrada à pessoa:** na hora [x] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [ ]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-000 do front-end do RH-System (botão "Exportar em PDF" sem rota)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: telas\fatia-3.md, tela NN (espelho de ponto do RH); contrato 0.2.0 (exemplo); não há operationId para isso em contrato\ROTAS.md.
  O que achei: o PNG da tela NN mostra o botão "Exportar em PDF", mas a nota da tela não diz se ele fica e o contrato só tem a exportação em CSV (exportarPontoParaFolha).
  O que sugiro: esconder o botão (o contrato não tem como atendê-lo). Se você quiser o PDF, é preciso uma rota nova no contrato.
  Pergunta: o botão "Exportar em PDF" fica fora da tela (A) ou você vai acrescentar a rota ao contrato (B)?
  Enquanto isso: construo o resto da tela sem o botão e sigo para a próxima tela.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)
