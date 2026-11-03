# Notas de tela da fatia 8: fechamento (front-end)

**A fatia 8 não tem tela nova** (roadmap: "0 telas") **nem rota nova** (`contrato\ROTAS.md` vai só até a fatia 7). Esta nota serve de **lista de conferência final** e de guia para a verificação a 1366×768. Ficha: `docs\plano-front-end\fichas\fatia-8.md`.

**Como ler.** "Li" é o que está nos arquivos citados (`roadmap`, `ROTAS.md`, `PROVISORIOS.md`, os PNG 00, 05, 06, 35 e 51); "suponho" é escolha minha. As **medidas dos PNG** abaixo são lidas a olho nas imagens, em pixels aproximados, não medidas por programa.

## 1. As 61 telas com print, por fatia (conferir ao lado do PNG)

Números de `Figma\telas\NN-perfil-tela.png`; a distribuição por fatia é a do roadmap (cada fatia: linha "Telas"). Conferi a conta: 9 + 8 + 16 + 9 + 8 + 11 = 61 (RH 32, funcionário 14, gestor 15).

| Fatia | Telas com print | Quantas |
|---|---|---|
| 1 Colaboradores | RH 01, 02, 03, 09, 10, 29, 30, 31; Gestor 52 | 9 |
| 3 Ponto | RH 04, 12, 13; Funcionário 33; Gestor 47, 54, 55, 56 | 8 |
| 4 Comunicação e documentos | RH 07, 08, 11, 14, 15, 27, 28; Funcionário 36, 37, 38, 43; Gestor 49, 50, 53, 59, 60 | 16 |
| 5 Formulários | RH 16, 17, 18, 19, 20, 21; Funcionário 39, 40; Gestor 57 | 9 |
| 6 TAD | RH 22, 23, 24, 25, 26; Funcionário 41, 42; Gestor 58 | 8 |
| 7 Gestão | RH 00, 05, 06; Funcionário 32, 34, 35, 44, 45; Gestor 46, 48, 51 | 11 |
| 0 e 2 | nenhuma | 0 |

## 2. Telas sem print

Não há PNG para elas: a pessoa confere pela **descrição em texto** da nota da fatia (e o caminho de cada uma está em `05-caminhos-de-tela.md`). Esta lista vem do roadmap (decisão 14 e as linhas "Telas" de cada fatia), de `ROTAS.md` e de `PROVISORIOS.md`, **não** das notas das outras fatias (outra parte do plano as escreve): ao consolidar, abra cada `telas\fatia-N.md` e confira que a seção existe; se faltar alguma, registre uma dúvida.

| Fatia | Tela sem print | De onde sai |
|---|---|---|
| 0 | Login | roadmap, decisão 14 |
| 1 | Convite, redefinição de senha, troca de senha, "Meus documentos" | roadmap, decisão 14 |
| 1 | Cadastros, "Empresa" e "Usuários do sistema" (itens do menu de Cadastros) | `salvarEmpresa` (contrato: "a tela não existe no Figma"); design seção 7 (tela 29) |
| 1 | "Editar dados", "Inativar" e "Readmitir" da ficha (tela 03) | `telas\fatia-1.md` |
| 2 | Importação por Excel (modo "Importar planilha" do cadastro) | roadmap, decisão 14 |
| 3 | Pedir ajuste de ponto (modal); Enviar atestado e "Meus atestados"; extrato completo do banco de horas; corrigir batidas do dia (modal "Editar"); atestados do RH; aba Ponto da ficha da equipe e atestados da equipe | roadmap, decisão 14 (atestado); `telas\fatia-3.md` |
| 4 | Lote de holerites; "Substituir holerite"; detalhe da advertência e "Responder contestação" (RH); "Contestar" e "Ver detalhes" (funcionário); detalhe da advertência e aba Advertências da ficha da equipe (gestor); "Publicar" e "Excluir" de aviso em rascunho ou agendado | `PROVISORIOS.md` ("Não há tela desenhada para o lote"); `telas\fatia-4.md` |
| 5 | Formulário público do candidato; resultado do formulário; respostas de uma pessoa ou candidato; responder um formulário; minhas respostas | roadmap, decisão 14; `obterResultadoFormulario` em `ROTAS.md` ("Mostra o resultado geral"); `telas\fatia-5.md` |
| 6 | Fazer o treinamento (módulos); validação pública do certificado; aba Treinamentos da ficha da equipe | `ROTAS.md` (`/api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}`, `/api/publico/certificados/{codigoValidacao}`); `telas\fatia-6.md` |
| 7 | 7.1 aba Férias da ficha da equipe; 7.2 pedidos de férias do RH; 7.3 pedidos de benefícios do RH; 7.4 pagamento; 7.5 pendências do RH; 7.6 pessoas sem registro hoje | `telas\fatia-7.md` |

## 3. Guia para a verificação a 1366×768

O Figma é de 1440 px; o projetor da demo pode ser de 1366×768 (design, seção 9: hipótese não verificada, "projetar para 1366 e conferir na demo").

**O que li nos PNG 00 e 51** (aproximado): menu lateral de uns **233 px**; área de conteúdo de uns **281 a 1392 px** (margem de uns 48 px de cada lado). **Conta minha:** a 1366 px o conteúdo mede uns **1037 px** (1366 − 233 − 48 − 48), cerca de **74 px a menos** que nos PNG (1111 px). A altura dos PNG 00 e 51 passa de 1500 px: **rolar a página na vertical é normal**; o que não pode é rolar na horizontal.

**Onde olhar primeiro** (li nos PNG; os demais, o verificador acha):

| Onde | Por quê |
|---|---|
| Tela 51, Painel da equipe | tabela de 7 colunas (Pessoa, Ponto hoje, Banco de horas, Férias disponíveis, Avaliação, Advertências, Ações) e quadro de 13 semanas lado a lado com os cartões |
| Tela 00, painel do RH | duas colunas (a esquerda de uns 692 px e a direita de uns 386 px no PNG), barra de presença e tabela de últimas alterações |
| Tela 06, modal "Registrar férias" | uns **905 px de altura** no PNG (do topo em 24 px até 929 px), mais que os 768 px de tela: o corpo rola por dentro e os botões do rodapé ficam à vista |
| Tela 44, Benefícios | 4 cartões por linha (uns 265 px cada no PNG; uns 245 px a 1366): rótulos de botão podem quebrar linha |
| Painéis laterais (telas 35, 45, 02 e fichas 03 e 52) | altura total da janela: o conteúdo rola por dentro e o rodapé com os botões fica à vista |
| Tabelas largas de outras fatias (lista de colaboradores, ponto da equipe, prévia da importação) | colunas que não cabem: rolagem **da tabela**, não da página (suponho; conferir) |

**O que é "pronto" aqui:** nenhuma rota com rolagem horizontal da **página** a 1366×768; modais e painéis com os botões principais sempre visíveis; 1440 px continua parecido com o print. **Quem verifica e como:** a **pessoa**, rota por rota (a lista de rotas é a de `05-caminhos-de-tela.md`), a 1366×768 fixa; é conferência da pessoa, no relatório final, e não bloqueia (a sessão não olha navegador nem escreve script de geometria). Ver a ficha, seção 3.3.

## 4. Perguntas abertas

Nenhuma do `PROVISORIOS.md` toca esta fatia (ela não tem rota). Em aberto no roadmap (fatia 8, "Decisões do dono"): **quais fluxos entram no roteiro** e **o que mostrar como trabalhos futuros**. Em aberto no design (seção 9): a **largura mínima** (1366×768 é hipótese). A ficha traz o conteúdo das três dúvidas (`D-F8-1`, `D-F8-2` e `D-F8-3`, seção 9) para registrar em `DUVIDAS.md`.
