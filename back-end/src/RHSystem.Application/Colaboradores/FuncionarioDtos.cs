using RHSystem.Application.Acesso;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

// Os nomes são os dos schemas de contrato/fontes/20-fatia-1-colaboradores.yaml; propriedades em PascalCase, JSON em camelCase.

/// <summary>A aba da lista e a situação de cada linha. No JSON: <c>ATIVO</c>, <c>EM_ADMISSAO</c>, <c>INATIVO</c>.</summary>
public enum FuncionarioSituacao
{
    Ativo,
    EmAdmissao,
    Inativo,
}

public enum FuncionarioDesligamentoSituacao
{
    Agendado,
    Efetivado,
}

/// <summary>As colunas de ordenação de <c>listarFuncionarios</c>. No JSON: <c>NOME</c> (padrão) e <c>DATA_ADMISSAO</c>.</summary>
public enum FuncionarioOrdenarPor
{
    Nome,
    DataAdmissao,
}

public sealed record CargoReferencia(int Id, string Nome);

public sealed record SetorReferencia(int Id, string Nome);

/// <summary>O gestor de uma pessoa: <c>GestorId</c> é o de <c>GESTORES</c>, <c>FuncionarioId</c> é o da pessoa que gere. A 1c também a usa.</summary>
public sealed record GestorReferencia(int GestorId, int FuncionarioId, string Nome);

public sealed record FuncionarioEndereco(string? Cep, string? Logradouro, string? Numero, string? Complemento, string? Bairro, string? Cidade, string? Uf);

/// <summary>Um benefício ativo da pessoa. <c>QuantidadeDependentes</c> é 0 até a fatia 7.</summary>
public sealed record FuncionarioBeneficioItem(int BeneficioId, string Nome, int QuantidadeDependentes);

public sealed record FuncionarioDesligamento(DateOnly UltimoDiaDeTrabalho, FuncionarioDesligamentoSituacao Situacao);

public sealed record FuncionarioAcesso(UsuarioSistemaSituacao Situacao, bool Ativo);

/// <summary>A ficha completa do colaborador, só para o RH (CPF e telefone sem máscara). Quem foi criado antes da fatia 1 (o administrador do seed) vem sem dados pessoais: os campos são <c>null</c> e o <c>Endereco</c> traz os sete campos <c>null</c>.</summary>
public sealed record FuncionarioFichaRh(
    int Id,
    string? Matricula,
    string Nome,
    string? EmailCorporativo,
    string? Cpf,
    string? Telefone,
    DateOnly? DataNascimento,
    string? Rg,
    string? RgOrgaoEmissor,
    string? EmailPessoal,
    FuncionarioEndereco Endereco,
    CargoReferencia? Cargo,
    SetorReferencia? Setor,
    GestorReferencia? Gestor,
    TipoDeContrato? TipoContrato,
    decimal? JornadaSemanalHoras,
    DateOnly DataAdmissao,
    Perfil? Perfil,
    bool Ativo,
    FuncionarioSituacao Situacao,
    SituacaoHoje? SituacaoHoje,
    FuncionarioDesligamento? Desligamento,
    IReadOnlyList<FuncionarioBeneficioItem> Beneficios,
    FuncionarioAcesso Acesso);

public sealed record FuncionarioListaItemRh(
    int Id,
    string Nome,
    string? EmailCorporativo,
    string? Cargo,
    string? Setor,
    DateOnly DataAdmissao,
    FuncionarioSituacao Situacao,
    SituacaoHoje? SituacaoHoje);

/// <summary>Os totais da empresa inteira: ignoram todos os filtros da lista.</summary>
public sealed record FuncionarioListaTotais(int Ativos, int EmAdmissao, int Inativos);

public sealed record FuncionarioListaRespostaRh(IReadOnlyList<FuncionarioListaItemRh> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, FuncionarioListaTotais Totais)
    : RespostaPaginada<FuncionarioListaItemRh>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);

/// <summary>Cada campo é o primeiro valor das fontes registradas para ele; sem fonte, <c>null</c> (a tela diz que ainda não há dado, e nunca mostra um zero inventado).</summary>
public sealed record FuncionarioIndicadoresRh(int? BancoDeHorasMinutos, int? FeriasDisponiveisDias, int? AdvertenciasQuantidade, DateOnly? ProximoPagamento);
