import { Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { clienteHttp } from '../../shared/api/api'
import { ErroDeApi } from '../../shared/api/erroDeApi'
import { abrirLinkTemporario, baixarExportacao } from '../../shared/arquivos/baixar'
import { mascararCpf } from '../../shared/formato/documentos'
import { Abas } from '../../shared/ui/Abas'
import { AreaDeTexto } from '../../shared/ui/AreaDeTexto'
import { Avatar } from '../../shared/ui/Avatar'
import { Aviso } from '../../shared/ui/Aviso'
import { AvisoDeErro } from '../../shared/ui/AvisoDeErro'
import { Bolinha } from '../../shared/ui/Bolinha'
import { Botao } from '../../shared/ui/Botao'
import { CabecalhoDePagina } from '../../shared/ui/CabecalhoDePagina'
import { CaixaDeSelecao } from '../../shared/ui/CaixaDeSelecao'
import { Campo } from '../../shared/ui/Campo'
import { CampoDeBusca } from '../../shared/ui/CampoDeBusca'
import { Carregando } from '../../shared/ui/Carregando'
import { Cartao } from '../../shared/ui/Cartao'
import { ComboDeBusca } from '../../shared/ui/ComboDeBusca'
import { Entrada } from '../../shared/ui/Entrada'
import { Esqueleto } from '../../shared/ui/Esqueleto'
import { EstadoVazio } from '../../shared/ui/EstadoVazio'
import { Etapas } from '../../shared/ui/Etapas'
import { Interruptor } from '../../shared/ui/Interruptor'
import { Janela } from '../../shared/ui/Janela'
import { JanelaDeConfirmacao } from '../../shared/ui/JanelaDeConfirmacao'
import { LogoR } from '../../shared/ui/LogoR'
import { Markdown } from '../../shared/ui/Markdown'
import { MenuDeAcoes } from '../../shared/ui/MenuDeAcoes'
import { OpcoesEmCartao } from '../../shared/ui/OpcoesEmCartao'
import { Paginacao } from '../../shared/ui/Paginacao'
import { Progresso } from '../../shared/ui/Progresso'
import { Secao } from '../../shared/ui/Secao'
import { Selecao } from '../../shared/ui/Selecao'
import { SeletorDeArquivo } from '../../shared/ui/SeletorDeArquivo'
import { Selo } from '../../shared/ui/Selo'
import { Tabela, type ColunaDaTabela, type OrdenacaoDaTabela } from '../../shared/ui/Tabela'
import { useNotificacao } from '../../shared/ui/useNotificacao'

type Linha = { id: number; nome: string; setor: string }

const LINHAS: Linha[] = [
  { id: 12, nome: 'Bruno Lima', setor: 'Financeiro' },
  { id: 21, nome: 'Diego Farias', setor: 'Comercial' },
]

const COLUNAS: ColunaDaTabela<Linha>[] = [
  { chave: 'nome', titulo: 'Nome', celula: (l) => l.nome, ordenavel: true },
  { chave: 'setor', titulo: 'Setor', celula: (l) => l.setor },
]

// O texto de ataque de 04-visual.md, seção 8. É DADO (o Markdown o mostra como texto); as partes
// "<sty" + "le>" e "sty" + "le=" vão separadas para o detector semHtmlCru continuar valendo no código.
const ATAQUE = [
  '<script>window.__xss = 1</script>',
  '<img src=x onerror="window.__xss = 2">',
  '<' + 'style>body{color:red}</' + 'style>',
  '<div sty' + 'le="color:red">x</div>',
  '[link](javascript:alert(1))',
  '![imagem](https://exemplo.com/x.png)',
  '**texto em negrito**',
  // Um parágrafo por parte: numa linha só, o "<script>" do começo vira um bloco de HTML inteiro e a
  // imagem nunca seria lida como imagem (medido: o controle de img-src não violava).
].join('\n\n')

export default function Vitrine() {
  const notificacao = useNotificacao()
  const [janela, setJanela] = useState(false)
  const [painel, setPainel] = useState(false)
  const [confirmacao, setConfirmacao] = useState(false)
  const [cargo, setCargo] = useState<string | null>(null)
  const [setor, setSetor] = useState<string | null>(null)
  const [marcada, setMarcada] = useState(false)
  const [ligado, setLigado] = useState(false)
  const [opcao, setOpcao] = useState<string | null>('contrato')
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [busca, setBusca] = useState('')
  const [aba, setAba] = useState('ativos')
  const [ordenacao, setOrdenacao] = useState<OrdenacaoDaTabela>({ campo: 'nome', direcao: 'ASC' })

  return (
    <div className="flex flex-col gap-10">
      <CabecalhoDePagina titulo="Vitrine de componentes" subtitulo="Só no build de prova." />

      <Secao titulo="Botões">
        <div className="flex flex-wrap items-center gap-3">
          <Botao icone={Plus}>Primário</Botao>
          <Botao variante="secundario">Secundário</Botao>
          <Botao variante="texto">Texto</Botao>
          <Botao variante="perigo">Perigo</Botao>
          <Botao variante="claro">Claro</Botao>
          <Botao tamanho="pequeno">Pequeno</Botao>
          <Botao carregando>Salvando</Botao>
        </div>
      </Secao>

      <Secao titulo="Selos, cartões e marcas">
        <div className="flex flex-wrap gap-2">
          <Selo>Neutro</Selo>
          <Selo tom="sucesso">Ativo</Selo>
          <Selo tom="info">Férias</Selo>
          <Selo tom="alerta">Pendente</Selo>
          <Selo tom="erro">Recusado</Selo>
          <Selo tom="contorno">Contorno</Selo>
        </div>
        <div className="grid grid-cols-5 gap-4">
          {(['branco', 'suave', 'destaque', 'escuro', 'tracejado'] as const).map((tom) => (
            <Cartao key={tom} tom={tom}>
              Cartão {tom}
            </Cartao>
          ))}
        </div>
        <div className="flex items-center gap-4">
          <Avatar nome="Ana Souza" />
          <Avatar nome="Bruno Lima" tamanho="menu" />
          <Avatar nome="Carlos Ribeiro" tamanho="ficha" />
          <Bolinha valor={4} />
          <LogoR />
          <LogoR tom="escuro" />
        </div>
      </Secao>

      <Secao titulo="Avisos e estados">
        <Aviso tom="info">Informação.</Aviso>
        <Aviso tom="alerta">Atenção.</Aviso>
        <Aviso tom="erro">Erro.</Aviso>
        <Aviso tom="sucesso">Tudo certo.</Aviso>
        <AvisoDeErro erro={new ErroDeApi(500, 'Erro.')} aoTentarDeNovo={() => {}} />
        <EstadoVazio titulo="Nenhum registro." descricao="Nada por aqui." />
        <Carregando />
        <Esqueleto className="h-6 w-64" />
      </Secao>

      <Secao titulo="Campos">
        <div className="grid grid-cols-2 gap-6">
          <Campo rotulo="Nome (vitrine)" erro="Campo obrigatório.">
            <Entrada />
          </Campo>
          <Campo rotulo="CPF (vitrine)">
            <Entrada mascara={mascararCpf} />
          </Campo>
          <Campo rotulo="Busca com ícone (vitrine)">
            <Entrada icone={Search} />
          </Campo>
          <Campo rotulo="Descrição (vitrine)">
            <AreaDeTexto limite={280} />
          </Campo>
          <Campo rotulo="Cargo (vitrine)">
            <Selecao
              rotulo="Cargo (vitrine)"
              opcoes={[
                { valor: 'analista', rotulo: 'Analista' },
                { valor: 'gerente', rotulo: 'Gerente' },
              ]}
              valor={cargo}
              aoMudar={setCargo}
            />
          </Campo>
          <Campo rotulo="Setor (vitrine)">
            <ComboDeBusca
              rotulo="Setor (vitrine)"
              opcoes={[
                { valor: '1', rotulo: 'Comercial' },
                { valor: '2', rotulo: 'Financeiro' },
                { valor: '6', rotulo: 'Logística' },
              ]}
              valor={setor}
              aoMudar={setSetor}
            />
          </Campo>
          <CaixaDeSelecao marcada={marcada} aoMudar={setMarcada} rotulo="Vale-refeição" />
          <Interruptor ligado={ligado} aoMudar={setLigado} rotulo="Gestor ausente" />
        </div>
        <OpcoesEmCartao
          rotulo="Tipo (vitrine)"
          opcoes={[
            { valor: 'contrato', titulo: 'Contrato', descricao: 'Documento assinado.' },
            { valor: 'atestado', titulo: 'Atestado' },
          ]}
          valor={opcao}
          aoMudar={setOpcao}
        />
        <SeletorDeArquivo
          arquivo={arquivo}
          aoMudar={setArquivo}
          tiposAceitos={['application/pdf']}
          tamanhoMaximoMb={10}
          textoDeAjuda="PDF até 10 MB"
        />
        <CampoDeBusca valor={busca} aoMudar={setBusca} rotulo="Buscar (vitrine)" />
      </Secao>

      <Secao titulo="Abas, tabela e navegação">
        <Abas
          rotulo="Situação (vitrine)"
          itens={[
            { valor: 'ativos', rotulo: 'Ativos', contagem: 101 },
            { valor: 'inativos', rotulo: 'Inativos', contagem: 7 },
          ]}
          valor={aba}
          aoMudar={setAba}
        />
        <Abas
          rotulo="Ficha (vitrine)"
          variante="sublinhada"
          itens={[
            { valor: 'ativos', rotulo: 'Dados' },
            { valor: 'inativos', rotulo: 'Ponto' },
          ]}
          valor={aba}
          aoMudar={setAba}
        />
        <Tabela
          rotulo="Colaboradores (vitrine)"
          colunas={COLUNAS}
          linhas={LINHAS}
          chaveDaLinha={(l) => l.id}
          ordenacao={ordenacao}
          aoOrdenar={(campo) =>
            setOrdenacao((atual) => ({
              campo,
              direcao: atual?.campo === campo && atual.direcao === 'ASC' ? 'DESC' : 'ASC',
            }))
          }
          rodape={
            <Paginacao
              dados={{ pagina: 1, tamanhoPagina: 8, total: 101, totalPaginas: 13 }}
              aoMudar={() => {}}
            />
          }
        />
        <Tabela
          rotulo="Carregando (vitrine)"
          colunas={COLUNAS}
          linhas={undefined}
          chaveDaLinha={(l) => l.id}
          carregando
        />
        <Tabela
          rotulo="Vazia (vitrine)"
          colunas={COLUNAS}
          linhas={[]}
          chaveDaLinha={(l) => l.id}
          vazio={<EstadoVazio titulo="Nenhum colaborador." />}
        />
        <Tabela
          rotulo="Com erro (vitrine)"
          colunas={COLUNAS}
          linhas={undefined}
          chaveDaLinha={(l) => l.id}
          erro={new ErroDeApi(0, 'x')}
          aoTentarDeNovo={() => {}}
        />
        <Progresso valor={18} maximo={24} rotulo="Progresso (vitrine)" />
        <Etapas
          etapas={[
            { chave: 'detalhes', rotulo: 'Detalhes' },
            { chave: 'perguntas', rotulo: 'Perguntas' },
            { chave: 'publicacao', rotulo: 'Publicação' },
          ]}
          atual="perguntas"
        />
        <MenuDeAcoes
          rotulo="Mais ações (vitrine)"
          acoes={[
            { chave: 'editar', rotulo: 'Editar', aoEscolher: () => {} },
            { chave: 'inativar', rotulo: 'Inativar', aoEscolher: () => {}, perigo: true },
          ]}
        />
      </Secao>

      <Secao titulo="Janelas, notificações e arquivos">
        <div className="flex flex-wrap gap-3">
          <Botao onClick={() => setJanela(true)}>Abrir janela</Botao>
          <Botao onClick={() => setPainel(true)}>Abrir painel</Botao>
          <Botao onClick={() => setConfirmacao(true)}>Abrir confirmação</Botao>
          <Botao onClick={() => notificacao.sucesso('Alterações salvas.')}>
            Mostrar notificação
          </Botao>
          <Botao
            variante="secundario"
            onClick={() =>
              void baixarExportacao(
                clienteHttp.GET('/api/rh/auditoria/exportacao', { parseAs: 'blob' }),
                'auditoria.csv',
              ).catch(() => notificacao.erro('Não foi possível baixar.'))
            }
          >
            Baixar CSV de teste
          </Botao>
          <Botao
            variante="secundario"
            onClick={() =>
              void clienteHttp
                .GET('/api/rh/funcionarios/{funcionarioId}/documentos/{documentoId}/download', {
                  params: { path: { funcionarioId: 12, documentoId: 61 } },
                })
                .then(({ data }) => {
                  if (data) abrirLinkTemporario(data)
                })
                .catch(() => notificacao.erro('Não foi possível abrir o arquivo.'))
            }
          >
            Abrir link de teste
          </Botao>
        </div>
        <Janela
          aberta={janela}
          aoFechar={() => setJanela(false)}
          titulo="Janela (vitrine)"
          rodape={<Botao onClick={() => setJanela(false)}>Fechar janela</Botao>}
        >
          <p>Corpo da janela.</p>
        </Janela>
        <Janela
          aberta={painel}
          aoFechar={() => setPainel(false)}
          titulo="Painel (vitrine)"
          posicao="direita"
        >
          <p>Corpo do painel.</p>
        </Janela>
        <JanelaDeConfirmacao
          aberta={confirmacao}
          titulo="Confirmação (vitrine)"
          variante="digitar"
          descricao="Digite para confirmar."
          textoParaDigitar="desativar"
          aoCancelar={() => setConfirmacao(false)}
          aoConfirmar={() => setConfirmacao(false)}
        />
      </Secao>

      <Secao titulo="Markdown com texto de ataque">
        <div data-testid="markdown-de-ataque">
          <Markdown texto={ATAQUE} />
        </div>
      </Secao>
    </div>
  )
}
