namespace BFA.Application.Franqueadora;

public interface IFranqueadoraDashboardConsulta
{
    Task<FranqueadoraDashboardResultado> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);
}

public enum EstadoFranqueadoraDashboard
{
    Disponivel = 1,
    SemAcesso = 2,
    SelecaoOrganizacaoNecessaria = 3
}

public sealed record FranqueadoraDashboardResumo(
    Guid OrganizacaoId,
    string NomeOrganizacao,
    int TotalUnidades,
    int UnidadesAtivas,
    int TotalAlunosAtivos,
    int TotalMatriculasAtivas,
    int TotalProfessores,
    decimal TotalReceita,
    decimal TotalPendente,
    decimal TotalAtrasado,
    IReadOnlyList<UnidadeResumoRede> Unidades);

public sealed record UnidadeResumoRede(
    Guid UnidadeId,
    string NomeUnidade,
    int TotalAlunos,
    int TotalMatriculas,
    bool Ativa);

public sealed record FranqueadoraDashboardResultado(
    EstadoFranqueadoraDashboard Estado,
    FranqueadoraDashboardResumo? Resumo)
{
    public static FranqueadoraDashboardResultado Disponivel(
        FranqueadoraDashboardResumo resumo)
    {
        ArgumentNullException.ThrowIfNull(resumo);
        return new(EstadoFranqueadoraDashboard.Disponivel, resumo);
    }

    public static FranqueadoraDashboardResultado SemAcesso()
    {
        return new(EstadoFranqueadoraDashboard.SemAcesso, null);
    }

    public static FranqueadoraDashboardResultado SelecaoOrganizacaoNecessaria()
    {
        return new(EstadoFranqueadoraDashboard.SelecaoOrganizacaoNecessaria, null);
    }
}
