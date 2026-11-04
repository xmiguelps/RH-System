namespace RHSystem.Infrastructure.Agendamento;

/// <summary>A seção <c>Agendador</c>. <c>Ativo</c> é <c>true</c> por padrão e <c>false</c> no <c>ApiFactory</c> (os testes chamam o ciclo direto).</summary>
public sealed class OpcoesDoAgendador
{
    public const string Secao = "Agendador";

    public bool Ativo { get; set; } = true;

    public int IntervaloSegundos { get; set; } = 30;

    public int RecuperacaoMinutos { get; set; } = 60;

    public int LoteMaximoPorCiclo { get; set; } = 10;

    public int ReservaMinutos { get; set; } = 10;

    public int MaximoDeTentativas { get; set; } = 5;

    /// <summary>Configuração errada falha na inicialização, com mensagem clara, e não no primeiro ciclo.</summary>
    public void Validar()
    {
        if (IntervaloSegundos < 1 || RecuperacaoMinutos < 1 || LoteMaximoPorCiclo < 1 || ReservaMinutos < 1 || MaximoDeTentativas < 1)
        {
            throw new InvalidOperationException("Agendador:IntervaloSegundos, RecuperacaoMinutos, LoteMaximoPorCiclo, ReservaMinutos e MaximoDeTentativas precisam ser pelo menos 1.");
        }
    }
}

/// <summary>A seção <c>Retencao</c>: por quantos dias o que já terminou fica nas tabelas técnicas (o endereço de e-mail é dado pessoal).</summary>
public sealed class OpcoesDeRetencao
{
    public const string Secao = "Retencao";

    public int TarefasDias { get; set; } = 30;

    public int EmailsDias { get; set; } = 90;

    public void Validar()
    {
        if (TarefasDias < 1 || EmailsDias < 1)
        {
            throw new InvalidOperationException("Retencao:TarefasDias e Retencao:EmailsDias precisam ser pelo menos 1.");
        }
    }
}
