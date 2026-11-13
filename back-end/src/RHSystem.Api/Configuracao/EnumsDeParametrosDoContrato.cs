using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RHSystem.Application.Acesso;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comunicacao.Avisos;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Painel;
using RHSystem.Domain.Beneficios;
using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Application.Ponto;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Comunicacao;
using RHSystem.Domain.Formularios;
using RHSystem.Domain.Ponto;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// Os filtros de enum chegam como TEXTO (<c>FiltrosDaListaDeAuditoria.Area</c> é <c>string?</c>) para o caso de uso ler os dois pelo nome do contrato e
/// devolver os erros de <c>area</c> e de <c>acao</c> juntos num 400 só. Por isso o gerador de OpenAPI os descreve como texto livre, e o contrato os
/// descreve como <c>enum</c>. Aqui o parâmetro volta a ter a lista de valores (<c>ACESSO_NEGADO</c>, não <c>AcessoNegado</c>), pelo par
/// <c>operationId</c> + nome do parâmetro. Toda rota nova com filtro de enum acrescenta o par aqui; <c>ContratoDivergenciaTests</c> acusa se esquecer.
/// </summary>
public static class EnumsDeParametrosDoContrato
{
    private static readonly Dictionary<(string Operacao, string Parametro), Type> Enums = new()
    {
        [("listarEventosDeAuditoria", "area")] = typeof(AuditoriaArea),
        [("listarEventosDeAuditoria", "acao")] = typeof(AuditoriaAcao),
        [("exportarAuditoria", "area")] = typeof(AuditoriaArea),
        [("exportarAuditoria", "acao")] = typeof(AuditoriaAcao),
        [("listarUsuariosDoSistema", "situacao")] = typeof(UsuarioSistemaSituacao),
        [("listarFuncionarios", "situacao")] = typeof(FuncionarioSituacao),
        [("listarFuncionarios", "tipoContrato")] = typeof(TipoDeContrato),
        [("listarFuncionarios", "ordenarPor")] = typeof(FuncionarioOrdenarPor),
        [("listarFuncionarios", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("listarMeusPedidosDeAjuste", "status")] = typeof(StatusDoPedidoDeAjuste),
        [("listarMeusPedidosDeAjuste", "etapa")] = typeof(EtapaDoPedido),
        [("listarMeusPedidosDeAjuste", "ordenarPor")] = typeof(PontoPedidoOrdenarPor),
        [("listarMeusPedidosDeAjuste", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("listarPedidosDeAjusteDaEquipe", "status")] = typeof(StatusDoPedidoDeAjuste),
        [("listarPedidosDeAjusteDaEquipe", "etapa")] = typeof(EtapaDoPedido),
        [("listarPedidosDeAjusteDaEquipe", "ordenarPor")] = typeof(PontoPedidoOrdenarPor),
        [("listarPedidosDeAjusteDaEquipe", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("listarPedidosDeAjusteParaRh", "status")] = typeof(StatusDoPedidoDeAjuste),
        [("listarPedidosDeAjusteParaRh", "etapa")] = typeof(EtapaDoPedido),
        [("listarPedidosDeAjusteParaRh", "ordenarPor")] = typeof(PontoPedidoOrdenarPor),
        [("listarPedidosDeAjusteParaRh", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("listarFeriasDaEquipe", "situacao")] = typeof(SituacaoDasFerias),
        [("listarFeriasParaORh", "situacao")] = typeof(SituacaoDasFerias),
        [("listarPedidosDeBeneficio", "situacao")] = typeof(StatusDoPedidoDeBeneficio),
        [("listarPendenciasDoRh", "tipo")] = typeof(PainelPendenciaTipo),
        [("listarMeusAtestados", "situacao")] = typeof(SituacaoDoAtestado),
        [("listarAtestadosDaEquipe", "situacao")] = typeof(SituacaoDoAtestado),
        [("listarAtestados", "situacao")] = typeof(SituacaoDoAtestado),
        [("listarAvisos", "situacao")] = typeof(SituacaoDoAviso),
        [("listarAvisos", "tipo")] = typeof(TipoDeAviso),
        [("listarLeiturasDoAviso", "situacao")] = typeof(FiltroDeLeitura),
        [("listarHoleritesDoFuncionario", "tipo")] = typeof(TipoDeHolerite),
        [("listarMeusHolerites", "tipo")] = typeof(TipoDeHolerite),
        [("listarAdvertencias", "situacao")] = typeof(SituacaoDaAdvertencia),
        [("listarAdvertencias", "tipo")] = typeof(TipoDeAdvertencia),
        [("listarAdvertenciasDaEquipe", "situacao")] = typeof(SituacaoDaAdvertencia),
        [("listarMinhasAdvertencias", "situacao")] = typeof(SituacaoDaAdvertencia),
        [("listarMeusFormularios", "tipo")] = typeof(TipoDeFormulario),
        [("listarMeusFormularios", "situacao")] = typeof(SituacaoMinhaDoFormulario),
        [("listarFormulariosRh", "tipo")] = typeof(TipoDeFormulario),
        [("listarFormulariosRh", "situacao")] = typeof(SituacaoDoFormulario),
        [("listarFormulariosRh", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("listarRespondentesFormulario", "situacao")] = typeof(SituacaoDoRespondente),
        [("contarPublicoDeTreinamento", "publico")] = typeof(PublicoDoTreinamento),
        [("listarTreinamentos", "situacao")] = typeof(SituacaoDoTreinamento),
        [("listarMeusTreinamentos", "aba")] = typeof(AbaDosMeusTreinamentos),
        [("listarParticipantesDeTreinamento", "situacao")] = typeof(SituacaoNoTreinamento),
        [("listarParticipantesDeTreinamento", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("exportarParticipantesDeTreinamento", "situacao")] = typeof(SituacaoNoTreinamento),
        [("listarTreinamentos", "direcao")] = typeof(DirecaoDeOrdenacao),
        [("obterMeuBancoDeHoras", "tipo")] = typeof(TipoDeLancamentoDoBanco),
        [("obterBancoDeHorasDeFuncionario", "tipo")] = typeof(TipoDeLancamentoDoBanco),
    };

    /// <summary>
    /// Os filtros de enum que o contrato declara OBRIGATÓRIOS na consulta. A API os liga como texto anulável (o caso de uso devolve o 400 com <c>errors.&lt;campo&gt;</c> junto com os outros erros, em vez de o binder recusar sozinho); o documento gerado os marca como obrigatórios como o contrato.
    /// </summary>
    private static readonly HashSet<(string Operacao, string Parametro)> Obrigatorios =
    [
        ("contarPublicoDeTreinamento", "publico"),
    ];

    /// <summary>Os valores que o contrato escreve em camelCase (não em MAIÚSCULAS_COM_SUBLINHADO), com o padrão, por <c>operationId</c> e nome do parâmetro.</summary>
    private static readonly Dictionary<(string Operacao, string Parametro), (string[] Valores, string Padrao)> Literais = new()
    {
        [("listarFormulariosRh", "ordenarPor")] = (["criadoEm", "titulo", "prazo"], "criadoEm"),
        [("listarTreinamentos", "ordenarPor")] = (["TITULO", "COMECA_EM"], "COMECA_EM"),
        [("listarParticipantesDeTreinamento", "ordenarPor")] = (["NOME", "SETOR", "SITUACAO", "INICIO", "CONCLUSAO"], "NOME"),
    };

    /// <summary>Tem de vir DEPOIS de <see cref="NomesDeParametrosDoContrato"/>: a chave usa o nome já em camelCase.</summary>
    public static OpenApiOptions UsarEnumsDeParametrosDoContrato(this OpenApiOptions opcoes) =>
        opcoes.AddOperationTransformer((operacao, _, _) =>
        {
            Aplicar(operacao);
            return Task.CompletedTask;
        });

    public static void Aplicar(OpenApiOperation operacao)
    {
        foreach (var parametro in operacao.Parameters ?? [])
        {
            if (parametro is OpenApiParameter { In: ParameterLocation.Query, Name: { } nomeLiteral } literal
                && Literais.TryGetValue((operacao.OperationId ?? string.Empty, nomeLiteral), out var valores))
            {
                literal.Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. valores.Valores.Select(v => (JsonNode)JsonValue.Create(v)!)],
                    Default = JsonValue.Create(valores.Padrao),
                };
                continue;
            }

            if (parametro is OpenApiParameter { In: ParameterLocation.Query, Name: { } nome } concreto
                && Enums.TryGetValue((operacao.OperationId ?? string.Empty, nome), out var tipo))
            {
                concreto.Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. Enum.GetNames(tipo).Select(n => (JsonNode)JsonValue.Create(JsonNamingPolicy.SnakeCaseUpper.ConvertName(n))!)],
                };
                if (Obrigatorios.Contains((operacao.OperationId ?? string.Empty, nome)))
                {
                    concreto.Required = true;
                }
            }
        }
    }
}
