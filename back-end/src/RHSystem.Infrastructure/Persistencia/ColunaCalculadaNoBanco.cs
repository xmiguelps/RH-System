using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RHSystem.Infrastructure.Persistencia;

internal static class ColunaCalculadaNoBanco
{
    /// <summary>
    /// Coluna <c>GENERATED ALWAYS AS (...) STORED</c> do MySQL (<c>EM_ABERTO</c>, <c>VIGENTE</c>): o EF só a lê. Fora do <c>INSERT</c> e do
    /// <c>UPDATE</c>, porque o MySQL recusa qualquer valor para ela (erro 3105). O teste de conformidade confere as três configurações.
    /// </summary>
    public static PropertyBuilder<T> CalculadaNoBanco<T>(this PropertyBuilder<T> propriedade)
    {
        propriedade.ValueGeneratedOnAddOrUpdate();
        propriedade.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        propriedade.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        return propriedade;
    }
}
