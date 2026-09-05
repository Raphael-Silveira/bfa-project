namespace BFA.Web.ViewModels.Shared;

public sealed class PaginacaoViewModel
{
    public required int PaginaAtual { get; init; }
    public required int TotalPaginas { get; init; }
    public required int TotalItens { get; init; }
    public required int PrimeiroIndice { get; init; }
    public required int UltimoIndice { get; init; }
    public string BaseQueryString { get; init; } = string.Empty;

    public bool TemPaginaAnterior => PaginaAtual > 1;
    public bool TemProximaPagina => PaginaAtual < TotalPaginas;

    public IReadOnlyList<int> PaginasVisiveis
    {
        get
        {
            const int maxVisiveis = 5;
            var total = TotalPaginas;
            var atual = PaginaAtual;

            if (total <= maxVisiveis)
                return Enumerable.Range(1, total).ToList();

            var inicio = Math.Max(1, atual - 2);
            var fim = Math.Min(total, inicio + maxVisiveis - 1);

            if (fim - inicio < maxVisiveis - 1)
                inicio = Math.Max(1, fim - maxVisiveis + 1);

            return Enumerable.Range(inicio, fim - inicio + 1).ToList();
        }
    }

    public string UrlPagina(int pagina)
    {
        var qs = string.IsNullOrWhiteSpace(BaseQueryString)
            ? $"pagina={pagina}"
            : $"{BaseQueryString}&pagina={pagina}";
        return $"?{qs}";
    }
}
