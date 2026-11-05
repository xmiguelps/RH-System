using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Acesso;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Acesso;

/// <summary>
/// Emite o link de convite e o de redefinição. As datas vêm do <see cref="IRelogio"/>, nunca do padrão do banco. O token em claro só vai para a carga do e-mail; o banco guarda o hash.
/// <para>
/// <b>Convite</b> (<see cref="EmitirAsync"/>): a linha de <c>FUNCIONARIOS</c> é travada (<c>FOR UPDATE</c>, em ordem de id) e é isso que serializa dois convites da mesma pessoa: o segundo espera,
/// vê o login e o link que o primeiro gravou, invalida o link e emite o seu. Gravação em DUAS etapas (logins e invalidações; depois os tokens), porque o EF pode mandar o <c>INSERT</c> antes do
/// <c>UPDATE</c> e a chave única de link em aberto <c>(ID_USUARIO, TIPO, EM_ABERTO)</c> o recusaria.
/// </para>
/// <para>
/// <b>Redefinição</b> (<see cref="SolicitarRedefinicaoAsync"/>): nunca revela se o e-mail existe nem lança por ele. O evento sai por <c>RegistrarComo</c> (a pessoa como ator) na mesma conexão e
/// transação do fato, e nunca por <c>RegistrarEGravar*</c>: essas abririam uma segunda conexão que esperaria a trava da linha da pessoa (o evento tem chave estrangeira para ela).
/// </para>
/// </summary>
public sealed class ServicoDeConvite(
    AppDbContext db,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio,
    IGeradorDeTokenDeSenha gerador,
    IFilaDeEmails fila,
    IAuditor auditor,
    OpcoesDeSenha opcoes) : IServicoDeConvite
{
    private const int MaximoPorChamada = 500;

    public const string TipoDoEmailDeConvite = "CONVITE";

    public const string TipoDoEmailDeRedefinicao = "REDEFINICAO_SENHA";

    public async Task<IReadOnlyList<ConviteEmitido>> EmitirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(funcionarioIds);
        var ids = funcionarioIds.Distinct().Order().ToList();
        if (ids.Count is < 1 or > MaximoPorChamada)
        {
            throw new ArgumentOutOfRangeException(nameof(funcionarioIds), ids.Count, $"Um convite vale de 1 a {MaximoPorChamada} pessoas por chamada.");
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Emitir convites exige uma transação aberta (a trava das pessoas e as duas gravações só são seguras dentro dela).");
        }

        var agora = relogio.AgoraUtc;

        // Trava as pessoas, em ordem de id. Os ids são inteiros que o chamador já validou, mas vão como parâmetros do mesmo jeito.
        var marcadores = string.Join(",", ids.Select((_, i) => "{" + i + "}"));
        var pessoas = await db.Funcionarios
            .FromSqlRaw("SELECT * FROM FUNCIONARIOS WHERE ID_FUNCIONARIO IN (" + marcadores + ") ORDER BY ID_FUNCIONARIO FOR UPDATE", [.. ids.Cast<object>()])
            .ToListAsync(ct);
        if (pessoas.Count != ids.Count)
        {
            throw new NaoEncontradoException("Uma das pessoas não foi encontrada.");
        }

        var usuarios = await db.Usuarios.Where(u => ids.Contains(u.FuncionarioId)).ToDictionaryAsync(u => u.FuncionarioId, ct);

        // As quatro recusas do contrato, nesta ordem; a primeira que aparecer desfaz o lote todo (quem chamou está numa transação).
        foreach (var pessoa in pessoas)
        {
            usuarios.TryGetValue(pessoa.Id, out var existente);
            if (!pessoa.Ativo)
            {
                throw new ErroDeNegocioException("FUNCIONARIO_INATIVO", "Não é possível enviar convite a um colaborador inativo.");
            }

            if (string.IsNullOrWhiteSpace(pessoa.EmailCorporativo))
            {
                throw new ErroDeNegocioException("FUNCIONARIO_SEM_EMAIL_CORPORATIVO", "Esta pessoa não tem e-mail corporativo cadastrado. Edite o cadastro antes de enviar o convite.");
            }

            if (existente is { SenhaProvisoria: false })
            {
                throw new ErroDeNegocioException("SENHA_JA_DEFINIDA", "Esta pessoa já definiu a senha. Se ela esqueceu, use \"Esqueci minha senha\" na tela de entrada.");
            }

            if (existente is { Ativo: false })
            {
                throw new ErroDeNegocioException("USUARIO_DESATIVADO", "O login desta pessoa está desativado. Libere o acesso em Usuários do sistema (Ativar) antes de enviar o convite.");
            }
        }

        // Primeira gravação: o login que falta e a invalidação do convite em aberto.
        var novos = new HashSet<int>();
        foreach (var pessoa in pessoas.Where(p => !usuarios.ContainsKey(p.Id)))
        {
            var usuario = Usuario.Criar(pessoa, HashProvisorio.Gerar(), senhaProvisoria: true);
            db.Usuarios.Add(usuario);
            usuarios[pessoa.Id] = usuario;
            novos.Add(pessoa.Id);
        }

        var idsDosLoginsQueJaExistiam = usuarios.Values.Where(u => u.Id != 0).Select(u => u.Id).ToList();
        await InvalidarEmAbertoAsync(idsDosLoginsQueJaExistiam, TipoDeTokenDeSenha.Convite, agora, ct);
        await db.SaveChangesAsync(ct);

        // Segunda gravação: os links novos (só o hash) e o e-mail de cada um.
        var validade = TimeSpan.FromDays(opcoes.ConviteDias);
        var gerados = new List<(Funcionario Pessoa, Usuario Usuario, TokenDeSenhaGerado Token, UsuarioTokenSenha Link)>();
        foreach (var pessoa in pessoas)
        {
            var usuario = usuarios[pessoa.Id];
            var token = gerador.Novo();
            var link = UsuarioTokenSenha.Criar(usuario.Id, TipoDeTokenDeSenha.Convite, token.Hash, agora, validade);
            db.UsuariosTokensSenha.Add(link);
            gerados.Add((pessoa, usuario, token, link));
        }

        await db.SaveChangesAsync(ct);

        foreach (var (pessoa, usuario, token, link) in gerados)
        {
            auditor.Registrar(new EventoDeAuditoriaNovo(
                AuditoriaArea.Acesso,
                novos.Contains(pessoa.Id) ? AuditoriaAcao.Criou : AuditoriaAcao.Alterou,
                novos.Contains(pessoa.Id) ? "Login criado e convite enviado" : "Convite de acesso reenviado",
                $"Colaborador {pessoa.Nome}",
                "USUARIOS",
                usuario.Id));
        }

        await fila.EnfileirarLoteAsync(
            [.. gerados.Select(g => new EmailParaEnfileirar(
                TipoDoEmailDeConvite,
                ChaveDeIdempotencia.Montar(TipoDoEmailDeConvite, ("func", g.Pessoa.Id), ("token", g.Link.Id)),
                g.Pessoa.EmailCorporativo!,
                new { nome = g.Pessoa.Nome, token = g.Token.Token, expiraEmUtc = g.Link.ExpiraEm },
                g.Pessoa.Id,
                g.Link.Id))],
            ct);
        await db.SaveChangesAsync(ct); // os eventos de auditoria que ainda estavam pendentes

        return [.. gerados.Select(g => new ConviteEmitido(g.Pessoa.Id, g.Usuario.Id, novos.Contains(g.Pessoa.Id), g.Link.ExpiraEm))];
    }

    public async Task SolicitarRedefinicaoAsync(string emailNormalizado, CancellationToken ct)
    {
        var email = (emailNormalizado ?? string.Empty).Trim().ToLowerInvariant();
        if (email.Length is 0 or > 150)
        {
            return; // nem chega ao banco (a rota já devolveu 400 para vazio e sem formato; aqui só se protege)
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var agora = relogio.AgoraUtc;

                // Acha e trava a pessoa de uma vez: ativa e com este e-mail. É a trava que serializa dois pedidos da mesma pessoa.
                var pessoa = (await db.Funcionarios
                    .FromSql($"SELECT * FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = {email} AND ATIVO = TRUE LIMIT 1 FOR UPDATE")
                    .ToListAsync(ct)).FirstOrDefault();
                if (pessoa is null)
                {
                    return 0;
                }

                var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.FuncionarioId == pessoa.Id, ct);
                if (usuario is null || !usuario.Ativo)
                {
                    return 0;
                }

                var intervalo = TimeSpan.FromSeconds(opcoes.IntervaloMinimoEntreRedefinicoesSegundos);
                var ultimo = await db.UsuariosTokensSenha
                    .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoDeTokenDeSenha.Redefinicao)
                    .OrderByDescending(t => t.CriadoEm)
                    .FirstOrDefaultAsync(ct);
                if (ultimo is not null && agora < ultimo.CriadoEm + intervalo)
                {
                    return 0; // o mesmo 202 e nada novo: não se enche a caixa de alguém a partir de vários IPs
                }

                await InvalidarEmAbertoAsync([usuario.Id], TipoDeTokenDeSenha.Redefinicao, agora, ct);
                await db.SaveChangesAsync(ct);

                var token = gerador.Novo();
                var link = UsuarioTokenSenha.Criar(usuario.Id, TipoDeTokenDeSenha.Redefinicao, token.Hash, agora, TimeSpan.FromMinutes(opcoes.RedefinicaoMinutos));
                db.UsuariosTokensSenha.Add(link);
                await db.SaveChangesAsync(ct);

                await db.Entry(pessoa).Reference(f => f.NivelDeAcesso).LoadAsync(ct);
                auditor.RegistrarComo(
                    new AtorDoEvento(pessoa.Id, pessoa.Nome, pessoa.Perfil),
                    new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Alterou, "Redefinição de senha solicitada", $"Colaborador {pessoa.Nome}", "USUARIOS", usuario.Id));
                await fila.EnfileirarAsync(
                    new EmailParaEnfileirar(
                        TipoDoEmailDeRedefinicao,
                        ChaveDeIdempotencia.Montar(TipoDoEmailDeRedefinicao, ("func", pessoa.Id), ("token", link.Id)),
                        pessoa.EmailCorporativo!,
                        new { nome = pessoa.Nome, token = token.Token, expiraEmUtc = link.ExpiraEm },
                        pessoa.Id,
                        link.Id),
                    ct);
                await db.SaveChangesAsync(ct); // o evento de auditoria

                return 0;
            },
            ct);
    }

    /// <summary>Invalida os links em aberto do tipo dado (o UPDATE sai antes do INSERT do link novo: gravações separadas).</summary>
    private async Task InvalidarEmAbertoAsync(IReadOnlyCollection<int> usuarioIds, TipoDeTokenDeSenha tipo, DateTime agora, CancellationToken ct)
    {
        if (usuarioIds.Count == 0)
        {
            return;
        }

        var abertos = await db.UsuariosTokensSenha
            .Where(t => usuarioIds.Contains(t.UsuarioId) && t.Tipo == tipo && t.UsadoEm == null && t.InvalidadoEm == null)
            .ToListAsync(ct);
        foreach (var aberto in abertos)
        {
            aberto.Invalidar(agora);
        }
    }
}
