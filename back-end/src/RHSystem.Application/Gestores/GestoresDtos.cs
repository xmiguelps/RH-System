using System.Text.Json.Serialization;
using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Gestores;

/// <summary>
/// Um gestor (quem tem linha em <c>GESTORES</c>): ou o gestor de um setor (<see cref="Setor"/>, um só), ou a pessoa do RH cadastrada só para decidir os pedidos de gestores
/// (<see cref="SoDecidePedidosDeGestores"/>, sem setor nem equipe). <see cref="Setor"/> e <see cref="AusenteAte"/> são obrigatórios e aceitam <c>null</c> no contrato, então não têm padrão.
/// </summary>
public sealed record GestorItem(
    int GestorId,
    FuncionarioResumo Funcionario,
    SetorReferencia? Setor,
    bool SoDecidePedidosDeGestores,
    int QuantidadeEquipe,
    bool Ausente,
    DateOnly? AusenteAte,
    bool AusenteHoje);

/// <summary>Lista curta, sem paginação, na ordem de <c>gestorId</c>.</summary>
public sealed record GestorListaResposta(IReadOnlyList<GestorItem> Itens);

/// <summary>Cadastra como gestor, sem setor, uma pessoa do RH.</summary>
public sealed record GestorCadastrarComando(int FuncionarioId);

/// <summary>
/// Marca ou desmarca "Gestor ausente". Os dois campos são obrigatórios no contrato (<c>ausenteAte</c> pode ser <c>null</c>) e o System.Text.Json trata campo ausente como o valor padrão
/// (<c>false</c>, <c>null</c>), que aqui seria uma ordem válida de desmarcar: por isso <see cref="JsonRequiredAttribute"/> manda o corpo sem o campo para o 400.
/// </summary>
public sealed record GestorAusenciaComando([property: JsonRequired] bool Ausente, [property: JsonRequired] DateOnly? AusenteAte);

/// <summary>O gestor com a marcação nova mais <c>pedidosAprovadosAgora</c>, que repete o que <c>IPedidosParadosNoGestor.AprovarEtapaDoGestorAsync</c> devolveu.</summary>
public sealed record GestorAusenciaResposta(
    int GestorId,
    FuncionarioResumo Funcionario,
    SetorReferencia? Setor,
    bool SoDecidePedidosDeGestores,
    int QuantidadeEquipe,
    bool Ausente,
    DateOnly? AusenteAte,
    bool AusenteHoje,
    PedidosAprovados PedidosAprovadosAgora);
