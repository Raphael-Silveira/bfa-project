namespace BFA.Application;

public sealed class PaginaResultado<T>
{
    public required IReadOnlyList<T> Itens { get; init; }
    public required int PaginaAtual { get; init; }
    public required int TamanhoPagina { get; init; }
    public required int TotalItens { get; init; }

    public int TotalPaginas => (int)Math.Ceiling((double)TotalItens / TamanhoPagina);
    public bool TemPaginaAnterior => PaginaAtual > 1;
    public bool TemProximaPagina => PaginaAtual < TotalPaginas;
    public int PrimeiroIndice => TotalItens == 0 ? 0 : (PaginaAtual - 1) * TamanhoPagina + 1;
    public int UltimoIndice => Math.Min(PaginaAtual * TamanhoPagina, TotalItens);
}
