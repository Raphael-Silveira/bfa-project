namespace BFA.Application.Franqueadora.Contratos;

public interface IContratosFranquiaServico
{
    Task<ResultadoContratoFranquia<Guid>> CriarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        TermosContratoFranquiaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoContratoFranquia> AtualizarRascunhoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        TermosContratoFranquiaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoContratoFranquia> EnviarDocumentoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        EnviarDocumentoContratoFranquiaSolicitacao solicitacao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoContratoFranquia> AtivarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken);

    Task<ResultadoContratoFranquia<Guid>> CriarNovaVersaoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        string motivoAlteracao,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoContratoFranquia> FormalizarVersaoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoContratoFranquia> CancelarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken);

    Task<ResultadoOperacaoContratoFranquia> EncerrarAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        CancellationToken cancellationToken);
}
