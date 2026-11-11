using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

// Os nomes são os dos schemas de contrato/fontes/20-fatia-1-colaboradores.yaml. São o DTO PRÓPRIO do gestor: nenhum deles tem CPF, RG, e-mail pessoal, endereço, matrícula, benefícios nem o telefone inteiro.

/// <summary>
/// Uma pessoa da lista da equipe. <c>SituacaoHoje</c> nunca é nula (quem está na equipe é ativo e já foi admitido). <c>Cargo</c> e <c>Setor</c> vão por último e com padrão <c>null</c>: o contrato
/// (<c>FuncionarioResumo</c>) os declara anuláveis e NÃO obrigatórios, e o gerador de OpenAPI marca como obrigatório todo parâmetro de construtor sem padrão (o JSON enviado é o mesmo: a chave sai sempre).
/// </summary>
public sealed record FuncionarioEquipeItem(int Id, string Nome, bool Ativo, SituacaoHoje SituacaoHoje, string? Cargo = null, string? Setor = null);

/// <summary>
/// A equipe do gestor (sem paginação, nome de A a Z). <c>Setor</c> é <c>null</c> para o gestor sem setor, que recebe a equipe vazia (total 0, contagens zeradas). <c>ContagensHoje</c> é calculada DESTA lista
/// (a soma dos seis é o <c>Total</c>); <c>DiaUtil</c> vem do calendário da situação de hoje, também para a equipe vazia.
/// </summary>
public sealed record FuncionarioEquipeResposta(SetorReferencia? Setor, int Total, bool DiaUtil, PresencaContagens ContagensHoje, IReadOnlyList<FuncionarioEquipeItem> Itens);

/// <summary>A ficha de uma pessoa da equipe, vista pelo gestor. <c>TelefoneMascarado</c> é <see cref="TelefoneMascarado.Mascarar"/> do telefone (nulo se não dá para mascarar).</summary>
public sealed record FuncionarioFichaGestor(
    int Id,
    string Nome,
    string? Cargo,
    string? Setor,
    bool Ativo,
    SituacaoHoje SituacaoHoje,
    string? EmailCorporativo,
    string? TelefoneMascarado,
    DateOnly DataAdmissao,
    TipoDeContrato? TipoContrato,
    decimal? JornadaSemanalHoras);

/// <summary>Os indicadores da pessoa da equipe: os quatro do RH menos o próximo pagamento, que o gestor não tem. <c>null</c> é "sem fonte".</summary>
public sealed record FuncionarioIndicadoresGestor(int? BancoDeHorasMinutos, int? FeriasDisponiveisDias, int? TreinamentosPendentes, int? AdvertenciasQuantidade);
