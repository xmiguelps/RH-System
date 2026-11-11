using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Acesso;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// A nova admissão de quem está inativo (<see cref="IReadmissaoDeFuncionarios"/>), tudo ou nada, na transação do próprio serviço (<see cref="OperacoesComTrava"/>: a pessoa travada primeiro, então o segundo de dois
/// cliques vê o cadastro ativo e recebe 409). Confere ANTES de gravar (400 por campo, depois 422 na ordem do cadastro: gestor do setor, caixas e senha já definida), para que a recusa não deixe nada para trás.
/// <para>
/// <b>Grava, nesta ordem:</b> (1) se algum setor ainda apontava para a pessoa como gestora, ele fica sem gestor e os membros dele também (<see cref="IGestorDoFuncionario.RefazerDosMembrosAsync"/>, que lê o
/// gestor do setor NO BANCO, então o setor é salvo antes); (2) o colaborador volta (<see cref="Funcionario.Readmitir"/>: nova data, cargo, setor, tipo de contrato; gestor = o do setor; nível = o que tinha, e
/// <c>Gestor</c> volta como <c>Funcionario</c>) e o desligamento <c>EFETIVADO</c> vai a <c>REVERTIDO</c>, e SÓ ENTÃO o login (o trigger de <c>USUARIOS</c> consulta o banco: a pessoa tem de estar ativa lá); (3) se o setor
/// apontava para ela, <see cref="IPedidosParadosNoGestor.PassarParaORhAsync"/> (a etapa de um pedido é fixada na criação: "passar ao RH" é ação explícita; se a 1c já trocou o gestor, ela já chamou a porta e aqui nada
/// se chama); (4) com <c>EnviarConvite</c>, reativa o login (cria se faltar) e emite o convite, o que equivale a "Ativar" mais "Reenviar convite"; (5) as caixas "Depois de salvar". Benefícios e férias NÃO voltam.
/// </para>
/// <para>
/// <b>Auditoria:</b> o interceptor grava o <c>Alterou</c> da área <c>COLABORADORES</c> com o que mudou (situação, data de admissão antes e depois, último dia de trabalho antes e vazio, cargo, setor): é onde ficam as
/// datas do vínculo anterior. O setor que perdeu o gestor grava o evento dele (área <c>CADASTROS</c>).
/// </para>
/// </summary>
public sealed class ReadmissaoDeFuncionarios(
    AppDbContext db,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio,
    IGestorDoFuncionario gestores,
    IServicoDeConvite convites,
    IInclusaoNoOnboarding onboarding,
    IEnvioDoFormularioDeAdmissao formulario,
    IPedidosParadosNoGestor pedidos) : IReadmissaoDeFuncionarios
{
    public Task ReadmitirAsync(int funcionarioId, DadosDaReadmissao dados, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dados);
        return OperacoesComTrava.EmTransacaoAsync(
            db,
            unidade,
            async () =>
            {
                var funcionario = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct) ?? throw new NaoEncontradoException();
                if (funcionario.Ativo)
                {
                    throw new ConflitoException("Este colaborador já está ativo. Só se readmite quem está inativo.", "FUNCIONARIO_JA_ATIVO");
                }

                await db.Entry(funcionario).Reference(f => f.NivelDeAcesso).LoadAsync(ct);
                var usuario = dados.EnviarConvite ? await db.Usuarios.SingleOrDefaultAsync(u => u.FuncionarioId == funcionarioId, ct) : null;
                await ConferirAsync(funcionario, dados, usuario, ct);

                // (1) O setor que ainda apontava para ela como gestora fica sem gestor, e os membros dele também.
                var gestorId = await db.Gestores.AsNoTracking().Where(g => g.FuncionarioId == funcionarioId).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);
                var setoresDela = gestorId is { } id ? await db.Setores.Where(s => s.GestorId == id).ToListAsync(ct) : [];
                if (setoresDela.Count > 0)
                {
                    foreach (var setor in setoresDela)
                    {
                        setor.DefinirGestor(null);
                    }

                    await db.SaveChangesAsync(ct); // RefazerDosMembrosAsync lê o gestor do setor no banco
                    foreach (var setor in setoresDela)
                    {
                        await gestores.RefazerDosMembrosAsync(setor.Id, ct);
                    }
                }

                // (2) O colaborador volta e o desligamento vira REVERTIDO; salva antes de mexer no login.
                var gestorDoSetor = (await gestores.ResolverAsync(dados.SetorId, funcionarioId, ct)).GestorId;
                var perfil = funcionario.Perfil is null or Perfil.Gestor ? Perfil.Funcionario : funcionario.Perfil.Value;
                var nomeDoNivel = NomesDeNivel.DoPerfil(perfil);
                var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == nomeDoNivel, ct);
                funcionario.Readmitir(dados.DataAdmissao, dados.CargoId, dados.SetorId, dados.TipoContrato, gestorDoSetor, nivel);
                var vigente = await OperacoesComTrava.TravarVigenteAsync(db, funcionarioId, ct);
                if (vigente is { Situacao: SituacaoDoDesligamento.Efetivado })
                {
                    vigente.Reverter(relogio.AgoraUtc);
                }

                await CadastroDeFuncionarios.SalvarLinhaAsync(db, 0, ct);

                // (3) Os pedidos que esperavam por ela passam ao RH.
                if (setoresDela.Count > 0)
                {
                    await pedidos.PassarParaORhAsync(gestorId!.Value, ct);
                }

                // (4) O login só com o convite: reativa (ou cria) e emite.
                if (dados.EnviarConvite)
                {
                    if (usuario is { Ativo: false })
                    {
                        usuario.Reativar();
                        await db.SaveChangesAsync(ct);
                    }

                    await convites.EmitirAsync([funcionarioId], ct);
                }

                // (5) As caixas "Depois de salvar", sem e-mail.
                if (dados.IncluirNoOnboarding)
                {
                    await onboarding.IncluirAsync([funcionarioId], ct);
                }

                if (dados.FormularioAdmissaoId is { } formularioId)
                {
                    await formulario.AdicionarDestinatariosAsync(formularioId, [funcionarioId], ct);
                }

                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);
    }

    /// <summary>400 por campo (todos juntos); depois 422, na ordem do cadastro: gestor do setor inativo, caixas sem fonte e convite para quem já definiu a senha. Nada é gravado antes.</summary>
    private async Task ConferirAsync(Funcionario funcionario, DadosDaReadmissao dados, Usuario? usuario, CancellationToken ct)
    {
        var problemas = new List<ProblemaDeCadastro>();
        if (dados.DataAdmissao == DateOnly.MinValue)
        {
            problemas.Add(Campo("dataAdmissao", "A data de admissão é obrigatória."));
        }
        else if (funcionario.DataDesligamento is { } ultimoDia && dados.DataAdmissao < ultimoDia)
        {
            problemas.Add(Campo("dataAdmissao", $"A data de admissão não pode ser anterior ao último dia de trabalho do vínculo anterior ({ultimoDia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})."));
        }

        if (!Enum.IsDefined(dados.TipoContrato))
        {
            problemas.Add(Campo("tipoContrato", "Informe o tipo de contrato."));
        }

        if (!await db.Cargos.AsNoTracking().AnyAsync(c => c.Id == dados.CargoId, ct))
        {
            problemas.Add(Campo("cargoId", "O cargo não existe."));
        }

        if (!await db.Setores.AsNoTracking().AnyAsync(s => s.Id == dados.SetorId, ct))
        {
            problemas.Add(Campo("setorId", "O setor não existe."));
        }

        if (problemas.Count > 0)
        {
            throw ProblemasDoCadastro.ParaExcecao(problemas);
        }

        if ((await gestores.ResolverAsync(dados.SetorId, funcionario.Id, ct)).GestorInativo)
        {
            throw new ErroDeNegocioException("GESTOR_DO_SETOR_INATIVO", "O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar.");
        }

        if (dados.IncluirNoOnboarding)
        {
            await onboarding.ConferirAsync(ct);
        }

        if (dados.FormularioAdmissaoId is { } formularioId)
        {
            await formulario.ConferirAsync(formularioId, ct);
        }

        if (usuario is { SenhaProvisoria: false })
        {
            throw new ErroDeNegocioException("SENHA_JA_DEFINIDA", "Esta pessoa já definiu a senha. Desmarque o convite e libere o acesso em Usuários do sistema (Ativar).");
        }
    }

    private static ProblemaDeCadastro Campo(string campo, string mensagem) =>
        new(0, TipoDeProblemaDeCadastro.Campo, campo, ValidacaoDeCamposDoCadastro.CodigoDeCampoInvalido, mensagem);
}
