using BFA.Application.Franqueadora;

namespace BFA.Web.ViewModels.Franqueadora;

public sealed class FranqueadoraDashboardViewModel
{
    public bool SelecaoOrganizacaoNecessaria { get; init; }

    public string NomeOrganizacao { get; init; } = string.Empty;

    public int TotalUnidades { get; init; }

    public int UnidadesAtivas { get; init; }

    public int TotalAlunosAtivos { get; init; }

    public int TotalMatriculasAtivas { get; init; }

    public int TotalProfessores { get; init; }

    public string TotalReceita { get; init; } = string.Empty;

    public string TotalPendente { get; init; } = string.Empty;

    public string TotalAtrasado { get; init; } = string.Empty;

    public IReadOnlyList<UnidadeResumoRedeViewModel> Unidades { get; init; } = [];
}

public sealed record UnidadeResumoRedeViewModel(
    Guid UnidadeId,
    string NomeUnidade,
    int TotalAlunos,
    bool Ativa);

public static class FranqueadoraDashboardMapper
{
    public static FranqueadoraDashboardViewModel Mapear(
        FranqueadoraDashboardResumo resumo) => new()
    {
        NomeOrganizacao = resumo.NomeOrganizacao,
        TotalUnidades = resumo.TotalUnidades,
        UnidadesAtivas = resumo.UnidadesAtivas,
        TotalAlunosAtivos = resumo.TotalAlunosAtivos,
        TotalMatriculasAtivas = resumo.TotalMatriculasAtivas,
        TotalProfessores = resumo.TotalProfessores,
        TotalReceita = resumo.TotalReceita.ToString("C"),
        TotalPendente = resumo.TotalPendente.ToString("C"),
        TotalAtrasado = resumo.TotalAtrasado.ToString("C"),
        Unidades = resumo.Unidades.Select(u => new UnidadeResumoRedeViewModel(
            u.UnidadeId,
            u.NomeUnidade,
            u.TotalAlunos,
            u.Ativa)).ToList()
    };
}
