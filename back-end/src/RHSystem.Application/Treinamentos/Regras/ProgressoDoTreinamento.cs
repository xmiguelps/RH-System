using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>Um módulo como o progresso o lê: a ordem, a duração (se tem) e se a pessoa o concluiu.</summary>
public sealed record ModuloParaProgresso(int Ordem, short? DuracaoMinutos, bool Concluido);

/// <summary>
/// O progresso da pessoa: <c>Percentual</c> é o PISO de concluídos sobre o total (0 a 100), <c>ModuloAtual</c> é a ordem do primeiro módulo ainda não concluído (<c>null</c> quando todos foram), <c>DuracaoTotalMinutos</c> soma só os módulos que têm duração (<c>null</c> se nenhum tem) e
/// <c>TudoConcluido</c> só é verdadeiro com pelo menos um módulo e todos concluídos (é o que conclui o treinamento).
/// </summary>
public sealed record ProgressoCalculado(int Total, int Concluidos, int Percentual, int? ModuloAtual, int? DuracaoTotalMinutos, bool TudoConcluido);

public static class ProgressoDoTreinamento
{
    public static ProgressoCalculado Calcular(IReadOnlyList<ModuloParaProgresso> modulos)
    {
        var total = modulos.Count;
        var concluidos = modulos.Count(m => m.Concluido);
        int? atual = modulos.Where(m => !m.Concluido).OrderBy(m => m.Ordem).Select(m => (int?)m.Ordem).FirstOrDefault();
        var comDuracao = modulos.Where(m => m.DuracaoMinutos is not null).ToList();
        int? duracao = comDuracao.Count == 0 ? null : comDuracao.Sum(m => (int)m.DuracaoMinutos!.Value);
        return new ProgressoCalculado(total, concluidos, total == 0 ? 0 : (int)((long)concluidos * 100 / total), atual, duracao, total > 0 && concluidos == total);
    }
}

/// <summary>A situação como a PESSOA a vê (<c>TreinamentoMinhaSituacao</c>): <c>DISPONIVEL</c> = opcional do público dela que ela ainda não escolheu (sem linha); <c>NAO_INICIADO</c> = linha <c>INSCRITO</c>. Não há valor "encerrado".</summary>
public enum MinhaSituacaoNoTreinamento
{
    Disponivel,
    NaoIniciado,
    EmAndamento,
    Concluido,
}

public static class MinhaSituacao
{
    public static MinhaSituacaoNoTreinamento De(SituacaoNoTreinamento? linha) => linha switch
    {
        null => MinhaSituacaoNoTreinamento.Disponivel,
        SituacaoNoTreinamento.Inscrito => MinhaSituacaoNoTreinamento.NaoIniciado,
        SituacaoNoTreinamento.EmAndamento => MinhaSituacaoNoTreinamento.EmAndamento,
        _ => MinhaSituacaoNoTreinamento.Concluido,
    };
}

/// <summary>As abas de "meus treinamentos" (<c>TreinamentoAbaMeus</c>): <c>TODOS</c> é tudo que NÃO foi concluído; <c>DISPONIVEIS</c> = disponível + não iniciado.</summary>
public enum AbaDosMeusTreinamentos
{
    Todos,
    Disponiveis,
    EmAndamento,
    Concluidos,
}

/// <summary>As contagens das abas (<c>todos = disponiveis + emAndamento</c>: o concluído não entra no "todos").</summary>
public sealed record ContagensDosMeusTreinamentos(int Todos, int Disponiveis, int EmAndamento, int Concluidos);

public static class AbasDosMeusTreinamentos
{
    public static bool Pertence(AbaDosMeusTreinamentos aba, MinhaSituacaoNoTreinamento situacao) => aba switch
    {
        AbaDosMeusTreinamentos.Todos => situacao != MinhaSituacaoNoTreinamento.Concluido,
        AbaDosMeusTreinamentos.Disponiveis => situacao is MinhaSituacaoNoTreinamento.Disponivel or MinhaSituacaoNoTreinamento.NaoIniciado,
        AbaDosMeusTreinamentos.EmAndamento => situacao == MinhaSituacaoNoTreinamento.EmAndamento,
        _ => situacao == MinhaSituacaoNoTreinamento.Concluido,
    };

    public static ContagensDosMeusTreinamentos Contar(IEnumerable<MinhaSituacaoNoTreinamento> situacoes)
    {
        var lista = situacoes.ToList();
        return new ContagensDosMeusTreinamentos(
            lista.Count(s => Pertence(AbaDosMeusTreinamentos.Todos, s)),
            lista.Count(s => Pertence(AbaDosMeusTreinamentos.Disponiveis, s)),
            lista.Count(s => Pertence(AbaDosMeusTreinamentos.EmAndamento, s)),
            lista.Count(s => Pertence(AbaDosMeusTreinamentos.Concluidos, s)));
    }
}

public static class VisibilidadeParaAPessoa
{
    /// <summary>
    /// O que a pessoa pode ver: publicado, com a data de início já chegada e (com linha, ou opcional do público dela); se o treinamento está encerrado, só com linha <c>EM_ANDAMENTO</c> ou <c>CONCLUIDO</c> (quem nunca começou perde o acesso). Fora disso é 404.
    /// </summary>
    public static bool Visivel(bool publicado, DateOnly? comecaEm, DateTime? encerradoEmUtc, DateOnly hoje, SituacaoNoTreinamento? linha, bool opcionalDoPublicoDela)
    {
        if (!RegraDaSituacaoDoTreinamento.Liberado(publicado, comecaEm, hoje) || (linha is null && !opcionalDoPublicoDela))
        {
            return false;
        }

        return encerradoEmUtc is null || linha is SituacaoNoTreinamento.EmAndamento or SituacaoNoTreinamento.Concluido;
    }
}
