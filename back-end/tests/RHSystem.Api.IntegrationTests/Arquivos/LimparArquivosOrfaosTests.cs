using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RHSystem.Api.IntegrationTests.Agendamento;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Infrastructure.Arquivos;
using RHSystem.Infrastructure.Arquivos.Tarefas;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Arquivos;

/// <summary>
/// <c>LIMPAR_ARQUIVOS_ORFAOS</c>: apaga do armazenamento o arquivo que NENHUMA linha do banco referencia, mas só nos prefixos que têm uma <see cref="IFonteDeReferenciasDeArquivo"/> e só o que é mais velho que
/// <c>Armazenamento:SobrasMinimoHoras</c> (o mínimo protege o envio que já gravou o blob e ainda não inseriu a linha). Armazenamento em memória e fontes falsas, com o relógio falso em 2026-10-07T12:42Z.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LimparArquivosOrfaosTests(MySqlFixture mysql)
{
    private static readonly DateTime Hoje = ElencoDeTeste.Hoje;

    private static string Caminho(string pasta = "documentos") => CaminhosDeArquivo.Novo(pasta, ".pdf");

    private static async Task ExecutarAsync(LimparArquivosOrfaosTarefa tarefa) =>
        await tarefa.ExecutarAsync(new TarefaParaExecutar(0, "LIMPAR_ARQUIVOS_ORFAOS:dia=2026-10-07", null, new DateOnly(2026, 10, 7), null, 1), CancellationToken.None);

    private static (LimparArquivosOrfaosTarefa Tarefa, ArmazenamentoEmMemoria Armazenamento) Montar(params IFonteDeReferenciasDeArquivo[] fontes)
    {
        var relogio = new RelogioDeTeste();
        relogio.Fixar(Hoje);
        var armazenamento = new ArmazenamentoEmMemoria(relogio);
        var tarefa = new LimparArquivosOrfaosTarefa(fontes, armazenamento, armazenamento, new AgendadorQueNaoSeUsa(), relogio, new OpcoesDeArmazenamento { SobrasMinimoHoras = 24 }, NullLogger<LimparArquivosOrfaosTarefa>.Instance);
        return (tarefa, armazenamento);
    }

    [Fact]
    public async Task Apaga_blob_antigo_sem_referencia_e_mantem_o_referenciado()
    {
        var referenciado = Caminho();
        var orfao = Caminho();
        var fonte = new FonteFalsa("documentos/", [referenciado]);
        var (tarefa, armazenamento) = Montar(fonte);
        armazenamento.Semear(referenciado, ArquivosDeTeste.Pdf(), Hoje.AddHours(-48));
        armazenamento.Semear(orfao, ArquivosDeTeste.Pdf(), Hoje.AddHours(-48));

        await ExecutarAsync(tarefa);

        Assert.True(armazenamento.Existe(referenciado));
        Assert.False(armazenamento.Existe(orfao));
        Assert.Equal([orfao], armazenamento.Apagados);
    }

    [Fact]
    public async Task Nunca_apaga_blob_mais_novo_que_o_minimo()
    {
        var gravadoAgora = Caminho();
        var quaseVinteEQuatroHoras = Caminho();
        var exatamenteVinteEQuatroHoras = Caminho();
        var passouDoMinimo = Caminho();
        var (tarefa, armazenamento) = Montar(new FonteFalsa("documentos/", []));
        armazenamento.Semear(gravadoAgora, ArquivosDeTeste.Pdf(), Hoje); // o envio que gravou o blob e ainda não inseriu a linha
        armazenamento.Semear(quaseVinteEQuatroHoras, ArquivosDeTeste.Pdf(), Hoje.AddHours(-24).AddMinutes(1));
        armazenamento.Semear(exatamenteVinteEQuatroHoras, ArquivosDeTeste.Pdf(), Hoje.AddHours(-24));
        armazenamento.Semear(passouDoMinimo, ArquivosDeTeste.Pdf(), Hoje.AddHours(-24).AddMinutes(-1));

        await ExecutarAsync(tarefa);

        Assert.True(armazenamento.Existe(gravadoAgora));
        Assert.True(armazenamento.Existe(quaseVinteEQuatroHoras));
        Assert.True(armazenamento.Existe(exatamenteVinteEQuatroHoras)); // "mais velho que" o mínimo: exatamente o mínimo ainda não
        Assert.False(armazenamento.Existe(passouDoMinimo));
    }

    [Fact]
    public async Task Nunca_apaga_blob_de_prefixo_sem_fonte_registrada()
    {
        var semFonte = Caminho("holerites");
        var comFonte = Caminho("documentos");
        var (tarefa, armazenamento) = Montar(new FonteFalsa("documentos/", []));
        armazenamento.Semear(semFonte, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));
        armazenamento.Semear(comFonte, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));

        await ExecutarAsync(tarefa);

        Assert.True(armazenamento.Existe(semFonte)); // sem fonte, ninguém sabe o que é referenciado: seguro é não tocar
        Assert.False(armazenamento.Existe(comFonte));
    }

    [Fact]
    public async Task Sem_nenhuma_fonte_registrada_nada_e_apagado()
    {
        var caminho = Caminho();
        var (tarefa, armazenamento) = Montar();
        armazenamento.Semear(caminho, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));

        await ExecutarAsync(tarefa);

        Assert.True(armazenamento.Existe(caminho));
        Assert.Empty(armazenamento.Apagados);
    }

    [Fact]
    public async Task Fonte_que_lanca_nao_impede_as_outras_e_nada_do_prefixo_dela_e_apagado()
    {
        var doQueFalha = Enumerable.Range(0, 3).Select(_ => Caminho("atestados")).ToList();
        var orfaoDoOutro = Caminho("documentos");
        var quebrada = new FonteFalsa("atestados/", [], lanca: new InvalidOperationException("a fonte caiu"));
        var (tarefa, armazenamento) = Montar(quebrada, new FonteFalsa("documentos/", []));
        foreach (var caminho in doQueFalha)
        {
            armazenamento.Semear(caminho, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));
        }

        armazenamento.Semear(orfaoDoOutro, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));

        // A tarefa termina o que dá e depois lança, para o agendador tentar de novo e a falha aparecer (FALHOU): ela não engole o erro em silêncio.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecutarAsync(tarefa));

        Assert.DoesNotContain("a fonte caiu", erro.Message); // a mensagem da exceção da fonte não sobe para ULTIMO_ERRO
        Assert.All(doQueFalha, caminho => Assert.True(armazenamento.Existe(caminho)));
        Assert.False(armazenamento.Existe(orfaoDoOutro)); // a fonte que funciona foi até o fim
        Assert.Equal([3], quebrada.Lotes); // os 3 candidatos num lote só
    }

    [Fact]
    public async Task Fonte_que_lanca_no_segundo_lote_nao_apaga_nada_nem_do_primeiro()
    {
        var caminhos = Enumerable.Range(0, 150).Select(_ => Caminho()).ToList();
        var fonte = new FonteFalsa("documentos/", [], lanca: new InvalidOperationException("caiu no meio"), lancaNoLote: 2);
        var (tarefa, armazenamento) = Montar(fonte);
        foreach (var caminho in caminhos)
        {
            armazenamento.Semear(caminho, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecutarAsync(tarefa));

        Assert.Equal([100, 50], fonte.Lotes);
        Assert.Equal(150, armazenamento.Quantidade); // nem os 100 do primeiro lote, que já tinham resposta: o prefixo inteiro fica como estava
    }

    [Fact]
    public async Task Mais_de_cem_blobs_sao_tratados_em_lotes()
    {
        var caminhos = Enumerable.Range(0, 250).Select(_ => Caminho()).ToList();
        var referenciados = caminhos.Take(10).ToList();
        var fonte = new FonteFalsa("documentos/", referenciados);
        var (tarefa, armazenamento) = Montar(fonte);
        foreach (var caminho in caminhos)
        {
            armazenamento.Semear(caminho, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));
        }

        await ExecutarAsync(tarefa);

        Assert.Equal([100, 100, 50], fonte.Lotes); // a fonte nunca recebe mais de 100 caminhos de uma vez
        Assert.Equal(10, armazenamento.Quantidade);
        Assert.All(referenciados, caminho => Assert.True(armazenamento.Existe(caminho)));
        Assert.Equal(240, armazenamento.Apagados.Count);
    }

    [Fact]
    public async Task Falha_ao_apagar_deixa_o_blob_para_o_dia_seguinte_e_a_tarefa_lanca_no_fim()
    {
        var a = Caminho();
        var b = Caminho();
        var (tarefa, armazenamento) = Montar(new FonteFalsa("documentos/", []));
        armazenamento.Semear(a, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));
        armazenamento.Semear(b, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));
        armazenamento.FalharNaExclusao = new IOException("o armazenamento recusou");

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecutarAsync(tarefa));

        Assert.Equal(2, armazenamento.Quantidade); // nada apagado: o erro é do armazenamento, e o dia seguinte tenta de novo
    }

    [Fact]
    public async Task Prefixo_vazio_ou_sem_barra_final_e_ignorado_para_nunca_listar_o_conteiner_inteiro()
    {
        var caminho = Caminho("documentos");
        var (tarefa, armazenamento) = Montar(new FonteFalsa(string.Empty, []), new FonteFalsa("docu", []));
        armazenamento.Semear(caminho, ArquivosDeTeste.Pdf(), Hoje.AddDays(-30));

        await ExecutarAsync(tarefa);

        Assert.True(armazenamento.Existe(caminho));
    }

    [Fact]
    public async Task RecuperarAsync_agenda_uma_por_dia_sem_duplicar_e_o_tipo_esta_registrado()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql); // relógio em 2026-10-07T12:42Z e tarefas vazias
        try
        {
            async Task RecuperarAsync()
            {
                await using var escopo = api.Services.CreateAsyncScope();
                var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == "LIMPAR_ARQUIVOS_ORFAOS"); // o tipo está registrado, e só uma vez
                await tarefa.RecuperarAsync(CancellationToken.None);
            }

            await RecuperarAsync();
            await RecuperarAsync(); // de novo no mesmo dia: a chave já existe

            Assert.Equal(1, await EscalarAsync(api.Conexao, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LIMPAR_ARQUIVOS_ORFAOS' AND CHAVE_IDEMPOTENCIA = 'LIMPAR_ARQUIVOS_ORFAOS:dia=2026-10-07'"));

            api.Relogio.Fixar(new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)); // o dia seguinte: uma nova
            await RecuperarAsync();
            Assert.Equal(2, await EscalarAsync(api.Conexao, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LIMPAR_ARQUIVOS_ORFAOS'"));
            Assert.Equal(1, await EscalarAsync(api.Conexao, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE CHAVE_IDEMPOTENCIA = 'LIMPAR_ARQUIVOS_ORFAOS:dia=2026-10-08'"));
        }
        finally
        {
            api.Relogio.Fixar(Hoje);
        }
    }

    private sealed class FonteFalsa(string prefixo, IEnumerable<string> referenciados, Exception? lanca = null, int lancaNoLote = 1) : IFonteDeReferenciasDeArquivo
    {
        private readonly HashSet<string> _referenciados = [.. referenciados];

        public string Prefixo => prefixo;

        /// <summary>O tamanho de cada lote que a limpeza perguntou, na ordem.</summary>
        public List<int> Lotes { get; } = [];

        public Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
        {
            Lotes.Add(caminhos.Count);
            if (lanca is not null && Lotes.Count >= lancaNoLote)
            {
                throw lanca;
            }

            return Task.FromResult<IReadOnlySet<string>>(caminhos.Where(_referenciados.Contains).ToHashSet());
        }
    }

    private sealed class AgendadorQueNaoSeUsa : IAgendador
    {
        public Task<bool> AgendarAsync(NovaTarefa tarefa, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> RegistrarComoConcluidaAsync(string tipo, string chaveDeIdempotencia, DateOnly? diaReferencia, CancellationToken ct) => throw new NotSupportedException();

        public Task<int> CancelarAsync(string chaveDeIdempotencia, CancellationToken ct) => throw new NotSupportedException();
    }
}
