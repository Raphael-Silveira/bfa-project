namespace BFA.Web.ViewModels.Unidade;

public sealed record GovernancaOperacionalUnidadeViewModel(
    bool EhAdministradorRede,
    bool PossuiFranqueadoAtivo,
    bool PodeGerenciarTurmas);

public static class GovernancaOperacionalUnidadeViewData
{
    public const string Chave = "BfaGovernancaOperacionalUnidade";
}
