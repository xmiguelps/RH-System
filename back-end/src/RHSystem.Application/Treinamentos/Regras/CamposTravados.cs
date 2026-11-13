namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>O que a edição de um treinamento pode fazer (<c>atualizarTreinamento</c>).</summary>
public enum ResultadoDaEdicao
{
    Permitida,

    /// <summary>422 <c>TREINAMENTO_CAMPO_TRAVADO</c>: o corpo muda um campo que depois de publicado não muda.</summary>
    CampoTravado,

    /// <summary>422 <c>TREINAMENTO_ENCERRADO</c>: encerrado não muda mais nada.</summary>
    Encerrado,
}

/// <summary>
/// O que muda depois de publicado. Rascunho: tudo. Publicado: só título, descrição, a caixa de incluir quem for admitido e, enquanto AGENDADO, a data de início; os demais campos (obrigatoriedade, onboarding, certificado, público e as duas listas, estas comparadas como CONJUNTOS) precisam vir com o valor atual.
/// Encerrado: nada.
/// </summary>
public static class CamposTravados
{
    public static ResultadoDaEdicao Decidir(SituacaoDoTreinamento situacao, CamposDoTreinamento atual, CamposDoTreinamento novo)
    {
        if (situacao == SituacaoDoTreinamento.Encerrado)
        {
            return ResultadoDaEdicao.Encerrado;
        }

        if (situacao == SituacaoDoTreinamento.Rascunho)
        {
            return ResultadoDaEdicao.Permitida;
        }

        var igual = atual.Obrigatorio == novo.Obrigatorio
            && atual.Onboarding == novo.Onboarding
            && atual.EmiteCertificado == novo.EmiteCertificado
            && atual.Publico == novo.Publico
            && atual.SetorIds.ToHashSet().SetEquals(novo.SetorIds)
            && atual.FuncionarioIds.ToHashSet().SetEquals(novo.FuncionarioIds)
            && (situacao == SituacaoDoTreinamento.Agendado || atual.ComecaEm == novo.ComecaEm);
        return igual ? ResultadoDaEdicao.Permitida : ResultadoDaEdicao.CampoTravado;
    }
}
