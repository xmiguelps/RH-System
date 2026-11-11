namespace RHSystem.Application.Colaboradores;

/// <summary>
/// A identificação curta de uma pessoa, para listas e cartões (<c>FuncionarioResumo</c> de <c>00-base.yaml</c>). <see cref="Cargo"/> e <see cref="Setor"/> são opcionais no contrato
/// (os dois nomes da pessoa; <c>null</c> quando ela não tem), por isso têm padrão: sem ele o gerador de OpenAPI os marcaria como obrigatórios. As fatias 3 e 7 a reaproveitam.
/// </summary>
public sealed record FuncionarioResumo(int Id, string Nome, bool Ativo, string? Cargo = null, string? Setor = null);
