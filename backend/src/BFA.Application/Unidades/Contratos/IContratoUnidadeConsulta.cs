using BFA.Domain.Contratos;

namespace BFA.Application.Unidades.Contratos;

public enum EstadoConsultaContratoUnidade
{
    Sucesso = 1,
    SemAcesso = 2,
    NaoEncontrado = 3,
    DocumentoIndisponivel = 4
}

public sealed record ResultadoConsultaContratoUnidade<T>(
    EstadoConsultaContratoUnidade Estado,
    T? Valor);

public sealed record DocumentoContratoUnidadeResumo(
    Guid Id,
    TipoDocumentoContratoFranquia TipoDocumento,
    string NomeOriginal,
    long TamanhoBytes);

public sealed record ContratoAtivoUnidadeResumo(
    Guid ContratoId,
    string? Numero,
    StatusContratoFranquia Status,
    Guid VersaoId,
    int NumeroVersao,
    DateOnly DataInicio,
    DateOnly? DataFim,
    decimal PercentualRoyalties,
    decimal MensalidadeFixa,
    decimal? TaxaAdesao,
    int? DiaVencimento,
    string? Observacoes,
    IReadOnlyList<DocumentoContratoUnidadeResumo> Documentos);

public sealed record PainelContratoUnidade(
    Guid OrganizacaoId,
    Guid UnidadeId,
    string UnidadeNome,
    ContratoAtivoUnidadeResumo? Contrato);

public sealed record DocumentoContratoUnidadeLeitura(
    Stream Conteudo,
    string NomeOriginal);

public interface IContratoUnidadeConsulta
{
    Task<ResultadoConsultaContratoUnidade<PainelContratoUnidade>> ObterAtivoAsync(
        Guid usuarioId,
        Guid unidadeId,
        CancellationToken cancellationToken);

    Task<ResultadoConsultaContratoUnidade<DocumentoContratoUnidadeLeitura>> AbrirDocumentoAsync(
        Guid usuarioId,
        Guid unidadeId,
        Guid documentoId,
        CancellationToken cancellationToken);
}
