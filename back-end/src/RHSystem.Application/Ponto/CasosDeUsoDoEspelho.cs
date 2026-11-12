using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Application.Ponto;

/// <summary>Do cálculo do mês para os tipos do contrato (a própria pessoa tem o botão "Pedir ajuste" em cada dia; o gestor e o RH, não).</summary>
internal static class MapeamentoDoEspelho
{
    public static PontoEspelhoDia Dia(DiaDoEspelho d) =>
        new(d.Data, d.DiaDaSemana, d.EhHoje, d.Situacao, d.NomeFeriado, d.Periodos, d.HorasMinutos, d.CargaMinutos, d.SaldoMinutos, d.AtrasoMinutos, d.PedidoEmAnaliseId);

    public static PontoEspelho ParaGestorOuRh(EspelhoDoMes e) =>
        new(
            new FuncionarioResumo(e.Pessoa.Id, e.Pessoa.Nome, e.Pessoa.Ativo, e.Pessoa.Cargo, e.Pessoa.Setor),
            e.Ano,
            e.Mes,
            e.Jornada,
            e.Resumo,
            [.. e.Dias.Select(d => new PontoEspelhoDia(d.Data, d.DiaDaSemana, d.EhHoje, d.Situacao, d.NomeFeriado, d.Periodos, d.HorasMinutos, d.CargaMinutos, d.SaldoMinutos, d.AtrasoMinutos, d.PedidoEmAnaliseId))]);

    public static PontoEspelhoProprio ParaAPropriaPessoa(EspelhoDoMes e) =>
        new(
            e.Ano,
            e.Mes,
            e.Jornada,
            e.Resumo,
            [.. e.Dias.Select(d => new PontoEspelhoDiaProprio(d.Data, d.DiaDaSemana, d.EhHoje, d.Situacao, d.NomeFeriado, d.Periodos, d.HorasMinutos, d.CargaMinutos, d.SaldoMinutos, d.AtrasoMinutos, d.PedidoEmAnaliseId, d.PodePedirAjuste))]);
}

/// <summary><c>GET /api/eu/ponto/espelho</c>: o espelho do mês da PRÓPRIA pessoa logada (qualquer perfil). O id nunca vem da requisição.</summary>
public sealed class ObterMeuEspelhoCasoDeUso(IEscopoDoUsuario escopo, IConsultaDoPonto consulta, MontadorDoEspelho montador)
{
    public async Task<PontoEspelhoProprio> ExecutarAsync(int? ano, int? mes, CancellationToken ct)
    {
        var pessoa = await consulta.PessoaAsync(escopo.FuncionarioId, ct) ?? throw new NaoEncontradoException();
        return MapeamentoDoEspelho.ParaAPropriaPessoa(await montador.MontarAsync(pessoa, ano, mes, ct));
    }
}

/// <summary>
/// <c>GET /api/gestor/equipe/{funcionarioId}/ponto</c>: o espelho de uma pessoa DA EQUIPE do gestor. Pessoa de outro setor, o próprio gestor, de outro gestor ou que não existe = 404, o mesmo 404; gestor sem setor também. O mês inválido
/// (400) vem antes do 404.
/// </summary>
public sealed class ObterEspelhoDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDoPonto consulta, MontadorDoEspelho montador)
{
    public async Task<PontoEspelho> ExecutarAsync(int funcionarioId, int? ano, int? mes, CancellationToken ct)
    {
        var equipe = await escopo.ObterEquipeAsync(ct);
        MontadorDoEspelho.Periodo(ano, mes, DateOnly.MinValue); // só valida (o mês atual é resolvido no montador)
        var pessoa = equipe is null ? null : await consulta.PessoaDaEquipeAsync(equipe, funcionarioId, ct);
        return MapeamentoDoEspelho.ParaGestorOuRh(await montador.MontarAsync(pessoa ?? throw new NaoEncontradoException(), ano, mes, ct));
    }
}

/// <summary><c>GET /api/rh/funcionarios/{funcionarioId}/ponto</c>: o espelho de qualquer pessoa, para o RH (também de quem está inativo).</summary>
public sealed class ObterEspelhoParaRhCasoDeUso(IConsultaDoPonto consulta, MontadorDoEspelho montador)
{
    public async Task<PontoEspelho> ExecutarAsync(int funcionarioId, int? ano, int? mes, CancellationToken ct)
    {
        MontadorDoEspelho.Periodo(ano, mes, DateOnly.MinValue);
        var pessoa = await consulta.PessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        return MapeamentoDoEspelho.ParaGestorOuRh(await montador.MontarAsync(pessoa, ano, mes, ct));
    }
}

/// <summary><c>GET /api/gestor/ponto/equipe</c>: o ponto de hoje (ou de um dia passado) de toda a equipe do gestor.</summary>
public sealed class ObterPontoDaEquipeCasoDeUso(IEscopoDoUsuario escopo, MontadorDoPontoDaEquipe montador)
{
    public async Task<PontoEquipeDia> ExecutarAsync(DateOnly? data, CancellationToken ct) => await montador.MontarAsync(await escopo.ObterEquipeAsync(ct), data, ct);
}
