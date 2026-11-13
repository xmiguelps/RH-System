using RHSystem.Application.Comum;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Tests.Treinamentos;

/// <summary>
/// Um teste por mutante real que sobreviveu à mutação do incremento 6a nas regras puras (T2): o limite de duração, o texto da mensagem do módulo de texto vazio e do arquivo recusado e a letra z do nome do CSV.
/// </summary>
public class MutantesDasRegrasDosTreinamentosTests
{
    private static Dictionary<string, string[]> ErrosDe(Action<ColetorDeErros> acao)
    {
        var erros = new ColetorDeErros();
        acao(erros);
        try
        {
            erros.LancarSeHouver();
            return [];
        }
        catch (ErroDeValidacaoException e)
        {
            return e.Erros.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(32767, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(32768, false)]
    public void A_duracao_vai_de_1_a_32767_minutos_com_as_duas_pontas_dentro(int minutos, bool vale)
    {
        var erros = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Abertura", TipoDeModulo.Video, minutos, "https://video.exemplo.com.br/a", null, false, false));

        Assert.Equal(vale, !erros.ContainsKey("duracaoMinutos"));
    }

    [Fact]
    public void O_modulo_de_texto_sem_texto_pede_o_texto_e_com_so_espacos_diz_que_nao_pode_ficar_em_branco()
    {
        var ausente = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Leitura", TipoDeModulo.Texto, null, null, null, false, false));
        var vazio = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Leitura", TipoDeModulo.Texto, null, null, "", false, false));
        var emBranco = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Leitura", TipoDeModulo.Texto, null, null, "   ", false, false));

        Assert.Equal(["Informe o texto do módulo."], ausente["conteudoTexto"]);
        Assert.Equal(["O texto do módulo não pode ficar em branco."], vazio["conteudoTexto"]);
        Assert.Equal(["O texto do módulo não pode ficar em branco."], emBranco["conteudoTexto"]);
    }

    [Fact]
    public void O_texto_em_branco_de_outro_tipo_nao_e_erro_de_texto()
    {
        var link = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Artigo", TipoDeModulo.Link, null, "https://a.com", "   ", false, false));

        Assert.Empty(link);
    }

    [Fact]
    public void O_arquivo_num_modulo_de_video_diz_que_e_link_externo_e_nos_outros_tipos_que_nao_tem_arquivo()
    {
        var video = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Abertura", TipoDeModulo.Video, null, "https://a.com", null, true, false));
        var texto = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Leitura", TipoDeModulo.Texto, null, null, "Texto.", true, false));
        var link = ErrosDe(e => ValidacaoDoModulo.Validar(e, "Artigo", TipoDeModulo.Link, null, "https://a.com", null, true, false));

        Assert.Equal(["O vídeo é um link externo: não envie arquivo."], video["arquivo"]);
        Assert.Equal(["Este tipo de módulo não tem arquivo."], texto["arquivo"]);
        Assert.Equal(["Este tipo de módulo não tem arquivo."], link["arquivo"]);
    }

    [Theory]
    [InlineData("Zona azul", "treinamento-zona-azul.csv")]
    [InlineData("ZZZ", "treinamento-zzz.csv")]
    [InlineData("  LGPD na prática!  ", "treinamento-lgpd-na-pratica.csv")]
    [InlineData("a/b\\c", "treinamento-a-b-c.csv")]
    [InlineData("!!!", "treinamento.csv")]
    public void O_nome_do_csv_usa_todas_as_letras_de_a_a_z_e_os_numeros_com_hifens(string titulo, string esperado) => Assert.Equal(esperado, NomesDeArquivoDoTreinamento.CsvDosParticipantes(titulo));
}
