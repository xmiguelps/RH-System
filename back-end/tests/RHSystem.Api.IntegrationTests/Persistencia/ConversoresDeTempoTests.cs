using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

[Collection(MySqlCollection.Nome)]
public class ConversoresDeTempoTests(MySqlFixture mysql)
{
    // O trigger de USUARIOS recusa senha com menos de 40 caracteres: um valor do tamanho de um BCrypt.
    private const string HashDeTeste = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    private enum Situacao
    {
        SemRegistro,
        Aprovado,
    }

    [Fact]
    public void Utc_grava_so_DateTime_com_Kind_Utc_e_lanca_para_Local_e_Unspecified()
    {
        var paraOBanco = ConversoresDeTempo.Utc.ConvertToProviderTyped;
        var utc = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

        Assert.Equal(utc, paraOBanco(utc));
        Assert.Throws<InvalidOperationException>(() => paraOBanco(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Local)));
        Assert.Throws<InvalidOperationException>(() => paraOBanco(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Utc_le_como_Kind_Utc_e_Brasil_grava_tres_horas_antes_e_le_tres_horas_depois_como_Kind_Utc()
    {
        var doBanco = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Unspecified);
        var lidoUtc = ConversoresDeTempo.Utc.ConvertFromProviderTyped(doBanco);
        Assert.Equal(doBanco.Ticks, lidoUtc.Ticks);
        Assert.Equal(DateTimeKind.Utc, lidoUtc.Kind);

        var utc = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);
        var gravado = ConversoresDeTempo.Brasil.ConvertToProviderTyped(utc);
        Assert.Equal(new DateTime(2026, 10, 7, 9, 42, 0).Ticks, gravado.Ticks);

        var lido = ConversoresDeTempo.Brasil.ConvertFromProviderTyped(new DateTime(2026, 10, 7, 9, 42, 0, DateTimeKind.Unspecified));
        Assert.Equal(utc.Ticks, lido.Ticks);
        Assert.Equal(DateTimeKind.Utc, lido.Kind);

        // A decisão de fuso é obrigatória nos dois: um DateTime que não seja UTC não entra em nenhuma das duas colunas.
        Assert.Throws<InvalidOperationException>(() => ConversoresDeTempo.Brasil.ConvertToProviderTyped(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Unspecified)));
        Assert.Throws<InvalidOperationException>(() => ConversoresDeTempo.Brasil.ConvertToProviderTyped(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void EnumComoTextoMaiusculo_converte_SemRegistro_para_SEM_REGISTRO_e_de_volta_e_lanca_para_texto_desconhecido()
    {
        var conversor = new EnumComoTextoMaiusculo<Situacao>();

        Assert.Equal("SEM_REGISTRO", conversor.ConvertToProviderTyped(Situacao.SemRegistro));
        Assert.Equal("APROVADO", conversor.ConvertToProviderTyped(Situacao.Aprovado));
        Assert.Equal(Situacao.SemRegistro, conversor.ConvertFromProviderTyped("SEM_REGISTRO"));
        Assert.Equal(Situacao.Aprovado, conversor.ConvertFromProviderTyped("APROVADO"));

        Assert.Throws<InvalidOperationException>(() => conversor.ConvertFromProviderTyped("DESCONHECIDO"));
        Assert.Throws<InvalidOperationException>(() => conversor.ConvertFromProviderTyped("SemRegistro"));
        Assert.Throws<InvalidOperationException>(() => conversor.ConvertToProviderTyped((Situacao)99));
    }

    [Fact]
    public async Task DataHoraUltimoLogin_e_gravada_tres_horas_antes_e_lida_de_volta_em_UTC()
    {
        // Relógio falso COM FRAÇÃO: a coluna é DATETIME sem fração e o MySQL pode arredondar em vez de truncar. Quem trunca ao segundo é
        // Usuario.RegistrarLogin; este teste prova que o banco guarda 09:42:00 em -03:00 (nunca 09:42:01) e que a leitura volta em UTC.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var instante = new DateTime(2026, 10, 7, 12, 42, 0, 700, DateTimeKind.Utc);
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var nivel = await escrita.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
            var funcionario = Funcionario.Criar("Ana Login", "ana.login@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
            var usuario = Usuario.Criar(funcionario, HashDeTeste);
            usuario.RegistrarLogin(instante);
            escrita.Usuarios.Add(usuario);
            await escrita.SaveChangesAsync();
        }

        await using (var c = new MySqlConnection(conexao))
        {
            await c.OpenAsync();
            await using var comando = new MySqlCommand("SELECT DATE_FORMAT(DATA_HORA_ULTIMO_LOGIN, '%Y-%m-%d %H:%i:%s') FROM USUARIOS", c);
            Assert.Equal("2026-10-07 09:42:00", (string?)await comando.ExecuteScalarAsync());
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lido = (await leitura.Usuarios.SingleAsync()).UltimoLoginEm!.Value;
        Assert.Equal(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), lido);
        Assert.Equal(DateTimeKind.Utc, lido.Kind);
    }
}
