namespace BFA.Application.Unidades;

public sealed record UnidadeAcessoResumo(
    Guid OrganizacaoId,
    Guid UnidadeId,
    string Nome);

public interface IUnidadesUsuarioConsulta
{
    Task<IReadOnlyList<UnidadeAcessoResumo>> ListarAdministradasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<UnidadeAcessoResumo?> ObterAdministradaAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnidadeAcessoResumo>> ListarProfessorAsync(
        Guid usuarioId,
        CancellationToken cancellationToken);

    Task<UnidadeAcessoResumo?> ObterProfessorAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}
