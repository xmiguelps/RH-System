using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Emails;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;

namespace RHSystem.Application.Formularios.Lembretes;

/// <summary>
/// Quem manda o lembrete e como, para a rota do RH, a do gestor e a tarefa do automático. <b>Com nome</b>: um e-mail por pessoa que ainda falta, com a chave formulário + pessoa + dia (o mesmo limite do manual e do automático: a repetida só entra em <c>ignorados</c>); pessoa inativa ou sem e-mail corporativo também é ignorada.
/// <b>Anônimo</b>: a rota só MARCA o dia (a marca vai na tabela de tarefas, nunca no formulário) e agenda UMA tarefa que resolve quem ainda não participou e enfileira e-mails SIGILOSOS (sem a pessoa na linha nem na chave, em lote só, numa transação, embaralhados pela fila); a rota nunca sabe quantos faltam.
/// </summary>
public sealed class LembretesDoFormulario(IDestinatariosDoFormulario destinatarios, IConsultaDeLembretes pessoas, IFilaDeEmails fila, IAgendador agendador, IUnidadeDeTrabalho unidade)
{
    private const int TamanhoDoLote = 100;

    /// <summary>
    /// Lembra quem falta, com nome (com <paramref name="equipe"/>, só quem é dela: o lembrete do gestor). <paramref name="pedidos"/> vazio = todos que faltam, e <c>ignorados</c> é todo mundo do público que NÃO recebeu agora (quem já respondeu, quem já recebeu hoje, quem não tem e-mail: "3 enviados e 5 ignorados" quando 5 já tinham respondido); com ids, só essas pessoas
    /// (quem já respondeu, não deve responder, está inativa, sem e-mail ou já recebeu hoje conta como ignorado).
    /// </summary>
    public async Task<(int Enviados, int Ignorados)> LembrarComNomeAsync(FormularioLido lido, IReadOnlyList<int> pedidos, DateOnly hoje, IReadOnlyCollection<int>? equipe, CancellationToken ct)
    {
        var quem = await destinatarios.ObterAsync(lido.Id, hoje, ct) ?? throw new InvalidOperationException("O formulário não tem público calculável.");
        if (equipe is not null)
        {
            quem = quem with { Pares = [.. quem.Pares.Where(p => equipe.Contains(p.PessoaId))] }; // o gestor só lembra a equipe dele
        }

        var faltam = quem.QuemFalta(await pessoas.JaResponderamAsync(lido.Id, false, ct)).Select(p => p.PessoaId).Distinct().ToList();
        var distintos = pedidos.Distinct().ToList();
        var alvo = distintos.Count == 0 ? faltam : [.. distintos.Where(faltam.Contains)];
        var ignorados = distintos.Count == 0 ? 0 : distintos.Count - alvo.Count;

        var podem = await pessoas.PodemReceberAsync(alvo, ct);
        ignorados += alvo.Count - podem.Count; // inativa ou sem e-mail corporativo
        var enfileirados = 0;
        foreach (var lote in podem.Chunk(TamanhoDoLote))
        {
            enfileirados += await fila.EnfileirarLoteAsync([.. lote.Select(p => new EmailParaEnfileirar(RegrasDoLembrete.TipoDoEmail, RegrasDoLembrete.ChaveDoEmail(lido.Id, p.Id, hoje), p.Email, Carga(lido, p), FuncionarioId: p.Id, ReferenciaId: lido.Id))], ct);
        }

        return distintos.Count == 0
            ? (enfileirados, quem.Pares.Select(p => p.PessoaId).Distinct().Count() - enfileirados)
            : (enfileirados, ignorados + (podem.Count - enfileirados));
    }

    /// <summary>
    /// A parte da ROTA no anônimo: marca "já saiu lembrete hoje" (<c>CONCLUIDA</c>) e agenda a tarefa que envia. Se a marca já existia (manual ou automático de hoje) não agenda de novo. Não devolve nada: a resposta é a mesma sempre.
    /// </summary>
    public async Task MarcarEAgendarSigilosoAsync(FormularioLido lido, DateOnly hoje, DateTime agoraUtc, int? setorId, CancellationToken ct)
    {
        var alcance = setorId is { } setor ? RegrasDoLembrete.AlcanceDoSetor(setor) : RegrasDoLembrete.AlcanceTodos;
        if (await agendador.RegistrarComoConcluidaAsync(RegrasDoLembrete.TipoDaTarefa, RegrasDoLembrete.ChaveDaMarca(lido.Id, hoje, alcance), hoje, ct))
        {
            _ = await agendador.AgendarAsync(new NovaTarefa(RegrasDoLembrete.TipoDaTarefa, RegrasDoLembrete.ChaveDoEnvio(lido.Id, hoje, alcance), agoraUtc, lido.Id, hoje, setorId is null ? null : new { setorId }), ct);
        }
    }

    /// <summary>A parte da TAREFA no anônimo: quem ainda não participou (ativo e com e-mail) recebe um e-mail sigiloso, tudo numa transação. Devolve quantos entraram na fila (só a tarefa sabe; nada o mostra).</summary>
    public async Task<int> EnviarSigilosoAsync(FormularioLido lido, DateOnly hoje, IReadOnlyCollection<int>? equipe, CancellationToken ct)
    {
        var quem = await destinatarios.ObterAsync(lido.Id, hoje, ct) ?? throw new InvalidOperationException("O formulário não tem público calculável.");
        if (equipe is not null)
        {
            quem = quem with { Pares = [.. quem.Pares.Where(p => equipe.Contains(p.PessoaId))] };
        }

        var faltam = quem.QuemFalta(await pessoas.JaResponderamAsync(lido.Id, true, ct)).Select(p => p.PessoaId).Distinct().ToList();
        var podem = await pessoas.PodemReceberAsync(faltam, ct);
        if (podem.Count == 0)
        {
            return 0;
        }

        var emails = podem.Select(p => new EmailParaEnfileirar(RegrasDoLembrete.TipoDoEmail, RegrasDoLembrete.ChaveDoEmailSigiloso(lido.Id, hoje, Guid.NewGuid()), p.Email, Carga(lido, p), FuncionarioId: null, ReferenciaId: lido.Id, Sigiloso: true)).ToList();
        return await unidade.ExecutarEmTransacaoAsync(() => fila.EnfileirarLoteAsync(emails, ct), ct);
    }

    private static object Carga(FormularioLido lido, PessoaParaLembrar pessoa) =>
        new { nome = pessoa.Nome, titulo = lido.Titulo, prazo = lido.Prazo is { } prazo ? prazo.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null };
}
