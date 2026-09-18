namespace BFA.Application.AlunoArea;

public sealed record FotoPerfilUpload(
    Stream Conteudo,
    string? ContentType,
    long TamanhoBytes);

public sealed record FotoPerfilArmazenada(
    string Chave,
    string ContentType,
    DateTime AtualizadaEmUtc);

public interface IFotoPerfilAluno
{
    Task<FotoPerfilArmazenada> ValidarProcessarSalvarAsync(
        Guid organizacaoId,
        Guid alunoId,
        FotoPerfilUpload upload,
        CancellationToken cancellationToken);

    Task<Stream?> AbrirAsync(
        string chave,
        CancellationToken cancellationToken);

    Task ExcluirAsync(string chave, CancellationToken cancellationToken);
}
