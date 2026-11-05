using Microsoft.Extensions.Logging;
using RHSystem.Application.Escopo;
using RHSystem.Application.Painel;
using RHSystem.Application.Tests.Falsos;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Painel;

/// <summary>
/// Os contadores do menu: várias fontes do mesmo contador somam, sem fonte o contador vale 0, uma fonte que lança conta 0 sem derrubar o menu e cada rota
/// consulta só as fontes dos contadores dela (a rota é leve: o front a chama a cada navegação).
/// </summary>
public class ContadoresDoMenuCasoDeUsoTests
{
    private sealed class EscopoFalso : IEscopoDoUsuario
    {
        public int UsuarioId => 1;

        public int FuncionarioId => 1;

        public Perfil Perfil => Perfil.Gestor;

        public string Nome => "Teste";

        public Task<EquipeDoGestor?> ObterEquipeAsync(CancellationToken ct) => Task.FromResult<EquipeDoGestor?>(null);
    }

    private sealed class FonteFalsa(ContadorDoMenu contador, int valor = 0, Exception? lanca = null) : IFonteDeContadorDoMenu
    {
        public ContadorDoMenu Contador => contador;

        public int Chamadas { get; private set; }

        public IEscopoDoUsuario? EscopoRecebido { get; private set; }

        public Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct)
        {
            Chamadas++;
            EscopoRecebido = escopo;
            return lanca is null ? Task.FromResult(valor) : throw lanca;
        }
    }

    private static ContadoresDoMenuCasoDeUso Criar(IEscopoDoUsuario escopo, LogColetor<ContadoresDoMenuCasoDeUso> log, params IFonteDeContadorDoMenu[] fontes) => new(fontes, escopo, log);

    [Fact]
    public async Task Sem_fonte_registrada_todos_os_contadores_valem_zero()
    {
        var casoDeUso = Criar(new EscopoFalso(), new LogColetor<ContadoresDoMenuCasoDeUso>());

        Assert.Equal(new PainelContadoresFuncionario(0, 0, 0), await casoDeUso.ObterDoFuncionarioAsync(CancellationToken.None));
        Assert.Equal(new PainelContadoresGestor(0, 0), await casoDeUso.ObterDoGestorAsync(CancellationToken.None));
        Assert.Equal(new PainelContadoresRh(0), await casoDeUso.ObterDoRhAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fontes_do_mesmo_contador_somam_e_cada_rota_usa_so_os_contadores_dela()
    {
        var escopo = new EscopoFalso();
        var avisosA = new FonteFalsa(ContadorDoMenu.AvisosNaoLidos, 2);
        var avisosB = new FonteFalsa(ContadorDoMenu.AvisosNaoLidos, 3);
        var formularios = new FonteFalsa(ContadorDoMenu.FormulariosPendentes, 1);
        var documentos = new FonteFalsa(ContadorDoMenu.DocumentosPendentes, 4);
        var equipe = new FonteFalsa(ContadorDoMenu.PendenciasDaEquipe, 7);
        var pontoDoGestor = new FonteFalsa(ContadorDoMenu.PedidosDeAjusteDePontoDoGestor, 2);
        var pontoDoRh = new FonteFalsa(ContadorDoMenu.PedidosDeAjusteDePontoDoRh, 9);
        FonteFalsa[] todas = [avisosA, avisosB, formularios, documentos, equipe, pontoDoGestor, pontoDoRh];
        var casoDeUso = Criar(escopo, new LogColetor<ContadoresDoMenuCasoDeUso>(), todas);

        var funcionario = await casoDeUso.ObterDoFuncionarioAsync(CancellationToken.None);
        var chamadasDoFuncionario = todas.Select(f => f.Chamadas).ToArray();

        Assert.Equal(new PainelContadoresFuncionario(5, 1, 4), funcionario); // 2 + 3 dos avisos
        Assert.Equal([1, 1, 1, 1, 0, 0, 0], chamadasDoFuncionario); // o funcionário não consulta as fontes do gestor nem do RH

        var gestor = await casoDeUso.ObterDoGestorAsync(CancellationToken.None);

        Assert.Equal(new PainelContadoresGestor(7, 2), gestor); // PendenciasDaEquipe = PendenciasDaEquipe; PedidosDeAjusteDePonto = o do gestor; sem DocumentosPendentes
        Assert.Equal([1, 1, 1, 1, 1, 1, 0], todas.Select(f => f.Chamadas).ToArray()); // o gestor consultou só as duas dele

        var rh = await casoDeUso.ObterDoRhAsync(CancellationToken.None);

        Assert.Equal(new PainelContadoresRh(9), rh);
        Assert.Equal([1, 1, 1, 1, 1, 1, 1], todas.Select(f => f.Chamadas).ToArray()); // o RH consultou só a dele
        Assert.All(todas, fonte => Assert.Same(escopo, fonte.EscopoRecebido)); // a fonte recebe o escopo da requisição
    }

    [Fact]
    public async Task Fonte_que_lanca_conta_zero_e_registra_erro_sem_derrubar_o_menu()
    {
        var log = new LogColetor<ContadoresDoMenuCasoDeUso>();
        var casoDeUso = Criar(
            new EscopoFalso(),
            log,
            new FonteFalsa(ContadorDoMenu.AvisosNaoLidos, lanca: new InvalidOperationException("dado pessoal na mensagem: ana@empresa.com.br")),
            new FonteFalsa(ContadorDoMenu.AvisosNaoLidos, 3), // outra fonte do mesmo contador: continua somando
            new FonteFalsa(ContadorDoMenu.FormulariosPendentes, 1));

        var resposta = await casoDeUso.ObterDoFuncionarioAsync(CancellationToken.None);

        Assert.Equal(new PainelContadoresFuncionario(3, 1, 0), resposta);
        var erro = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Error);
        Assert.Contains("AvisosNaoLidos", erro.Mensagem);
        Assert.Contains(nameof(InvalidOperationException), erro.Mensagem);
        Assert.DoesNotContain("ana@empresa.com.br", erro.Mensagem); // o log leva o nome da exceção, nunca a mensagem (pode ter dado pessoal)
    }

    [Fact]
    public async Task Cancelamento_do_pedido_nao_vira_zero()
    {
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();
        var casoDeUso = Criar(new EscopoFalso(), new LogColetor<ContadoresDoMenuCasoDeUso>(), new FonteFalsa(ContadorDoMenu.AvisosNaoLidos, lanca: new OperationCanceledException(cancelamento.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => casoDeUso.ObterDoFuncionarioAsync(cancelamento.Token));
    }
}
