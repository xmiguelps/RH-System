using RHSystem.Application.Comum;

namespace RHSystem.Application.Cadastros;

/// <summary>Um erro do apoio: o <c>codigo</c> que o contrato fixa e o <c>detail</c> em português, iguais aos do exemplo da resposta no contrato.</summary>
public sealed record MensagemDoApoio(string Codigo, string Detalhe)
{
    /// <summary>A resposta 409 com este código e este texto.</summary>
    public ConflitoException ComoConflito() => new(Detalhe, Codigo);

    /// <summary>A resposta 422 com este código e este texto.</summary>
    public ErroDeNegocioException ComoErroDeNegocio() => new(Codigo, Detalhe);
}

/// <summary>
/// Os textos e os códigos de erro dos cadastros de apoio, da empresa e dos documentos, <b>copiados dos exemplos do contrato</b> (<c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>) e nunca reescritos: o front mostra o
/// <c>detail</c> como vem. <c>MensagensDoApoioTests</c> lê o contrato de verdade e confere cada um. Os que levam nome (da pessoa, do setor) trocam só o nome.
/// </summary>
public static class MensagensDoApoio
{
    public static MensagemDoApoio NomeJaCadastrado { get; } = new("NOME_JA_CADASTRADO", "Já existe um cadastro com este nome.");

    public static MensagemDoApoio GestorJaCadastrado { get; } = new("GESTOR_JA_CADASTRADO", "Esta pessoa já está cadastrada como gestor.");

    public static MensagemDoApoio GestorSemSetorSoParaRh { get; } =
        new("GESTOR_SEM_SETOR_SO_PARA_RH", "Só quem é do RH é cadastrado como gestor sem setor. Para gerenciar um setor, escolha a pessoa no cadastro de Setores.");

    public static MensagemDoApoio FuncionarioInativo { get; } = new("FUNCIONARIO_INATIVO", "A pessoa escolhida está inativa.");

    public static MensagemDoApoio CadastroEmUso { get; } = new("CADASTRO_EM_USO", "Este benefício já foi dado a colaboradores e não pode ser excluído.");

    public static MensagemDoApoio EmpresaNaoConfigurada { get; } = new("EMPRESA_NAO_CONFIGURADA", "A empresa ainda não foi configurada.");

    public static MensagemDoApoio DocumentoSemArquivo { get; } = new("DOCUMENTO_SEM_ARQUIVO", "Este documento ainda não foi enviado.");

    public static MensagemDoApoio DocumentoNaoPedido { get; } = new("DOCUMENTO_NAO_PEDIDO", "Este documento não está aguardando envio.");

    public static MensagemDoApoio GestorJaRespondePorOutroSetor(string nomeGestor, string nomeSetor) =>
        new("GESTOR_JA_RESPONDE_POR_OUTRO_SETOR", $"{nomeGestor} já é o gestor do setor {nomeSetor}. Tire-o de lá antes de escolhê-lo aqui.");

    public static MensagemDoApoio GestorPrecisaTerPerfilGestor(string nome) =>
        new("GESTOR_PRECISA_TER_PERFIL_GESTOR", $"{nome} tem o nível RH. Só quem tem o nível Gestor pode gerenciar um setor.");

    public static MensagemDoApoio GestorSemEquipe(string nome) =>
        new("GESTOR_SEM_EQUIPE", $"{nome} só decide pedidos de gestores e não tem equipe. Não há etapa do gestor para aprovar sozinha.");
}
