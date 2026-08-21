namespace BFA.Application.Unidades;

public sealed record UnidadeContextoResumo(
    Guid OrganizacaoId,
    Guid UnidadeId,
    string Nome);

public interface IUnidadeContextoConsulta
{
    Task<UnidadeContextoResumo?> ObterAtivaAsync(
        Guid unidadeId,
        CancellationToken cancellationToken);
}
