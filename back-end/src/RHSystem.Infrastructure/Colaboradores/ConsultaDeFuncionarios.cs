using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Acesso;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// A leitura do cadastro pelo RH. "Hoje" é o dia em Brasília no instante do <see cref="IRelogio"/>: <c>EM_ADMISSAO</c> é ativo com admissão depois de hoje; <c>ATIVO</c> é o resto dos ativos
/// (quem tem só um desligamento AGENDADO continua ativo). O perfil e o tipo de contrato são convertidos em memória (um tipo fora de <c>CLT</c> e <c>ESTAGIO</c> sai <c>null</c>, sem derrubar a leitura).
/// </summary>
public sealed partial class ConsultaDeFuncionarios(AppDbContext db, IRelogio relogio, ISituacaoDeHoje situacaoDeHoje, IConsultaDeUsuariosDoSistema usuarios) : IConsultaDeFuncionarios
{
    private static readonly MapaDeOrdenacao<Funcionario> Ordem = new MapaDeOrdenacao<Funcionario>("NOME", DirecaoDeOrdenacao.Asc, f => f.Id)
        .Por("NOME", f => f.Nome)
        .Por("DATA_ADMISSAO", f => f.DataAdmissao);

    private static FuncionarioSituacao Mapear(SituacaoCadastral situacao) => situacao switch
    {
        SituacaoCadastral.Ativo => FuncionarioSituacao.Ativo,
        SituacaoCadastral.EmAdmissao => FuncionarioSituacao.EmAdmissao,
        _ => FuncionarioSituacao.Inativo,
    };

    public async Task<FuncionarioListaRespostaRh> ListarAsync(ParametrosDeLista p, FuncionarioSituacao situacao, int? setorId, TipoDeContrato? tipoContrato, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var consulta = db.Funcionarios.AsNoTracking().AsQueryable();

        consulta = situacao switch
        {
            FuncionarioSituacao.Ativo => consulta.Where(f => f.Ativo && f.DataAdmissao <= hoje),
            FuncionarioSituacao.EmAdmissao => consulta.Where(f => f.Ativo && f.DataAdmissao > hoje),
            _ => consulta.Where(f => !f.Ativo),
        };

        if (setorId is { } setor)
        {
            consulta = consulta.Where(f => f.SetorId == setor);
        }

        if (tipoContrato is { } contrato)
        {
            var texto = TiposDeContrato.ParaTexto(contrato);
            consulta = consulta.Where(f => f.TipoContrato == texto);
        }

        if (p.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            var digitos = DigitosDeDocumento(busca);
            consulta = digitos is null
                ? consulta.Where(f => EF.Functions.Like(f.Nome, like))
                : consulta.Where(f =>
                    EF.Functions.Like(f.Nome, like)
                    || (f.Matricula != null && f.Matricula.Contains(digitos))
                    || db.DetalhesDosFuncionarios.Any(d => d.FuncionarioId == f.Id && d.Cpf.Contains(digitos)));
        }

        var total = await consulta.CountAsync(ct);
        var linhas = await Ordem.Aplicar(consulta, p)
            .Skip(p.Pular)
            .Take(p.TamanhoPagina)
            .Select(f => new
            {
                f.Id,
                f.Nome,
                f.EmailCorporativo,
                Cargo = f.Cargo != null ? f.Cargo.Nome : null,
                Setor = f.Setor != null ? f.Setor.Nome : null,
                f.DataAdmissao,
                f.Ativo,
            })
            .ToListAsync(ct);

        // A situação de hoje (ponto, férias, atestado) só interessa a quem é ATIVO; as outras linhas vêm null.
        var idsAtivos = linhas.Where(l => Mapear(Funcionario.CalcularSituacao(l.Ativo, l.DataAdmissao, hoje)) == FuncionarioSituacao.Ativo).Select(l => l.Id).ToList();
        var situacoes = idsAtivos.Count == 0 ? null : (await situacaoDeHoje.ObterAsync(idsAtivos, ct)).Situacoes;

        var itens = linhas.Select(l =>
        {
            var situacaoDaLinha = Mapear(Funcionario.CalcularSituacao(l.Ativo, l.DataAdmissao, hoje));
            SituacaoHoje? deHoje = situacaoDaLinha == FuncionarioSituacao.Ativo && situacoes is not null ? situacoes[l.Id] : null;
            return new FuncionarioListaItemRh(l.Id, l.Nome, l.EmailCorporativo, l.Cargo, l.Setor, l.DataAdmissao, situacaoDaLinha, deHoje);
        }).ToList();

        var pagina = RespostaPaginada<FuncionarioListaItemRh>.De(itens, total, p);
        return new FuncionarioListaRespostaRh(pagina.Itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, pagina.TotalPaginas, await TotaisAsync(hoje, ct));
    }

    /// <summary>Os totais da empresa inteira (nenhum filtro da lista entra aqui).</summary>
    private async Task<FuncionarioListaTotais> TotaisAsync(DateOnly hoje, CancellationToken ct)
    {
        var porGrupo = await db.Funcionarios.AsNoTracking()
            .GroupBy(f => !f.Ativo ? 2 : f.DataAdmissao > hoje ? 1 : 0)
            .Select(g => new { Grupo = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);
        int Quantos(int grupo) => porGrupo.Where(g => g.Grupo == grupo).Select(g => g.Quantidade).FirstOrDefault();
        return new FuncionarioListaTotais(Quantos(0), Quantos(1), Quantos(2));
    }

    public async Task<FuncionarioFichaRh?> ObterFichaRhAsync(int funcionarioId, CancellationToken ct)
    {
        var f = await db.Funcionarios.AsNoTracking()
            .Include(x => x.Cargo)
            .Include(x => x.Setor)
            .Include(x => x.NivelDeAcesso)
            .Include(x => x.Detalhes)
            .FirstOrDefaultAsync(x => x.Id == funcionarioId, ct);
        if (f is null)
        {
            return null;
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var situacao = Mapear(f.Situacao(hoje));

        // O gestor vem de ID_GESTOR (derivado do setor); quem é o próprio gestor do setor não tem gestor.
        GestorReferencia? gestor = null;
        if (f.GestorId is { } gestorId)
        {
            var achado = await (
                from g in db.Gestores.AsNoTracking()
                where g.Id == gestorId
                join quem in db.Funcionarios.AsNoTracking() on g.FuncionarioId equals quem.Id
                select new { GestorId = g.Id, FuncionarioId = quem.Id, quem.Nome }).FirstOrDefaultAsync(ct);
            if (achado is not null && achado.FuncionarioId != f.Id)
            {
                gestor = new GestorReferencia(achado.GestorId, achado.FuncionarioId, achado.Nome);
            }
        }

        var beneficios = await db.BeneficiosDosFuncionarios.AsNoTracking()
            .Where(b => b.FuncionarioId == funcionarioId && b.Ativo && b.Status == StatusDoPedidoDeBeneficio.Aprovado)
            .OrderBy(b => b.BeneficioId)
            .Select(b => new { b.BeneficioId, b.Beneficio!.Nome, Dependentes = db.Dependentes.Count(d => d.AdesaoId == b.Id && d.Status == StatusDoPedidoDeBeneficio.Aprovado) })
            .ToListAsync(ct);

        var agendado = await db.DesligamentosAgendados.AsNoTracking()
            .Where(d => d.FuncionarioId == funcionarioId && d.Situacao == SituacaoDoDesligamento.Agendado)
            .Select(d => (DateOnly?)d.UltimoDiaTrabalho)
            .FirstOrDefaultAsync(ct);
        FuncionarioDesligamento? desligamento = agendado is { } dia
            ? new FuncionarioDesligamento(dia, FuncionarioDesligamentoSituacao.Agendado)
            : !f.Ativo && f.DataDesligamento is { } ultimo ? new FuncionarioDesligamento(ultimo, FuncionarioDesligamentoSituacao.Efetivado) : null;

        SituacaoHoje? situacaoHoje = situacao == FuncionarioSituacao.Ativo ? (await situacaoDeHoje.ObterAsync([funcionarioId], ct)).Situacoes[funcionarioId] : null;
        var acesso = await usuarios.ObterAsync(funcionarioId, ct);
        var detalhes = f.Detalhes;

        return new FuncionarioFichaRh(
            f.Id,
            f.Matricula,
            f.Nome,
            f.EmailCorporativo,
            detalhes?.Cpf,
            detalhes?.Telefone,
            f.DataNascimento,
            detalhes?.Rg,
            detalhes?.RgOrgaoEmissor,
            detalhes?.EmailPessoal,
            new FuncionarioEndereco(detalhes?.Cep, detalhes?.Logradouro, detalhes?.Numero, detalhes?.Complemento, detalhes?.Bairro, detalhes?.Cidade, detalhes?.Uf),
            f.Cargo is null ? null : new CargoReferencia(f.Cargo.Id, f.Cargo.Nome),
            f.Setor is null ? null : new SetorReferencia(f.Setor.Id, f.Setor.Nome),
            gestor,
            TiposDeContrato.DoTexto(f.TipoContrato),
            f.Cargo?.CargaSemanalHoras,
            f.DataAdmissao,
            f.Perfil,
            f.Ativo,
            situacao,
            situacaoHoje,
            desligamento,
            [.. beneficios.Select(b => new FuncionarioBeneficioItem(b.BeneficioId, b.Nome, b.Dependentes))],
            new FuncionarioAcesso(acesso?.Situacao ?? UsuarioSistemaSituacao.SemConvite, acesso?.Ativo ?? false));
    }

    /// <summary>
    /// Os dígitos do termo quando ele parece um documento: só dígitos, <c>.</c>, <c>-</c>, <c>/</c> e espaço, com pelo menos um dígito. Termo com letra procura só no nome (devolve <c>null</c>).
    /// </summary>
    private static string? DigitosDeDocumento(string busca)
    {
        if (!SoDocumento().IsMatch(busca))
        {
            return null;
        }

        var digitos = new string(busca.Where(c => c is >= '0' and <= '9').ToArray());
        return digitos.Length == 0 ? null : digitos;
    }

    [GeneratedRegex(@"^[0-9.\-/\s]+\z")]
    private static partial Regex SoDocumento();
}
