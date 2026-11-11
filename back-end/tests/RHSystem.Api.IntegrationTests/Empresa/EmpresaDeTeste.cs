using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Empresa;

/// <summary>A semente da configuração da empresa (uma linha só, <c>ID_CONFIGURACAO_EMPRESA = 1</c>) por SQL, para os testes controlarem o que o banco tem.</summary>
internal static class EmpresaDeTeste
{
    public static Task LimparAsync(string conexao) => ApoioDeTeste.ExecutarAsync(conexao, "DELETE FROM CONFIGURACOES_EMPRESA");

    public static async Task InserirAsync(string conexao, string nome = "Empresa semeada", string uf = "SP", string municipio = "São Paulo", string entrada = "08:00:00", string saida = "17:30:00", int tolerancia = 10, int dia = 5)
    {
        await LimparAsync(conexao);
        await ApoioDeTeste.ExecutarAsync(
            conexao,
            "INSERT INTO CONFIGURACOES_EMPRESA (ID_CONFIGURACAO_EMPRESA, NOME_EMPRESA, UF, MUNICIPIO, HORARIO_ENTRADA, HORARIO_SAIDA, TOLERANCIA_ATRASO_MINUTOS, DIA_PAGAMENTO) VALUES (1, @nome, @uf, @municipio, @entrada, @saida, @tolerancia, @dia)",
            ("@nome", nome),
            ("@uf", uf),
            ("@municipio", municipio),
            ("@entrada", TimeOnly.Parse(entrada, System.Globalization.CultureInfo.InvariantCulture).ToTimeSpan()),
            ("@saida", TimeOnly.Parse(saida, System.Globalization.CultureInfo.InvariantCulture).ToTimeSpan()),
            ("@tolerancia", tolerancia),
            ("@dia", dia));
    }
}
