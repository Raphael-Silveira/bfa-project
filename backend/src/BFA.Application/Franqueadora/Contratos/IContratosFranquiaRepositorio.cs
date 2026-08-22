using BFA.Domain.Contratos;

namespace BFA.Application.Franqueadora.Contratos;

public interface IContratosFranquiaRepositorio
{
    Task<ContextoContratoFranquia?> ObterContextoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ContratoFranquiaPainel> ObterPainelAsync(
        ContextoContratoFranquia contexto,
        CancellationToken cancellationToken);

    Task<ContratoFranquia?> ObterContratoParaAtualizacaoAsync(
        Guid franqueadoUnidadeId,
        Guid contratoId,
        CancellationToken cancellationToken);

    Task<ContratoFranquiaVersao?> ObterVersaoParaAtualizacaoAsync(
        Guid contratoId,
        Guid versaoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContratoFranquiaVersao>> ListarVersoesParaAtualizacaoAsync(
        Guid contratoId,
        CancellationToken cancellationToken);

    Task<bool> ExisteContratoAtivoOutroAsync(
        Guid franqueadoUnidadeId,
        Guid contratoId,
        CancellationToken cancellationToken);

    Task<bool> ExisteDocumentoAsync(
        Guid versaoId,
        IReadOnlyCollection<TipoDocumentoContratoFranquia> tipos,
        CancellationToken cancellationToken);

    Task<DocumentoContratoFranquiaAcesso?> ObterDocumentoAsync(
        Guid organizacaoId,
        Guid franqueadoId,
        Guid unidadeId,
        Guid contratoId,
        Guid versaoId,
        Guid documentoId,
        CancellationToken cancellationToken);

    void Adicionar(ContratoFranquia contrato);

    void Adicionar(ContratoFranquiaVersao versao);

    Task<EstadoPersistenciaContratoFranquia> SalvarTransacaoAsync(
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaContratoFranquia> SalvarNovaVersaoAsync(
        ContratoFranquiaVersao versao,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaContratoFranquia> SalvarFormalizacaoAsync(
        ContratoFranquiaVersao versaoVigenteAnterior,
        ContratoFranquiaVersao novaVersaoVigente,
        CancellationToken cancellationToken);

    Task<EstadoPersistenciaContratoFranquia> SalvarDocumentoAsync(
        DocumentoContratoFranquia documento,
        string identificadorTemporario,
        CancellationToken cancellationToken);
}
