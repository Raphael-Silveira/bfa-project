namespace BFA.Web.ViewModels.Franqueadora;

public sealed class UnidadesFranqueadoraIndexViewModel
{
    public IReadOnlyList<UnidadeFranqueadoraItemViewModel> Unidades { get; init; } = [];
}

public sealed record UnidadeFranqueadoraItemViewModel(
    Guid Id,
    string Nome,
    string Slug,
    bool Ativa,
    DateTime CriadoEmUtc,
    bool PossuiFranqueadoAtivo);
