using RHSystem.Application.Documentos;
using RHSystem.Application.Escopo;
using RHSystem.Application.Painel;

namespace RHSystem.Infrastructure.Documentos;

/// <summary>
/// A bolinha <c>documentosPendentes</c> do menu: quantos documentos <c>Pendente</c> (que o RH pediu) a PRÓPRIA pessoa tem, em uma consulta de contagem. Vale para qualquer perfil, sempre com o funcionário do token.
/// </summary>
public sealed class FonteDeDocumentosPendentes(IDocumentosRepositorio documentos) : IFonteDeContadorDoMenu
{
    public ContadorDoMenu Contador => ContadorDoMenu.DocumentosPendentes;

    public Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct) => documentos.ContarPendentesAsync(escopo.FuncionarioId, ct);
}
