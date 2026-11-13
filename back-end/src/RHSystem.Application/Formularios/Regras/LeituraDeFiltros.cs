using RHSystem.Application.Comum;

namespace RHSystem.Application.Formularios.Regras;

/// <summary>Junta os erros de campo de várias leituras de filtro (página, enum de consulta) num só <see cref="ColetorDeErros"/>: cada leitura lança <see cref="ErroDeValidacaoException"/> e todas as mensagens saem juntas num 400 só.</summary>
public static class LeituraDeFiltros
{
    public static void Tentar(ColetorDeErros erros, Action leitura)
    {
        try
        {
            leitura();
        }
        catch (ErroDeValidacaoException erro)
        {
            foreach (var (campo, mensagens) in erro.Erros)
            {
                foreach (var mensagem in mensagens)
                {
                    erros.Adicionar(campo, mensagem);
                }
            }
        }
    }
}
