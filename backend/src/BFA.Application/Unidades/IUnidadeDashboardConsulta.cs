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

    public IReadOnlyList<AulaHojeResumo> AulasHoje { get; init; } = [];

    public IReadOnlyList<AtividadeRecente> AtividadesRecentes { get; init; } = [];
}

public sealed record AulaHojeResumo(
    Guid AulaId,
    string Horario,
    string TurmaNome,
    string ProfessorNome,
    int Inscritos,
    int Capacidade,
    string Status);

public sealed record AtividadeRecente(
    string IconeTipo,
    string Titulo,
    string Subtitulo,
    string TempoRelativo,
    DateTime CriadoEmUtc);
