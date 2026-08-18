namespace BFA.Application.Franqueadora.AcessosUnidade;

public interface IAcessosUnidadeServico
{
    Task<ResultadoOperacaoAcessoUnidade> AdicionarAsync(
        Guid usuarioId,
        Guid unidadeId,
        AdicionarAdministradorUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoAcessoUnidade> AtivarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoAcessoUnidade> DesativarAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid vinculoId,
        CancellationToken cancellationToken);
}
