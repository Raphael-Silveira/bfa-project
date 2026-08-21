namespace BFA.Application.Contratos;

public sealed record ArquivoTemporarioDocumentoContrato(
    string Identificador,
    long TamanhoBytes,
    string HashSha256,
    bool PossuiAssinaturaPdf);

public sealed class TamanhoDocumentoContratoExcedidoException(long tamanhoMaximoBytes)
    : Exception($"O documento excede o limite de {tamanhoMaximoBytes} bytes.")
{
    public long TamanhoMaximoBytes { get; } = tamanhoMaximoBytes;
}

public interface IArmazenamentoDocumentosContrato
{
    Task SalvarAsync(
        string chaveArmazenamento,
        Stream conteudo,
        CancellationToken cancellationToken = default);

    Task<Stream> AbrirLeituraAsync(
        string chaveArmazenamento,
        CancellationToken cancellationToken = default);

    Task<bool> ExisteAsync(
        string chaveArmazenamento,
        CancellationToken cancellationToken = default);

    Task<ArquivoTemporarioDocumentoContrato> SalvarTemporarioAsync(
        Stream conteudo,
        CancellationToken cancellationToken = default);

    Task ConfirmarTemporarioAsync(
        string identificadorTemporario,
        string chaveArmazenamento,
        CancellationToken cancellationToken = default);

    Task DescartarTemporarioAsync(
        string identificadorTemporario,
        CancellationToken cancellationToken = default);

    Task DescartarArquivoNaoConfirmadoAsync(
        string chaveArmazenamento,
        CancellationToken cancellationToken = default);
}
