namespace BFA.Web.ViewModels.Franqueadora;

public sealed class PainelFranqueadoraViewModel
{
    public bool SelecaoOrganizacaoNecessaria { get; init; }

    public string NomeOrganizacao { get; init; } = string.Empty;

    public int TotalUnidades { get; init; }

    public int UnidadesAtivas { get; init; }

    public int AdministradoresRedeAtivos { get; init; }

    public int AdministradoresUnidadeAtivos { get; init; }
}
