using BFA.Application.Franqueadora;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class FranqueadoraAlunosViewModel
{
    public string NomeOrganizacao { get; init; } = string.Empty;

    public int TotalAlunos { get; init; }

    public int PaginaAtual { get; init; } = 1;

    public int TotalPaginas { get; init; }

    public int ItensPorPagina { get; init; } = 20;

    public Guid? UnidadeIdSelecionada { get; init; }

    public string? Busca { get; init; }

    public IReadOnlyList<FranqueadoraAlunoItemViewModel> Alunos { get; init; } = [];

    public IReadOnlyList<FranqueadoraUnidadeSelecaoViewModel> Unidades { get; init; } = [];

    public bool TemPaginaAnterior => PaginaAtual > 1;

    public bool TemProximaPagina => PaginaAtual < TotalPaginas;
}

public sealed record FranqueadoraAlunoItemViewModel(
    Guid AlunoId,
    string NomeCompleto,
    string? CpfFormatado,
    string? Email,
    string? Telefone,
    bool Ativo,
    string NomeUnidade,
    Guid UnidadeId,
    string DataNascimentoFormatada,
    string CriadoEmFormatada);

public sealed record FranqueadoraUnidadeSelecaoViewModel(
    Guid UnidadeId,
    string Nome);

public static class FranqueadoraAlunosMapper
{
    private const int ItensPorPaginaPadrao = 20;

    public static FranqueadoraAlunosViewModel Mapear(
        FranqueadoraAlunosResumo resumo,
        int pagina,
        string? busca,
        Guid? unidadeId)
    {
        var totalPaginas = (int)Math.Ceiling((double)resumo.TotalAlunos / ItensPorPaginaPadrao);
        if (totalPaginas < 1) totalPaginas = 1;

        if (pagina < 1) pagina = 1;
        if (pagina > totalPaginas) pagina = totalPaginas;

        var alunosPaginados = resumo.Alunos
            .Skip((pagina - 1) * ItensPorPaginaPadrao)
            .Take(ItensPorPaginaPadrao)
            .ToList();

        return new FranqueadoraAlunosViewModel
        {
            NomeOrganizacao = resumo.NomeOrganizacao,
            TotalAlunos = resumo.TotalAlunos,
            PaginaAtual = pagina,
            TotalPaginas = totalPaginas,
            ItensPorPagina = ItensPorPaginaPadrao,
            UnidadeIdSelecionada = unidadeId,
            Busca = busca,
            Alunos = alunosPaginados.Select(a => new FranqueadoraAlunoItemViewModel(
                a.AlunoId,
                a.NomeCompleto,
                FormatarCpf(a.Cpf),
                a.Email,
                a.Telefone,
                a.Ativo,
                a.NomeUnidade,
                a.UnidadeId,
                a.DataNascimento.ToString("dd/MM/yyyy"),
                a.CriadoEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))).ToList(),
            Unidades = resumo.Unidades.Select(u => new FranqueadoraUnidadeSelecaoViewModel(
                u.UnidadeId,
                u.Nome)).ToList()
        };
    }

    private static string? FormatarCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
        {
            return cpf;
        }

        return $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
    }
}
