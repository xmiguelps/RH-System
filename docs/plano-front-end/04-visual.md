# 04 · Visual: cores, medidas, fontes, ícones e componentes-base, tirados dos PNG

Tudo aqui sai dos PNG de `Figma\telas\` (61 arquivos, todos de 1440 px de largura; alturas de 891 a 2015 px). **Ninguém precisa do Figma.** Este arquivo é a única fonte do visual: `fatia-1a-fundacao.md` o transforma em código e as notas de tela dizem só "como o PNG NN".

Como ler as marcas (a mesma ideia das notas de tela):

- **medido** = li pixels do PNG com um script (execução de 2026-10-30, `pngjs` 7.0.0 numa pasta temporária **fora** do repositório). Vale como número; o PNG foi exportado com anti-aliasing, então medidas de 1 px podem variar meio pixel.
- **visto** = abri o PNG com a ferramenta Read e olhei. **palpite** = deduzi olhando (ícones, estados que o Figma não desenha; a fonte já foi decidida pelo dono, seção 4). **suponho** = escolha minha onde nada decide. A pessoa que confere o visual olha os palpites primeiro.
- PNG que **abri um a um**: 00, 01, 02, 03, 10, 12, 13, 17, 18, 28, 29, 30, 31, 32, 33, 44, 46 e 51 (mais recortes ampliados de ícones e de texto). Os outros 43 PNG **não** abri aqui: o que digo deles vem das notas de tela (`telas\fatia-N.md`) e da repetição dos mesmos blocos.
- Os valores abaixo **não** mudam sem a pessoa conferir o PNG ao lado da tela. Onde o PNG diverge das notas de tela e do contrato, valem as notas e o contrato.

Como medi (para repetir, se quiser): numa pasta fora do repositório, `npm init -y; npm install pngjs@7.0.0`; um script de 20 linhas que lê o PNG (`PNG.sync.read`), lê o pixel `(x, y)` em `data[(largura * y + x) * 4]`, e (a) conta as cores mais frequentes de um retângulo, (b) percorre uma linha ou coluna e lista onde a cor muda (dá larguras, alturas e espessuras de borda), (c) acha a caixa de tudo que difere do fundo (dá o tamanho de um botão, de um selo ou de um texto) e (d) ajusta o raio de um canto comparando com `r − √(r² − (r − t)²)`.

---

## 1. Cores (medidas)

Cada hexadecimal é a cor **mais frequente** na região indicada (contagem de pixels no PNG) ou a de uma borda de 1 px nítida. Os nomes são os **tokens** da seção 2.

| Token | Hex | Onde e como foi medido |
|---|---|---|
| `pagina` | `#fffffa` | Fundo da página: 906.436 px da área de conteúdo (PNG 00); também a coluna `x 233..1439` de uma ponta à outra. |
| `superficie` | `#ffffff` | Cartão, campo, linha de tabela, cabeçalho e rodapé de janela (573.125 px no PNG 00). |
| `superficie-suave` | `#f2f8f7` | Cabeçalho de tabela inteiro (PNG 01, `y 242..286`); painel de indicadores (PNG 03, 33); bloco "Depois de salvar" (PNG 02). |
| `superficie-destaque` | `#e6f4f3` | Cartão "Presença agora" (PNG 00, 65.110 px); grupo de abas em pílula (PNG 01); faixa "Hoje" (PNG 51); cartão "Aniversariantes" (PNG 32). |
| `borda` | `#d9e4e2` | 1 px de cartão e de tabela (PNG 01 `y=241`, nítido); trilha da barra de progresso; cartão de opção não marcado (PNG 10). |
| `borda-campo` | `#c9d8d6` | 1 px de campo e de filtro (PNG 01 `y=149` e `y=192`; 02; 33). |
| `divisor` | `#e4ecea` | Linha entre cabeçalho e corpo de tabela, entre linhas, sob o cabeçalho e sobre o rodapé de janela e painel (PNG 01, 02, 10). |
| `tracejado` | `#9bbdba` | Borda tracejada da área de soltar arquivo (PNG 10) e do cartão "disponível para você" (PNG 44, visto). |
| `texto` | `#0e3236` | Títulos, valores, texto de tabela (PNG 00, 1.801 px só no título "Bom dia, Ana"). |
| `texto-suave` | `#4e6466` | Subtítulo, rótulo, cabeçalho de tabela, descrição (PNG 00, 01, 03). |
| `texto-fraco` | `#757575` | Texto de exemplo ("placeholder") dos campos (PNG 00 e 02). É a cor padrão do navegador, **não** da paleta. |
| `primaria` | `#0d5c63` | Menu lateral inteiro (333.044 px), botão primário (PNG 01, 1.977 px), links fortes, número positivo, avatar grande, barra "Trabalhando". |
| `primaria-media` | `#247b7b` | Links ("Ver todas as pendências"), borda e texto de botão sem preenchimento (PNG 01 `y=304`), preenchimento da barra de progresso, avatar do rodapé do menu, barra de férias aprovada (PNG 51). |
| `primaria-clara` | `#78cdd7` | Quadrado do "R", bolinha do menu, botão claro sobre fundo escuro (PNG 32), foco do campo "Saída almoço", dia selecionado no calendário. |
| `primaria-ativa` | `#25757d` | Item ativo do menu (PNG 00 `y 107..150`); "slot" preenchido do cartão escuro (PNG 32). |
| `primaria-suave` | `#1c666c` | "Slot" vazio do cartão escuro (PNG 32). **palpite:** também serve de hover do item do menu (o Figma não desenha hover). |
| `menu-texto` | `#f3fbfa` | Texto dos itens do menu e do nome no rodapé (PNG 00). |
| `menu-rotulo` | `#a6d9de` | Título de grupo do menu ("Pessoas", "Rotina", PNG 00). |
| `menu-apoio` | `#b8e3e7` | "[Nome da empresa]" e a linha de apoio do rodapé ("Perfil RH, acesso total", PNG 00). |
| `menu-linha` | `#2c7277` | Linha de 1 px sobre o rodapé do menu (PNG 00, `y=1442`). A borda direita do menu (token `menu-borda`) é `#68999b`, 1 px, em `x=232`. |
| `sucesso-fundo` / `-texto` | `#e6f4f3` / `#0d5c63` | Selo "Ativo", "Aprovado", "Respondida" (PNG 01, 33, 51). |
| `info-fundo` / `-texto` | `#d6f0f3` / `#145a61` | Selo "Férias", "Hoje" (PNG 01, 33; "Hoje" medi `#2c6d73` no texto, igual em efeito). |
| `alerta-fundo` / `-texto` | `#fcefd9` / `#8a4f06` | Selo "Atestado", "Em análise", "Pendente"; número "Atrasos", "Sem registro"; faixa de aviso (PNG 01, 32, 33, 51). `alerta-forte` `#6e3f05` é o tom mais escuro do **texto da faixa** de aviso (PNG 32); `alerta-ponto` `#d9822b` é a bolinha laranja ("Sem registro", marcador de aviso). |
| `erro-fundo` / `-texto` | `#fbe7e4` / `#a8322a` | Selo "Excluiu", "Acesso negado", "Recusado"; botão "Inativar" e "Recusar" (borda e texto `#a8322a`); número de advertências (PNG 31, 33, 03, 51). |
| `neutro-fundo` / `-texto` | `#eef2f1` / `#3d5557` | Selo "Alterou", "Folga", "Enviou e-mail" (PNG 31, 33; no PNG 51 "Não se aplica" usa o texto `#4e6466`). |
| `barra-ferias` | `#44a1a0` | Segmento "Férias" da barra de presença e seu ponto de legenda (PNG 00). |
| `barra-folga` | `#cfe9ea` | Segmento "Folga" (PNG 00). A barra usa ainda `primaria` ("Trabalhando"), `primaria-clara` ("Atestado") e `alerta-ponto` ("Sem registro"). |
| `avatar-fundo` | `#d6edec` | Círculo de iniciais das listas (PNG 01, 36 px). |
| `scrim` | `#0e3236` a 42% | Cortina atrás de janela e painel. **Medido** em dois fundos conhecidos: o menu (`#0d5c63` → `#0d4a50`) e a página (`#fffffa` → `#9aa9a8`) dão o **mesmo** alfa de 0,417 nos três canais; a cor que resolve a conta é `#0d3135`, que é `texto` a menos de 1 por canal. |

Contraste (calculado, WCAG 2.1): `texto` sobre `pagina` 13,7:1; `texto-suave` sobre `pagina` 6,3:1; `texto-fraco` sobre branco 4,6:1; `#fffffa` sobre `primaria` 7,7:1; `menu-rotulo` sobre `primaria` 5,0:1; `menu-apoio` 5,6:1; todos os pares de selo de 5,6 a 7,1:1; **abaixo de 4,5:1** só o número da bolinha do menu (`primaria` sobre `primaria-clara`, 4,2:1, texto pequeno em negrito) e as bordas de campo e de cartão (1,5:1 e 1,3:1, que o WCAG pede 3:1 para componentes). O plano **segue o Figma** nos dois casos; fica registrado como risco para quem avaliar acessibilidade.

---

## 2. Tokens de design (cole em `front-end\src\index.css`)

Tailwind 4 transforma cada `--color-*` em utilitários (`bg-primaria`, `text-texto-suave`, `border-borda`), cada `--radius-*` em `rounded-*`, cada `--text-*` em `text-*` e cada `--spacing-*` em `w-*`, `p-*` etc. O `@theme static` **emite todas as variáveis em `:root`** (sem o `static`, o Tailwind só emite as usadas), então também servem a CSS escrito à mão: `var(--color-primaria)`. Medi num projeto de teste (Tailwind 4.3.3) que o build emite `--color-primaria:#0d5c63;` e os utilitários `bg-pagina`, `w-menu`, `rounded-cartao`, `data-checked:bg-primaria`, `aria-[sort=ascending]:text-primaria`, e que o HTML do `dist` não ganha `<style>` nem `style=`.

```css
@import 'tailwindcss';
@import '@fontsource-variable/inter';

@theme static {
  --font-sans: 'Inter Variable', ui-sans-serif, system-ui, sans-serif;

  /* superfícies e linhas */
  --color-pagina: #fffffa;
  --color-superficie: #ffffff;
  --color-superficie-suave: #f2f8f7;
  --color-superficie-destaque: #e6f4f3;
  --color-borda: #d9e4e2;
  --color-borda-campo: #c9d8d6;
  --color-divisor: #e4ecea;
  --color-tracejado: #9bbdba;

  /* texto */
  --color-texto: #0e3236;
  --color-texto-suave: #4e6466;
  --color-texto-fraco: #757575;

  /* marca e menu lateral */
  --color-primaria: #0d5c63;
  --color-primaria-media: #247b7b;
  --color-primaria-clara: #78cdd7;
  --color-primaria-ativa: #25757d;
  --color-primaria-suave: #1c666c;
  --color-menu-texto: #f3fbfa;
  --color-menu-rotulo: #a6d9de;
  --color-menu-apoio: #b8e3e7;
  --color-menu-linha: #2c7277;
  --color-menu-borda: #68999b;

  /* estados: fundo e texto (selos, avisos, botões de perigo) */
  --color-sucesso-fundo: #e6f4f3;
  --color-sucesso-texto: #0d5c63;
  --color-info-fundo: #d6f0f3;
  --color-info-texto: #145a61;
  --color-alerta-fundo: #fcefd9;
  --color-alerta-texto: #8a4f06;
  --color-alerta-forte: #6e3f05;
  --color-alerta-ponto: #d9822b;
  --color-erro-fundo: #fbe7e4;
  --color-erro-texto: #a8322a;
  --color-neutro-fundo: #eef2f1;
  --color-neutro-texto: #3d5557;

  /* gráficos e avatar */
  --color-barra-ferias: #44a1a0;
  --color-barra-folga: #cfe9ea;
  --color-avatar-fundo: #d6edec;
  --color-scrim: rgb(14 50 54 / 0.42);

  /* raios */
  --radius-botao: 8px;
  --radius-campo: 10px;
  --radius-cartao: 14px;
  --radius-janela: 22px;
  --radius-destaque: 24px;

  /* tamanhos de letra (o nome não repete o dos tokens de cor, para o Tailwind não confundir `text-*`) */
  --text-t1: 36px;
  --text-ficha: 28px;
  --text-numero: 32px;
  --text-t2: 22px;
  --text-t3: 20px;
  --text-corpo: 15px;
  --text-tabela: 14px;
  --text-pequeno: 13px;
  --text-mini: 12px;
  --text-relogio: 85px;

  /* medidas de layout */
  --spacing-menu: 232px;
}

body {
  font-family: var(--font-sans);
  background: var(--color-pagina);
  color: var(--color-texto);
}
```

Pesos: use os do Tailwind (500 `font-medium`, 600 `font-semibold`, 700 `font-bold`, 800 `font-extrabold`). Espaçamentos: a escala padrão do Tailwind (1 = 4 px) cobre o que as telas usam; as medidas da seção 3 estão em px para você traduzir (ex.: 44 px = `h-11`, 48 px = `px-12`).

---

## 3. Medidas (medidas, em px, a 1440 px de largura)

**Estrutura da página (PNG 00, 01, 32, 33, 46, 51):**

| O quê | Medida |
|---|---|
| Menu lateral | **232 px** de largura (`x 0..231`) mais 1 px de borda `menu-borda` em `x=232`: conteúdo começa em `x=233`. Altura: a da janela; o menu é fixo e o conteúdo rola. |
| Margem lateral do conteúdo | **48 px** de cada lado (títulos em `x=281`; fim de tabela e de cartão em `x=1391`): conteúdo útil de **1111 px** a 1440. |
| Título da página (`t1`) | texto começa em `y≈35` (altura de caixa da letra maiúscula 24 px); subtítulo logo abaixo (`y≈83..96`); o primeiro bloco da página começa uns 20 a 24 px depois do subtítulo (`y=117` no PNG 00). Botão de ação da página fica à direita, alinhado ao título (PNG 01, 12, 33). |
| Espaço entre cartões lado a lado | 20 a 21 px (PNG 32, 33) e uns 33 px entre as duas colunas do painel (PNG 00). |
| Item do menu | caixa de **44 px** de altura e 205 px de largura (`x 14..218`), com 12 px de recuo do texto; passo de **46,5 px** entre itens (≈ 2,5 px de vão). Rótulo de grupo: `mini`, a uns 39 px acima do primeiro item do grupo. |
| Marca do menu | quadrado arredondado de 38 × 34 px em `(24, 24)`, raio ≈ 9 px, fundo `primaria-clara`, letra "R" em `primaria`, negrito; ao lado, "Portal de RH" (18 px, 600) e embaixo o nome da empresa (13 px). |
| Rodapé do menu | linha `menu-linha`; avatar redondo de **42 px** (`primaria-media`, iniciais `menu-texto`, negrito); nome (15 px, 600) e linha de apoio (12 px). |
| Bolinha do menu | 25 × 20 px, pílula, fundo `primaria-clara`, número `primaria` (13 px, 700), encostada à direita do item. |

**Componentes:**

| O quê | Medida | Fonte |
|---|---|---|
| Botão grande | **44 px** de altura (primário, contorno, perigo; `Adicionar colaborador`, `Cancelar`, `Inativar`) | PNG 01, 02, 03 |
| Botão pequeno | **40 px** (`Abrir ficha`, `Anterior`, `Próxima`, `Editar` de tabela) | PNG 01 |
| Raio de botão | 8 a 9 px (ajuste do canto: 8 no preenchido, 9 no contorno) | PNG 01 |
| Contorno de botão | 1 px `primaria-media`, fundo branco ou `pagina`, texto `primaria`, peso 600 | PNG 01 |
| Botão "claro" (sobre o cartão escuro) | 51 px de altura, fundo `primaria-clara`, texto `#0b3f44` | PNG 32 |
| Botão só ícone (fechar janela, "...") | **44 × 44 px**, contorno 1 px `borda-campo` (medido: `#c9d8d6`), fundo branco, raio ≈ 10 px | PNG 02, 03, 10 |
| Campo, filtro | **44 px** de altura, 1 px `borda-campo`, raio **10 px**, fundo branco, texto de 15 px, recuo de 13 px | PNG 01, 02 |
| Rótulo de campo | 14 px, peso 600, `texto`, 8 px acima do campo | PNG 02 |
| Caixa de seleção | quadrado de **20 × 20 px**, marcada em `primaria` (o Figma não mostra o "visto" dentro: **palpite**, desenhe o visto branco); vazia com borda cinza do navegador `#767676` (use `borda-campo` mais escura: **palpite**) | PNG 02 |
| Botão de opção (rádio) | círculo de 20 px, marcado em `primaria` com miolo branco | PNG 10 |
| Cartão de opção | raio ≈ 14 px (visto); **selecionado:** borda de **2 px** `primaria`; **não selecionado:** 1 px `borda` | PNG 10, 17, 28 |
| Selo | pílula de **26 px** de altura (largura = texto + 20 px), texto 13 px, peso 600 | PNG 01, 31, 33 |
| Selo de horário ("07:56 - 12:01") | 27 px, **não** pílula (raio ≈ 8 px), fundo `superficie-suave`, texto `texto` 600 | PNG 33 |
| Avatar de lista | círculo de **36 px**, fundo `avatar-fundo`, iniciais `primaria` 13 px 700 | PNG 01 |
| Avatar da ficha | círculo de **76 px**, fundo `primaria`, iniciais `pagina` | PNG 03 |
| Cartão | borda 1 px `borda`, fundo branco, raio **14 px**; recuo interno de 21 px (28 px no cartão escuro) | PNG 00, 03, 32 |
| Cartão "destaque" | sem borda, fundo `superficie-destaque`, raio 14 a 16 px | PNG 00, 32 |
| Cartão escuro ("Seu ponto hoje") | fundo `primaria`, raio **24 px**; "slots" de 154 × 80 px (`primaria-ativa` preenchido, `primaria-suave` vazio), foco com 2 px `primaria-clara` | PNG 32, 33 |
| Tabela | cartão com borda `borda` e raio 14; cabeçalho de **45 px** em `superficie-suave` (texto 14 px, 500, `texto-suave`); linhas de **73,5 px** (duas linhas de texto: nome 600 e e-mail 13 px `texto-suave`); recuo da célula 18 px; divisor 1 px `divisor`; rodapé de paginação dentro do cartão | PNG 01 |
| Paginação | texto "Mostrando 1 a 8 de 101" (14 px `texto-suave`) à esquerda; botões pequenos `Anterior` e `Próxima` à direita, sem números de página | PNG 01, 29, 31 |
| Abas em pílula | contêiner `superficie-destaque`, raio ≈ 16, a aba ativa é uma pílula branca de 44 px; contagem no rótulo ("Ativos 101") | PNG 01, 27 |
| Abas sublinhadas | texto 15 px; ativa `primaria` 600 com **3 px** `primaria` sob ela, linha de base 1 px `divisor`; inativas `texto-suave` | PNG 03 |
| Barra de progresso | trilha de **8 px**, raio cheio, `borda`; preenchimento `primaria-media` | PNG 00 |
| Barra de presença | **15 px**, segmentos separados por 4 px de fundo, raio cheio, cores `primaria`, `barra-ferias`, `primaria-clara`, `barra-folga`, `alerta-ponto`; legenda com bolinhas de 13 px | PNG 00 |
| Etapas (assistente) | círculos de ~22 px ligados por linha de 2 px `borda-campo`; ativa: anel `primaria` com miolo; concluída: círculo `primaria` com visto; futura: anel `tracejado`; rótulo 14 px (ativa `primaria` 600) | PNG 17, 18 |
| Janela (modal) | fundo `pagina`; **cabeçalho** de 76 px em branco com título (22 px, 700) e botão X de 44 px, linha `divisor`; **corpo** com recuo de 28 px; **rodapé** em branco com linha `divisor` e botões à direita (44 px). Raio **22 px**. Larguras: **640** (PNG 10 e 30, medido), **1040** (PNG 13, visto), **1180** (PNG 17, medido; suponho igual nos PNG 18 a 20 e 23 a 26, que não abri); altura até 905 px (PNG 06, de `telas\fatia-8.md`: não abri) | PNG 10, 13, 17, 30 |
| Painel lateral | encosta na direita, altura total; cabeçalho de 80 px (título 22 px 700 + X), linha `divisor`; rodapé de 78 px com botões à direita. Larguras: **580** (PNG 28, visto), **600** (PNG 02, medido) e **864** (PNG 03, a ficha, medido) | PNG 02, 03, 28 |
| Faixa de aviso | fundo `alerta-fundo`, raio 14 a 16, texto 15 px `alerta-forte`, ícone `Clock` à esquerda e link sublinhado à direita | PNG 32 |
| Área de soltar arquivo | borda tracejada 1 px `tracejado`, fundo `#fafcfb`, raio 14, ícone, "Arraste o arquivo aqui", texto de apoio, botão `Escolher arquivo` | PNG 10 |
| Ícones | glifos de 12 a 14 px nos botões e campos (`size={16}` no lucide), 17 px na faixa de aviso (`size={20}`), 20 px na área de soltar (`size={24}`) e 20 a 22 px dentro do quadrado de 48 px (`avatar-fundo`/`superficie-destaque`, raio 12) dos benefícios (`size={24}`); traço fino (`strokeWidth={1.75}`) | PNG 01, 10, 32, 44 |

---

## 4. Tipografia

**Fonte: Inter, decidida pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono": manter a Inter; a Hanken Grotesk só se ele pedir depois). O que segue é o **registro da medição** que levou à pergunta; ela não reabre a decisão. A fonte dos PNG não se lê de pixels. Fiz o que dá para medir: de 34 famílias variáveis de licença aberta do `@fontsource-variable` (instaladas numa pasta temporária, não no repositório), ajustei o tamanho de cada família para que 10 textos de peso 400 (que não têm dúvida de peso) tivessem a **largura medida no PNG**, e ordenei pela **proximidade de tamanhos redondos** (um desenhista usa 14, 15, 16 px, não 14,37). Resultado (erro médio de arredondamento em px, menor é melhor; 34 famílias): **Hanken Grotesk 0,122 (1º)**, Lexend 0,167, Outfit 0,173, Golos Text 0,176, Nunito Sans 0,193, Geist e Host Grotesk 0,206, …, Figtree 0,226 (10º), Instrument Sans 0,238 (13º), **Inter 0,279 (21º)**. Repeti com 35 textos de dois pesos livres: Hanken Grotesk 0,127, Figtree 0,174, Inter 0,178, Urbanist 0,197, Instrument Sans 0,231 (Hanken continua em 1º; Inter fica no meio). Com Hanken Grotesk os textos caem em 36, 22, 15, 14, 13 e 12 px quase exatos (ex.: "Colaboradores" 35,9; "Presença agora" 22,0; "bruno.lima@..." 13,0; "Analista pleno" 14,0). A olho, lado a lado (recortes ampliados dos PNG 00 e 01 contra as famílias renderizadas no Edge), Hanken Grotesk também parece mais próxima que Inter (o "86" pesado e redondo e o "g" de uma barriga só). **Isto é palpite, não prova**: a diferença entre as famílias é pequena e eu não vi a fonte do Figma.

- **Decisão:** a pilha (`02-pilha.md`) fixa `@fontsource-variable/inter` 5.3.0 e o dono **decidiu mantê-la** (2026-11-02); `fatia-1a-fundacao.md` usa **Inter**, sem dúvida a registrar. A fonte entra num só lugar (`--font-sans`). A pessoa ainda confere a letra ao lado do PNG na conferência visual (não bloqueia).
- **Alternativa medida (só se o dono pedir depois):** `@fontsource-variable/hanken-grotesk` **5.3.0** (licença OFL-1.1, a mesma de Inter; a família no CSS é `'Hanken Grotesk Variable'`). Medi num projeto de teste, sob a política de segurança do site (`default-src 'self'`), que o build copia os `.woff2` para `dist\assets`, que `document.fonts.check('700 36px "Hanken Grotesk Variable"')` dá verdadeiro e que **não há violação**. Para trocar (só depois de o dono pedir a troca): `npm --prefix front-end install @fontsource-variable/hanken-grotesk@5.3.0 --save-exact; npm --prefix front-end uninstall @fontsource-variable/inter`; no `index.css`, `@import '@fontsource-variable/hanken-grotesk';` e `--font-sans: 'Hanken Grotesk Variable', ui-sans-serif, system-ui, sans-serif;`; rode `test`, `build` e `csp`.
- **Com Inter** os mesmos textos saem uns 5 a 7% mais largos que no PNG (Inter é mais larga). Os tamanhos da tabela abaixo valem para as duas; se uma coluna quebrar de linha onde o PNG não quebra, diminua o recuo, não a letra.

**Escala (tamanhos medidos pelo ajuste com Hanken Grotesk; arredondei ao inteiro mais próximo):**

| Token (`text-*`) | px | Peso | Onde |
|---|---|---|---|
| `t1` | 36 | 700 | Título da página ("Colaboradores", "Bom dia, Ana"); tamanho de letra maiúscula medido: 24 px |
| `ficha` | 28 | 700 | Nome na ficha ("Bruno Lima", PNG 03) |
| `numero` | 32 | 700 | Valor grande de cartão ("6 de novembro", "+6h20"; 31,4 a 31,6 no ajuste) |
| `t2` | 22 | 700 | Título de seção, de janela e de painel ("Precisa da sua atenção", "Adicionar documento de Bruno") |
| `t3` | 20 | 700 | Título de cartão e de bloco ("Seus benefícios", "Dados pessoais"; 19,5 a 20,8) |
| `corpo` | 15 | 400 | Subtítulo da página, campos, item de menu (500), botões (600) |
| `tabela` | 14 | 400 a 600 | Células, cabeçalho de tabela (500), rótulos de campo (600), botão pequeno (600) |
| `pequeno` | 13 | 400 a 600 | Selo (600), e-mail sob o nome, "Atualizado até ontem" |
| `mini` | 12 | 400 | Título de grupo do menu, linha de apoio do rodapé |
| `relogio` | 85 | 800 | Hora grande do cartão escuro ("09:42", PNG 32; 211 px de largura) |

Altura de linha e espaço entre letras: **não medi** (o PNG não separa as duas coisas); use o padrão do Tailwind e deixe as alturas de campo, botão e linha de tabela (seção 3) mandarem. Números de horas e dinheiro: o Figma alinha em coluna; use `tabular-nums` (`font-variant-numeric`, vale em Inter e em Hanken).

---

## 5. Ícones (palpite) e o mapa para `lucide-react` 1.65.0

Os ícones do Figma são de traço fino e cantos arredondados, no estilo do Lucide. Abri os recortes ampliados dos ícones e comparei com o desenho de cada candidato renderizado (os nomes abaixo **existem** em `lucide-react` 1.65.0: confirmei importando cada um). **O menu lateral não tem ícones** (só texto e a bolinha): não ponha ícone nos itens do menu.

| Onde aparece (PNG) | O que o Figma desenha | `lucide-react` | Ajuste |
|---|---|---|---|
| Busca (00, 01, 16, 29, 31) | lupa | `Search` | `size={16}` |
| "+ Adicionar colaborador", "+ Novo cargo" (01, 29) | sinal de mais | `Plus` | `size={16}` |
| Fechar janela e painel (02, 03, 10, 13, 17, 28) | X | `X` | `size={16}` dentro do botão de 44 px |
| "Abrir ficha" (01, 51) | quadrado com barra no lado esquerdo | `PanelLeft` | `size={16}` |
| Faixa de aviso (32) | relógio | `Clock` | `size={20}` |
| Área de soltar arquivo (10) | seta para cima sobre uma linha | `ArrowUpFromLine` | `size={24}` |
| Menu "..." (03, 16, 22) | três pontos na horizontal | `Ellipsis` | `size={16}` |
| Etapa concluída (18, 19) | visto em círculo cheio | `Check` (o círculo é CSS) | `size={14}` |
| Vale-refeição (44) | garfo e faca | `Utensils` | `size={24}` |
| Plano de saúde (44) | coração com sinal de mais **dentro** | `HeartPlus` (**palpite**: o do Lucide tem o mais no canto) | `size={24}` |
| Vale-transporte (44) | ônibus de frente | `BusFront` (alternativa: `Bus`, de lado) | `size={24}` |
| Seguro de vida (44) | guarda-chuva | `Umbrella` | `size={24}` |
| Plano odontológico (44) | dente | **não existe** dente no Lucide 1.65.0: `Smile` (**palpite**, de reserva) | `size={24}` |
| Auxílio-creche (44) | pessoa diante de uma mesa | **não existe**: `Baby` (**palpite**, de reserva) | `size={24}` |
| Data (02, 10, 28, 12) | calendário do navegador | **nenhum**: é o `<input type="date">` nativo (ver seção 7) | — |

**Ícones que o Figma não desenha mas as telas precisam** (todos **palpite**, no mesmo traço): ordenação de coluna `ArrowUpDown`, `ArrowUp`, `ArrowDown`; seta de seleção `ChevronDown`; `Check` dentro da caixa de seleção e do item escolhido; erro `CircleAlert`; aviso `TriangleAlert`; informação `Info`; carregando `LoaderCircle` (com `animate-spin`); baixar `Download`; ver `Eye`; editar `Pencil`; apagar `Trash2`; anexo `Paperclip`; sair `LogOut`; sem resultado `SearchX`; arquivo `FileText`. Importe sempre **um por um** (`import { Search } from 'lucide-react'`): o Vite remove os não usados.

---

## 6. O "R" do menu

Quadrado arredondado (38 × 34 px, raio ≈ 9) com a letra "R" em negrito, **medido**: fundo `#78cdd7` e letra `#0d5c63` (PNG 00, 468 px e 35 px na região da marca). Construa como componente (`LogoR`, um `<span>` com classes; **sem arquivo de imagem**, porque a política barra imagem `data:` e o Vite não pode embutir SVG pequeno). Sobre fundo claro (tela de entrar, que não tem print) use a versão invertida: fundo `primaria`, letra `primaria-clara`. O `favicon.svg` da fatia 0 (azul `#1d4ed8`, letras "RH") **não combina** com a paleta: a fundação o troca por um quadrado `#0d5c63` com a letra "R" `#78cdd7` (o arquivo é estático e vai a `public\`; a política permite imagem do próprio domínio).

---

## 7. Inventário de componentes e a peça do Base UI que implementa cada um

`@base-ui/react` **1.9.0** (conferi as peças importando cada caminho; o pacote **não** tem tabela, paginação, selo, cartão, avatar com iniciais nem área de texto própria). Os que têm peça usam `CSPProvider disableStyleElements` na raiz (`02-pilha.md`, 5.2). Medi sob a política do site (servidor de teste + Edge, 0 violações), com **tudo aberto ao mesmo tempo** e depois abrindo o Select e o Combobox: `Dialog`, `AlertDialog`, `Drawer`, `Select`, `Combobox`, `Tabs`, `Checkbox`, `Radio`/`RadioGroup`, `Switch`, `Progress`, `Meter`, `Field`, `Input`, `Menu`, `Popover`, `Tooltip` e `Toast` (aqui só o contêiner vazio; o aviso aberto já estava na medida da pilha). (`02-pilha.md` já tinha medido 16 deles; **`AlertDialog`, `Drawer`, `Field`, `Input` e `Meter` são medidas novas**.) A única violação do teste foi de propósito: uma imagem externa dentro do markdown (seção 8).

| Componente do plano (nome em `src\shared\ui\`) | Onde aparece | Peça do Base UI (`@base-ui/react/...`) |
|---|---|---|
| `Botao` primário, secundário (contorno), texto (link), perigo, claro | todas | **nenhuma** (`<button>`); `button` só se precisar de `focusableWhenDisabled` |
| `Campo` (rótulo, dica, erro) | 02, 10, 17, 28, 30 | `field`: `Field.Root`, `Field.Label`, `Field.Description`, `Field.Error`; grupos de opções: `fieldset` |
| `Entrada` (texto, e-mail, senha, data, número) | 01, 02, 10 | `input` (dentro do `Field.Root` liga sozinho ao rótulo e ao erro: **provado** com Testing Library) |
| `AreaDeTexto` | 10, 28, 30 | `Field.Control` com `render={<textarea />}` (não há peça própria) |
| `Selecao` | 01, 02, 12, 31 | `select`: `Select.Root`, `Trigger`, `Value`, `Icon`, `Portal`, `Positioner`, `Popup`, `List`, `Item`, `ItemText`, `ItemIndicator` |
| `ComboDeBusca` (escolher uma pessoa digitando) | 12 ("Colaborador"), 15 | `combobox`: `Combobox.Root`, `Input`, `Portal`, `Positioner`, `Popup`, `Empty`, `List`, `Item` |
| `CampoDeBusca` (lupa, filtra a lista) | 00, 01, 16, 29, 31 | **nenhuma** (`Entrada` com ícone) |
| `CaixaDeSelecao` | 02, 12, 28 | `checkbox`: `Checkbox.Root`, `Checkbox.Indicator` (grupo: `checkbox-group`) |
| `OpcoesEmCartao` (rádio em cartão) | 10, 17, 28 | `radio-group` + `radio`: `RadioGroup`, `Radio.Root`, `Radio.Indicator` |
| `Interruptor` | 29 ("Gestor ausente") | `switch`: `Switch.Root`, `Switch.Thumb` |
| `Abas` (pílula e sublinhada) | 01, 03, 27, 39, 41 | `tabs`: `Tabs.Root`, `List`, `Tab`, `Indicator`, `Panel` |
| `Tabela` (ordenação, vazio, carregando, erro) | 01, 12, 31, 33, 51 | **nenhuma** (`<table>`; cabeçalho ordenável é um `<button>` com `aria-sort`) |
| `Paginacao` | 01, 29, 31 | **nenhuma** (dois `Botao`) |
| `Selo` | 01, 03, 31, 33, 51 | **nenhuma** (`<span>`) |
| `Cartao` (branco, suave, destaque, escuro, tracejado) | 00, 32, 33, 44, 51 | **nenhuma** (`<section>`/`<div>`) |
| `Avatar` (iniciais) | 00, 01, 03, 51 | **nenhuma** (`<span aria-hidden>`; `avatar` do Base UI é para foto) |
| `Progresso` | 00 ("Treinamentos em andamento"), 22 | `progress`: `Progress.Root`, `Track`, `Indicator`, `Label`, `Value` (para valor estático, `meter`) |
| `Aviso` (faixa), `AvisoDeErro` | 32, 46; todos os estados de erro | **nenhuma** (`role="status"` / `role="alert"`) |
| `Janela` (centro e lateral) | 02, 03, 10, 13, 17 a 20, 23 a 26, 28, 30 | `dialog`: `Dialog.Root`, `Portal`, `Backdrop`, `Popup`, `Title`, `Description`, `Close` (o painel lateral é a mesma peça posicionada à direita; `drawer` também deu 0 violações, mas é para arrastar com o dedo: **não use**) |
| `JanelaDeConfirmacao` (digite "desativar"; "N pessoas ainda não concluíram") | 03 (Inativar), 22, 16, 29 | `alert-dialog`: `AlertDialog.Root`, `Portal`, `Backdrop`, `Popup`, `Title`, `Description`, `Close` |
| `MenuDeAcoes` (o "...") | 03, 16, 22 | `menu`: `Menu.Root`, `Trigger`, `Portal`, `Positioner`, `Popup`, `Item` |
| `Notificacoes` (mensagem de sucesso que some) | "Pedido enviado.", "Colaborador cadastrado." | `toast`: `Toast.Provider`, `Portal`, `Viewport`, `Root`, `Content`, `Title`, `Close`, `useToastManager` |
| `Etapas` (assistente) | 17 a 20, 23 a 26 | **nenhuma** (`<ol>` com `aria-current="step"`) |
| `SeletorDeArquivo` | 10 e telas sem print de upload | **nenhuma** (`<input type="file">` escondido e uma área de soltar) |
| `EstadoVazio`, `Carregando`, `Esqueleto` | todas as listas | **nenhuma** (`role="status"`) |
| `Bolinha` (número do menu) | menu | **nenhuma** (`<span>`) |
| `Markdown` | avisos detalhados, perguntas, treinamentos | **nenhuma** (`react-markdown`, seção 8) |

---

## 8. Regra "sem `<style>` nem atributo `style` no HTML" (política de segurança) e markdown

A política do site (`02-pilha.md`, seção 1) barra `<style>` injetado e `style="..."` escrito no HTML. **Regras do código:**

1. **Nunca escreva** `<style>`, `style="..."` (texto), `dangerouslySetInnerHTML`, `innerHTML` nem `insertAdjacentHTML`. Um teste da fundação (`semHtmlCru.test.ts`) varre `src\` e falha se encontrar um deles.
2. **Estilo = classes do Tailwind** (CSS estático, gerado no build). Valor **dinâmico** (a largura de 45% de uma barra) usa a prop `style={{ width: '45%' }}` do React: ela muda `element.style` por script, que a política **permite** (medi agora: largura calculada de 554 px = 45% do pai, **0 violações**; o atributo `style` que aparece no DOM nasce do script, não do HTML). Use só para número que muda com os dados.
3. **Nunca** `import 'x.svg'` pequeno nem `url(data:...)`: o Vite está com `assetsInlineLimit: 0` (`02-pilha.md`, 5.4). Ícone = componente do `lucide-react`.
4. Baixar arquivo pelo navegador com `URL.createObjectURL(blob)` e um `<a download>` clicado por script **passa**: não é imagem nem requisição. Quem mede é a **sonda de download** do `csp` (`03-contrato-e-mocks.md`, seção 8; Edge 156 e Chrome 149, 2026-11-02: baixa, 0 violações). Já uma **imagem** `blob:` (`<img src="blob:...">`) é barrada (`02-pilha.md`, seção 1).

**Markdown sanitizado: `react-markdown` 10.1.0** (licença MIT; depende de `unified`/`remark`; escreve elementos do React, nunca HTML cru; **está em `02-pilha.md`** e é instalada com o resto na Task 1 da fatia 0). Medi sob a política com este texto de ataque: `<script>`, `<img onerror>`, `<style>`, `<div style>`, `[x](javascript:alert(1))`, `![](https://exemplo.com/x.png)`. Resultado: o HTML cru aparece como **texto** (nada executa; `window.__xss` continuou nulo), o link `javascript:` virou `href=""`, `<style>` e `style=` não chegaram ao DOM e a **imagem externa gerou 1 violação `img-src`** (a política barra imagem de outro domínio) e uma requisição a terceiro. Por isso a regra é: **só estes elementos** (`allowedElements`: `p`, `br`, `strong`, `em`, `del`, `ul`, `ol`, `li`, `a`, `blockquote`, `code`, `pre`, `hr`, `h1` a `h4`), com `unwrapDisallowed`, **sem `img`**; links com `target="_blank"` e `rel="noopener noreferrer"`. Não use `rehype-raw` nem `remark-gfm` (não há tabela no markdown dos avisos: YAGNI).

---

## 9. Largura: projeto para 1366 px com o desenho de 1440 (provisório)

- **Provisório** (design, seção 9): a demonstração pode ser num projetor de 1366 × 768; ninguém verificou. **Projete para 1366** e confira na demo.
- **Conta (medida nos PNG):** menu 233 px (232 + 1) e margens de 48 px: a 1440 o conteúdo útil é de 1111 px; **a 1366 é de 1037 px** (1366 − 233 − 96), 74 px a menos. O que encolhe é o **conteúdo**, nunca o menu.
- **Regra de rolagem:** (1) a **página rola na vertical** (os PNG têm até 2015 px de altura; isso é normal); (2) **nunca** rolagem horizontal da página a partir de 1366 px; (3) uma tabela mais larga que o conteúdo rola **dentro** do cartão dela (`overflow-x-auto` no contêiner da tabela), nunca a página; (4) o menu lateral é **fixo** (`sticky top-0 h-screen`) e, se não couber, rola por dentro; (5) janelas e painéis têm `max-height` da janela do navegador menos 48 px, o **corpo rola por dentro** e o cabeçalho e o rodapé (com os botões) ficam sempre à vista (a janela do PNG 06 tem 905 px e não cabe em 768); (6) larguras de janela e painel com teto `calc(100vw - 48px)`.
- **Abaixo de 1280 px** não há desenho (nenhum PNG): **o `PortalLayout`** (as telas logadas) ganha `min-w-[1280px]` (**suponho**: `fatia-1a-fundacao.md`, Task 12) para a tela não desmontar; celular e tablet **ficam fora**. **Não** ponha `min-width` no `body`: ele quebraria as páginas públicas. Elas (entrar, esqueci minha senha, definir senha, formulário do candidato) ficam **fora** do `PortalLayout`, usam uma coluna de até 400 px e funcionam de 360 px em diante (`telas\fatia-0.md`).
- **Lugares a olhar primeiro a 1366** (de `telas\fatia-8.md`, seção 3): PNG 51 (tabela de 7 colunas e quadro de 13 semanas), PNG 00 (duas colunas), PNG 06 (janela alta), PNG 44 (quatro cartões por linha: 245 px cada a 1366).

---

## 10. Estados que o Figma não desenha (todos **palpite**; a pessoa confere)

- **Foco do teclado:** contorno de 2 px `primaria-media` com 2 px de distância (`focus-visible:outline-2 outline-offset-2 outline-primaria-media`); sobre fundo escuro, `primaria-clara`. (O Figma só mostra o foco do "slot" do cartão escuro.)
- **Hover:** botão primário passa a `primaria-ativa`; contorno ganha fundo `superficie-destaque`; item do menu ganha `primaria-suave`; linha de tabela com ação ganha `superficie-suave`.
- **Desabilitado:** `opacity-50` e `cursor-not-allowed`; botão que grava mostra o mesmo texto (os testes procuram o botão pelo nome).
- **Carregando:** `Esqueleto` em `superficie-destaque` com `animate-pulse` (a animação é CSS estático do Tailwind, não gera `<style>`); texto "Carregando…" com `role="status"`.
- **Erro de campo:** borda `erro-texto`, mensagem de 13 px `erro-texto` abaixo do campo; **erro geral** numa faixa `erro-fundo` com `role="alert"` e o botão `Tentar de novo`.
- **Selo de situação que o front não conhece** (valor novo do contrato): tom `neutro` com o texto como veio.
