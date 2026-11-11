namespace RHSystem.Infrastructure.Acesso;

/// <summary>A seção <c>Senha</c>: a validade dos links de convite e de redefinição e o intervalo mínimo entre dois pedidos de redefinição da mesma pessoa.</summary>
public sealed class OpcoesDeSenha
{
    public const string Secao = "Senha";

    public int ConviteDias { get; set; } = 7;

    public int RedefinicaoMinutos { get; set; } = 60;

    /// <summary>Dentro deste intervalo do último pedido da pessoa, o seguinte devolve o mesmo 202 e não enfileira nada (impede encher a caixa de alguém). Zero desliga.</summary>
    public int IntervaloMinimoEntreRedefinicoesSegundos { get; set; } = 60;

    public void Validar()
    {
        if (ConviteDias < 1)
        {
            throw new InvalidOperationException("Senha:ConviteDias precisa ser pelo menos 1.");
        }

        if (RedefinicaoMinutos < 1)
        {
            throw new InvalidOperationException("Senha:RedefinicaoMinutos precisa ser pelo menos 1.");
        }

        if (IntervaloMinimoEntreRedefinicoesSegundos < 0)
        {
            throw new InvalidOperationException("Senha:IntervaloMinimoEntreRedefinicoesSegundos não pode ser negativo.");
        }
    }
}
