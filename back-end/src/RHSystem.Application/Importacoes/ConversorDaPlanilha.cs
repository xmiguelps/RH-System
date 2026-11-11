using System.Globalization;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// O que uma linha da planilha vira: o <see cref="NovoFuncionario"/> e os erros de conversão, na ordem das colunas. A coluna que não converteu deixa o valor sentinela no <see cref="NovoFuncionario"/>
/// (id 0, data e enums <c>default</c>): o cadastro o recusa com a mensagem do campo, e o montador da prévia descarta essa mensagem, que repetiria o erro de conversão da mesma coluna.
/// </summary>
public sealed record ConversaoDaLinha(NovoFuncionario Novo, IReadOnlyList<ImportacaoLinhaErro> Erros);

/// <summary>
/// Texto da planilha para <see cref="NovoFuncionario"/> (decisões 7 e 8 do plano da fatia 2). Só acha o que é da PLANILHA: coluna vazia, data que não existe, nome que não está no catálogo. Formato de CPF, telefone e
/// e-mail, tamanho do nome e duplicidade são do cadastro (<see cref="ICadastroDeFuncionarios.ValidarAsync"/>).
/// </summary>
public static class ConversorDaPlanilha
{
    private const string ColunaVazia = "Preencha esta coluna.";

    // As chaves são NomesNormalizados.Chave dos nomes que o modelo mostra ("CLT", "Estágio"; "Colaborador", "Gestor", "RH").
    private static readonly Dictionary<string, TipoDeContrato> TiposPorChave = new() { ["CLT"] = TipoDeContrato.Clt, ["ESTAGIO"] = TipoDeContrato.Estagio };

    private static readonly Dictionary<string, Perfil> PerfisPorChave = new() { ["COLABORADOR"] = Perfil.Funcionario, ["GESTOR"] = Perfil.Gestor, ["RH"] = Perfil.RH };

    public static ConversaoDaLinha Converter(ImportacaoLinhaValores valores, CatalogoDaImportacao catalogo)
    {
        var erros = new List<ImportacaoLinhaErro>();
        var cargos = Indice(catalogo.Cargos);
        var setores = Indice(catalogo.Setores);
        var beneficios = Indice(catalogo.Beneficios);

        // A ordem das chamadas é a ordem das colunas: os erros já saem ordenados.
        var nome = Obrigatorio(ColunaDaImportacao.Nome, valores.Nome);
        var cpf = Obrigatorio(ColunaDaImportacao.Cpf, valores.Cpf);
        var nascimento = Data(ColunaDaImportacao.DataNascimento, valores.DataNascimento);
        var telefone = Obrigatorio(ColunaDaImportacao.Telefone, valores.Telefone);
        var email = Obrigatorio(ColunaDaImportacao.EmailCorporativo, valores.EmailCorporativo);
        var admissao = Data(ColunaDaImportacao.DataAdmissao, valores.DataAdmissao);
        var tipo = Escolha(ColunaDaImportacao.TipoContrato, valores.TipoContrato, TiposPorChave, "tipo de contrato", "Use CLT ou Estágio.");
        var cargo = PeloNome(ColunaDaImportacao.Cargo, valores.Cargo, cargos, "cargo");
        var setor = PeloNome(ColunaDaImportacao.Setor, valores.Setor, setores, "setor");
        var perfil = Escolha(ColunaDaImportacao.NivelDeAcesso, valores.NivelDeAcesso, PerfisPorChave, "nível de acesso", "Use Colaborador, Gestor ou RH.");
        var beneficioIds = Beneficios(valores.Beneficios);

        var novo = new NovoFuncionario(
            nome ?? string.Empty,
            cpf is null ? string.Empty : Cpf.Normalizar(cpf),
            nascimento,
            telefone is null ? string.Empty : TelefoneMascarado.SoDigitos(telefone) ?? string.Empty,
            email ?? string.Empty,
            admissao,
            tipo,
            cargo,
            setor,
            perfil,
            beneficioIds);
        return new ConversaoDaLinha(novo, erros);

        string? Obrigatorio(ColunaDaImportacao coluna, string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                erros.Add(new ImportacaoLinhaErro(coluna, ColunaVazia));
                return null;
            }

            return texto.Trim();
        }

        DateOnly Data(ColunaDaImportacao coluna, string? texto)
        {
            var valor = Obrigatorio(coluna, texto);
            if (valor is null)
            {
                return default;
            }

            if (DateOnly.TryParseExact(valor, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                return data;
            }

            erros.Add(new ImportacaoLinhaErro(coluna, $"A data \"{valor}\" não é válida. Use dd/mm/aaaa."));
            return default;
        }

        TValor Escolha<TValor>(ColunaDaImportacao coluna, string? texto, Dictionary<string, TValor> porChave, string rotulo, string dica)
            where TValor : struct
        {
            var valor = Obrigatorio(coluna, texto);
            if (valor is null)
            {
                return default;
            }

            if (porChave.TryGetValue(NomesNormalizados.Chave(valor), out var escolhido))
            {
                return escolhido;
            }

            erros.Add(new ImportacaoLinhaErro(coluna, $"O {rotulo} \"{valor}\" não existe. {dica}"));
            return default;
        }

        int PeloNome(ColunaDaImportacao coluna, string? texto, Dictionary<string, int> indice, string rotulo)
        {
            var valor = Obrigatorio(coluna, texto);
            if (valor is null)
            {
                return 0;
            }

            if (indice.TryGetValue(NomesNormalizados.Chave(valor), out var id))
            {
                return id;
            }

            erros.Add(new ImportacaoLinhaErro(coluna, NaoExiste(rotulo, valor)));
            return 0;
        }

        List<int> Beneficios(string? texto)
        {
            var ids = new List<int>();
            var vistos = new HashSet<string>();
            foreach (var parte in (texto ?? string.Empty).Split(';'))
            {
                var nomeDoBeneficio = parte.Trim();
                var chave = NomesNormalizados.Chave(nomeDoBeneficio);
                if (nomeDoBeneficio.Length == 0 || !vistos.Add(chave))
                {
                    continue;
                }

                if (beneficios.TryGetValue(chave, out var id))
                {
                    ids.Add(id);
                }
                else
                {
                    erros.Add(new ImportacaoLinhaErro(ColunaDaImportacao.Beneficios, NaoExiste("benefício", nomeDoBeneficio)));
                }
            }

            return ids;
        }
    }

    /// <summary>A coluna da planilha de cada campo do cadastro (<see cref="ProblemaDeCadastro.Campo"/>, em camelCase).</summary>
    /// <exception cref="InvalidOperationException">O campo não tem coluna (um campo novo no cadastro sem coluna no modelo é defeito do código, não do RH).</exception>
    public static ColunaDaImportacao ColunaDoCampo(string campoDoCadastro) => campoDoCadastro switch
    {
        "nome" => ColunaDaImportacao.Nome,
        "cpf" => ColunaDaImportacao.Cpf,
        "dataNascimento" => ColunaDaImportacao.DataNascimento,
        "telefone" => ColunaDaImportacao.Telefone,
        "emailCorporativo" => ColunaDaImportacao.EmailCorporativo,
        "dataAdmissao" => ColunaDaImportacao.DataAdmissao,
        "tipoContrato" => ColunaDaImportacao.TipoContrato,
        "cargoId" => ColunaDaImportacao.Cargo,
        "setorId" => ColunaDaImportacao.Setor,
        "perfil" => ColunaDaImportacao.NivelDeAcesso,
        "beneficioIds" => ColunaDaImportacao.Beneficios,
        _ => throw new InvalidOperationException($"O campo \"{campoDoCadastro}\" do cadastro não tem coluna na planilha."),
    };

    private static string NaoExiste(string rotulo, string valor) => $"O {rotulo} \"{valor}\" não existe. Use um {rotulo} da aba Valores válidos.";

    private static Dictionary<string, int> Indice(IReadOnlyList<ItemDoCatalogo> itens) => itens.ToDictionary(i => NomesNormalizados.Chave(i.Nome), i => i.Id);
}
