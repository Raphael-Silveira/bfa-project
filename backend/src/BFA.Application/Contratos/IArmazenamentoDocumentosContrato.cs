namespace BFA.Application.Contratos;

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
}
