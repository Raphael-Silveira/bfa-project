namespace BFA.Application.Franqueadora.Unidades;

public interface IUnidadesFranqueadoraServico
{
    Task<ResultadoOperacaoUnidade> CriarAsync(
        Guid usuarioId,
        CriarUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoUnidade> AtualizarAsync(
        Guid usuarioId,
        Guid unidadeId,
        AtualizarUnidadeSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoUnidade> AtivarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoUnidade> DesativarAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}
