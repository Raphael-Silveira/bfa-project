namespace BFA.Application.Unidades;

public interface IUnidadeDashboardConsulta
{
    Task<UnidadeDashboardMetricas?> ObterMetricasAsync(
        Guid unidadeId,
        CancellationToken cancellationToken);
}

public sealed record UnidadeDashboardMetricas
{
    public int TotalAlunosAtivos { get; init; }

    public int TotalTurmasAtivas { get; init; }

    public int TotalAulasSemana { get; init; }

    public decimal PercentualFrequencia { get; init; }

    public decimal ReceitaMes { get; init; }

    public decimal Pendente { get; init; }

    public decimal EmAtraso { get; init; }
}
