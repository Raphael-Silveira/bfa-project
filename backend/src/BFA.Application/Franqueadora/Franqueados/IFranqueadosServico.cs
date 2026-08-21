namespace BFA.Application.Franqueadora.Franqueados;

public interface IFranqueadosServico
{
    Task<ResultadoOperacaoFranqueado> AtualizarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        AtualizarFranqueadoSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoFranqueado> VincularUnidadeAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        VincularUnidadeFranqueadoSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoFranqueado> DesativarUnidadeAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken);
}
