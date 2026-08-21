namespace BFA.Application.Franqueadora.Contratos;

public interface IContratosFranquiaConsulta
{
    Task<ResultadoContratoFranquia<ContratoFranquiaPainel>> ObterAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoContratoFranquia<VersaoContratoFranquiaResumo>> ObterVersaoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken);

    Task<ResultadoContratoFranquia<DocumentoContratoFranquiaLeitura>> AbrirDocumentoAsync(
        Guid usuarioAtualId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        CancellationToken cancellationToken);
}
