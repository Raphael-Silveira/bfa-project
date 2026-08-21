namespace BFA.Web.ViewModels.Unidade;

public interface IUnidadeContextoViewModel
{
    Guid OrganizacaoId { get; }

    Guid UnidadeId { get; }

    string NomeUnidade { get; }

    bool PodeTrocarUnidade { get; }
}

public sealed class PainelUnidadeViewModel : IUnidadeContextoViewModel
{
    public required Guid OrganizacaoId { get; init; }

    public required Guid UnidadeId { get; init; }

    public required string NomeUnidade { get; init; }

    public required bool PodeTrocarUnidade { get; init; }
}

public sealed class SelecaoUnidadeViewModel
{
    public string? PrimeiroNomeUsuario { get; init; }

    public required IReadOnlyList<UnidadeSelecaoItemViewModel> Unidades { get; init; }
}

public sealed record UnidadeSelecaoItemViewModel(Guid UnidadeId, string Nome);
