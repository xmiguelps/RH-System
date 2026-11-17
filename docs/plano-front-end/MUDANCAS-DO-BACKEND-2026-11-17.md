# O que mudou no back-end depois do plano do front (decisões do dono de 2026-11-17)

Contrato `contrato/openapi.yaml`, versão **0.3.0**. Cada item diz o que o front precisa fazer. O que não está aqui não mudou. Os detalhes de cada rota estão no `contrato/CHANGELOG.md` (seção 0.3.0), em `contrato/STATUS.md` e nas descrições do próprio contrato.

## Preciso de uma tela nova, um campo novo ou um comportamento novo

1. **Captcha na candidatura pública** (`obterFormularioPublico`, `responderFormularioPublico`).
   - A resposta da página traz `captcha`: um objeto `{ chaveDoSite }` quando o captcha está ligado e `null` quando está desligado.
   - Ligado: desenhe o widget do **Cloudflare Turnstile** com `chaveDoSite` e mande o token que ele gera no campo de texto `captchaToken` do `multipart/form-data` do envio. Sem o campo, 400 em `errors.captchaToken` ("Confirme que você não é um robô."); token que o provedor não confirma, 422 com `codigo: CAPTCHA_INVALIDO`. Cada token vale uma tentativa e uns 5 minutos: depois de qualquer erro, peça outro (reinicie o widget).
   - Desligado: não mostre nada e não mande o campo.
   - **A política de segurança do site (CSP) precisa liberar o Turnstile.** Hoje `front-end/public/staticwebapp.config.json` tem `default-src 'self'`, e o teste `staticwebapp.config.test.ts` trava as diretivas. Falta acrescentar `script-src 'self' https://challenges.cloudflare.com` e `frame-src https://challenges.cloudflare.com`. Sem isso o widget não carrega e, com o captcha ligado, nenhuma candidatura entra. Essa é a única exceção à regra "nenhum recurso externo" desta página.
2. **Vídeo por arquivo nos treinamentos** (`adicionarModuloDeTreinamento`, `atualizarModuloDeTreinamento`, `obterConteudoDeModulo`, `baixarArquivoDeModulo`).
   - **RH:** o módulo `VIDEO` aceita um link (`urlExterna`) **ou** um arquivo (`arquivo`, MP4 ou WebM, até 100 MB), nunca os dois nem nenhum. Erros: 400 em `errors.arquivo` ("Escolha um só: o link ou o arquivo de vídeo.", "Envie um vídeo MP4 ou WebM.", "O vídeo pode ter até 100 MB.") e 400 em `errors.urlExterna` ("Informe o endereço do vídeo ou envie o arquivo."). A tela precisa de uma barra de envio para um arquivo grande. Ao editar, o vídeo que já tem arquivo fica como está se a tela não mandar nada novo, troca o arquivo se mandar um, e vira link se mandar `urlExterna`.
   - **Quem assiste:** o módulo `VIDEO` por arquivo vem com `temArquivo: true` e `urlExterna: null`. Chame `baixarArquivoDeModulo` e use o `url` como `src` de um `<video controls>`; o link vale só alguns minutos, então peça de novo ao abrir o módulo.
   - **A política de segurança do site também precisa liberar isso:** `media-src 'self' https://<conta>.blob.core.windows.net` (o endereço real só existe depois do deploy do armazenamento). Sem isso o navegador bloqueia o vídeo; nesse caso o botão deve só abrir o link, que baixa o arquivo.
3. **Exportação do ponto para a folha** (`exportarPontoParaFolha`). Três parâmetros novos, todos opcionais: `colunas` (os códigos das colunas que saem, na ordem pedida, separados por vírgula), `formatoHoras` (`MINUTOS`, `HORAS_DECIMAIS` ou `HH_MM`) e `separador` (`PONTO_E_VIRGULA` ou `VIRGULA`). Sem nenhum, o arquivo é o de sempre. A tela de exportação ganha a escolha das colunas (com a ordem), do formato das horas e do separador. Os códigos das colunas estão na descrição da rota; o cabeçalho das colunas de horas leva a unidade (`_min`, `_h`, `_hhmm`).
4. **Advertência: quem do RH registrou** (`AdvertenciaDetalheRh.registradaPor`). O detalhe da advertência, **só na tela do RH**, ganha `registradaPor` (`FuncionarioResumo` ou `null`). Mostre "Registrada por {nome}" quando vier preenchido; `null` quer dizer que o gestor registrou pela tela dele ou que a advertência é antiga. O colaborador e o gestor nunca recebem o campo, e a coluna "Aplicada por" continua mostrando "RH" sem nome.
5. **Formulários: duas rotas novas** (`alterarPrazoDoFormulario`, `voltarFormularioParaRascunho`).
   - `PUT /api/rh/formularios/{id}/prazo` muda o prazo de um formulário **publicado** (corpo `{ "prazo": "AAAA-MM-DD" }`, obrigatório, nunca anterior a hoje, e não dá para tirar o prazo). Erros 422: `FORMULARIO_NAO_PUBLICADO`, `FORMULARIO_ENCERRADO` e `FORMULARIO_RECORRENTE` (formulário recorrente não muda o prazo por aqui).
   - `POST /api/rh/formularios/{id}/rascunho` devolve um formulário **agendado** a rascunho (sem corpo). 422 `FORMULARIO_NAO_ESTA_AGENDADO` nos outros casos. O link por código de um agendado deixa de valer; publicar de novo gera outro.
6. **Erros novos para tratar:** `ADVERTENCIA_ANTES_DA_ADMISSAO` (422 ao registrar advertência com o dia do ocorrido anterior à admissão), `CAPTCHA_INVALIDO`, `FORMULARIO_RECORRENTE`, `FORMULARIO_NAO_ESTA_AGENDADO`. O código `FORMULARIO_JA_RESPONDIDO` (422 de editar um formulário publicado que já tem respostas) agora também está no contrato.

## O texto de uma tela muda

7. **Minhas férias, quando o gestor do setor está ausente** (`obterMinhasFerias`): `gestorQueAnalisa` vem `null` (só o RH decide), como nos outros casos em que o gestor não analisa. Mostre "O RH analisa o pedido".

## Pendências que dependem do deploy

- Preencher a política de segurança do site (itens 1 e 2) quando o endereço do armazenamento e o do Cloudflare estiverem definidos.
- O captcha só liga quando o dono criar a conta no Cloudflare e configurar as chaves no ambiente (`Captcha:Ativo`, `Captcha:ChaveDoSite`, `Captcha:ChaveSecreta`). Para ensaiar sem conta, o Turnstile tem chaves de teste (veja o `README.md`, tabela de configuração).
