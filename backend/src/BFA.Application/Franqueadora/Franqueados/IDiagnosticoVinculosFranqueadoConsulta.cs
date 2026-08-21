namespace BFA.Application.Franqueadora.Franqueados;

public sealed record InconsistenciaVinculosFranqueado(
    Guid FranqueadoId,
    string Franqueado,
    Guid UsuarioPrincipalId,
    Guid UnidadeId,
    string Unidade);

public sealed record DiagnosticoVinculosFranqueado(
    IReadOnlyList<InconsistenciaVinculosFranqueado> AcessosSemVinculoComercial,
    IReadOnlyList<InconsistenciaVinculosFranqueado> VinculosComerciaisSemAcessoPrincipal);

public interface IDiagnosticoVinculosFranqueadoConsulta
{
    Task<DiagnosticoVinculosFranqueado> DiagnosticarAsync(
        CancellationToken cancellationToken);
}
