using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Empresa;

/// <summary>
/// A configuração da empresa (<c>CONFIGURACOES_EMPRESA</c>): UMA linha só (<see cref="IdUnico"/>; o banco confere com <c>CHECK</c>), que a empresa preenche uma vez e o RH edita. A tabela nasce vazia: até o primeiro
/// <c>PUT</c> a empresa "ainda não foi configurada". Horário de saída menor que o de entrada é válido (turno que atravessa a meia-noite). <see cref="Criar"/> e <see cref="Atualizar"/> recusam com
/// <see cref="ArgumentException"/> o que o banco recusaria (UF, tolerância, dia de pagamento, tamanhos), antes de mudar qualquer campo.
/// </summary>
[Auditado(AuditoriaArea.Cadastros, "Empresa")]
public class ConfiguracaoDaEmpresa : IAuditavel
{
    public const byte IdUnico = 1;

    private static readonly HashSet<string> Ufs =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    private ConfiguracaoDaEmpresa()
    {
    }

    /// <summary>As 27 siglas, em maiúsculas (o <c>CHECK</c> do banco exige maiúsculas e só confere o formato; a lista é da aplicação).</summary>
    public static IReadOnlySet<string> UfsValidas => Ufs;

    public byte Id { get; private set; } = IdUnico;

    [Auditar("Nome da empresa")]
    public string NomeEmpresa { get; private set; } = string.Empty;

    [Auditar("UF")]
    public string Uf { get; private set; } = string.Empty;

    [Auditar("Município")]
    public string Municipio { get; private set; } = string.Empty;

    [Auditar("Horário de entrada")]
    public TimeOnly HorarioEntrada { get; private set; }

    [Auditar("Horário de saída")]
    public TimeOnly HorarioSaida { get; private set; }

    [Auditar("Tolerância de atraso")]
    public int ToleranciaAtrasoMinutos { get; private set; }

    [Auditar("Dia de pagamento")]
    public int DiaPagamento { get; private set; }

    public string NomeParaAuditoria => NomeEmpresa;

    public string RegistroAfetado => "Configuração da empresa";

    public static ConfiguracaoDaEmpresa Criar(string nomeEmpresa, string uf, string municipio, TimeOnly entrada, TimeOnly saida, int toleranciaAtrasoMinutos, int diaPagamento)
    {
        var configuracao = new ConfiguracaoDaEmpresa();
        configuracao.Aplicar(nomeEmpresa, uf, municipio, entrada, saida, toleranciaAtrasoMinutos, diaPagamento);
        return configuracao;
    }

    public void Atualizar(string nomeEmpresa, string uf, string municipio, TimeOnly entrada, TimeOnly saida, int toleranciaAtrasoMinutos, int diaPagamento) =>
        Aplicar(nomeEmpresa, uf, municipio, entrada, saida, toleranciaAtrasoMinutos, diaPagamento);

    /// <summary>Valida TUDO primeiro e só então grava nos campos: o que for recusado não deixa a configuração pela metade.</summary>
    private void Aplicar(string nomeEmpresa, string uf, string municipio, TimeOnly entrada, TimeOnly saida, int toleranciaAtrasoMinutos, int diaPagamento)
    {
        var nome = CamposDeTexto.Obrigatorio(nomeEmpresa, nameof(nomeEmpresa), 150);
        var cidade = CamposDeTexto.Obrigatorio(municipio, nameof(municipio), 100);
        if (uf is null || !Ufs.Contains(uf))
        {
            throw new ArgumentException("A UF é uma das 27 siglas, em maiúsculas (por exemplo SP).", nameof(uf));
        }

        if (toleranciaAtrasoMinutos is < 0 or > 120)
        {
            throw new ArgumentException("A tolerância de atraso vai de 0 a 120 minutos.", nameof(toleranciaAtrasoMinutos));
        }

        if (diaPagamento is < 1 or > 31)
        {
            throw new ArgumentException("O dia de pagamento vai de 1 a 31.", nameof(diaPagamento));
        }

        NomeEmpresa = nome;
        Uf = uf;
        Municipio = cidade;
        HorarioEntrada = entrada;
        HorarioSaida = saida;
        ToleranciaAtrasoMinutos = toleranciaAtrasoMinutos;
        DiaPagamento = diaPagamento;
    }
}
