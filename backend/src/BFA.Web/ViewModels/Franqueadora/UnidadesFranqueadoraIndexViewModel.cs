using BFA.Application.Franqueadora.Unidades;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class UnidadesFranqueadoraIndexViewModel
{
    public string? Busca { get; init; }

    public TipoUnidadeFiltro Tipo { get; init; } = TipoUnidadeFiltro.Todos;

    public IReadOnlyList<UnidadeFranqueadoraItemViewModel> Unidades { get; init; } = [];

    public bool PossuiFiltros => !string.IsNullOrWhiteSpace(Busca)
        || Tipo != TipoUnidadeFiltro.Todos;
}

public sealed record UnidadeFranqueadoraItemViewModel(
    Guid Id,
    string Nome,
    string Slug,
    bool Ativa,
    DateTime CriadoEmUtc,
    Guid? FranqueadoIdAtivo)
{
    public bool PossuiFranqueadoAtivo => FranqueadoIdAtivo.HasValue;
}
